namespace Pharma.UiTests;


public class NavigationUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void App_opens_on_the_dashboard_screen()
    {
        // Read at launch — the suite shares one app and navigates away.
        Assert.Equal("Dashboard", app.InitialPageTitle);
    }

    [Theory]
    [InlineData("NavDashboard", "Dashboard", null)]
    [InlineData("NavPatients", "Patients", "PatientsGrid")]
    [InlineData("NavSale", "Pharmacy counter", "SaleLinesGrid")]
    [InlineData("NavProducts", "Medicines", "ProductsGrid")]
    [InlineData("NavReports", "Reports", null)]
    [InlineData("NavSettings", "Settings", "SettingsTabs")]
    [InlineData("NavOpd", "OPD", "OpdWaitingList")]
    public void Every_module_opens_from_the_sidebar(string navId, string title, string? expectedElement)
    {
        app.Navigate(navId, title);

        Assert.Equal(title, app.TextOf("PageTitle"));

        if (expectedElement is not null)
            Assert.NotNull(app.Find(expectedElement));
    }

    /// <summary>Pharmacy counter/Medicines/Inventory sit under a collapsible
    /// "PHARMACY" group header — clicking it hides and shows the children,
    /// the same as every clinical module's Patient/Master group does. Ends
    /// by re-expanding, so later tests in this shared-app class still find
    /// NavSale/NavProducts regardless of run order.</summary>
    [Fact]
    public void A_nav_group_can_be_collapsed_and_expanded()
    {
        app.Navigate("NavDashboard", "Dashboard");

        // Pharmacy defaults on and its group defaults expanded, so the
        // children are visible from app start.
        AppFixture.WaitUntil(() => app.Find("NavSale") is not null, "the Pharmacy group to start expanded");

        app.Click("NavPharmacyGroup");
        AppFixture.WaitUntil(() => app.Find("NavSale") is null, "the group's children to collapse");
        Assert.Null(app.Find("NavProducts"));
        Assert.Null(app.Find("NavInventory"));

        app.Click("NavPharmacyGroup");
        AppFixture.WaitUntil(() => app.Find("NavSale") is not null, "the group's children to expand again");
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
