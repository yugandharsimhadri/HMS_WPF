using FlaUI.Core.AutomationElements;

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

    /// <summary>
    /// Regression guard for a real bug: the print/title font pickers are
    /// editable ComboBoxes, and their shared control template had the
    /// editable textbox's clickable area covering the dropdown arrow too —
    /// a genuine mouse click anywhere on the box, arrow included, just
    /// focused the text and never opened the list. It only went unnoticed
    /// because reading a FlaUI ComboBox's Items expands the dropdown
    /// through automation regardless of whether a click would, which is
    /// exactly the gap this closes: a real click, at the arrow's own
    /// screen point, checked without ever touching Items first.
    /// </summary>
    [StaFact]
    public void The_print_font_pickers_dropdown_actually_opens_on_a_real_click()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        var element = app.Find("PrintFontFamily")!;
        var bounds = element.BoundingRectangle;

        var stateBefore = element.AsComboBox().Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value;
        Assert.Equal(FlaUI.Core.Definitions.ExpandCollapseState.Collapsed, stateBefore);

        app.MainWindow.Focus();
        var arrowPoint = new System.Drawing.Point((int)bounds.Right - 14, (int)bounds.Y + (int)(bounds.Height / 2));
        FlaUI.Core.Input.Mouse.Click(arrowPoint);

        AppFixture.WaitUntil(
            () => element.AsComboBox().Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value
                  == FlaUI.Core.Definitions.ExpandCollapseState.Expanded,
            "the dropdown to open from a real click on the arrow");
    }

    [StaFact]
    public void The_print_font_picker_offers_more_than_a_couple_of_system_fonts()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        // Reading Items is what a picker is actually for — Arial and Segoe UI
        // ship on every Windows PC this runs on, so their presence is what
        // proves this is really system fonts and not an empty or tiny list.
        var fonts = app.ComboBox("PrintFontFamily").Items.Select(i => i.Text).ToList();

        Assert.True(fonts.Count > 20, $"Only {fonts.Count} font(s) offered — expected the PC's real font list.");
        Assert.Contains(fonts, f => f == "Arial");
        Assert.Contains(fonts, f => f == "Segoe UI");
    }

    [Fact]
    public void Print_and_title_font_choices_persist_across_screens()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        app.Type("PrintFontFamily", "Georgia");
        app.Type("PrintFontSizeDelta", "2");
        app.Type("TitleFontFamily", "Calibri");
        app.Type("TitleFontSizeDelta", "4");
        app.Click("DocumentThemeSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the font choices to save");

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Reports");

        AppFixture.WaitUntil(() => app.TextBox("PrintFontFamily").Text == "Georgia", "the print font to reload");
        Assert.Equal("2", app.TextBox("PrintFontSizeDelta").Text);
        Assert.Equal("Calibri", app.TextBox("TitleFontFamily").Text);
        Assert.Equal("4", app.TextBox("TitleFontSizeDelta").Text);
    }
}
