using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// The reported fault, driven through the real screens: a medicine whose pack
/// size says 15 while units-per-pack says 1, stock received as 59 strips, and a
/// child who needs 9 tablets.
/// </summary>
public class PackSizeRepairUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string GivenAMedicineSetUpWrongly(string suffix)
    {
        var name = $"Repair Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", "12");

        // Typing the pack size fills units-per-pack in — so type over it to get
        // the wrong state a user could previously save without noticing.
        app.Type("ProductPackSize", "15 TAB");
        app.Type("ProductUnitsPerPack", "1");

        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        // 59 strips onto the shelf.
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Type("StockBatchNo", $"R{suffix}");
        app.Type("StockQuantity", "59");
        app.Type("StockPurchaseRate", "22.00");
        app.Type("StockMrp", "30.00");
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "stock");

        return name;
    }

    /// <summary>
    /// Picks the medicine on the Inventory screen. Receiving stock clears the
    /// screen, so anything wanting to look at what it just received has to find
    /// it again — as the person at the desk does.
    /// </summary>
    private void SelectInInventory(string name)
    {
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, $"'{name}' in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
    }

    [Fact]
    public void The_pack_size_is_taken_from_what_is_typed()
    {
        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductPackSize", "15 TAB");

        // Nobody has to know that "15 TAB" and "units in one pack" are the same
        // fact stated twice — the second one fills itself in.
        AppFixture.WaitUntil(() => app.TextBox("ProductUnitsPerPack").Text == "15",
                             "units per pack to follow the pack size");
    }

    [Fact]
    public void A_disagreement_is_called_out_where_stock_is_handled()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineSetUpWrongly(suffix);

        SelectInInventory(name);

        AppFixture.WaitUntil(() => app.TextOf("InventoryPackWarning").Contains("15"),
                             "the pack size warning");

        Assert.Contains("sell whole packs", app.TextOf("InventoryPackWarning"));
    }

    [Fact]
    public void Correcting_the_medicine_recounts_the_stock_already_on_the_shelf()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineSetUpWrongly(suffix);

        // The shelf currently believes 59 sellable units.
        SelectInInventory(name);
        Assert.Contains("59", app.TextOf("PageSubtitle"));

        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine");
        app.Grid("ProductsGrid").Rows[0].Select();

        app.Type("ProductUnitsPerPack", "15");
        app.Click("ProductSave");

        // It offers to re-count the stock that is already there.
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the re-count question");
        app.MainWindow.ModalWindows[0].FindFirstDescendant(cf => cf.ByName("Yes"))?.AsButton().Invoke();

        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("re-counted"),
                             "the stock to be re-counted");

        // 59 strips of 15 is 885 tablets. Nothing on the shelf moved.
        Assert.Contains("885", app.TextOf("ProductsStatus"));

        // Saving clears the screen, so find it again to read the shelf count.
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine again");

        var cells = app.Grid("ProductsGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();
        Assert.Equal("885", cells[8]);
    }

    [Fact]
    public void Nine_tablets_are_nine_tablets_at_the_counter()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicineSetUpWrongly(suffix);

        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine");
        app.Grid("ProductsGrid").Rows[0].Select();

        app.Type("ProductUnitsPerPack", "15");
        app.Click("ProductSave");

        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the re-count question");
        app.MainWindow.ModalWindows[0].FindFirstDescendant(cf => cf.ByName("Yes"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("re-counted"), "the re-count");

        // The shelf agrees before we go anywhere near the counter. The screen
        // clears on save, so search it out again.
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine again");

        Assert.Equal("885", app.Grid("ProductsGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray()[8]);

        // Two a day for four and a half days.
        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleQuantity", "9");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        // MRP 30.00 for 15 is 2.00 a tablet. Nine tablets is 18.00, not 270.00.
        var total = app.TextOf("SaleNetTotal");
        Assert.Contains("18", total);
        Assert.DoesNotContain("270", total);

        app.Click("SaleSave");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("INV"), "the bill");

        // 885 - 9 = 876. Not 885 - 135.
        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine");

        var cells = app.Grid("ProductsGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();
        Assert.Equal("876", cells[8]);
    }
}
