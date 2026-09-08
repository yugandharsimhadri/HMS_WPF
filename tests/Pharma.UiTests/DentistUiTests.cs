using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The Dentist module end to end: the Features toggle, the patient panel
/// sitting above the tabs, opening a case on the Cases tab, adding sittings
/// and replacements on Treatment and recording payments against the
/// running balance on Payments — one task per tab, a case summary strip
/// carrying context across Treatment and Payments — and the four masters
/// behind it on the shared General Master nav destination (also used by
/// Pediatrics for Vaccine Master and the Procedure Master both specialties
/// bill against).
/// </summary>
public class DentistUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void EnsureDentistEnabled()
    {
        if (app.Find("NavDentist") is not null) return;

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("DentistEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavDentist") is not null, "the nav button to appear");
    }

    /// <summary>Jumps to the shared General Master destination — vaccine
    /// and procedure masters, and the four dentist-specific masters, all
    /// split out of the patient screen onto their own destination.</summary>
    private void GoToMaster() => app.Navigate("NavGeneralMaster", "General Master");

    /// <summary>Selects the Procedures tab and the Dentist department pill
    /// on it — Dentist is already the default department once only Dentist
    /// is enabled, but selecting it explicitly keeps this correct
    /// regardless of what another module toggled earlier in the run.</summary>
    private void GoToDentistProcedures()
    {
        app.SelectTab("GeneralMasterTabs", "Procedures");
        app.Click("GeneralMasterDeptDentist");
    }

    /// <summary>Registers a brand new patient from the patient panel above
    /// the tabs, clearing any patient a previous test in this class left
    /// selected first — DentistViewModel is a DI singleton shared by every
    /// test here. Also lands on the Cases tab regardless of which tab an
    /// earlier test left selected (SelectedTabIndex is on the same
    /// singleton), since opening a case for the newly registered patient
    /// is what every caller does next.</summary>
    private string RegisterAndSelectPatient(string suffix)
    {
        var name = $"Dent Patient {suffix}";

        app.Navigate("NavDentist", "Dentist");
        app.SelectTab("DentistTabs", "Cases");

        if (app.Find("DentistChangePatient") is not null) app.Click("DentistChangePatient");
        AppFixture.WaitUntil(() => app.Find("DentistNewPatient") is not null, "the patient panel to render");

        app.Click("DentistNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor to open");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "35");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(
            () => app.TextOf("DentistSelectedPatient").Contains(name),
            "the newly registered patient to be selected");

        return name;
    }

    private string AddProcedure(string suffix, string price = "5000")
    {
        var name = $"DentProc {suffix}";

        GoToMaster();
        GoToDentistProcedures();
        app.Click("GeneralMasterNewProcedure");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is not null, "the procedure editor to open");
        app.Type("ProcedureEditorName", name);
        app.Type("ProcedureEditorPrice", price);
        app.Click("ProcedureEditorSave");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is null, "the editor to close");

        return name;
    }

    /// <summary>Registers a patient, adds a fresh procedure, and opens a
    /// case against it — the common setup nearly every case test needs.</summary>
    private string OpenCaseForNewPatient(string suffix, string price = "5000")
    {
        var procedureName = AddProcedure(suffix, price);
        RegisterAndSelectPatient(suffix);

        AppFixture.WaitUntil(() => app.Find("DentistDoctor") is not null, "the Cases tab to render");

        app.ComboBox("DentistNewCaseProcedure").Select(procedureName);
        app.Click("DentistOpenCase");

        AppFixture.WaitUntil(() => app.Grid("DentistCasesGrid").RowCount >= 1, "the opened case to list");
        app.Grid("DentistCasesGrid").Rows[0].Select();
        AppFixture.WaitUntil(() => app.Find("DentistCaseTotalCost") is not null, "the case detail panel");

        return procedureName;
    }

    [Fact]
    public void Enabling_dentist_shows_the_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("DentistEnabled").IsChecked = false;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (off)");
        AppFixture.WaitUntil(() => app.Find("NavDentist") is null, "the nav button to disappear");

        app.CheckBox("DentistEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavDentist") is not null, "the nav button to reappear");

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        AppFixture.WaitUntil(() => app.CheckBox("DentistEnabled").IsChecked == true, "the toggle to reload as on");
    }

    [Fact]
    public void Opening_a_case_against_a_procedure_shows_its_price_as_the_total_cost()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        OpenCaseForNewPatient(suffix, "5000");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseTotalCost").TrimStart('₹'), out var v) && v == 5000m,
            "the total cost to read the procedure's price");
    }

    [Fact]
    public void Adding_a_sitting_with_anesthesia_increases_the_total_cost()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        OpenCaseForNewPatient(suffix, "5000");

        app.SelectTab("DentistTabs", "Treatment");
        app.Type("DentistSittingWorkDone", "Access opening");
        app.Type("DentistSittingAnesthesiaCost", "300");
        app.Click("DentistAddSitting");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseTotalCost").TrimStart('₹'), out var v) && v == 5300m,
            "the total cost to include the anesthesia");

        AppFixture.WaitUntil(() => app.Grid("DentistSittingsGrid").RowCount >= 1, "the sitting to list");
    }

    [Fact]
    public void Adding_a_replacement_increases_the_total_cost_by_quantity()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        OpenCaseForNewPatient(suffix, "5000");

        var replacementName = $"Crown {suffix}";
        GoToMaster();
        app.SelectTab("GeneralMasterTabs", "Dental Replacements");
        app.Click("DentistNewReplacement");
        AppFixture.WaitUntil(() => app.Find("ReplacementEditorName") is not null, "the replacement editor to open");
        app.Type("ReplacementEditorName", replacementName);
        app.Type("ReplacementEditorUnitCost", "1000");
        app.Click("ReplacementEditorSave");
        AppFixture.WaitUntil(() => app.Find("ReplacementEditorName") is null, "the editor to close");

        app.Navigate("NavDentist", "Dentist");
        app.SelectTab("DentistTabs", "Cases");
        AppFixture.WaitUntil(() => app.Find("DentistCaseTotalCost") is not null, "the case still selected");

        app.SelectTab("DentistTabs", "Treatment");
        app.ComboBox("DentistReplacementPicker").Select(replacementName);
        app.Type("DentistReplacementQuantity", "2");
        app.Click("DentistAddReplacement");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseTotalCost").TrimStart('₹'), out var v) && v == 7000m,
            "the total cost to include 2 crowns at 1000 each");
    }

    [Fact]
    public void Recording_a_partial_payment_leaves_the_correct_balance_and_prints()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        OpenCaseForNewPatient(suffix, "5000");

        app.SelectTab("DentistTabs", "Payments");
        app.Type("DentistPaymentAmount", "2000");
        app.Click("DentistRecordPaymentAndPrint");

        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;
        Assert.NotNull(preview);
        preview!.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the preview to close");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseBalance").TrimStart('₹'), out var v) && v == 3000m,
            "the balance to read 3000 after a 2000 payment against a 5000 case");
    }

    [Fact]
    public void Marking_a_case_completed_still_allows_a_later_payment()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        OpenCaseForNewPatient(suffix, "1000");

        app.Click("DentistCompleteCase");
        AppFixture.WaitUntil(
            () => app.Grid("DentistCasesGrid").Rows.Any(r => r.Cells[2].Value == "Completed"),
            "the case to show Completed");

        app.Grid("DentistCasesGrid").Rows[0].Select();
        AppFixture.WaitUntil(() => app.Find("DentistCaseTotalCost") is not null, "the case detail to reload");

        app.SelectTab("DentistTabs", "Payments");
        app.Type("DentistPaymentAmount", "1000");
        app.Click("DentistRecordPayment");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseBalance").TrimStart('₹'), out var v) && v == 0m,
            "the balance to clear after paying a completed case in full");
    }

    [Fact]
    public void Adding_a_new_package_makes_it_available_to_open_a_case_against()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var procedureName = AddProcedure(suffix, "3000");

        var packageName = $"FullMouth {suffix}";
        app.SelectTab("GeneralMasterTabs", "Dental Packages");
        app.Click("DentistNewPackage");
        AppFixture.WaitUntil(() => app.Find("PackageEditorName") is not null, "the package editor to open");

        app.Type("PackageEditorName", packageName);
        app.Type("PackageEditorPrice", "20000");
        app.ComboBox("PackageEditorProcedurePicker").Select(procedureName);
        app.Click("PackageEditorAddItem");
        AppFixture.WaitUntil(() => app.Find("PackageEditorItems") is not null, "the item to be added to the breakdown");
        app.Click("PackageEditorSave");
        AppFixture.WaitUntil(() => app.Find("PackageEditorName") is null, "the editor to close");

        RegisterAndSelectPatient(suffix);
        AppFixture.WaitUntil(() => app.Find("DentistDoctor") is not null, "the Cases tab to render");

        app.ComboBox("DentistNewCasePackage").Select(packageName);
        app.Click("DentistOpenCase");

        AppFixture.WaitUntil(() => app.Grid("DentistCasesGrid").RowCount >= 1, "the case opened against the package");
        app.Grid("DentistCasesGrid").Rows[0].Select();

        // The package price (20000) wins over the sum of its items (3000).
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("DentistCaseTotalCost").TrimStart('₹'), out var v) && v == 20000m,
            "the case to bill the package price, not the sum of its items");
    }

    /// <summary>Treatment and Payments both need a case picked on Cases
    /// first — a placeholder shows instead of an empty form — and once one
    /// is, its title, status and totals carry across both tabs via the case
    /// strip, whose "Switch case" link jumps straight back to Cases.</summary>
    [Fact]
    public void Treatment_and_payments_are_gated_behind_picking_a_case_and_the_case_strip_carries_context()
    {
        EnsureDentistEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var procedureName = AddProcedure(suffix, "5000");

        RegisterAndSelectPatient(suffix);

        app.SelectTab("DentistTabs", "Treatment");
        AppFixture.WaitUntil(() => app.Find("DentistAddSitting") is null, "no sitting form before a case is picked");

        app.SelectTab("DentistTabs", "Payments");
        AppFixture.WaitUntil(() => app.Find("DentistPaymentAmount") is null, "no payment form before a case is picked");

        app.SelectTab("DentistTabs", "Cases");
        app.ComboBox("DentistNewCaseProcedure").Select(procedureName);
        app.Click("DentistOpenCase");
        AppFixture.WaitUntil(() => app.Grid("DentistCasesGrid").RowCount >= 1, "the opened case to list");
        app.Grid("DentistCasesGrid").Rows[0].Select();
        AppFixture.WaitUntil(() => app.Find("DentistCaseTotalCost") is not null, "the case detail panel");

        app.SelectTab("DentistTabs", "Treatment");
        AppFixture.WaitUntil(() => app.Find("DentistAddSitting") is not null, "the treatment form to unlock once a case is picked");
        AppFixture.WaitUntil(
            () => app.TextOf("DentistSelectedCaseTitle") == procedureName,
            "the case strip to name the selected case");

        app.SelectTab("DentistTabs", "Payments");
        AppFixture.WaitUntil(() => app.Find("DentistPaymentAmount") is not null, "the payment form to unlock too");
        AppFixture.WaitUntil(
            () => app.TextOf("DentistSelectedCaseTitle") == procedureName,
            "the case strip to carry the same case onto Payments");

        app.Click("DentistSwitchCase");
        AppFixture.WaitUntil(() => app.Find("DentistOpenCase") is not null, "Switch case to land back on the Cases tab");
    }
}
