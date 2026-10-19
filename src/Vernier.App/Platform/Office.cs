using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Vernier.App.Platform;

/// <summary>
/// Writes into the Office document the learner has open, through Office's own object model, reached from the exact window
/// in front (not "some running Word"): Word inserts paragraphs after the cursor's paragraph, PowerPoint adds a slide after
/// the current one, Excel sets one cell. Every call runs on its own STA thread, releases what it touched before the thread
/// ends, and reads back what it wrote. Nothing is ever deleted, saved, closed or run; formulas that reach outside the
/// workbook are refused, and an Excel write goes only to the cell the learner saw in the preview.
/// </summary>
public static partial class Office
{
    public enum App { None, Word, PowerPoint, Excel }

    /// <summary>Where an Excel write goes, fixed at preview time so approval covers exactly that cell.</summary>
    public sealed record CellTarget(string Book, string Sheet, string Address, string Old);

    /// <summary>The class of the document pane whose native object model is the document window. PowerPoint 2016 and later: mdiClass.</summary>
    private static readonly Dictionary<App, string[]> PaneClass = new() { [App.Word] = ["_WwG"], [App.PowerPoint] = ["paneClassDC", "mdiClass"], [App.Excel] = ["EXCEL7"] };

    public static App Of(string? processName) => processName?.ToLowerInvariant() switch
    {
        "winword" => App.Word, "powerpnt" => App.PowerPoint, "excel" => App.Excel, _ => App.None,
    };

    private const uint OBJID_NATIVEOM = 0xFFFFFFF0;
    private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(nint hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object? obj);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumProc callback, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int max);
    private delegate bool EnumProc(nint hwnd, nint lParam);

    [GeneratedRegex(@"^[A-Z]{1,3}[1-9][0-9]{0,6}$")]
    private static partial Regex CellAddress();

    [GeneratedRegex(@"([A-Z_][A-Z0-9._]*)\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionCall();

    private static nint Pane(nint top, App app)
    {
        nint found = 0;
        EnumChildWindows(top, (h, _) =>
        {
            var name = new StringBuilder(64);
            GetClassName(h, name, name.Capacity);
            if (!PaneClass[app].Contains(name.ToString())) return true;
            found = h;
            return false;
        }, 0);
        return found;
    }

    // The document window object of the pane in front, or a readable reason it cannot be reached.
    private static dynamic Window(nint top, App app)
    {
        var pane = Pane(top, app);
        if (pane == 0) throw new OfficeException($"No {app} document is open in that window (it may be on its start screen or in a dialog).");
        var iid = IID_IDispatch;
        if (AccessibleObjectFromWindow(pane, OBJID_NATIVEOM, ref iid, out var obj) != 0 || obj is null)
            throw new OfficeException($"{app} did not let Vernier reach the document (it may be busy, in a dialog, or in Protected View).");
        return obj;
    }

    /// <summary>A cell as the learner would type it (B2), or null when it is not one plain cell on the sheet in front.</summary>
    public static string? Cell(string? cell) => cell?.Trim().ToUpperInvariant() is { } c && CellAddress().IsMatch(c) ? c : null;

    /// <summary>What a write would do, read before the learner approves it (no change is made). For Excel, also the exact cell it will go to.</summary>
    public static Task<(string Where, CellTarget? Cell)> DescribeAsync(nint top, App app, string? cell) => OnSta(() =>
    {
        dynamic w = Window(top, app);
        switch (app)
        {
            case App.Word:
            {
                dynamic doc = w.Document;
                if ((bool)doc.ReadOnly || (int)doc.ProtectionType != -1) throw new OfficeException($"\"{doc.Name}\" is read-only or protected, so Vernier will not write into it.");
                string paragraph = w.Selection.Range.Paragraphs[1].Range.Text;
                return ($"Word document \"{doc.Name}\", after the paragraph \"{Short(paragraph)}\"", (CellTarget?)null);
            }
            case App.PowerPoint:
            {
                dynamic pres = w.Presentation;
                if ((int)w.Application.SlideShowWindows.Count > 0) throw new OfficeException("The presentation is running as a slide show; Vernier adds slides only while it is being edited.");
                int current = CurrentSlide(w);
                return ($"PowerPoint presentation \"{pres.Name}\", a new slide {current + 1} after slide {current}", null);
            }
            default:
            {
                dynamic sheet = w.ActiveSheet;
                if ((bool)sheet.ProtectContents) throw new OfficeException($"The sheet \"{sheet.Name}\" is protected, so Vernier will not write into it.");
                var address = string.IsNullOrWhiteSpace(cell) ? (string)w.Application.ActiveCell.Address(false, false)
                    : Cell(cell) ?? throw new OfficeException($"\"{cell}\" is not one cell on this sheet. Give a plain address such as B2 (no sheet names, ranges or names).");
                string old = sheet.Range[address].Formula?.ToString() ?? "";
                var target = new CellTarget((string)w.Parent.Name, (string)sheet.Name, address, old);
                return ($"Excel \"{target.Book}\", sheet \"{target.Sheet}\", cell {address} (now {(old.Length == 0 ? "empty" : $"\"{Short(old)}\"")})", target);
            }
        }
    });

    /// <summary>Word: inserts <paramref name="text"/> (paragraphs separated by newlines) as new paragraphs after the one the cursor is in, as one undo step.</summary>
    public static Task<string> WordInsertAsync(nint top, string? heading, string text) => OnSta(() =>
    {
        dynamic w = Window(top, App.Word);
        dynamic doc = w.Document;
        if ((bool)doc.ReadOnly || (int)doc.ProtectionType != -1) throw new OfficeException($"\"{doc.Name}\" is read-only or protected, so nothing was written.");
        dynamic app = w.Application;
        app.UndoRecord.StartCustomRecord("Vernier note");
        try
        {
            // Just before the paragraph mark of the cursor's paragraph: that paragraph stays whole, the new ones follow it.
            int end = w.Selection.Range.Paragraphs[1].Range.End;
            var body = (heading is { Length: > 0 } ? heading + "\r" : "") + text.ReplaceLineEndings("\r");
            dynamic range = doc.Range(end - 1, end - 1);
            range.InsertAfter("\r" + body);
            if (heading is { Length: > 0 }) doc.Range(end, end).Paragraphs[1].Style = -3; // wdStyleHeading2, in every language
            string written = range.Text;
            return $"Inserted {body.Length} characters into \"{doc.Name}\" after the cursor's paragraph; read back: \"{Short(written.Trim())}\". The learner can undo it in one step (Ctrl+Z).";
        }
        finally { app.UndoRecord.EndCustomRecord(); }
    });

    /// <summary>PowerPoint: adds a Title and Content slide after the current one. Returns the new slide's number as read back.</summary>
    public static Task<string> AddSlideAsync(nint top, string title, IReadOnlyList<string> bullets) => OnSta(() =>
    {
        dynamic w = Window(top, App.PowerPoint);
        if ((int)w.Application.SlideShowWindows.Count > 0) throw new OfficeException("The presentation is running as a slide show, so no slide was added.");
        dynamic pres = w.Presentation;
        var index = CurrentSlide(w) + 1;
        dynamic? layout = null;
        foreach (dynamic l in pres.SlideMaster.CustomLayouts) if ((string)l.Name == "Title and Content") { layout = l; break; }
        layout ??= pres.SlideMaster.CustomLayouts[Math.Min(2, (int)pres.SlideMaster.CustomLayouts.Count)];
        dynamic slide = pres.Slides.AddSlide(index, layout);
        if ((int)slide.Shapes.HasTitle != 0) slide.Shapes.Title.TextFrame.TextRange.Text = title;
        if (bullets.Count > 0 && (int)slide.Shapes.Placeholders.Count >= 2) slide.Shapes.Placeholders[2].TextFrame.TextRange.Text = string.Join("\r", bullets);
        w.View.GotoSlide(index);
        return $"Added slide {(int)slide.SlideIndex} \"{Short(title)}\" with {bullets.Count} bullet(s) after slide {index - 1} of \"{pres.Name}\" ({(int)pres.Slides.Count} slides now).";
    });

    /// <summary>
    /// Excel: sets the one cell the learner saw in the preview. It refuses if the learner has since switched workbook or sheet,
    /// or if the cell's content changed; a cell that is not empty is changed only with <paramref name="overwrite"/>.
    /// </summary>
    public static Task<string> SetCellAsync(nint top, CellTarget target, string value, bool overwrite) => OnSta(() =>
    {
        if (FormulaProblem(value) is { } problem) throw new OfficeException(problem);
        if (Cell(target.Address) is null) throw new OfficeException($"\"{target.Address}\" is not one cell; give a plain address such as B2.");
        dynamic w = Window(top, App.Excel);
        dynamic sheet = w.ActiveSheet;
        if ((string)w.Parent.Name != target.Book || (string)sheet.Name != target.Sheet)
            throw new OfficeException($"The learner switched to another workbook or sheet after the preview, so nothing was written. Call write_to_app again for \"{sheet.Name}\" if that is where it should go.");
        if ((bool)sheet.ProtectContents) throw new OfficeException($"The sheet \"{sheet.Name}\" is protected, so nothing was written.");
        dynamic range = sheet.Range[target.Address];
        string old = range.Formula?.ToString() ?? "";
        if (old != target.Old) throw new OfficeException($"Cell {target.Address} changed after the preview (it now holds \"{Short(old)}\"), so nothing was written.");
        if (old.Length > 0 && !overwrite)
            throw new OfficeException($"Cell {target.Address} already holds \"{Short(old)}\". To replace it call write_to_app again with \"overwrite\": true (the learner sees the old value when approving).");
        range.Formula = value;
        string shown = range.Text?.ToString() ?? "";
        return $"Set {target.Sheet}!{target.Address} to \"{Short(value)}\"; it now shows \"{Short(shown)}\"" + (shown.StartsWith('#') ? " (an Excel error: check the formula)." : ".");
    });

    // Functions that reach the network, other files, databases or code: a formula built from the sheet's own cells could send them out.
    private static readonly HashSet<string> Banned = new(StringComparer.OrdinalIgnoreCase)
    {
        "HYPERLINK", "WEBSERVICE", "FILTERXML", "IMAGE", "STOCKHISTORY", "CALL", "REGISTER", "REGISTER.ID", "EXEC", "DDE", "RTD", "SQL.REQUEST", "MSQUERY",
        "IMPORTDATA", "IMPORTXML", "IMPORTHTML", "IMPORTRANGE", "INFO", "CUBEVALUE", "CUBEMEMBER", "CUBESET", "CUBERANKEDMEMBER", "CUBEKPIMEMBER", "CUBEMEMBERPROPERTY", "CUBESETCOUNT", "PY",
    };

    /// <summary>Why a value may not go into a cell: formulas that reach the web, other files or code could leak data or run something.</summary>
    public static string? FormulaProblem(string value)
    {
        var v = value.TrimStart();
        if (v.Length == 0 || v[0] is not ('=' or '+' or '-' or '@')) return null;
        foreach (Match m in FunctionCall().Matches(v))
            if (Banned.Contains(m.Groups[1].Value)) return $"Formulas using {m.Groups[1].Value.ToUpperInvariant()} are not written by Vernier (they reach outside the workbook). Give the learner the formula to type instead.";
        if (v.Contains('[') || v.Contains(":\\") || v.Contains("\\\\") || v.Contains('|') || v.Contains('!')) return "Formulas that refer to other sheets, files or programs are not written by Vernier. Give the learner the formula to type instead.";
        return null;
    }

    private static int CurrentSlide(dynamic w)
    {
        try { return (int)w.View.Slide.SlideIndex; }
        catch (COMException) { return (int)w.Presentation.Slides.Count; } // outline or sorter view: add at the end
    }

    private static string Short(string? s)
    {
        var t = (s ?? "").ReplaceLineEndings(" ").Trim();
        return t.Length > 60 ? t[..60] + "…" : t;
    }

    // On its own STA thread, with Office's COM errors as sentences (busy, a dialog, a cell being edited). Before the thread ends
    // every COM reference it made is released, so Word or Excel can close when the learner closes it.
    private static Task<T> OnSta<T>(Func<T> work) => StaThread.RunAsync(() =>
    {
        try { return work(); }
        catch (COMException e) when ((uint)e.HResult is 0x80010001 or 0x8001010A)
        {
            throw new OfficeException("Office is busy (a dialog is open or a cell is being edited). Ask the learner to finish that, then try again.");
        }
        catch (COMException e) { throw new OfficeException($"Office refused the change ({e.Message.Trim()})."); }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { throw new OfficeException("This version of Office did not accept the change."); }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    });
}

public sealed class OfficeException(string message) : Exception(message);
