using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vernier.Core.Providers;
using Vernier.Core.Security;

namespace Vernier.Core.Knowledge;

public static class LineDiff
{
    /// <summary>Line diff (LCS) rendered as "+", "-" and " " prefixed lines. Bounded for preview use.</summary>
    public static string Unified(string? before, string after, int context = 2, int maxLines = 4000)
    {
        var a = Split(before ?? "");
        var b = Split(after);
        if (a.Length > maxLines || b.Length > maxLines)
            return $"(File too large to diff line by line: {a.Length} → {b.Length} lines.)";

        // ponytail: O(n·m) LCS table; fine for notes and source files under maxLines, swap for Myers if larger files matter.
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<(char Op, string Line)>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y]) { ops.Add((' ', a[x])); x++; y++; }
            else if (lcs[x + 1, y] >= lcs[x, y + 1]) ops.Add(('-', a[x++]));
            else ops.Add(('+', b[y++]));
        }
        while (x < a.Length) ops.Add(('-', a[x++]));
        while (y < b.Length) ops.Add(('+', b[y++]));

        if (ops.All(o => o.Op == ' ')) return "(No changes.)";
        var keep = new bool[ops.Count];
        for (var i = 0; i < ops.Count; i++)
            if (ops[i].Op != ' ')
                for (var k = Math.Max(0, i - context); k <= Math.Min(ops.Count - 1, i + context); k++) keep[k] = true;

        var sb = new StringBuilder();
        var skipping = false;
        for (var i = 0; i < ops.Count; i++)
        {
            if (!keep[i]) { if (!skipping) sb.AppendLine("  …"); skipping = true; continue; }
            skipping = false;
            sb.Append(ops[i].Op).Append(' ').AppendLine(ops[i].Line);
        }
        return sb.ToString();
    }

    private static string[] Split(string s) => s.Length == 0 ? [] : s.Replace("\r\n", "\n").Split('\n');
}

public enum FileChangeKind { Create, Modify, Append }

/// <summary>A proposed write. Nothing touches disk until <see cref="ScopedFiles.Apply"/> is called after approval.</summary>
public sealed record FileChange(FileChangeKind Kind, string RelativePath, string FullPath, string? Before, string After)
{
    public string Diff => LineDiff.Unified(Before, After);
    public string Summary => Kind switch
    {
        FileChangeKind.Create => $"Create {RelativePath}",
        FileChangeKind.Append => $"Append to {RelativePath}",
        _ => $"Modify {RelativePath}",
    };
}

/// <summary>Reads and writes confined to one authorised folder. Used for both the vault and the workspace.</summary>
public class ScopedFiles(string root)
{
    public const long MaxReadBytes = 512 * 1024;
    public PathGuard Guard { get; } = new(root);

    public string Read(string relativePath)
    {
        var full = Guard.Resolve(relativePath);
        var info = new FileInfo(full);
        if (!info.Exists) throw new FileNotFoundException("File not found.", relativePath);
        if (info.Length > MaxReadBytes) throw new IOException($"{relativePath} is larger than {MaxReadBytes / 1024} KB and was not read.");
        var text = File.ReadAllText(full);
        if (text.Take(4000).Contains('\0')) throw new IOException($"{relativePath} looks like a binary file.");
        return text;
    }

    public FileChange ProposeWrite(string relativePath, string content)
    {
        var full = Guard.Resolve(relativePath);
        var before = File.Exists(full) ? Read(relativePath) : null;
        return new FileChange(before is null ? FileChangeKind.Create : FileChangeKind.Modify, Relative(full), full, before, content);
    }

    /// <summary>Existing content is preserved; the new text is added after it.</summary>
    public FileChange ProposeAppend(string relativePath, string addition)
    {
        var full = Guard.Resolve(relativePath);
        if (!File.Exists(full)) return new FileChange(FileChangeKind.Create, Relative(full), full, null, addition);
        var before = Read(relativePath);
        return new FileChange(FileChangeKind.Append, Relative(full), full, before, before.TrimEnd() + "\n\n" + addition);
    }

    /// <summary>
    /// Applies an approved change. Refuses if the file changed since the preview was made, so an
    /// approval can never overwrite edits the user has not seen.
    /// </summary>
    public void Apply(FileChange change) => ApplyAll([change]);

    /// <summary>
    /// One file in a transaction: its path inside the folder, its exact bytes before (null: it did not exist) and a hash of
    /// what the transaction writes, so recovery only undoes a file that still holds that write.
    /// </summary>
    public sealed record JournalEntry(string Path, string? BeforeBase64, string AfterSha256);

    public const string JournalName = ".vernier-journal.json";
    public string JournalPath => Path.Combine(Guard.Root, JournalName);

    // One transaction at a time per folder, across every ScopedFiles instance in the process.
    private static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);
    private object Lock => Locks.GetOrAdd(Guard.Root, _ => new object());

    /// <summary>
    /// Applies approved changes as one: every file is checked against what was previewed before anything is written, the
    /// previous contents are journalled, and a failure part-way restores every file. A journal left by a crash is undone
    /// by <see cref="Recover"/>.
    /// </summary>
    public void ApplyAll(IReadOnlyList<FileChange> changes)
    {
        lock (Lock)
        {
            var targets = changes.Select(c => (Change: c, Full: Guard.Resolve(c.FullPath))).ToList();
            foreach (var (change, full) in targets)
                if ((File.Exists(full) ? File.ReadAllText(full) : null) != change.Before)
                    throw new IOException($"{change.RelativePath} changed after the preview was created. Nothing was written; review the change again.");
            var journal = targets.Select(t => new JournalEntry(Relative(t.Full),
                File.Exists(t.Full) ? Convert.ToBase64String(File.ReadAllBytes(t.Full)) : null, Hash(Encode(t.Change.After)))).ToList();
            Write(JournalPath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(journal)));
            var written = 0;
            try
            {
                foreach (var (change, full) in targets)
                {
                    Write(full, Encode(change.After));
                    written++;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
            {
                Restore(journal.Take(written + 1));
                Commit();
                throw new IOException($"Writing {targets[written].Change.RelativePath} failed ({e.Message}). Every file was put back as it was.", e);
            }
            Commit();
        }
    }

    /// <summary>
    /// After a crash mid-transaction: puts back every journalled file that still holds what the transaction wrote; a file
    /// edited since is left alone. True when a journal was found.
    /// </summary>
    public bool Recover()
    {
        lock (Lock)
        {
            if (!File.Exists(JournalPath)) return false;
            List<JournalEntry>? journal;
            try { journal = JsonSerializer.Deserialize<List<JournalEntry>>(File.ReadAllBytes(JournalPath)); }
            catch (JsonException) { journal = null; } // cut short while being written, or marked committed: no file was touched by it
            if (journal is not null) Restore(journal.Where(j => File.Exists(Guard.Resolve(j.Path)) && Hash(File.ReadAllBytes(Guard.Resolve(j.Path))) == j.AfterSha256));
            Commit();
            return true;
        }
    }

    // The transaction is over: remove the journal. If a sync client or scanner holds it, retry, then mark it committed so
    // a later Recover cannot undo a finished write.
    private void Commit()
    {
        for (var attempt = 0; ; attempt++)
        {
            try { File.Delete(JournalPath); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt < 4) { Thread.Sleep(50 * (attempt + 1)); continue; }
                try { File.WriteAllText(JournalPath, "committed"); return; }
                catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException)
                {
                    throw new IOException("The change was saved, but Vernier could not clear its journal file (" + JournalName + "). Delete it before restarting Vernier.", e2);
                }
            }
        }
    }

    private void Restore(IEnumerable<JournalEntry> entries)
    {
        foreach (var entry in entries)
        {
            var full = Guard.Resolve(entry.Path); // a tampered journal still cannot reach outside the folder
            if (entry.BeforeBase64 is null) { if (File.Exists(full)) File.Delete(full); }
            else Write(full, Convert.FromBase64String(entry.BeforeBase64));
        }
    }

    private static byte[] Encode(string text) => new UTF8Encoding(false).GetBytes(text);
    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    private static void Write(string full, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp = full + ".vernier-tmp";
        try
        {
            File.WriteAllBytes(tmp, content);
            File.Move(tmp, full, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            throw;
        }
    }

    public IReadOnlyList<string> List(string pattern, int max, CancellationToken ct) =>
        Guard.EnumerateFiles(pattern, max, ct).Select(Relative).ToList();

    protected string Relative(string full) => Path.GetRelativePath(Guard.Root, full).Replace('\\', '/');
}

public sealed record NoteRef(string Title, string RelativePath);
public sealed record GraphNode(string Id, string Label, int Degree);
public sealed record GraphEdge(string From, string To, string Relation);
public sealed record KnowledgeGraph(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, string Source, bool Truncated)
{
    public IReadOnlyList<string> Neighbours(string id) =>
        Edges.Where(e => e.From.Equals(id, StringComparison.OrdinalIgnoreCase)).Select(e => e.To)
            .Concat(Edges.Where(e => e.To.Equals(id, StringComparison.OrdinalIgnoreCase)).Select(e => e.From))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>Provider-neutral knowledge graph. Implemented by wikilink parsing and by Graphify's graph.json.</summary>
public interface IKnowledgeGraphSource
{
    string Name { get; }
    Task<KnowledgeGraph?> LoadAsync(string root, CancellationToken ct);
}

/// <summary>An Obsidian vault is a folder of Markdown files; this needs neither Obsidian running nor a plugin.</summary>
public sealed partial class ObsidianVault(string root) : ScopedFiles(root)
{
    public const int MaxNotes = 5000;
    public const string Folder = "Vernier";

    [GeneratedRegex(@"\[\[([^\]\|#\^]+)(?:[#\^][^\]\|]*)?(?:\|[^\]]*)?\]\]")]
    private static partial Regex WikiLink();

    [GeneratedRegex(@"[\\/:*?""<>|#\^\[\]]")]
    private static partial Regex IllegalInName();

    public static string SafeName(string title)
    {
        var name = IllegalInName().Replace(title, " ").Trim().TrimEnd('.');
        name = Regex.Replace(name, @"\s+", " ");
        if (name.Length > 80) name = name[..80].Trim();
        // CON, NUL, COM1 … are devices on Windows, not files, whatever the extension.
        if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\s|$)", RegexOptions.IgnoreCase)) name += " (note)";
        return name.Length == 0 ? "Untitled" : name;
    }

    public IReadOnlyList<NoteRef> Notes(CancellationToken ct, int max = MaxNotes) =>
        Guard.EnumerateFiles("*.md", max, ct).Select(f => new NoteRef(Path.GetFileNameWithoutExtension(f), Relative(f))).ToList();

    public static IReadOnlyList<string> LinksIn(string markdown) =>
        WikiLink().Matches(markdown).Select(m => m.Groups[1].Value.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public FileChange ProposeConceptNote(string subject, string concept, string summary, string? formula, IEnumerable<string> prerequisites, string? misconception, string? example)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---").AppendLine("source: vernier").AppendLine($"subject: \"{subject.Replace("\"", "'")}\"").AppendLine($"updated: {DateTime.Now:yyyy-MM-dd}").AppendLine("---").AppendLine();
        sb.AppendLine($"# {concept}").AppendLine().AppendLine(summary.Trim()).AppendLine();
        if (!string.IsNullOrWhiteSpace(formula)) sb.AppendLine("## Key formula").AppendLine().AppendLine(formula.Trim()).AppendLine();
        var prereqs = prerequisites.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => $"[[{SafeName(p)}]]").ToList();
        if (prereqs.Count > 0) sb.AppendLine("## Builds on").AppendLine().AppendLine(string.Join(", ", prereqs)).AppendLine();
        if (!string.IsNullOrWhiteSpace(example)) sb.AppendLine("## Worked example").AppendLine().AppendLine(example.Trim()).AppendLine();
        if (!string.IsNullOrWhiteSpace(misconception)) sb.AppendLine("## Mistake to watch").AppendLine().AppendLine(misconception.Trim()).AppendLine();
        sb.AppendLine($"Subject: [[{SafeName(subject)}]]");

        // One note per concept: "Newton's second law" and "newtons second laws" are the same note, wherever it lives.
        var path = FindNote(concept)?.RelativePath ?? $"{Folder}/Concepts/{SafeName(concept)}.md";
        // An existing note is never replaced: new material is appended under a dated heading.
        return File.Exists(Guard.Resolve(path))
            ? ProposeAppend(path, $"## Update {DateTime.Now:yyyy-MM-dd}\n\n{summary.Trim()}" + (string.IsNullOrWhiteSpace(misconception) ? "" : $"\n\nMistake to watch: {misconception.Trim()}"))
            : ProposeWrite(path, sb.ToString());
    }

    public FileChange ProposeSessionNote(string title, DateTimeOffset when, IEnumerable<(string Role, string Text)> messages, IEnumerable<string> concepts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---").AppendLine("source: vernier").AppendLine($"date: {when.ToLocalTime():yyyy-MM-dd}").AppendLine("---").AppendLine();
        sb.AppendLine($"# {title}").AppendLine();
        var links = concepts.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => $"[[{SafeName(c)}]]").Distinct().ToList();
        if (links.Count > 0) sb.AppendLine("Concepts: " + string.Join(", ", links)).AppendLine();
        foreach (var (role, text) in messages)
            sb.AppendLine(role == "user" ? "**Me**" : "**Tutor**").AppendLine().AppendLine(text.Trim()).AppendLine();
        return ProposeWrite($"{Folder}/Sessions/{when.ToLocalTime():yyyy-MM-dd} {SafeName(title)}.md", sb.ToString());
    }

    // ---------- finding notes ----------

    /// <summary>A title reduced to what makes it the same concept: case, punctuation, apostrophes and plurals ignored.</summary>
    public static string Key(string title) =>
        string.Join(" ", Regex.Split(title.ToLowerInvariant().Replace("'", "").Replace("’", ""), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length > 0).Select(w => w.Length > 3 && w.EndsWith('s') && !w.EndsWith("ss", StringComparison.Ordinal) ? w[..^1] : w));

    [GeneratedRegex(@"^aliases:\s*\[?(.*?)\]?\s*$", RegexOptions.Multiline)]
    private static partial Regex Aliases();

    /// <summary>The note for this concept, by title or by an alias in its front matter; null when there is none.</summary>
    public NoteRef? FindNote(string title, CancellationToken ct = default)
    {
        var key = Key(title);
        if (key.Length == 0) return null;
        var notes = Notes(ct);
        if (notes.FirstOrDefault(n => Key(n.Title) == key) is { } byTitle) return byTitle;
        foreach (var note in notes)
        {
            string head;
            try { head = Read(note.RelativePath); }
            catch (IOException) { continue; }
            if (!head.StartsWith("---", StringComparison.Ordinal)) continue;
            var end = head.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end < 0 || Aliases().Match(head[..end]) is not { Success: true } m) continue;
            if (m.Groups[1].Value.Split(',').Any(a => Key(a.Trim().Trim('"', '\'')) == key)) return note;
        }
        return null;
    }

    public sealed record NoteHit(NoteRef Note, double Score, string Snippet);

    private static readonly HashSet<string> StopWords = ["the", "and", "for", "with", "that", "this", "what", "how", "why", "are", "was", "from", "into", "about"];

    /// <summary>Notes about a query, best first: title words count most, then how often the words occur. Reads at most a few thousand notes.</summary>
    public IReadOnlyList<NoteHit> Search(string query, int max, CancellationToken ct)
    {
        var terms = Key(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3 && !StopWords.Contains(t)).Distinct().ToArray();
        if (terms.Length == 0) return [];
        var hits = new List<NoteHit>();
        foreach (var note in Notes(ct, 3000))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try { text = Read(note.RelativePath); }
            catch (IOException) { continue; }
            var title = Key(note.Title);
            var body = Key(text);
            var score = terms.Sum(t => (title.Contains(t, StringComparison.Ordinal) ? 3.0 : 0) + Math.Min(5, Occurrences(body, t)) * 0.4);
            if (score <= 0) continue;
            var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => !l.StartsWith('#') && !l.StartsWith("---", StringComparison.Ordinal) && terms.Any(t => Key(l).Contains(t, StringComparison.Ordinal))) ?? "";
            hits.Add(new NoteHit(note, score / terms.Length, line.Length > 160 ? line[..160] + "…" : line));
        }
        return [.. hits.OrderByDescending(h => h.Score).ThenBy(h => h.Note.RelativePath, StringComparer.Ordinal).Take(max)];
    }

    private static int Occurrences(string text, string term)
    {
        var count = 0;
        for (var at = text.IndexOf(term, StringComparison.Ordinal); at >= 0; at = text.IndexOf(term, at + term.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    // ---------- the index note ----------

    public const string IndexPath = Folder + "/Index.md";
    private const string IndexStart = "<!-- vernier:index -->", IndexEnd = "<!-- /vernier:index -->";

    [GeneratedRegex(@"^subject:\s*""?(.*?)""?\s*$", RegexOptions.Multiline)]
    private static partial Regex SubjectLine();

    /// <summary>
    /// The index of Vernier's concept notes, grouped by subject, as it will be once <paramref name="pending"/> is applied.
    /// Only the block between Vernier's markers is rewritten; anything the learner wrote around it stays.
    /// </summary>
    public FileChange ProposeIndex(IEnumerable<FileChange> pending, CancellationToken ct = default)
    {
        var concepts = new Dictionary<string, (string Title, string Subject)>(StringComparer.OrdinalIgnoreCase);
        string SubjectOf(string text) => SubjectLine().Match(text) is { Success: true } m && m.Groups[1].Value.Trim().Length > 0 ? m.Groups[1].Value.Trim() : "Other";
        foreach (var note in Notes(ct).Where(n => n.RelativePath.StartsWith(Folder + "/Concepts/", StringComparison.OrdinalIgnoreCase)))
        {
            try { concepts[note.RelativePath] = (note.Title, SubjectOf(Read(note.RelativePath))); }
            catch (IOException) { }
        }
        foreach (var change in pending.Where(c => c.RelativePath.StartsWith(Folder + "/Concepts/", StringComparison.OrdinalIgnoreCase)))
            concepts[change.RelativePath] = (Path.GetFileNameWithoutExtension(change.RelativePath), SubjectOf(change.After));

        var block = new StringBuilder(IndexStart).AppendLine();
        foreach (var subject in concepts.Values.GroupBy(c => c.Subject).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.AppendLine().AppendLine($"## {subject.Key}").AppendLine();
            foreach (var title in subject.Select(c => c.Title).Order(StringComparer.OrdinalIgnoreCase)) block.AppendLine($"- [[{title}]]");
        }
        block.AppendLine().Append(IndexEnd);

        var full = Guard.Resolve(IndexPath);
        var before = File.Exists(full) ? Read(IndexPath) : null;
        string after;
        if (before is null) after = $"# Vernier index\n\nConcepts Vernier has written up with you, by subject.\n\n{block}\n";
        else
        {
            var start = before.IndexOf(IndexStart, StringComparison.Ordinal);
            var end = before.IndexOf(IndexEnd, StringComparison.Ordinal);
            after = start >= 0 && end > start
                ? before[..start] + block + before[(end + IndexEnd.Length)..]
                : before.TrimEnd() + "\n\n" + block + "\n";
        }
        return new FileChange(before is null ? FileChangeKind.Create : FileChangeKind.Modify, IndexPath, full, before, after);
    }

    public FileChange ProposeMistakeLogEntry(string concept, string description) =>
        ProposeAppend($"{Folder}/Mistake log.md", $"- {DateTime.Now:yyyy-MM-dd} · [[{SafeName(concept)}]] — {description.Trim()}");
}

/// <summary>Fallback graph built from [[wikilinks]] between notes. Always available.</summary>
public sealed class WikiLinkGraphSource : IKnowledgeGraphSource
{
    public string Name => "Markdown wikilinks";

    public Task<KnowledgeGraph?> LoadAsync(string root, CancellationToken ct) => Task.Run<KnowledgeGraph?>(() =>
    {
        var vault = new ObsidianVault(root);
        var notes = vault.Notes(ct);
        var edges = new List<GraphEdge>();
        var names = new HashSet<string>(notes.Select(n => n.Title), StringComparer.OrdinalIgnoreCase);
        foreach (var note in notes)
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try { text = vault.Read(note.RelativePath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PathGuardException) { continue; }
            foreach (var link in ObsidianVault.LinksIn(text))
            {
                var target = Path.GetFileName(link);
                names.Add(target);
                edges.Add(new GraphEdge(note.Title, target, "links to"));
            }
        }
        var degree = names.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var e in edges) { degree[e.From]++; degree[e.To]++; }
        var nodes = names.Select(n => new GraphNode(n, n, degree[n])).OrderByDescending(n => n.Degree).ThenBy(n => n.Label).ToList();
        return new KnowledgeGraph(nodes, edges, Name, notes.Count >= ObsidianVault.MaxNotes);
    }, ct);
}

/// <summary>
/// Graphify integration through its documented surface only: the graphify-out/graph.json file it
/// writes, and the `graphify update &lt;path&gt;` CLI command (code re-extraction, no LLM).
/// Building a full semantic graph is done by the user with the /graphify skill; Vernier does not fake that.
/// </summary>
public sealed class GraphifySource : IKnowledgeGraphSource
{
    public const int MaxNodes = 20000;
    public string Name => "Graphify (graphify-out/graph.json)";

    public static string GraphPath(string root) => Path.Combine(root, "graphify-out", "graph.json");

    public static string? FindCli()
    {
        string[] names = OperatingSystem.IsWindows() ? ["graphify.exe", "graphify.cmd"] : ["graphify"];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var n in names)
            {
                try { var c = Path.Combine(dir.Trim(), n); if (File.Exists(c)) return c; }
                catch (ArgumentException) { }
            }
        return null;
    }

    public async Task<KnowledgeGraph?> LoadAsync(string root, CancellationToken ct)
    {
        var file = GraphPath(root);
        if (!File.Exists(file)) return null;
        if (new FileInfo(file).Length > 64 * 1024 * 1024) throw new IOException("graph.json is larger than 64 MB and was not loaded.");
        await using var stream = File.OpenRead(file);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return Parse(doc.RootElement);
    }

    /// <summary>Reads node-link JSON (networkx style: "nodes" plus "links" or "edges"). Unknown fields are ignored.</summary>
    public static KnowledgeGraph Parse(JsonElement root)
    {
        var nodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var truncated = false;
        if (root.TryGetProperty("nodes", out var ns) && ns.ValueKind == JsonValueKind.Array)
            foreach (var n in ns.EnumerateArray())
            {
                if (nodes.Count >= MaxNodes) { truncated = true; break; }
                var id = Str(n, "id");
                if (id is null) continue;
                nodes[id] = Str(n, "label") ?? Str(n, "name") ?? id;
            }
        var edges = new List<GraphEdge>();
        var edgeProp = root.TryGetProperty("links", out var ls) ? ls : root.TryGetProperty("edges", out var es) ? es : default;
        if (edgeProp.ValueKind == JsonValueKind.Array)
            foreach (var e in edgeProp.EnumerateArray())
            {
                var from = Str(e, "source") ?? Str(e, "from");
                var to = Str(e, "target") ?? Str(e, "to");
                if (from is null || to is null || !nodes.ContainsKey(from) || !nodes.ContainsKey(to)) continue;
                edges.Add(new GraphEdge(from, to, Str(e, "relation") ?? Str(e, "type") ?? "related"));
            }
        var degree = nodes.Keys.ToDictionary(k => k, _ => 0);
        foreach (var e in edges) { degree[e.From]++; degree[e.To]++; }
        return new KnowledgeGraph(
            nodes.Select(n => new GraphNode(n.Key, n.Value, degree[n.Key])).OrderByDescending(n => n.Degree).ToList(),
            edges, "Graphify", truncated);
    }

    /// <summary>Runs `graphify update &lt;root&gt;` (documented: re-extract code files, no LLM). User-triggered only.</summary>
    public static async Task<(bool Ok, string Output)> UpdateAsync(string root, CancellationToken ct)
    {
        var cli = FindCli();
        if (cli is null) return (false, "The graphify CLI was not found on PATH.");
        var guard = new PathGuard(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var (exit, stdout, stderr) = await ProcessRunner.RunAsync(cli, ["update", guard.Root], null, guard.Root, timeout.Token).ConfigureAwait(false);
            var text = SecretRedactor.Redact((stdout + "\n" + stderr).Trim());
            return (exit == 0, text.Length > 2000 ? text[^2000..] : text);
        }
        catch (OperationCanceledException) { return (false, "Cancelled or timed out after 5 minutes."); }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception) { return (false, "graphify could not be started: " + e.Message); }
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
            : null;
}
