namespace Pharma.Automation.Support;

/// <summary>
/// Creates a medicine and receives an opening batch of stock for it — the
/// precondition Purchase, Medicine Sale and Inventory all need something on the
/// shelf to act on. Mirrors the sequence PharmacyUiTests.CreateMedicineWithStock
/// and InventoryPopupUiTests already prove correct.
/// </summary>
internal static class ProductStockSupport
{
    public static void CreateProductWithStock(
        AppFixture app, string name, string batch, int packs, decimal mrp, string gstRate = "12")
    {
        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", gstRate);
        app.Click("ProductSave");

        AppFixture.WaitUntil(
            () => app.TextOf("ProductsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "medicine to save");

        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", batch);
        app.Type("StockQuantity", packs.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.7m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");

        AppFixture.WaitUntil(
            () => app.TextOf("InventoryStatus").Contains("added to batch", StringComparison.OrdinalIgnoreCase),
            "stock to be added");
    }
}
