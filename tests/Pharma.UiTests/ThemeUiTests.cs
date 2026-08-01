using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// Switching between light and dark.
///
/// The palette is swapped at runtime, so the screen has to repaint without a
/// restart — a half-themed window, or one that only changes after closing the
/// application, is worse than not offering the choice.
/// </summary>
public class ThemeUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void Choose(string theme)
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "General");
        app.ComboBox("AppThemeChoice").Select(theme);
    }

    [Fact]
    public void The_theme_can_be_switched_and_the_window_repaints()
    {
        Choose("Dark");

        // Nothing to assert about colour through automation, but a screen that
        // fell over mid-swap would stop responding — so drive it afterwards.
        app.Navigate("NavSale", "Pharmacy counter");
        Assert.Equal("Pharmacy counter", app.TextOf("PageTitle"));

        app.Navigate("NavOpd", "OPD");
        Assert.Equal("OPD", app.TextOf("PageTitle"));

        Choose("Light");

        app.Navigate("NavReports", "Reports");
        Assert.Equal("Reports", app.TextOf("PageTitle"));
    }

    [Fact]
    public void The_choice_is_remembered()
    {
        Choose("Dark");
        app.Click("GeneralSave");

        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("dark"), "the saved confirmation");

        // Away and back: the dropdown still shows what was chosen.
        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "General");

        Assert.Equal("Dark", app.ComboBox("AppThemeChoice").SelectedItem.Text);

        // Put it back so the screenshots and the other classes are unaffected.
        Choose("Light");
        app.Click("GeneralSave");
        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("light"), "the theme to be restored");
    }
}
