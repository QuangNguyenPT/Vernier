using System.Text.Json;
using Vernier.Core.Knowledge;
using Vernier.Core.Security;
using Xunit;

namespace Vernier.Tests;

public class KnowledgeStoreTests
{
    private static (TempDir Dir, ObsidianVault Vault) Vault()
    {
        var dir = new TempDir();
        return (dir, new ObsidianVault(dir.Path));
    }

    private static void Put(TempDir dir, string relative, string text)
    {
        var full = Path.Combine(dir.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Theory]
    [InlineData("Newton's second law", "newtons second laws")]
    [InlineData("Kinetic Energy", "kinetic-energy")]
    [InlineData("Glass", "glass")] // "ss" is not a plural
    public void The_same_concept_has_the_same_key(string a, string b) => Assert.Equal(ObsidianVault.Key(a), ObsidianVault.Key(b));

    [Fact]
    public void A_concept_already_in_the_vault_is_added_to_wherever_it_lives_by_title_or_alias()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        Put(dir, "Physics/Newtons Second Law.md", "# N2\n\nF = ma.");
        Put(dir, "Physics/Work.md", "---\naliases: [Mechanical work, \"Work done\"]\n---\n# Work");

        var change = vault.ProposeConceptNote("Physics", "Newton's second law", "Force is mass times acceleration.", null, [], null, null);
        Assert.Equal(FileChangeKind.Append, change.Kind);
        Assert.Equal("Physics/Newtons Second Law.md", change.RelativePath);
        Assert.Equal("Physics/Work.md", vault.FindNote("work done")?.RelativePath);
        Assert.Null(vault.FindNote("Torque"));
        Assert.Equal(FileChangeKind.Create, vault.ProposeConceptNote("Physics", "Torque", "Turning effect.", null, [], null, null).Kind);
    }

    [Fact]
    public void A_file_changed_after_the_preview_aborts_the_whole_transaction_and_nothing_is_written()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        var note = vault.ProposeConceptNote("Physics", "Torque", "Turning effect.", null, [], null, null);
        var index = vault.ProposeIndex([note]);
        Put(dir, ObsidianVault.IndexPath, "the learner edited this meanwhile");

        Assert.Throws<IOException>(() => vault.ApplyAll([note, index]));
        Assert.False(File.Exists(note.FullPath)); // the first file was not written either
        Assert.Equal("the learner edited this meanwhile", File.ReadAllText(index.FullPath));
        Assert.False(File.Exists(vault.JournalPath));
    }

    [Fact]
    public void A_write_that_fails_part_way_puts_every_file_back()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        Put(dir, "a.md", "original a");
        var a = vault.ProposeWrite("a.md", "new a");
        var b = vault.ProposeWrite("b.md", "new b");
        Directory.CreateDirectory(Path.Combine(dir.Path, "b.md")); // a folder where the file must go: the move fails

        var e = Assert.Throws<IOException>(() => vault.ApplyAll([a, b]));
        Assert.Contains("put back", e.Message);
        Assert.Equal("original a", File.ReadAllText(a.FullPath));
        Assert.False(File.Exists(vault.JournalPath));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.vernier-tmp", SearchOption.AllDirectories));
    }

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    private static string B64(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public void A_transaction_cut_short_by_a_crash_is_undone_at_the_next_start_but_later_edits_are_kept()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        Put(dir, "a.md", "written by the interrupted transaction");
        Put(dir, "new.md", "created by the interrupted transaction");
        Put(dir, "edited.md", "the learner edited this after the crash");
        File.WriteAllText(vault.JournalPath, JsonSerializer.Serialize(new[]
        {
            new ScopedFiles.JournalEntry("a.md", B64("original a"), Sha("written by the interrupted transaction")),
            new ScopedFiles.JournalEntry("new.md", null, Sha("created by the interrupted transaction")),
            new ScopedFiles.JournalEntry("edited.md", B64("original edited"), Sha("what the transaction wrote")),
        }));

        Assert.True(vault.Recover());
        Assert.Equal("original a", File.ReadAllText(Path.Combine(dir.Path, "a.md")));
        Assert.False(File.Exists(Path.Combine(dir.Path, "new.md")));
        Assert.Equal("the learner edited this after the crash", File.ReadAllText(Path.Combine(dir.Path, "edited.md")));
        Assert.False(vault.Recover()); // nothing left to undo
    }

    [Theory]
    [InlineData("committed")]   // the journal could not be deleted after a finished write
    [InlineData("[{\"Path\":\"a.m")] // cut short while being written: no file had been touched yet
    public void A_journal_that_does_not_describe_a_pending_write_undoes_nothing(string journal)
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        Put(dir, "a.md", "saved");
        File.WriteAllText(vault.JournalPath, journal);
        Assert.True(vault.Recover());
        Assert.Equal("saved", File.ReadAllText(Path.Combine(dir.Path, "a.md")));
        Assert.False(File.Exists(vault.JournalPath));
    }

    [Fact]
    public void Previous_bytes_come_back_exactly_even_in_another_encoding()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        var full = Path.Combine(dir.Path, "latin.md");
        var latin1 = System.Text.Encoding.Latin1.GetBytes("café");
        File.WriteAllBytes(full, latin1);
        var change = vault.ProposeWrite("latin.md", "new");
        var b = vault.ProposeWrite("b.md", "new b");
        Directory.CreateDirectory(Path.Combine(dir.Path, "b.md"));
        Assert.Throws<IOException>(() => vault.ApplyAll([change, b]));
        Assert.Equal(latin1, File.ReadAllBytes(full));
    }

    [Fact]
    public void A_tampered_journal_cannot_write_outside_the_vault()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        using var other = new TempDir();
        var outside = Path.Combine(other.Path, "victim.md");
        File.WriteAllText(outside, "theirs");
        File.WriteAllText(vault.JournalPath, JsonSerializer.Serialize(new[] { new ScopedFiles.JournalEntry(outside, B64("overwritten"), Sha("theirs")) }));
        Assert.Throws<PathGuardException>(() => vault.Recover());
        Assert.Equal("theirs", File.ReadAllText(outside));
    }

    [Fact]
    public void A_junction_in_a_middle_folder_cannot_lead_outside_the_vault()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        using var other = new TempDir();
        File.WriteAllText(Path.Combine(other.Path, "secret.md"), "outside");
        Directory.CreateSymbolicLink(Path.Combine(dir.Path, "sub"), other.Path); // a directory link, as a junction would be
        Assert.Throws<PathGuardException>(() => vault.Read("sub/secret.md"));
        Assert.Throws<PathGuardException>(() => vault.ProposeWrite("sub/new.md", "x"));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1 port")]
    public void Device_names_are_never_note_names(string title) => Assert.EndsWith("(note)", ObsidianVault.SafeName(title));

    [Fact]
    public void The_index_groups_concepts_by_subject_and_keeps_what_the_learner_wrote_around_it()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        var torque = vault.ProposeConceptNote("Physics", "Torque", "Turning effect.", null, [], null, null);
        var limits = vault.ProposeConceptNote("Calculus", "Limits", "Approaching a value.", null, [], null, null);
        var first = vault.ProposeIndex([torque, limits]);
        Assert.Equal(FileChangeKind.Create, first.Kind);
        Assert.Contains("## Calculus", first.After);
        Assert.True(first.After.IndexOf("## Calculus", StringComparison.Ordinal) < first.After.IndexOf("## Physics", StringComparison.Ordinal));
        Assert.Contains("- [[Torque]]", first.After);
        vault.ApplyAll([torque, limits, first]);

        File.WriteAllText(first.FullPath, "My own intro.\n\n" + File.ReadAllText(first.FullPath) + "\n\nMy own footer.");
        var work = vault.ProposeConceptNote("Physics", "Work", "Force times distance.", null, [], null, null);
        var second = vault.ProposeIndex([work]);
        Assert.StartsWith("My own intro.", second.After);
        Assert.EndsWith("My own footer.", second.After.TrimEnd());
        Assert.Contains("- [[Work]]", second.After);
        Assert.Contains("- [[Torque]]", second.After);
        Assert.Equal(1, second.After.Split("<!-- vernier:index -->").Length - 1);
    }

    [Fact]
    public void Search_ranks_title_matches_first_and_returns_a_line_that_matched()
    {
        var (dir, vault) = Vault();
        using var _ = dir;
        Put(dir, "Torque.md", "# Torque\n\nThe turning effect of a force about a pivot.");
        Put(dir, "Levers.md", "# Levers\n\nA lever multiplies force; torque balances on each side.");
        Put(dir, "Cooking.md", "# Bread\n\nFlour and water.");

        var hits = vault.Search("torque of a force", 5, default);
        Assert.Equal(["Torque.md", "Levers.md"], hits.Select(h => h.Note.RelativePath));
        Assert.Contains("torque balances", hits[1].Snippet);
        Assert.Empty(vault.Search("the and of", 5, default)); // only stop words
    }
}
