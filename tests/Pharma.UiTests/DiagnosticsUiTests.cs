using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Pharma.UiTests;

/// <summary>
/// The Diagnostics module is an optional feature — off by default, switched
/// on from Settings → Features. What this guards: the toggle persists, and
/// the sidebar reacts to it immediately, with no restart needed.
/// </summary>
public class DiagnosticsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void Enabling_diagnostics_shows_the_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        // Whatever state a previous test run left it in, force it off first so
        // this test can prove the on-transition, not just find it already on.
        app.CheckBox("DiagnosticsEnabled").IsChecked = false;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (off)");
        AppFixture.WaitUntil(() => app.Find("NavDiagnostics") is null, "the nav button to disappear");

        app.CheckBox("DiagnosticsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (on)");

        // No restart, no re-navigation — the shell is watching this live.
        AppFixture.WaitUntil(() => app.Find("NavDiagnostics") is not null, "the nav button to appear");

        // Leave and come back — the checkbox itself must also reload correctly.
        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        AppFixture.WaitUntil(() => app.CheckBox("DiagnosticsEnabled").IsChecked == true, "the toggle to reload as on");
    }

    /// <summary>Whatever an earlier test in this class left the toggle as.</summary>
    private void EnsureDiagnosticsEnabled()
    {
        if (app.Find("NavDiagnostics") is not null) return;

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("DiagnosticsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavDiagnostics") is not null, "the nav button to appear");
    }

    private string NewPatient(string name) => NewPatient(name, $"9{DateTime.Now:HHmmssfff}");

    private string NewPatient(string name, string phone)
    {
        app.Navigate("NavPatients", "Patients");
        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");

        app.Type("PatientName", name);
        app.Type("PatientPhone", phone);
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.TextOf("PatientsStatus").Contains("saved"), $"{name} to save");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the form to close");

        return name;
    }

    [Fact]
    public void Picking_a_patient_from_several_matches_selects_the_one_clicked_not_the_first()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        NewPatient($"ZDiagPick Alpha {suffix}");
        NewPatient($"ZDiagPick Beta {suffix}");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", $"ZDiagPick");

        AppFixture.WaitUntil(() => app.ListBox("DiagnosticsPatientMatches").Items.Length == 2, "both patients");

        // By text, not by a hardcoded index — SearchPatientsAsync orders
        // newest-first, so which row "Beta" lands on depends on creation
        // order, not on which one this test happened to save second.
        var beta = app.ListBox("DiagnosticsPatientMatches").Items.Single(i => i.Text.Contains("Beta"));
        beta.Select();

        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains("Beta"),
            "the clicked patient to be confirmed as selected");

        Assert.DoesNotContain("Alpha", app.TextOf("DiagnosticsSelectedPatient"));

        // The match list gives way to the confirmation — the two must never
        // both be on screen, and the list must not be silently un-selected.
        AppFixture.WaitUntil(() => app.Find("DiagnosticsPatientMatches") is null, "the match list to give way");

        // Left filled in, it kept showing the query even though the list
        // underneath had already given way to the confirmation — read as the
        // search having found nothing.
        AppFixture.WaitUntil(() => app.TextBox("DiagnosticsPatientSearch").Text == "", "the search box to clear");
    }

    [Fact]
    public void A_single_matching_patient_is_selected_automatically()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = NewPatient($"ZDiagSolo {suffix}");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", name);

        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains(name),
            "the single match to be selected automatically");

        AppFixture.WaitUntil(() => app.TextBox("DiagnosticsPatientSearch").Text == "", "the search box to clear");
    }

    [Fact]
    public void Two_children_sharing_a_phone_number_are_both_offered_not_defaulted_to_the_first()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var phone = $"9{suffix}";
        NewPatient($"ZDiagFamily Kid1 {suffix}", phone);
        NewPatient($"ZDiagFamily Kid2 {suffix}", phone);

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", phone);

        // Both children on the one number — neither is picked for the desk;
        // the list has to stay up so a specific child can be chosen.
        AppFixture.WaitUntil(() => app.ListBox("DiagnosticsPatientMatches").Items.Length == 2, "both children");
        Assert.Null(app.Find("DiagnosticsSelectedPatient"));

        var kid2 = app.ListBox("DiagnosticsPatientMatches").Items.Single(i => i.Text.Contains("Kid2"));
        kid2.Select();

        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains("Kid2"),
            "the specifically clicked child to be selected");
        Assert.DoesNotContain("Kid1", app.TextOf("DiagnosticsSelectedPatient"));
    }

    [Fact]
    public void Two_patients_sharing_a_name_are_both_offered_not_defaulted_to_the_first()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"ZDiagTwin {suffix}";
        NewPatient(name, $"9{suffix}1");
        NewPatient(name, $"9{suffix}2");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", name);

        AppFixture.WaitUntil(() => app.ListBox("DiagnosticsPatientMatches").Items.Length == 2, "both patients");
        Assert.Null(app.Find("DiagnosticsSelectedPatient"));

        // Same name, so tell them apart by phone — pick the second one and
        // confirm it is that specific record, not whichever sorted first.
        var second = app.ListBox("DiagnosticsPatientMatches").Items.Single(i => i.Text.EndsWith($"{suffix}2"));
        second.Select();

        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatientDetails").EndsWith($"{suffix}2"),
            "the specifically clicked patient to be selected");
    }

    [Fact]
    public void Adding_a_test_requires_a_patient_first()
    {
        EnsureDiagnosticsEnabled();
        app.Navigate("NavDiagnostics", "Diagnostics");

        // A fresh visit to the tab, with nobody picked yet — the button is
        // there so it's obvious what to do next, but there is nothing to
        // bill until a patient exists to bill it to.
        AppFixture.WaitUntil(() => app.Find("DiagnosticsAddTest") is not null, "the Add test button");
        Assert.False(app.Button("DiagnosticsAddTest").IsEnabled);
    }

    [Fact]
    public void The_test_picker_adds_several_tests_to_the_bill_without_closing()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = NewPatient($"ZDiagBill {suffix}");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", name);
        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains(name), "the patient to be selected");

        AppFixture.WaitUntil(() => app.Button("DiagnosticsAddTest").IsEnabled, "Add test to enable");
        app.Click("DiagnosticsAddTest");

        // Opens already listing every active test — nothing to type first.
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is not null, "the test picker to open");
        AppFixture.WaitUntil(() => app.Grid("TestPickerGrid").RowCount > 1, "the full test list");
        Assert.Contains(name, app.TextOf("TestPickerHeader"));

        // Several seeded tests share "Blood" in their name — no test-master
        // data needs creating to prove searching inside the picker filters it.
        app.Type("TestPickerSearch", "Blood");
        AppFixture.WaitUntil(() => app.Grid("TestPickerGrid").RowCount >= 2, "more than one blood test");

        var grid = app.Grid("TestPickerGrid");
        var sugarRow = grid.Rows.Single(r => (r.Cells[0].Value ?? "").Contains("Random Blood Sugar"));
        sugarRow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))!
            .AsButton().Invoke();

        // Still open — adding one does not close the picker, since a bill is
        // rarely just one test.
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is not null, "the picker to stay open");
        AppFixture.WaitUntil(() => app.TextOf("TestPickerAddedCount").StartsWith("1"), "the running count to update");

        app.Type("TestPickerSearch", "ESR");
        AppFixture.WaitUntil(() => app.Grid("TestPickerGrid").RowCount == 1, "just ESR");
        app.Grid("TestPickerGrid").Rows[0]
            .FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))!
            .AsButton().Invoke();

        AppFixture.WaitUntil(() => app.TextOf("TestPickerAddedCount").StartsWith("2"), "the second test to count");

        app.Click("TestPickerDone");

        // Back on the billing screen, both tests are on the bill.
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is null, "the picker to close");
        AppFixture.WaitUntil(() => app.Grid("DiagnosticsLinesGrid").RowCount == 2, "both tests on the bill");
    }

    [Fact]
    public void Adding_the_same_test_twice_is_refused()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = NewPatient($"ZDiagDup {suffix}");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", name);
        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains(name), "the patient to be selected");

        app.Click("DiagnosticsAddTest");
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is not null, "the test picker to open");

        app.Type("TestPickerSearch", "TSH");
        AppFixture.WaitUntil(() => app.Grid("TestPickerGrid").RowCount == 1, "just TSH");
        app.Grid("TestPickerGrid").Rows[0]
            .FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))!
            .AsButton().Invoke();

        AppFixture.WaitUntil(() => app.TextOf("TestPickerAddedCount").StartsWith("1"), "TSH to be added");

        // Same search again, in the same picker session — TSH is already on
        // the bill, so it must not be offered a second time.
        app.Type("TestPickerSearch", "TS");
        AppFixture.WaitUntil(() => app.TextBox("TestPickerSearch").Text == "TS", "the search text to settle");
        Assert.Equal(0, app.Grid("TestPickerGrid").RowCount);

        app.Click("TestPickerDone");
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is null, "the picker to close");

        // Only the one line — no duplicate landed on the bill either.
        AppFixture.WaitUntil(() => app.Grid("DiagnosticsLinesGrid").RowCount == 1, "exactly one TSH line");
    }

    [Fact]
    public void A_walk_in_patient_can_be_registered_without_leaving_the_screen()
    {
        EnsureDiagnosticsEnabled();
        app.Navigate("NavDiagnostics", "Diagnostics");

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"ZDiagWalkIn {suffix}";

        app.Click("DiagnosticsNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor to open");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");

        // Selected automatically — no need to search for the person just typed in.
        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains(name),
            "the newly registered patient to be selected");
        AppFixture.WaitUntil(() => app.Find("DiagnosticsNewPatient") is null, "the search panel to give way");
    }

    [Fact]
    public void A_new_bills_status_is_fixed_text_until_it_is_saved()
    {
        EnsureDiagnosticsEnabled();
        app.Navigate("NavDiagnostics", "Diagnostics");

        // A dropdown with nothing to move between yet — the bill has no id,
        // so there is no saved status to change — reads as broken if it is
        // there but disabled. Before a save, only the fixed-text label exists.
        AppFixture.WaitUntil(() => app.Find("DiagnosticsStatusFixed") is not null, "the fixed status label");
        Assert.Equal("Ordered", app.TextOf("DiagnosticsStatusFixed"));
        Assert.Null(app.Find("DiagnosticsStatus"));
    }

    /// <summary>A "Print preview" modal is up, the same signal
    /// <see cref="ReprintUiTests"/> uses for the pharmacy and OPD side.</summary>
    private FlaUI.Core.AutomationElements.Window WaitForPreview()
    {
        var preview = FlaUI.Core.Tools.Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(preview);
        return preview!;
    }

    [Fact]
    public void A_diagnostic_bill_can_be_reprinted_from_the_reports_screen()
    {
        EnsureDiagnosticsEnabled();

        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = NewPatient($"ZDiagReprint {suffix}");

        app.Navigate("NavDiagnostics", "Diagnostics");
        app.Type("DiagnosticsPatientSearch", name);
        AppFixture.WaitUntil(
            () => app.TextOf("DiagnosticsSelectedPatient").Contains(name), "the patient to be selected");

        app.Click("DiagnosticsAddTest");
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is not null, "the test picker to open");
        app.Type("TestPickerSearch", "ESR");
        AppFixture.WaitUntil(() => app.Grid("TestPickerGrid").RowCount == 1, "just ESR");
        app.Grid("TestPickerGrid").Rows[0]
            .FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))!
            .AsButton().Invoke();
        app.Click("TestPickerDone");
        AppFixture.WaitUntil(() => app.Find("TestPickerGrid") is null, "the picker to close");

        // Saved without printing, so the only preview this test sees is the
        // one raised later from Reports — proving that path on its own.
        app.Click("DiagnosticsSave");
        AppFixture.WaitUntil(() => app.TextOf("DiagnosticsStatusMessage").Contains("saved"), "the bill to save");

        app.Navigate("NavReports", "Reports");
        app.SelectTab("ReportsTabs", "Diagnostics");

        AppFixture.WaitUntil(
            () => (app.Grid("ReportsDiagnosticsTodayGrid").Rows.FirstOrDefault(r => (r.Cells[2].Value ?? "").Contains(name))) is not null,
            "the new bill to show up in today's diagnostic bills");

        app.Grid("ReportsDiagnosticsTodayGrid").Rows
            .Single(r => (r.Cells[2].Value ?? "").Contains(name)).Select();

        app.Click("ReprintDiagnosticBill");

        var preview = WaitForPreview();
        Assert.Contains("duplicate", preview.Title, StringComparison.OrdinalIgnoreCase);
        preview.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
    }
}
