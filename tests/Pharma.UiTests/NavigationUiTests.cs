namespace Pharma.UiTests;


public class NavigationUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void App_opens_on_the_opd_screen()
    {
        // Read at launch — the suite shares one app and navigates away.
        Assert.Equal("OPD", app.InitialPageTitle);
    }

    [Theory]
    [InlineData("NavPatients", "Patients", "PatientsGrid")]
    [InlineData("NavSale", "Pharmacy counter", "SaleLinesGrid")]
    [InlineData("NavProducts", "Medicines", "ProductsGrid")]
    [InlineData("NavReports", "Reports", null)]
    [InlineData("NavSettings", "Settings", "ShopName")]
    [InlineData("NavOpd", "OPD", "OpdWaitingList")]
    public void Every_module_opens_from_the_sidebar(string navId, string title, string? expectedElement)
    {
        app.Navigate(navId, title);

        Assert.Equal(title, app.TextOf("PageTitle"));

        if (expectedElement is not null)
            Assert.NotNull(app.Find(expectedElement));
    }

    [Fact]
    public void The_catalogue_lists_the_seeded_medicines()
    {
        app.Navigate("NavProducts", "Medicines");

        // Another test may have left a filter in the search box.
        app.Type("ProductSearch", "");
        app.Click("ProductsSearchButton");

        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount >= 6, "the seeded medicines");

        Assert.True(app.Grid("ProductsGrid").RowCount >= 6);
    }
}
