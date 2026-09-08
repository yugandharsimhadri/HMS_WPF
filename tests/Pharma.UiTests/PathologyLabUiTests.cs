namespace Pharma.UiTests;

/// <summary>
/// The Pathology Lab module end to end: the Features toggle, the patient
/// panel sitting above the tabs, the analyte / report / package masters on
/// their own "Master" nav destination, placing an order against a report,
/// a package discounting the order to its own flat price, and the
/// zero-results verify guard. Result-value entry itself (flagging, upsert,
/// verify, print) is covered at the service layer in
/// PathologyLabServiceTests — editing a live DataGrid cell has no
/// established automation helper in this suite, so the UI tests here stop
/// at what the existing FlaUI helpers can drive reliably: form fields,
/// combo pickers and grid row/cell reads.
/// </summary>
public class PathologyLabUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void EnsurePathologyLabEnabled()
    {
        if (app.Find("NavPathologyLab") is not null) return;

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("PathologyLabEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLab") is not null, "the nav button to appear");
    }

    /// <summary>Jumps to Pathology Lab's Master nav destination — analyte,
    /// report and package masters, all split out of the patient screen
    /// onto their own destination.</summary>
    private void GoToMaster() => app.Navigate("NavPathologyLabMaster", "Pathology Lab — Master");

    /// <summary>Registers a brand new patient from the patient panel above
    /// the tabs, clearing any patient a previous test in this class left
    /// selected first — PathologyLabViewModel is a DI singleton shared by
    /// every test here.</summary>
    private string RegisterAndSelectPatient(string suffix)
    {
        var name = $"Lab Patient {suffix}";

        app.Navigate("NavPathologyLab", "Pathology Lab");

        if (app.Find("LabChangePatient") is not null) app.Click("LabChangePatient");
        AppFixture.WaitUntil(() => app.Find("LabNewPatient") is not null, "the patient panel to render");

        app.Click("LabNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor to open");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "40");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(
            () => app.TextOf("LabSelectedPatient").Contains(name),
            "the newly registered patient to be selected");

        return name;
    }

    private string AddAnalyte(string suffix)
    {
        var name = $"LabAnalyte {suffix}";

        GoToMaster();
        app.SelectTab("PathologyLabMasterTabs", "Analyte Master");
        app.Click("LabNewAnalyte");
        AppFixture.WaitUntil(() => app.Find("AnalyteEditorName") is not null, "the analyte editor to open");

        app.Type("AnalyteEditorName", name);
        app.Type("AnalyteEditorUnits", "mg/dL");
        app.Click("AnalyteEditorSave");
        AppFixture.WaitUntil(() => app.Find("AnalyteEditorName") is null, "the editor to close");

        return name;
    }

    private string AddReport(string suffix, string analyteName, string price)
    {
        var name = $"LabReport {suffix}";

        GoToMaster();
        app.SelectTab("PathologyLabMasterTabs", "Report Master");
        app.Click("LabNewReport");
        AppFixture.WaitUntil(() => app.Find("ReportEditorName") is not null, "the report editor to open");

        app.Type("ReportEditorName", name);
        app.Type("ReportEditorPrice", price);
        app.ComboBox("ReportEditorAnalytePicker").Select(analyteName);
        app.Click("ReportEditorAddMember");
        AppFixture.WaitUntil(() => app.Find("ReportEditorMembers") is not null, "the analyte to be added to the panel");
        app.Click("ReportEditorSave");
        AppFixture.WaitUntil(() => app.Find("ReportEditorName") is null, "the editor to close");

        return name;
    }

    [Fact]
    public void Enabling_pathology_lab_shows_the_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("PathologyLabEnabled").IsChecked = false;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (off)");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLab") is null, "the nav button to disappear");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLabMaster") is null, "the Master nav button to disappear too");

        app.CheckBox("PathologyLabEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLab") is not null, "the nav button to reappear");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLabMaster") is not null, "the Master nav button to reappear too");

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        AppFixture.WaitUntil(() => app.CheckBox("PathologyLabEnabled").IsChecked == true, "the toggle to reload as on");
    }

    [Fact]
    public void An_analyte_added_to_a_report_and_a_report_added_to_a_package_all_list_correctly()
    {
        EnsurePathologyLabEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var analyteName = AddAnalyte(suffix);
        AppFixture.WaitUntil(
            () => app.Grid("LabAnalyteMasterGrid").Rows.Any(r => r.Cells[0].Value == analyteName),
            "the new analyte to list in the master grid");

        var reportName = AddReport(suffix, analyteName, "450");
        AppFixture.WaitUntil(
            () => app.Grid("LabReportMasterGrid").Rows.Any(r => r.Cells[0].Value == reportName),
            "the new report to list in the master grid");

        var packageName = $"LabPackage {suffix}";
        app.SelectTab("PathologyLabMasterTabs", "Package Master");
        app.Click("LabNewPackage");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorName") is not null, "the package editor to open");

        app.Type("LabPackageEditorName", packageName);
        app.Type("LabPackageEditorPrice", "400");
        app.ComboBox("LabPackageEditorReportPicker").Select(reportName);
        app.Click("LabPackageEditorAddMember");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorMembers") is not null, "the report to be added to the package");
        app.Click("LabPackageEditorSave");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorName") is null, "the editor to close");

        AppFixture.WaitUntil(
            () => app.Grid("LabPackageMasterGrid").Rows.Any(r => r.Cells[0].Value == packageName),
            "the new package to list in the master grid");
    }

    [Fact]
    public void Placing_an_order_against_a_report_lists_it_and_its_analyte_for_result_entry()
    {
        EnsurePathologyLabEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var analyteName = AddAnalyte(suffix);
        var reportName = AddReport(suffix, analyteName, "600");
        RegisterAndSelectPatient(suffix);

        AppFixture.WaitUntil(() => app.Find("LabReportPicker") is not null, "the Orders tab to render");

        app.ComboBox("LabReportPicker").Select(reportName);
        app.Click("LabAddReportLine");
        AppFixture.WaitUntil(() => app.Find("LabNewOrderLines") is not null, "the report to be added to the order");
        app.Click("LabSaveOrder");

        AppFixture.WaitUntil(() => app.Grid("LabOrdersGrid").RowCount >= 1, "the saved order to list");
        app.Grid("LabOrdersGrid").Rows[0].Select();
        AppFixture.WaitUntil(() => app.Find("LabResultsGrid") is not null, "the order detail panel");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.Grid("LabOrdersGrid").Rows[0].Cells[2].Value, out var v) && v == 600m,
            "the order amount to read the report's price");

        AppFixture.WaitUntil(
            () => app.Grid("LabResultsGrid").Rows.Any(r => r.Cells[1].Value == analyteName),
            "the report's analyte to appear in the result entry grid");

        // A report does not print until the order has been verified.
        Assert.False(app.Button("LabPrintReport").IsEnabled);

        // Verifying with nothing entered yet is refused.
        app.Click("LabVerifyOrder");
        AppFixture.WaitUntil(
            () => app.TextOf("LabOrdersStatus").Contains("at least one result", StringComparison.OrdinalIgnoreCase),
            "the zero-results verify guard to show");
    }

    [Fact]
    public void Applying_a_package_discounts_the_order_to_the_package_price()
    {
        EnsurePathologyLabEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var analyteName = AddAnalyte(suffix);
        var reportName = AddReport(suffix, analyteName, "600");

        var packageName = $"LabPackage {suffix}";
        GoToMaster();
        app.SelectTab("PathologyLabMasterTabs", "Package Master");
        app.Click("LabNewPackage");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorName") is not null, "the package editor to open");
        app.Type("LabPackageEditorName", packageName);
        app.Type("LabPackageEditorPrice", "400");
        app.ComboBox("LabPackageEditorReportPicker").Select(reportName);
        app.Click("LabPackageEditorAddMember");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorMembers") is not null, "the report to be added to the package");
        app.Click("LabPackageEditorSave");
        AppFixture.WaitUntil(() => app.Find("LabPackageEditorName") is null, "the editor to close");

        RegisterAndSelectPatient(suffix);
        AppFixture.WaitUntil(() => app.Find("LabPackagePicker") is not null, "the Orders tab to render");

        app.ComboBox("LabPackagePicker").Select(packageName);
        app.Click("LabApplyPackage");
        AppFixture.WaitUntil(() => app.Find("LabNewOrderLines") is not null, "the package's report to be added");

        // The package price (400) wins over the sum of its reports (600),
        // reached here through an automatic discount — see
        // PathologyLabViewModel.ApplyPackageAsync.
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextBox("LabDiscount").Text, out var v) && v == 200m,
            "the discount to bring the order down to the package price");

        app.Click("LabSaveOrder");
        AppFixture.WaitUntil(() => app.Grid("LabOrdersGrid").RowCount >= 1, "the saved order to list");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.Grid("LabOrdersGrid").Rows[0].Cells[2].Value, out var v) && v == 400m,
            "the order to bill the package price, not the sum of its reports");
    }
}
