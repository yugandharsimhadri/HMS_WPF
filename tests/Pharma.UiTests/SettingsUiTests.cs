namespace Pharma.UiTests;

/// <summary>
/// Settings split into tabs — General, Clinic, Pharmacy, Doctors, Reports —
/// each backed by its own row of Settings keys. These guard that each tab
/// actually persists on its own, independent of the others.
/// </summary>
public class SettingsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void Clinic_details_persist_across_screens()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Clinic");

        var clinicName = $"UI Test Clinic {DateTime.Now:HHmmss}";
        app.Type("ClinicName", clinicName);

        app.CheckBox("ClinicGstRegistered").IsChecked = true;
        app.Type("ClinicGstin", "29CLINIC1234F1Z5");
        app.Click("ClinicSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "clinic settings to save");

        // Leave and come back — the values must be read back from the database.
        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Clinic");

        AppFixture.WaitUntil(() => app.TextBox("ClinicName").Text == clinicName, "clinic name to reload");

        Assert.Equal(clinicName, app.TextBox("ClinicName").Text);
        Assert.Equal("29CLINIC1234F1Z5", app.TextBox("ClinicGstin").Text);
    }

    [Fact]
    public void Pharmacy_details_persist_independently_of_the_clinics()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Pharmacy");

        var pharmacyName = $"UI Test Pharmacy {DateTime.Now:HHmmss}";
        app.Type("PharmacyName", pharmacyName);

        // A GSTIN can only be entered once the pharmacy says it is registered.
        app.CheckBox("PharmacyGstRegistered").IsChecked = true;
        app.Type("PharmacyGstin", "29PHARMACY1234F1Z5");
        app.Click("PharmacySave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "pharmacy settings to save");

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Pharmacy");

        AppFixture.WaitUntil(() => app.TextBox("PharmacyName").Text == pharmacyName, "pharmacy name to reload");

        Assert.Equal(pharmacyName, app.TextBox("PharmacyName").Text);
        Assert.Equal("29PHARMACY1234F1Z5", app.TextBox("PharmacyGstin").Text);

        // The clinic's own name is a different row and was not touched by this.
        app.SelectTab("SettingsTabs", "Clinic");
        Assert.NotEqual(pharmacyName, app.TextBox("ClinicName").Text);
    }

    [Fact]
    public void Document_branding_persists_across_screens()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        var footer = $"UI test footer {DateTime.Now:HHmmss}";
        app.Type("DocumentFooter", footer);
        app.Click("DocumentThemeSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "document branding to save");

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        AppFixture.WaitUntil(() => app.TextBox("DocumentFooter").Text == footer, "the footer to reload");
    }
}
