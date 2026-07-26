namespace Pharma.UiTests;


public class SettingsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void Shop_details_persist_across_screens()
    {
        app.Navigate("NavSettings", "Settings");

        var shopName = $"UI Test Pharmacy {DateTime.Now:HHmmss}";
        app.Type("ShopName", shopName);
        app.Type("ShopGstin", "29ABCDE1234F1Z5");
        app.Click("ShopSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "settings to save");

        // Leave and come back — the values must be read back from the database.
        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");

        AppFixture.WaitUntil(() => app.TextBox("ShopName").Text == shopName, "shop name to reload");

        Assert.Equal(shopName, app.TextBox("ShopName").Text);
        Assert.Equal("29ABCDE1234F1Z5", app.TextBox("ShopGstin").Text);
    }
}
