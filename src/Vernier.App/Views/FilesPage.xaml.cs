using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Vernier.Core.Knowledge;
using Vernier.Core.Providers;
using Vernier.Core.Security;

namespace Vernier.App.Views;

public partial class FilesPage : UserControl, IRefreshable
{
    private sealed record NodeRow(string Id, string Label, int Degree, string Links);

    private static readonly string[] TextExtensions =
        [".md", ".txt", ".cs", ".py", ".js", ".ts", ".tsx", ".jsx", ".java", ".c", ".h", ".cpp", ".hpp", ".rs", ".go", ".rb", ".tex", ".json", ".yaml", ".yml", ".xml", ".html", ".css", ".sql", ".m", ".r", ".ipynb", ".csv", ".toml", ".sh", ".ps1"];

    private KnowledgeGraph? _graph;
    private CancellationTokenSource? _work;

    public FilesPage() => InitializeComponent();

    private static Core.Settings.AppSettings Settings => AppServices.Settings;

    public void Refresh()
    {
        var hasVault = !string.IsNullOrWhiteSpace(Settings.VaultPath);
        VaultPath.Text = hasVault ? Settings.VaultPath : "No vault connected.";
        DisconnectVault.IsEnabled = WriteConcepts.IsEnabled = WriteMistakes.IsEnabled = LoadGraph.IsEnabled = hasVault;
        var cli = GraphifySource.FindCli();
        GraphifyUpdate.IsEnabled = hasVault && cli is not null;
        if (_graph is null)
            GraphStatus.Text = !hasVault ? "knowledge graph: connect a vault first"
                : File.Exists(GraphifySource.GraphPath(Settings.VaultPath!)) ? "knowledge graph: a graphify graph.json exists for this vault · press load"
                : cli is not null ? "knowledge graph: no graphify output yet · wikilinks will be used · graphify cli found"
                : "knowledge graph: graphify not installed · wikilinks between notes will be used";

        WorkspacePath.Text = string.IsNullOrWhiteSpace(Settings.WorkspacePath) ? "No workspace folder chosen." : Settings.WorkspacePath;
        LoadFiles();
    }

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (VaultView is null || WorkspaceView is null) return;
        VaultView.Visibility = TabVault.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceView.Visibility = TabWorkspace.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private Window Owner => Window.GetWindow(this)!;

    private void Report(string text) => Status.Text = text;

    private static string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>Notes and files stay local unless the learner has agreed to send them to a remote provider.</summary>
    private bool ConsentToSend(string what)
    {
        var provider = Settings.ActiveProvider;
        if (provider is null) { Report("Choose an AI provider in Settings first."); return false; }
        if (provider.IsLocal || Settings.AllowSendNotesToRemote) return true;
        if (!ConfirmDialog.Ask(Owner, "Send this to a remote provider?",
                $"To answer, the contents of {what} will be sent to {provider.DisplayName}, a remote service.\n\n" +
                "Your notes and files otherwise stay on this computer. Choosing Allow remembers this for notes, files and the summary of your learning record; you can turn it off in Settings › Privacy.",
                "Allow and send")) return false;
        Settings.AllowSendNotesToRemote = true;
        AppServices.SaveSettings();
        return true;
    }

    // ---------- vault ----------

    private void OnChooseVault(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Choose your Obsidian vault folder") is not { } folder) return;
        try
        {
            _ = new PathGuard(folder);
            Settings.VaultPath = folder;
            AppServices.SaveSettings();
            _graph = null;
            Nodes.ItemsSource = null;
            Refresh();
        }
        catch (PathGuardException ex) { Report(ex.Message); }
    }

    private void OnDisconnectVault(object sender, RoutedEventArgs e)
    {
        Settings.VaultPath = null;
        AppServices.SaveSettings();
        _graph = null;
        Nodes.ItemsSource = null;
        Refresh();
        Report("Vault disconnected. Nothing in it was changed.");
    }

    private void ApplyWithPreview(ObsidianVault vault, IEnumerable<FileChange> changes)
    {
        int written = 0, skipped = 0;
        foreach (var change in changes.Where(c => c.After != c.Before))
        {
            if (!ConfirmDialog.Ask(Owner, change.Summary, change.Diff, "Write this change", mono: true)) { skipped++; continue; }
            try
            {
                vault.Apply(change);
                written++;
            }
            catch (Exception ex) when (ex is IOException or PathGuardException or UnauthorizedAccessException)
            {
                Report(ex.Message);
                skipped++;
            }
        }
        Report($"{written} change(s) written, {skipped} skipped.");
        AppServices.Log.Info($"Vault: {written} change(s) written, {skipped} skipped.");
    }

    private void OnWriteConcepts(object sender, RoutedEventArgs e)
    {
        try
        {
            var vault = new ObsidianVault(Settings.VaultPath!);
            var store = AppServices.Store;
            var formulas = store.Formulas();
            var examples = store.Examples();
            var mistakes = store.Misconceptions(includeResolved: false);
            var concepts = store.ConceptStates().Where(c => !c.IsSample).Take(20).ToList();
            if (concepts.Count == 0) { Report("There are no concepts of your own to write yet."); return; }
            ApplyWithPreview(vault, concepts.Select(c => vault.ProposeConceptNote(
                c.Subject, c.Name,
                string.IsNullOrWhiteSpace(c.Summary) ? $"Mastery: {c.Mastery.Level}. {c.Mastery.Reason}" : c.Summary,
                formulas.FirstOrDefault(f => f.Concept == c.Name)?.Expression,
                store.Prerequisites(c.Id),
                mistakes.FirstOrDefault(m => m.ConceptId == c.Id)?.Description,
                examples.FirstOrDefault(x => x.Concept == c.Name) is { } ex ? ex.Problem + "\n\n" + ex.Solution : null)));
            ApplyWithPreview(vault, [vault.ProposeIndex([])]); // the index lists what is now on disk
        }
        catch (PathGuardException ex) { Report(ex.Message); }
    }

    private void OnWriteMistakes(object sender, RoutedEventArgs e)
    {
        try
        {
            var vault = new ObsidianVault(Settings.VaultPath!);
            var open = AppServices.Store.Misconceptions(includeResolved: false).Where(m => !m.Description.StartsWith("(Sample)", StringComparison.Ordinal)).Take(20).ToList();
            if (open.Count == 0) { Report("There are no unresolved mistakes to log."); return; }
            // One preview at a time: each entry is appended to the same file, so each must be built on the last.
            foreach (var m in open) ApplyWithPreview(vault, [vault.ProposeMistakeLogEntry(m.Concept, m.Description)]);
        }
        catch (PathGuardException ex) { Report(ex.Message); }
    }

    private async void OnLoadGraph(object sender, RoutedEventArgs e)
    {
        _work?.Cancel();
        _work = new CancellationTokenSource();
        Report("Reading the graph…");
        try
        {
            var root = Settings.VaultPath!;
            // Graphify's own output is used when it exists; otherwise the links already in the notes.
            _graph = await new GraphifySource().LoadAsync(root, _work.Token) ?? await new WikiLinkGraphSource().LoadAsync(root, _work.Token);
            if (_graph is null) { Report("No graph could be built."); return; }
            Nodes.ItemsSource = _graph.Nodes.Take(300).Select(n => new NodeRow(n.Id, n.Label, n.Degree,
                string.Join(", ", _graph.Neighbours(n.Id).Take(8)))).ToList();
            GraphStatus.Text = $"knowledge graph · source: {_graph.Source} · {_graph.Nodes.Count} node(s), {_graph.Edges.Count} link(s){(_graph.Truncated ? " · truncated at the size limit" : "")} · most connected first";
            Report("");
        }
        catch (OperationCanceledException) { Report("Cancelled."); }
        catch (Exception ex) when (ex is IOException or PathGuardException or System.Text.Json.JsonException or UnauthorizedAccessException) { Report("The graph could not be read: " + ex.Message); }
    }

    private async void OnGraphifyUpdate(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(Owner, "Run graphify on this vault?",
                $"This runs the installed graphify command:\n\n    graphify update \"{Settings.VaultPath}\"\n\n" +
                "It re-extracts code files in the folder and writes its results to a graphify-out subfolder. It makes no AI call. " +
                "To build a full semantic graph of notes and documents, run the /graphify skill in Claude Code on the vault; Vernier then reads the result.",
                "Run graphify")) return;
        GraphifyUpdate.IsEnabled = false;
        Report("graphify is running…");
        var (ok, output) = await GraphifySource.UpdateAsync(Settings.VaultPath!, CancellationToken.None);
        AppServices.Log.Info("graphify update " + (ok ? "succeeded" : "failed"));
        Report(ok ? "graphify finished. Press Load knowledge graph." : "graphify did not finish: " + output.Split('\n').LastOrDefault(l => l.Trim().Length > 0));
        Refresh();
    }

    private void OnNodeSelected(object sender, SelectionChangedEventArgs e) => AskAboutNote.IsEnabled = Nodes.SelectedItem is not null && _graph?.Source != "Graphify";

    private void OnAskAboutNote(object sender, RoutedEventArgs e)
    {
        if (Nodes.SelectedItem is not NodeRow row) return;
        try
        {
            var vault = new ObsidianVault(Settings.VaultPath!);
            var note = vault.Notes(CancellationToken.None).FirstOrDefault(n => n.Title.Equals(row.Label, StringComparison.OrdinalIgnoreCase));
            if (note is null) { Report("That note has no file yet (it is only linked to)."); return; }
            if (!ConsentToSend($"the note \"{note.Title}\"")) return;
            var text = vault.Read(note.RelativePath);
            AskWithContext($"Quiz me on my note \"{note.Title}\": ask one question that checks I understand it rather than remember it.", $"Note: {note.RelativePath}\n\n{text}");
        }
        catch (Exception ex) when (ex is IOException or PathGuardException) { Report(ex.Message); }
    }

    private async void AskWithContext(string question, string context)
    {
        (Owner as MainWindow)?.Navigate("Tutor");
        await AppServices.Session.AskAsync(question, context);
    }

    // ---------- workspace ----------

    private void OnChooseWorkspace(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Choose a project or document folder") is not { } folder) return;
        try
        {
            _ = new PathGuard(folder);
            Settings.WorkspacePath = folder;
            AppServices.SaveSettings();
            Refresh();
        }
        catch (PathGuardException ex) { Report(ex.Message); }
    }

    private void LoadFiles()
    {
        Files.ItemsSource = null;
        FileText.Text = "";
        Explain.IsEnabled = Suggest.IsEnabled = false;
        if (string.IsNullOrWhiteSpace(Settings.WorkspacePath) || !Directory.Exists(Settings.WorkspacePath)) return;
        try
        {
            var files = new ScopedFiles(Settings.WorkspacePath);
            Files.ItemsSource = files.List("*", 3000, CancellationToken.None)
                .Where(f => TextExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).Take(400).ToList();
        }
        catch (PathGuardException ex) { Report(ex.Message); }
    }

    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        Explain.IsEnabled = Suggest.IsEnabled = false;
        if (Files.SelectedItem is not string relative) return;
        try
        {
            FileText.Text = new ScopedFiles(Settings.WorkspacePath!).Read(relative);
            Explain.IsEnabled = Suggest.IsEnabled = true;
            Report("");
        }
        catch (Exception ex) when (ex is IOException or PathGuardException or UnauthorizedAccessException)
        {
            FileText.Text = "";
            Report(ex.Message);
        }
    }

    private void OnExplainFile(object sender, RoutedEventArgs e)
    {
        if (Files.SelectedItem is not string relative || !ConsentToSend($"the file \"{relative}\"")) return;
        var selected = FileText.SelectedText;
        var context = $"File: {relative}\n\n{(selected.Length > 0 ? selected : FileText.Text)}";
        AskWithContext(selected.Length > 0
            ? $"Explain the selected part of {relative}: what it does, why it is written this way, and what I should understand before changing it."
            : $"Explain {relative}: its purpose, how it is organised, and the two or three ideas I need to understand it.", context);
    }

    private async void OnSuggestEdit(object sender, RoutedEventArgs e)
    {
        if (Files.SelectedItem is not string relative) return;
        var instruction = EditInstruction.Text.Trim();
        if (instruction.Length == 0) { Report("Describe the edit you want in the box first."); return; }
        if (!ConsentToSend($"the file \"{relative}\"")) return;

        var files = new ScopedFiles(Settings.WorkspacePath!);
        Suggest.IsEnabled = false;
        Report("Asking for a proposed edit…");
        try
        {
            var original = files.Read(relative);
            var provider = AppServices.Providers.Create(Settings.ActiveProvider!);
            var system = "You revise one file as instructed. Return the complete revised file inside a single fenced code block and nothing else. " +
                         "Change only what the instruction requires. " + Untrusted.SystemRule;
            var prompt = "Instruction from the user: " + instruction + "\n\nFile " + relative + ":\n" + Untrusted.Wrap("file", original, 200_000);
            var reply = await Task.Run(() => ChatRunner.CompleteAsync(provider, new ChatRequest(system, [ChatMessage.User(prompt)], 16000), TimeSpan.FromMinutes(3), CancellationToken.None));
            var match = Regex.Match(reply.Text, @"```[^\n]*\n(?<body>.*)```", RegexOptions.Singleline);
            if (!match.Success) { Report("The provider did not return a file. Nothing was changed."); return; }

            var change = files.ProposeWrite(relative, match.Groups["body"].Value.Replace("\r\n", "\n").Replace("\n", original.Contains("\r\n") ? "\r\n" : "\n"));
            if (change.Before == change.After) { Report("The proposal is identical to the current file."); return; }
            if (!ConfirmDialog.Ask(Owner, $"Apply this edit to {relative}?", change.Diff, "Apply edit", mono: true)) { Report("Edit discarded. The file was not changed."); return; }
            files.Apply(change);
            FileText.Text = change.After;
            Report($"Edit applied to {relative}.");
            AppServices.Log.Info("Applied an approved edit to a workspace file.");
        }
        catch (ProviderException ex) { Report($"The provider could not answer ({ex.Kind}): {ex.Message}"); }
        catch (Exception ex) when (ex is IOException or PathGuardException or UnauthorizedAccessException) { Report(ex.Message); }
        finally { Suggest.IsEnabled = true; }
    }
}
