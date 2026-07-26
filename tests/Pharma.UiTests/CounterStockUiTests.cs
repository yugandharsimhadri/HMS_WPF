using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The counter putting stock on the shelf itself.
///
/// The operator knows the medicine is in the shop; the system says none. Sending
/// them off to Inventory to do a full goods-inward with a patient waiting is how
/// a counter stops being used, so it can be done from where they are standing.
/// </summary>
public class CounterStockUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string GivenAMedicineWithNoStock(string suffix)
    {
        var name = $"Counter Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductPackSize", "10 TAB");
        app.Type("ProductGstRate", "12");
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        return name;
    }

    private Window OpenQuickStockFor(string name)
    {
        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();

        AppFixture.WaitUntil(() => app.TextOf("SaleSelectedSummary").Contains("out of stock"),
                             "the out-of-stock note");

        app.Click("SaleQuickStock");

        return Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Add stock", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result!;
    }

    private static void Type(Window window, string automationId, string text)
    {
        var box = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))!.AsTextBox();
        box.Focus();
        box.Text = text;
    }

    private static string TextOf(Window window, string automationId)
        => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsLabel().Text ?? "";

    [Fact]
    public void Stock_goes_on_the_shelf_without_leaving_the_bill()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineWithNoStock(suffix);

        var dialog = OpenQuickStockFor(name);

        Assert.Contains(name, TextOf(dialog, "QuickStockMedicine"));

        // The least that can be asked for: how many packs, and the MRP.
        Type(dialog, "QuickStockPacks", "5");
        Type(dialog, "QuickStockMrp", "30.00");

        // It says what that means in tablets before anything is committed.
        AppFixture.WaitUntil(
            () => TextOf(dialog, "QuickStockPreview").Contains("50"),
            "the packs-to-units preview");

        Assert.Contains("5 pack(s) × 10 = 50", TextOf(dialog, "QuickStockPreview"));

        dialog.FindFirstDescendant(cf => cf.ByAutomationId("QuickStockAdd"))!.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.Contains("Add stock", StringComparison.OrdinalIgnoreCase)),
            "the dialog to close");

        // Straight back to the bill, with the medicine now sellable.
        AppFixture.WaitUntil(() => app.TextOf("SaleSelectedSummary").Contains("50"), "the new stock");
        Assert.Contains("reconcile", app.TextOf("SaleStatus"));
    }

    [Fact]
    public void It_can_be_billed_the_moment_it_is_added()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineWithNoStock(suffix);

        var dialog = OpenQuickStockFor(name);
        Type(dialog, "QuickStockPacks", "5");
        Type(dialog, "QuickStockMrp", "30.00");
        dialog.FindFirstDescendant(cf => cf.ByAutomationId("QuickStockAdd"))!.AsButton().Invoke();

        AppFixture.WaitUntil(() => app.TextOf("SaleSelectedSummary").Contains("50"), "the new stock");

        // Nine tablets out of a strip of ten — ₹3.00 each, not ₹30.00.
        app.Type("SaleQuantity", "9");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        AppFixture.WaitUntil(() => app.TextOf("SaleNetTotal").Contains("27"), "the total");
        Assert.DoesNotContain("270", app.TextOf("SaleNetTotal"));

        app.Click("SaleSave");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("INV"), "the bill number");
    }

    [Fact]
    public void Everything_added_this_way_is_listed_for_reconciliation()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineWithNoStock(suffix);

        var dialog = OpenQuickStockFor(name);
        Type(dialog, "QuickStockPacks", "5");
        Type(dialog, "QuickStockMrp", "30.00");
        dialog.FindFirstDescendant(cf => cf.ByAutomationId("QuickStockAdd"))!.AsButton().Invoke();

        AppFixture.WaitUntil(() => app.TextOf("SaleSelectedSummary").Contains("50"), "the new stock");

        // The books can be squared later because nothing was done quietly.
        app.Navigate("NavReports", "Reports");

        // A tab that has never been shown has no content built yet.
        app.MainWindow.FindFirstDescendant(cf => cf.ByName("Stock to reconcile"))!.AsTabItem().Select();

        AppFixture.WaitUntil(() => app.Find("ReconcileGrid") is not null && app.Grid("ReconcileGrid").RowCount >= 1,
                             "the reconciliation list");

        var row = app.Grid("ReconcileGrid").Rows
                     .First(r => (r.Cells[1].Value ?? "").ToString()!.Contains(name));

        var cells = row.Cells.Select(c => c.Value?.ToString() ?? "").ToArray();

        Assert.Contains(name, cells[1]);
        Assert.StartsWith("CTR-", cells[2]);   // allocated, not printed on the pack
        Assert.Equal("50", cells[4]);
    }

    [Fact]
    public void A_medicine_has_to_be_chosen_first()
    {
        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", $"nothing matches this {Guid.NewGuid():N}");

        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 0, "an empty result");

        app.Click("SaleQuickStock");

        // A warning, not a dialog for a medicine nobody picked.
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the warning");

        var warning = app.MainWindow.ModalWindows[0];
        Assert.DoesNotContain("Add stock", warning.Title);
        warning.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton().Invoke();

        app.DismissModals();
    }
}
