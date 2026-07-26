using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// The two rules the counter has to hold, both of which were previously
/// recorded on the medicine and then ignored at the till.
/// </summary>
public class CounterRulesUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string GivenAMedicine(string suffix, bool allowLoose, string schedule)
    {
        var name = $"Rule Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductPackSize", "10 TAB");
        app.Type("ProductGstRate", "12");
        app.ComboBox("ProductSchedule").Select(schedule);
        // Assigning the value it already holds toggles it, so only change it.
        var loose = app.CheckBox("ProductAllowLoose");
        if (loose.IsChecked != allowLoose) loose.IsChecked = allowLoose;
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Type("StockBatchNo", $"R{suffix}");
        app.Type("StockQuantity", "5");
        app.Type("StockPurchaseRate", "21.00");
        app.Type("StockMrp", "30.00");
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "stock");

        return name;
    }

    private void SelectAtCounter(string name)
    {
        app.Navigate("NavSale", "Pharmacy counter");

        // A half-built bill survives navigating away on purpose, so start clean.
        app.Click("SaleClear");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 0, "an empty bill");

        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();
    }

    private void DismissWarning()
    {
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the warning");
        app.MainWindow.ModalWindows[0].FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton().Invoke();
        app.DismissModals();
    }

    [Fact]
    public void A_medicine_not_sold_loose_goes_out_in_whole_packs()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix, allowLoose: false, schedule: "None");

        SelectAtCounter(name);

        // Seven out of a strip of ten is refused, with the number to type instead.
        app.Type("SaleQuantity", "7");
        app.Click("SaleAddLine");

        DismissWarning();
        Assert.Contains("whole packs", app.TextOf("SaleStatus"));
        Assert.Contains("10", app.TextOf("SaleStatus"));
        Assert.Equal(0, app.Grid("SaleLinesGrid").RowCount);

        // A whole strip is fine.
        app.Type("SaleQuantity", "10");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the whole pack");
    }

    [Fact]
    public void A_medicine_sold_loose_still_takes_any_quantity()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix, allowLoose: true, schedule: "None");

        SelectAtCounter(name);

        app.Type("SaleQuantity", "7");
        app.Click("SaleAddLine");

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the loose sale");
    }

    [Fact]
    public void A_schedule_H1_sale_cannot_be_saved_without_the_prescriber()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix, allowLoose: true, schedule: "H1");

        SelectAtCounter(name);
        app.Type("SaleQuantity", "4");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line");

        // The register is kept for three years and the prescriber is the point of it.
        app.Type("SaleDoctorName", "");
        app.Click("SaleSave");

        DismissWarning();
        Assert.Contains("H1", app.TextOf("SaleStatus"));
        Assert.DoesNotContain("INV", app.TextOf("SaleStatus"));

        // With a prescriber it saves.
        app.Type("SaleDoctorName", "Dr. A. Kumar");
        app.Click("SaleSave");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("INV"), "the bill number");
    }
}
