using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// The quantity box and the unit beside it.
///
/// A bare "9" is what turned nine tablets into nine strips, so the unit is now
/// always on screen next to the number, and the bill can only have its quantity
/// changed — never its price.
/// </summary>
public class CounterQuantityUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string GivenAStockedMedicine(string suffix, int packs = 5)
    {
        var name = $"Qty Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductPackSize", "10 TAB");
        app.Type("ProductGstRate", "12");
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", $"Q{suffix}");
        app.Type("StockQuantity", packs.ToString());
        app.Type("StockPurchaseRate", "21.00");
        app.Type("StockMrp", "30.00");
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "stock");

        return name;
    }

    private void SelectAtCounter(string name)
    {
        app.Navigate("NavSale", "Pharmacy counter");
        app.Click("SaleClear");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 0, "an empty bill");

        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();
    }

    [Fact]
    public void The_unit_is_offered_beside_the_number_and_defaults_to_tablets()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        var unit = app.ComboBox("SaleQuantityUnit");

        AppFixture.WaitUntil(() => unit.Items.Length == 2, "both units on offer");

        // Selling loose is the common case, so it leads.
        Assert.Equal("tablets", unit.SelectedItem.Text);
        Assert.Contains(unit.Items, i => i.Text == "strips of 10");
    }

    [Fact]
    public void Choosing_strips_multiplies_the_number_by_the_pack()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        app.ComboBox("SaleQuantityUnit").Select("strips of 10");
        app.Type("SaleQuantity", "2");
        app.Click("SaleAddLine");

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        // Two strips is twenty tablets, at ₹30.00 a strip.
        Assert.Equal("20", app.CellOf("SaleLinesGrid", "QTY"));      // QTY, in tablets
        Assert.Contains("2 × 10 TAB", app.CellOf("SaleLinesGrid", "PACKS"));  // PACKS reads it back
        Assert.Contains("60", app.TextOf("SaleNetTotal"));
    }

    [Fact]
    public void Tablets_stay_tablets()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        app.Type("SaleQuantity", "9");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        Assert.Equal("9", app.CellOf("SaleLinesGrid", "QTY"));
        Assert.Contains("27", app.TextOf("SaleNetTotal"));   // 9 × ₹3.00
    }

    [Fact]
    public void The_chosen_unit_is_remembered_for_that_medicine()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAStockedMedicine(suffix);

        SelectAtCounter(name);
        app.ComboBox("SaleQuantityUnit").Select("strips of 10");

        // Away and back again.
        app.Navigate("NavReports", "Reports");
        SelectAtCounter(name);

        Assert.Equal("strips of 10", app.ComboBox("SaleQuantityUnit").SelectedItem.Text);
    }

    [Fact]
    public void The_bill_price_cannot_be_edited()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        app.Type("SaleQuantity", "5");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        var header = app.Grid("SaleLinesGrid").Header;
        var columns = header.Columns.Select(c => c.Text).ToArray();

        // Discounts are not given at the counter, so the column is gone entirely.
        Assert.DoesNotContain("DISC %", columns);

        // MRP belongs to the batch. Editing it moved money with nothing recording it.
        var mrp = app.Grid("SaleLinesGrid").Rows[0].Cells[5];
        Assert.False(mrp.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault == false,
                     "MRP must not be editable on a bill line");
    }

    [Fact]
    public void Raising_the_quantity_past_one_batch_re_takes_the_stock()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAStockedMedicine(suffix, packs: 2);   // 20 tablets

        // A second batch, so there is somewhere for the extra to come from.
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", $"Q{suffix}B");
        app.Type("StockQuantity", "3");
        app.Type("StockPurchaseRate", "22.00");
        app.Type("StockMrp", "33.00");
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "the second batch");

        SelectAtCounter(name);
        app.Type("SaleQuantity", "10");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "one line");

        // Asking for more than the first batch holds used to leave the line
        // pinned to it and only fail on save. It now spans batches, as adding
        // would — through the edit-quantity popup rather than the cell
        // itself, which is no longer directly editable.
        EditQuantity(rowIndex: 0, newQuantity: "25");

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 2, "the line to span two batches");

        var total = app.Grid("SaleLinesGrid").Rows
                       .Select((_, i) => int.Parse(app.CellOf("SaleLinesGrid", "QTY", i)))
                       .Sum();

        Assert.Equal(25, total);
    }

    [Fact]
    public void The_edit_quantity_popup_shows_the_new_amount_before_confirming()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        app.Type("SaleQuantity", "5");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        // Clicking anywhere on the row opens the popup — the MEDICINE cell
        // here, specifically not the remove button at the end of the row.
        app.Grid("SaleLinesGrid").Rows[0].Cells[0].Click();

        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the edit-quantity popup");
        var popup = app.MainWindow.ModalWindows[0];

        var quantityBox = popup.FindFirstDescendant(cf => cf.ByAutomationId("EditQuantityValue"))!.AsTextBox();
        quantityBox.Focus();
        quantityBox.Text = "8";

        // 8 tablets at ₹3.00 each, shown live before Update is even pressed.
        AppFixture.WaitUntil(
            () => (popup.FindFirstDescendant(cf => cf.ByAutomationId("EditQuantityAmount"))?.Name ?? "")
                .Contains("24.00"),
            "the live amount preview");

        popup.FindFirstDescendant(cf => cf.ByAutomationId("EditQuantityCancel"))!.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the popup to close");

        // Cancel leaves the line exactly as it was.
        Assert.Equal("5", app.CellOf("SaleLinesGrid", "QTY"));
    }

    [Fact]
    public void Confirming_the_edit_quantity_popup_updates_the_line_and_the_bill_total()
    {
        var name = GivenAStockedMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectAtCounter(name);

        app.Type("SaleQuantity", "5");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        EditQuantity(rowIndex: 0, newQuantity: "8");

        AppFixture.WaitUntil(() => app.CellOf("SaleLinesGrid", "QTY") == "8", "the updated quantity");
        Assert.Contains("24", app.TextOf("SaleNetTotal"));   // 8 × ₹3.00
    }

    /// <summary>Clicks a row to open the edit-quantity popup, types the new
    /// number in, and confirms. The popup is a separate modal window, not
    /// part of the main window's own element tree, so it is found through
    /// MainWindow.ModalWindows rather than through AppFixture's usual
    /// Find/Type helpers.</summary>
    private void EditQuantity(int rowIndex, string newQuantity)
    {
        // The MEDICINE cell, specifically — not the remove button at the
        // end of the row, which has its own action.
        app.Grid("SaleLinesGrid").Rows[rowIndex].Cells[0].Click();

        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the edit-quantity popup");
        var popup = app.MainWindow.ModalWindows[0];

        var quantityBox = popup.FindFirstDescendant(cf => cf.ByAutomationId("EditQuantityValue"))!.AsTextBox();
        quantityBox.Focus();
        quantityBox.Text = newQuantity;

        popup.FindFirstDescendant(cf => cf.ByAutomationId("EditQuantityConfirm"))!.AsButton().Invoke();

        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the popup to close");
    }
}
