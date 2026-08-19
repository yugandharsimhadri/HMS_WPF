using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// OPD and Pharmacy are no longer hardcoded — they are two more module
/// toggles under Settings → Features, exactly like Diagnostics already is,
/// except that both default on rather than off (see
/// <see cref="Pharma.Data.GeneralSettings.OpdEnabled"/>). What this guards:
/// the sidebar reacts to either toggle immediately, with no restart needed,
/// and the application refuses to let every module be switched off at once.
/// </summary>
public class ModuleFeatureTogglesUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void SaveFeatures()
    {
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save");
    }

    [Fact]
    public void Turning_off_OPD_hides_its_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("OpdEnabled").IsChecked = false;
        SaveFeatures();
        AppFixture.WaitUntil(() => app.Find("NavOpd") is null, "the OPD nav button to disappear");

        app.CheckBox("OpdEnabled").IsChecked = true;
        SaveFeatures();
        AppFixture.WaitUntil(() => app.Find("NavOpd") is not null, "the OPD nav button to reappear");

        // Leave and come back through a nav item that is never gated — Patients
        // is the one screen every module needs — and confirm the checkbox
        // itself reloads correctly, not just the nav button.
        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        AppFixture.WaitUntil(() => app.CheckBox("OpdEnabled").IsChecked == true, "the toggle to reload as on");
    }

    [Fact]
    public void Turning_off_Pharmacy_hides_the_counter_medicines_and_inventory_nav_buttons()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("PharmacyEnabled").IsChecked = false;
        SaveFeatures();

        AppFixture.WaitUntil(() => app.Find("NavSale") is null, "the Pharmacy counter nav button to disappear");
        Assert.Null(app.Find("NavProducts"));
        Assert.Null(app.Find("NavInventory"));

        app.CheckBox("PharmacyEnabled").IsChecked = true;
        SaveFeatures();
        AppFixture.WaitUntil(() => app.Find("NavSale") is not null, "the Pharmacy counter nav button to reappear");
    }

    /// <summary>
    /// A dentist-only clinic is the scenario the whole toggle redesign exists
    /// for: everything else, OPD and Pharmacy included, switched off.
    /// </summary>
    [Fact]
    public void OPD_and_Pharmacy_can_both_be_switched_off_at_once()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        // Diagnostics is off by default and this class never turns it on, so
        // switching off OPD and Pharmacy here leaves no module enabled at
        // all — which is exactly the state the guard in the next test
        // refuses. Leave Diagnostics on first so this save is legal on its
        // own, then the "everything off" test below covers the refusal.
        app.CheckBox("DiagnosticsEnabled").IsChecked = true;
        app.CheckBox("OpdEnabled").IsChecked = false;
        app.CheckBox("PharmacyEnabled").IsChecked = false;
        SaveFeatures();

        AppFixture.WaitUntil(() => app.Find("NavOpd") is null, "OPD to disappear");
        Assert.Null(app.Find("NavSale"));
        Assert.NotNull(app.Find("NavDiagnostics"));

        // Restore, so later tests in this class see the defaults they expect.
        app.CheckBox("OpdEnabled").IsChecked = true;
        app.CheckBox("PharmacyEnabled").IsChecked = true;
        app.CheckBox("DiagnosticsEnabled").IsChecked = false;
        SaveFeatures();
    }

    /// <summary>The guard: switching every module off at once is refused,
    /// not silently accepted with an empty sidebar as the result.</summary>
    [Fact]
    public void Switching_off_every_module_at_once_is_refused_with_a_warning()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        // Diagnostics defaults off and nothing in this test turns it on, so
        // switching off OPD and Pharmacy too leaves nothing enabled.
        app.CheckBox("OpdEnabled").IsChecked = false;
        app.CheckBox("PharmacyEnabled").IsChecked = false;
        app.Click("FeaturesSave");

        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the refusal warning");
        app.MainWindow.ModalWindows[0].FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the warning to close");

        // Refused means unchanged — OPD is still on, its nav button never left.
        Assert.NotNull(app.Find("NavOpd"));

        // The checkboxes themselves were never reloaded from the (unsaved)
        // database, so they still show the illegal combination the operator
        // just tried — put them back for any test that runs after this one.
        app.CheckBox("OpdEnabled").IsChecked = true;
        app.CheckBox("PharmacyEnabled").IsChecked = true;
        SaveFeatures();
    }
}
