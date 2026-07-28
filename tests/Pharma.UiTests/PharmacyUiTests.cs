namespace Pharma.UiTests;

/// <summary>
/// The money path, driven through the UI: create a medicine, receive stock,
/// sell it, and check the printed-total arithmetic on screen.
/// </summary>

public class PharmacyUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string CreateMedicineWithStock(string suffix, decimal gstRate, int quantity, decimal mrp)
    {
        var name = $"UI Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");

        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", gstRate.ToString("0.##"));
        app.Click("ProductSave");

        AppFixture.WaitUntil(
            () => app.TextOf("ProductsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "medicine to save");

        // Stock now lives on its own screen; find the medicine there.
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Type("StockBatchNo", $"B{suffix}");
        app.Type("StockQuantity", quantity.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.7m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");

        AppFixture.WaitUntil(
            () => app.TextOf("InventoryStatus").Contains("added to batch", StringComparison.OrdinalIgnoreCase),
            "stock to be added");

        return name;
    }

    [Fact]
    public void A_new_medicine_can_be_created_and_stocked()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = CreateMedicineWithStock(suffix, gstRate: 12m, quantity: 100, mrp: 50m);

        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");

        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine to be listed");

        Assert.Equal(name, app.CellOf("ProductsGrid", "MEDICINE"));
        Assert.Equal("100", app.CellOf("ProductsGrid", "STOCK"));
    }

    [Fact]
    public void Selling_at_mrp_extracts_gst_rather_than_adding_it()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        // MRP 112.00 at 12% GST: 10 units must come to exactly ₹1120, not ₹1254.40.
        var name = CreateMedicineWithStock(suffix, gstRate: 12m, quantity: 50, mrp: 112m);

        app.Navigate("NavSale", "Pharmacy counter");

        app.Type("SaleSearch", name);

        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine to be found");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleQuantity", "10");
        app.Click("SaleAddLine");

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line to be added");
        AppFixture.WaitUntil(() => app.TextOf("SaleNetTotal").Contains("1,120"), "the total to settle");

        Assert.Contains("1,120", app.TextOf("SaleNetTotal"));
    }

    [Fact]
    public void Saving_a_bill_numbers_it_and_deducts_the_stock()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = CreateMedicineWithStock(suffix, gstRate: 5m, quantity: 40, mrp: 25m);

        app.Navigate("NavSale", "Pharmacy counter");

        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine to be found");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleCustomerName", "UI Counter Customer");
        app.Type("SaleQuantity", "4");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line to be added");

        // Save without printing — "Save & print" would open a modal print dialog.
        app.Click("SaleSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SaleStatus").Contains("INV", StringComparison.Ordinal),
            "the bill to be numbered");

        Assert.Contains("INV", app.TextOf("SaleStatus"));
        Assert.Equal(0, app.Grid("SaleLinesGrid").RowCount);   // counter reset for the next customer

        // 40 received, 4 sold.
        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine to be listed");

        Assert.Equal("36", app.CellOf("ProductsGrid", "STOCK"));
    }

    [Fact]
    public void The_day_book_shows_the_bill_that_was_just_saved()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = CreateMedicineWithStock(suffix, gstRate: 12m, quantity: 20, mrp: 60m);

        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine to be found");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleQuantity", "2");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line to be added");
        app.Click("SaleSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SaleStatus").Contains("INV", StringComparison.Ordinal),
            "the bill to be numbered");

        app.Navigate("NavReports", "Reports");

        AppFixture.WaitUntil(
            () => app.TextOf("PageSubtitle").Contains("bill", StringComparison.OrdinalIgnoreCase),
            "the day book to load");

        Assert.Contains("bill", app.TextOf("PageSubtitle"), StringComparison.OrdinalIgnoreCase);
    }
}
