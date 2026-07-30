using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The data health screen, opened for real.
///
/// Its grid threw a XamlParseException the first time it was opened against a
/// shop with duplicates — a converter declared on the main window is invisible
/// to another window, and the cell template could not load. Nothing but opening
/// it would have found that.
/// </summary>
public class DataHealthUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private Window OpenHealthCheck()
    {
        app.Navigate("NavSettings", "Settings");
        app.Click("CheckDataHealth");

        return Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Data health", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result!;
    }

    private static void Close(Window window)
        => window.FindFirstDescendant(cf => cf.ByAutomationId("HealthClose"))!.AsButton().Invoke();

    [Fact]
    public void It_opens_and_says_the_shop_is_clean()
    {
        var health = OpenHealthCheck();

        AppFixture.WaitUntil(
            () => (health.FindFirstDescendant(cf => cf.ByAutomationId("HealthSummary"))
                        ?.AsLabel().Text ?? "").Length > 0,
            "the summary");

        var summary = health.FindFirstDescendant(cf => cf.ByAutomationId("HealthSummary"))!.AsLabel().Text;

        // A freshly seeded shop has nothing wrong with it, and must say so
        // rather than showing an empty grid with no explanation.
        Assert.Contains("Nothing to put right", summary);

        Close(health);
    }

    [Fact]
    public void The_grid_renders_when_there_is_something_to_fix()
    {
        // A medicine whose pack size and units-per-pack disagree, which is the
        // state that puts a row — and its Merge button template — on screen.
        var suffix = DateTime.Now.ToString("HHmmssfff");

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", $"Health Drug {suffix}");
        app.Type("ProductPackSize", "15 TAB");
        app.Type("ProductUnitsPerPack", "1");
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine");

        var health = OpenHealthCheck();

        AppFixture.WaitUntil(
            () => health.FindFirstDescendant(cf => cf.ByAutomationId("HealthGrid"))!.AsGrid().RowCount >= 1,
            "the findings");

        var grid = health.FindFirstDescendant(cf => cf.ByAutomationId("HealthGrid"))!.AsGrid();
        var row = grid.Rows.First(r => (r.Cells[1].Value ?? "").ToString()!.Contains(suffix));
        var cells = row.Cells.Select(c => c.Value?.ToString() ?? "").ToArray();

        Assert.Contains("Pack size disagrees", cells[2]);
        Assert.Contains("pack says 15", cells[3]);
        Assert.Contains("15 per pack", cells[4]);

        Close(health);
    }

    [Fact]
    public void Repairing_from_the_screen_puts_the_medicine_right()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Repair Me {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductPackSize", "10 TAB");
        app.Type("ProductUnitsPerPack", "1");
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine");

        var health = OpenHealthCheck();

        AppFixture.WaitUntil(
            () => health.FindFirstDescendant(cf => cf.ByAutomationId("HealthGrid"))!.AsGrid().RowCount >= 1,
            "the findings");

        health.FindFirstDescendant(cf => cf.ByAutomationId("HealthRepair"))!.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => (health.FindFirstDescendant(cf => cf.ByAutomationId("HealthStatus"))
                        ?.AsLabel().Text ?? "").Contains("put right"),
            "the repair to finish");

        Close(health);

        // The counter can now sell it by the tablet. Read it off the catalogue
        // rather than the editor — the grid is what everyone looks at, and it
        // does not need the medicine opened to say so.
        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine");

        AppFixture.WaitUntil(() => app.CellOf("ProductsGrid", "PER PACK") == "10",
                             "units per pack to be corrected");
    }
}
