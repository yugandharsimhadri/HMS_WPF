using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Pharma.UiTests;

/// <summary>
/// Drives the Reports screen end to end, focused on the Stock Register: real
/// stock created through the Medicines/Sale screens must show up (and disappear)
/// correctly, and Export Excel must produce a real, correctly named workbook.
/// </summary>
// IClassFixture is what actually supplies the AppFixture; without it xunit has
// nothing to hand the constructor and every test in the class fails before it
// starts. Every other UI class here declares it the same way.
public class ReportsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string CreateMedicineWithStock(string suffix, int quantity, decimal mrp)
    {
        var name = $"Stock UI Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", "12");
        app.Click("ProductSave");

        AppFixture.WaitUntil(
            () => app.TextOf("ProductsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "medicine to save");

        // Stock lives on its own screen: Medicines describes a medicine, Inventory
        // is where it arrives. Find it there first.
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Type("StockBatchNo", $"SB{suffix}");
        app.Type("StockQuantity", quantity.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.7m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");

        AppFixture.WaitUntil(
            () => app.TextOf("InventoryStatus").Contains("added to batch", StringComparison.OrdinalIgnoreCase),
            "stock to be added");

        return name;
    }

    /// <summary>Sells every unit of a medicine's only batch, so the batch is
    /// depleted (QtyOnHand 0) rather than deleted.</summary>
    private void SellAllStock(string medicineName, int quantity)
    {
        app.Navigate("NavSale", "Pharmacy counter");

        // The counter filters as you type now — there is no Find button, and no
        // batch to pick: nearest expiry is chosen for the operator.
        app.Type("SaleSearch", medicineName);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine to be found");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleQuantity", quantity.ToString());
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the line to be added");

        app.Click("SaleSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SaleStatus").Contains("INV", StringComparison.Ordinal),
            "the bill to be numbered");
    }

    /// <summary>
    /// Every tab shows its own content, not just its own highlight.
    ///
    /// The tabs are drawn by the theme rather than by Windows, and a retemplated
    /// TabControl that loses the part its content is hosted in still selects
    /// perfectly — it just shows a blank page underneath. Selecting a tab proves
    /// nothing on its own, so each one is checked for something only it has.
    /// </summary>
    [Theory]
    [InlineData("Day book", "ReportsDayBookGrid")]
    [InlineData("GST summary", "ReportsGstGrid")]
    [InlineData("OPD register", "ReportsOpdGrid")]
    [InlineData("Expiring soon", "ReportsExpiringGrid")]
    [InlineData("Part packs", "PartPacksGrid")]
    [InlineData("Stock to reconcile", "ReconcileGrid")]
    [InlineData("Low stock", "ReportsLowStockGrid")]
    [InlineData("Stock Register", "ReportsStockGrid")]
    [InlineData("Schedule H1 register", "ReportsH1Grid")]
    public void Every_report_tab_shows_its_own_content(string header, string grid)
    {
        app.Navigate("NavReports", "Reports");
        app.SelectTab("ReportsTabs", header);

        AppFixture.WaitUntil(() => app.Find(grid) is not null, $"the {header} report");
    }

    [Fact]
    public void Reports_screen_opens_all_seven_tabs_and_the_stock_register_lists_real_stock()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = CreateMedicineWithStock(suffix, quantity: 7, mrp: 45m);

        app.Navigate("NavReports", "Reports");
        Assert.Equal("Reports", app.TextOf("PageTitle"));

        // The six previously-accepted tabs plus the new seventh must all still open.
        foreach (var header in new[]
                 {
                     "Day book", "GST summary", "OPD register", "Expiring soon",
                     "Low stock", "Stock Register", "Schedule H1 register"
                 })
        {
            app.SelectTab("ReportsTabs", header);
        }

        app.SelectTab("ReportsTabs", "Stock Register");
        app.Type("ReportsStockSearch", name);

        AppFixture.WaitUntil(() => app.Grid("ReportsStockGrid").RowCount == 1, "the new medicine's batch to appear");

        var cells = app.Grid("ReportsStockGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();
        Assert.Equal(name, cells[0]);       // MEDICINE
        Assert.Equal("7", cells[6]);        // CURRENT STOCK
    }

    [Fact]
    public void Include_zero_stock_hides_and_reveals_a_depleted_batch()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = CreateMedicineWithStock(suffix, quantity: 2, mrp: 30m);
        SellAllStock(name, 2);

        app.Navigate("NavReports", "Reports");
        app.SelectTab("ReportsTabs", "Stock Register");
        app.Type("ReportsStockSearch", name);

        var includeZero = app.CheckBox("ReportsStockIncludeZero");
        if (includeZero.IsChecked == true) includeZero.Toggle();

        AppFixture.WaitUntil(() => app.Grid("ReportsStockGrid").RowCount == 0, "the depleted batch to be hidden by default");

        includeZero.Toggle();
        AppFixture.WaitUntil(() => app.Grid("ReportsStockGrid").RowCount == 1, "the depleted batch to reappear");

        var cells = app.Grid("ReportsStockGrid").Rows[0].Cells.Select(c => c.Value ?? "").ToArray();
        Assert.Equal(name, cells[0]);
        Assert.Equal("0", cells[6]);
    }

    [Fact]
    public void Export_excel_writes_a_correctly_named_stock_register_workbook()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        CreateMedicineWithStock(suffix, quantity: 5, mrp: 20m);

        app.Navigate("NavReports", "Reports");
        app.SelectTab("ReportsTabs", "Stock Register");

        // ReportsViewModel is a singleton — clear any search left over from
        // another test in this shared-app collection so the export isn't
        // filtered down to zero rows (which would disable the button).
        app.Type("ReportsStockSearch", "");

        var expectedName = $"StockRegister_{DateTime.Today:yyyy-MM-dd}.xlsx";
        var expectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), expectedName);
        if (File.Exists(expectedPath)) File.Delete(expectedPath);

        try
        {
            var button = app.Button("ReportsExportExcel");
            Assert.True(button.IsEnabled, "Export Excel button was disabled — the register was empty when clicked.");
            button.Invoke();

            // The native Save dialog is a modal, separate-process common dialog on
            // this OS — this test's UI Automation cannot reliably enumerate it as a
            // window, even though it genuinely appears (confirmed via the app log).
            // Pressing Enter accepts its default (Save) button without needing to
            // find the window at all, exactly as a user tabbing to Save and hitting
            // Enter would.
            Thread.Sleep(800);
            Keyboard.Press(VirtualKeyShort.RETURN);

            // Checked together, not existence-then-length: SaveAs briefly leaves an
            // empty/partial file on disk before its content is fully flushed.
            AppFixture.WaitUntil(
                () => File.Exists(expectedPath) && new FileInfo(expectedPath).Length > 500,
                "the workbook to be fully written on disk", 20);

            var info = new FileInfo(expectedPath);
            Assert.True(info.Exists);
            Assert.True(info.Length > 500, "Exported workbook is suspiciously small.");
        }
        finally
        {
            try { if (File.Exists(expectedPath)) File.Delete(expectedPath); } catch (IOException) { }
        }
    }
}
