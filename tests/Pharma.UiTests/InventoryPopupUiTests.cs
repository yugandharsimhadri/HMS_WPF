using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Pharma.UiTests;

/// <summary>
/// Receiving and correcting are two jobs, so they are two forms, and each opens
/// over the shell against one medicine chosen on the page behind.
///
/// The old screen had both in a 380px column that was never empty: whatever the
/// last delivery left behind was still sitting there when the next one started.
/// These tests are mostly about that — what is in the form when it opens, and
/// what is left on the screen when it closes.
/// </summary>
public class InventoryPopupUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    /// <summary>A medicine sold in strips of ten, with nothing on the shelf yet.</summary>
    private string GivenAMedicine(string suffix)
    {
        var name = $"Popup Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductPackSize", "10 TAB");
        app.Type("ProductGstRate", "12");
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        return name;
    }

    private void SelectInInventory(string name)
    {
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, $"'{name}' in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
    }

    private void ReceiveOnto(string name, string batch, int packs, decimal mrp)
    {
        SelectInInventory(name);
        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is not null, "the receiving form");

        app.Type("StockBatchNo", batch);
        app.Type("StockQuantity", packs.ToString());
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");

        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "the stock");
    }

    /// <summary>Reads a warning message box, checks it, and closes it.</summary>
    private void DismissWarning(string expected)
    {
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the warning");

        var modal = app.MainWindow.ModalWindows[0];
        var said = string.Join(" ", modal.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                                         .Select(t => t.Name ?? ""));

        Assert.Contains(expected, said, StringComparison.OrdinalIgnoreCase);

        modal.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the warning to close");
    }

    [Fact]
    public void The_receiving_form_names_the_medicine_it_is_receiving()
    {
        var name = GivenAMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectInInventory(name);

        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("ReceiveStockHeader") is not null, "the receiving form");

        // Which medicine, and what is already there. A delivery line typed
        // against the wrong medicine looks exactly like one typed against the
        // right one, so the form says so at the top rather than nowhere.
        Assert.Contains(name, app.TextOf("ReceiveStockHeader"));
        Assert.Contains("one pack is 10", app.TextOf("ReceiveStockOnHand"));

        app.Click("ReceiveStockCancel");
    }

    /// <summary>
    /// The receiving form has no Clear button any more: Cancel takes it and
    /// everything typed into it away. What matters is that backing out receives
    /// nothing, and that the next delivery starts from an empty form.
    /// </summary>
    [Fact]
    public void Cancelling_the_receiving_form_receives_nothing()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix);

        ReceiveOnto(name, $"K{suffix}", packs: 4, mrp: 25.00m);
        SelectInInventory(name);

        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is not null, "the receiving form");

        app.Type("StockBatchNo", "HALFTYPED");
        app.Type("StockQuantity", "99");
        app.Click("ReceiveStockCancel");

        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is null, "the form to close");

        // Nothing went onto the shelf, and the half-typed batch is not waiting
        // to be saved onto the next delivery.
        Assert.Equal("40", app.CellOf("InventoryProductsGrid", "ON HAND"));

        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is not null, "the form again");
        Assert.Equal("", app.TextBox("StockBatchNo").Text);

        app.Click("ReceiveStockCancel");
    }

    [Fact]
    public void Clearing_the_inventory_screen_empties_the_search_and_the_selection()
    {
        var name = GivenAMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectInInventory(name);

        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains(name),
                             "the heading to name the medicine");

        app.Click("InventoryClear");

        AppFixture.WaitUntil(() => app.TextBox("InventorySearch").Text == "", "the search box to empty");

        // Nothing selected, so the heading goes back to the count of medicines.
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains("pick one"),
                             "the heading to reset");
    }

    [Fact]
    public void It_reads_back_what_arrives_in_tablets_before_anything_is_saved()
    {
        var name = GivenAMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectInInventory(name);

        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("StockQuantity") is not null, "the receiving form");

        app.Type("StockQuantity", "5");

        // "Qty" is the most misread field in a pharmacy: the shop counts strips,
        // the counter sells tablets. The form does the multiplication out loud.
        AppFixture.WaitUntil(() => app.TextOf("StockIntakePreview").Contains("50"), "the preview");
        Assert.Contains("5 pack(s) × 10 = 50", app.TextOf("StockIntakePreview"));

        app.Click("ReceiveStockCancel");
    }

    [Fact]
    public void Receiving_closes_the_form_and_leaves_the_screen_clear()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix);

        ReceiveOnto(name, $"P{suffix}", packs: 4, mrp: 25.00m);

        // The form went away with everything typed into it.
        Assert.Null(app.Find("StockBatchNo"));

        // And so did the search and the selection: receiving twice against a
        // medicine still sitting selected is how one delivery becomes two.
        AppFixture.WaitUntil(() => app.TextBox("InventorySearch").Text == "", "the search to empty");
        Assert.Contains("pick one", app.TextOf("PageSubtitle"));

        // 4 strips of 10.
        SelectInInventory(name);
        Assert.Equal("40", app.CellOf("InventoryProductsGrid", "ON HAND"));
    }

    [Fact]
    public void There_is_nothing_to_correct_before_anything_has_arrived()
    {
        var name = GivenAMedicine(DateTime.Now.ToString("HHmmssfff"));
        SelectInInventory(name);

        app.Click("InventoryCorrect");

        // It says why rather than opening a form with an empty batch list, which
        // only invites a correction against whatever else was selected.
        DismissWarning("no stock on the shelf");
        Assert.Null(app.Find("CorrectQuantity"));
    }

    [Fact]
    public void A_correction_is_written_down_with_its_reason()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix);

        ReceiveOnto(name, $"C{suffix}", packs: 4, mrp: 25.00m);

        SelectInInventory(name);
        app.Click("InventoryCorrect");
        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is not null, "the correction form");

        // One batch, so it is already chosen, and the box starts at what the
        // system believes — the operator changes a number rather than typing one
        // from nothing.
        Assert.Equal("40", app.TextBox("CorrectQuantity").Text);

        app.Type("CorrectQuantity", "37");
        app.Type("CorrectNotes", $"Counted the shelf {suffix}");
        app.Click("CorrectStock");

        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is null, "the correction form to close");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("40 → 37"), "the correction");

        // Stock only ever moves by receiving or selling, and both leave a
        // document. A correction has none, so it writes one.
        AppFixture.WaitUntil(() => app.Grid("AdjustmentsGrid").RowCount >= 1, "the corrections list");
        Assert.Equal(name, app.CellOf("AdjustmentsGrid", "MEDICINE"));
        Assert.Equal("40", app.CellOf("AdjustmentsGrid", "WAS"));
        Assert.Equal("37", app.CellOf("AdjustmentsGrid", "NOW"));
        Assert.Contains(suffix, app.CellOf("AdjustmentsGrid", "NOTES"));
    }

    [Fact]
    public void Backing_out_of_a_correction_changes_nothing()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAMedicine(suffix);

        ReceiveOnto(name, $"X{suffix}", packs: 4, mrp: 25.00m);

        SelectInInventory(name);
        app.Click("InventoryCorrect");
        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is not null, "the correction form");

        app.Type("CorrectQuantity", "0");
        app.Click("CorrectStockCancel");

        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is null, "the form to close");

        // Nothing was written off, and nothing was recorded as if it had been.
        SelectInInventory(name);
        Assert.Equal("40", app.CellOf("InventoryProductsGrid", "ON HAND"));
    }
}
