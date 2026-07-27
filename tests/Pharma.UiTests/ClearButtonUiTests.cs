namespace Pharma.UiTests;

/// <summary>
/// Clear has to clear what is on the screen — including the search box.
///
/// It emptied the form and left the search text sitting there, so the list
/// stayed filtered and the screen did not look cleared at all. Reported from the
/// Medicines screen; the same was true of Patients and Inventory.
/// </summary>
public class ClearButtonUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void Clearing_the_medicine_form_empties_the_search_and_shows_everything()
    {
        app.Navigate("NavProducts", "Medicines");

        // Filter down to one, and start typing a new medicine.
        app.Type("ProductSearch", "Cetirizine");
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, "the filtered list");

        app.Type("ProductName", "Half typed medicine");

        app.Click("ProductClear");

        AppFixture.WaitUntil(() => app.TextBox("ProductSearch").Text == "", "the search box to empty");

        // And the list is no longer filtered to what was typed.
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount > 1, "the whole catalogue");

        Assert.Equal("", app.TextBox("ProductName").Text);
    }

    [Fact]
    public void Clearing_the_patient_form_empties_the_search()
    {
        app.Navigate("NavPatients", "Patients");

        app.Type("PatientsSearchBox", "nobody called this");
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 0, "an empty result");

        app.Type("PatientName", "Half typed name");
        app.Click("PatientClear");

        AppFixture.WaitUntil(() => app.TextBox("PatientsSearchBox").Text == "", "the search box to empty");
        Assert.Equal("", app.TextBox("PatientName").Text);
    }

    [Fact]
    public void Clearing_the_receiving_form_empties_the_search_and_the_selection()
    {
        app.Navigate("NavInventory", "Inventory");

        app.Type("InventorySearch", "Cetirizine");
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the filtered list");
        app.Grid("InventoryProductsGrid").Rows[0].Select();

        app.Type("StockBatchNo", "HALFTYPED");
        app.Click("StockClear");

        AppFixture.WaitUntil(() => app.TextBox("InventorySearch").Text == "", "the search box to empty");

        Assert.Equal("", app.TextBox("StockBatchNo").Text);

        // Nothing selected, so the heading goes back to the count of medicines.
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains("pick one"),
                             "the heading to reset");
    }

    [Fact]
    public void Clearing_the_counter_empties_its_search_too()
    {
        app.Navigate("NavSale", "Pharmacy counter");

        app.Type("SaleSearch", "Cetirizine");
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, "the filtered list");

        app.Click("SaleClear");

        AppFixture.WaitUntil(() => app.TextBox("SaleSearch").Text == "", "the search box to empty");
    }
}
