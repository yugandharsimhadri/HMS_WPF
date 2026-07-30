namespace Pharma.UiTests;

/// <summary>
/// What the screen looks like once the thing has been done.
///
/// Saving used to leave the record loaded and the list still filtered to it. It
/// reads as "ready for the next one" and is not: the next name typed into that
/// form saves over the one just entered instead of adding anybody. The same
/// applied at the counter, where the medicine already on the bill stayed in the
/// search box and selected.
/// </summary>
public class AfterSaveUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private string GivenAStockedMedicine(string suffix)
    {
        var name = $"After Drug {suffix}";

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

        app.Type("StockBatchNo", $"A{suffix}");
        app.Type("StockQuantity", "5");
        app.Type("StockPurchaseRate", "21.00");
        app.Type("StockMrp", "30.00");
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "stock");

        return name;
    }

    // ── Patients ───────────────────────────────────────────────────────────

    [Fact]
    public void Saving_a_patient_closes_the_form_and_empties_the_search()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Cleared {suffix}";

        app.Navigate("NavPatients", "Patients");
        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.TextOf("PatientsStatus").Contains("saved"), "the patient to save");

        // The form is a layer over the shell, so "clear" means it has gone,
        // taking everything typed into it with it.
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the form to close");
        AppFixture.WaitUntil(() => app.TextBox("PatientsSearchBox").Text == "", "the search box to empty");

        // Cleared, not lost.
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient that was saved");
    }

    /// <summary>
    /// Backing out writes nothing, and leaves nothing behind for the next
    /// patient to be saved on top of.
    /// </summary>
    [Fact]
    public void Cancelling_the_patient_form_writes_nothing()
    {
        app.Navigate("NavPatients", "Patients");
        var before = app.Grid("PatientsGrid").RowCount;

        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");
        app.Type("PatientName", "Half typed child");
        app.Click("PatientEditorCancel");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the form to close");
        Assert.Equal(before, app.Grid("PatientsGrid").RowCount);

        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the form again");
        Assert.Equal("", app.TextBox("PatientName").Text);

        app.Click("PatientEditorCancel");
    }

    [Fact]
    public void Clearing_the_register_empties_the_search_and_the_selection()
    {
        app.Navigate("NavPatients", "Patients");
        var whole = app.Grid("PatientsGrid").RowCount;

        app.Type("PatientsSearchBox", "nobody called this");
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 0, "an empty result");

        app.Click("PatientClear");

        AppFixture.WaitUntil(() => app.TextBox("PatientsSearchBox").Text == "", "the search box to empty");

        // The filter went with it — leaving the box full kept the list narrowed,
        // so the screen did not look cleared at all.
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == whole, "the whole register");
    }

    /// <summary>
    /// The fault itself: two patients entered one after the other have to end up
    /// as two records. With the first left selected, the second was saved on top
    /// of it and the first child disappeared from the register.
    /// </summary>
    [Fact]
    public void The_next_patient_is_a_new_record_not_an_edit_of_the_last()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");

        app.Navigate("NavPatients", "Patients");

        foreach (var child in new[] { "First", "Second" })
        {
            app.Click("PatientsNew");
            AppFixture.WaitUntil(() => app.Find("PatientName") is not null, $"the form for {child}");

            app.Type("PatientName", $"{child} {suffix}");
            app.Type("PatientPhone", $"9{suffix}");
            app.Click("PatientSave");

            AppFixture.WaitUntil(() => app.Find("PatientName") is null, $"{child} to save");
        }

        // Both are on the register, on the one number they share.
        app.Type("PatientsSearchBox", suffix);
        app.Click("PatientsSearchButton");

        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 2, "both patients");
    }

    // ── Pharmacy counter ───────────────────────────────────────────────────

    [Fact]
    public void Adding_to_the_bill_empties_the_counter_search()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = GivenAStockedMedicine(suffix);

        app.Navigate("NavSale", "Pharmacy counter");
        app.Click("SaleClear");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 0, "an empty bill");

        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length == 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleQuantity", "4");
        app.Click("SaleAddLine");

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");

        // The line is on the bill, so the search that found it goes.
        AppFixture.WaitUntil(() => app.TextBox("SaleSearch").Text == "", "the search box to empty");

        // And nothing is left selected, so Add cannot re-lay the same line.
        AppFixture.WaitUntil(() => app.TextOf("SaleSelectedSummary") == "", "the selection to clear");

        // The bill itself is untouched by the clearing.
        Assert.Equal(1, app.Grid("SaleLinesGrid").RowCount);

        // And Clear empties the search box as well as the bill — leaving it full
        // kept the list filtered, so the screen did not look cleared at all.
        app.Type("SaleSearch", name);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, "the filtered list");

        app.Click("SaleClear");
        AppFixture.WaitUntil(() => app.TextBox("SaleSearch").Text == "", "the counter search to empty");
    }

    // ── Medicines ──────────────────────────────────────────────────────────

    [Fact]
    public void Saving_a_medicine_empties_the_search_and_the_form()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"After Save Drug {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", "12");
        app.Click("ProductSave");

        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), "the medicine to save");

        // The editor is a layer over the shell now, so "the form is clear" means
        // it has gone. There is no half-filled form left behind to clear,
        // because the next medicine gets a new one.
        AppFixture.WaitUntil(() => app.Find("ProductName") is null, "the editor to close");

        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the medicine that was saved");
    }

    /// <summary>
    /// The medicine form has no Clear button any more, because it has nothing to
    /// clear: it opens over the shell holding one medicine and Cancel takes it
    /// away. What matters is that backing out writes nothing.
    /// </summary>
    [Fact]
    public void Cancelling_the_medicine_editor_writes_nothing()
    {
        app.Navigate("NavProducts", "Medicines");
        var before = app.Grid("ProductsGrid").RowCount;

        app.Click("ProductsNew");
        app.Type("ProductName", "Half typed medicine");
        app.Click("MedicineEditorCancel");

        AppFixture.WaitUntil(() => app.Find("ProductName") is null, "the editor to close");
        Assert.Equal(before, app.Grid("ProductsGrid").RowCount);

        app.Click("ProductsNew");
        AppFixture.WaitUntil(() => app.Find("ProductName") is not null, "the editor again");
        Assert.Equal("", app.TextBox("ProductName").Text);

        app.Click("MedicineEditorCancel");
    }

    // ── Inventory ──────────────────────────────────────────────────────────

    /// <summary>
    /// The next line of a supplier's invoice is usually a different medicine, and
    /// a batch received against whichever one was left selected is stock counted
    /// onto the wrong shelf.
    /// </summary>
    [Fact]
    public void Receiving_stock_lets_go_of_the_medicine()
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        GivenAStockedMedicine(suffix);

        AppFixture.WaitUntil(() => app.TextBox("InventorySearch").Text == "", "the search box to empty");

        // The form is a layer over the shell now, so "clear" means it has gone,
        // taking the batch, rate, supplier and invoice number with it.
        Assert.Null(app.Find("StockBatchNo"));

        // Nothing selected, so the heading goes back to the count of medicines.
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains("pick one"), "the heading to reset");

        // The confirmation still says what went on the shelf.
        Assert.Contains("added to batch", app.TextOf("InventoryStatus"));
    }
}
