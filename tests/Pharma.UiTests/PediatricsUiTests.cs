using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The Pediatrics module end to end: the Features toggle, the patient panel
/// (search/select/clear) sitting above the tabs and carrying across them,
/// the Growth, Care and Immunization tabs each gated behind picking a
/// patient first, procedure billing and vaccination through their popup
/// forms on Care — landing on the same running bill — the vaccine and
/// procedure masters and the immunization schedule reference on the shared
/// General Master nav destination, and the vaccination/growth history that
/// shows up back on the Patients screen once this module is on.
/// </summary>
public class PediatricsUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void EnsurePediatricsEnabled()
    {
        if (app.Find("NavPediatrics") is not null) return;

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("PediatricsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavPediatrics") is not null, "the nav button to appear");
    }

    /// <summary>Jumps to the shared General Master destination — vaccine
    /// and procedure masters, and the immunization schedule reference, all
    /// split out of the patient screen onto their own destination and
    /// shared with Dentist. Lands with the Pediatrics department pill
    /// already selected, since <c>GeneralMasterViewModel.LoadAsync</c>
    /// defaults to whichever specialty is actually enabled.</summary>
    private void GoToMaster() => app.Navigate("NavGeneralMaster", "General Master");

    /// <summary>Registers a brand new patient from the patient panel's own
    /// "+ New patient" overlay and leaves them selected. The panel sits
    /// above the tabs now, so it needs no tab of its own to reach.</summary>
    private string RegisterAndSelectPatient(string suffix)
    {
        var name = $"Peds Patient {suffix}";

        app.Navigate("NavPediatrics", "Pediatrics");

        // PediatricsViewModel is a DI singleton shared by every test in this
        // class, so whichever patient an earlier test left selected is still
        // selected now — which hides "+ New patient" behind "Clear patient"
        // (see HasSelectedPatient in the view). Clear it first if so.
        if (app.Find("PediatricsChangePatient") is not null) app.Click("PediatricsChangePatient");

        AppFixture.WaitUntil(() => app.Find("PediatricsNewPatient") is not null, "the patient panel to render");

        app.Click("PediatricsNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor to open");

        app.Type("PatientName", name);
        app.Type("PatientPhone", $"9{suffix}");
        app.Type("PatientAge", "3");
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsSelectedPatient").Contains(name),
            "the newly registered patient to be selected");

        return name;
    }

    /// <summary>Creates a Medicine and receives stock for it, exactly the
    /// way any pharmacy product is stocked — vaccination now draws its
    /// brand, batch and price from here, never from a second, parallel
    /// entry of the same information inside Pediatrics.</summary>
    private string CreateStockedProduct(string suffix, decimal mrp = 500m)
    {
        var name = $"UI Vax {suffix}";

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Click("ProductSave");
        AppFixture.WaitUntil(
            () => app.TextOf("ProductsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the product to save");

        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the product in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", $"B{suffix}");
        app.Type("StockQuantity", "10");
        app.Type("StockPurchaseRate", (mrp * 0.7m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");
        AppFixture.WaitUntil(
            () => app.TextOf("InventoryStatus").Contains("added to batch", StringComparison.OrdinalIgnoreCase),
            "stock to be added");

        return name;
    }

    /// <summary>Creates a vaccine on the Master screen's Vaccine Master tab,
    /// leaving whichever nav destination the caller was on before untouched
    /// otherwise.</summary>
    private void CreateVaccine(string name)
    {
        GoToMaster();
        app.SelectTab("GeneralMasterTabs", "Vaccine Master");
        app.Click("PediatricsNewVaccine");
        AppFixture.WaitUntil(() => app.Find("VaccineEditorName") is not null, "the vaccine editor to open");
        app.Type("VaccineEditorName", name);
        app.Click("VaccineEditorSave");
        AppFixture.WaitUntil(() => app.Find("VaccineEditorName") is null, "the editor to close");
    }

    /// <summary>Opens the vaccination popup, picks the vaccine type and the
    /// pharmacy brand actually given, and saves — the standard flow every
    /// vaccination test in this class now follows, since a brand with real
    /// stock is required before Save will accept the form.</summary>
    private void RecordVaccination(string vaccineName, string productName)
    {
        app.Click("PediatricsOpenRecordVaccination");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is not null, "the vaccination popup to open");
        app.ComboBox("RecordVaccinationVaccinePicker").Select(vaccineName);

        app.Type("RecordVaccinationProductSearch", productName);
        AppFixture.WaitUntil(
            () => app.Find("RecordVaccinationProductMatches") is not null
                  && app.ListBox("RecordVaccinationProductMatches").Items.Length == 1,
            "the brand to be found in pharmacy stock");
        app.ListBox("RecordVaccinationProductMatches").Items[0].Select();
        AppFixture.WaitUntil(
            () => app.TextOf("RecordVaccinationSelectedProduct").Contains(productName),
            "the picked brand to show as selected");

        app.Click("RecordVaccinationSave");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is null, "the popup to close");
    }

    [Fact]
    public void Enabling_pediatrics_shows_the_nav_button_immediately_and_it_persists()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");

        app.CheckBox("PediatricsEnabled").IsChecked = false;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "features to save (off)");
        AppFixture.WaitUntil(() => app.Find("NavPediatrics") is null, "the nav button to disappear");

        app.CheckBox("PediatricsEnabled").IsChecked = true;
        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavPediatrics") is not null, "the nav button to reappear");

        app.Navigate("NavPatients", "Patients");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        AppFixture.WaitUntil(() => app.CheckBox("PediatricsEnabled").IsChecked == true, "the toggle to reload as on");
    }

    /// <summary>The Care tab shows a placeholder, not a live billing/
    /// vaccination screen, until a patient has actually been picked from
    /// the panel above the tabs.</summary>
    [Fact]
    public void The_care_tab_is_gated_behind_picking_a_patient_first()
    {
        EnsurePediatricsEnabled();

        app.Navigate("NavPediatrics", "Pediatrics");
        if (app.Find("PediatricsChangePatient") is not null) app.Click("PediatricsChangePatient");

        app.SelectTab("PediatricsTabs", "Care");
        AppFixture.WaitUntil(() => app.Find("PediatricsOpenAddProcedure") is null, "the Care actions to stay hidden with no patient selected");

        RegisterAndSelectPatient(DateTime.Now.ToString("HHmmssfff"));
        app.SelectTab("PediatricsTabs", "Care");
        AppFixture.WaitUntil(() => app.Find("PediatricsOpenAddProcedure") is not null, "the Care actions to unlock once a patient is selected");
    }

    /// <summary>A second clear-patient button sits next to the vaccination
    /// history print button — printing a record for one child and then
    /// clearing straight for the next doesn't need a trip back to the
    /// patient panel's own "Clear patient" button.</summary>
    [Fact]
    public void The_care_tab_can_clear_the_selected_patient_next_to_the_history_print_button()
    {
        EnsurePediatricsEnabled();

        RegisterAndSelectPatient(DateTime.Now.ToString("HHmmssfff"));
        app.SelectTab("PediatricsTabs", "Care");
        AppFixture.WaitUntil(() => app.Find("PediatricsClearPatientAfterPrint") is not null, "the clear-patient button to show");

        app.Click("PediatricsClearPatientAfterPrint");
        AppFixture.WaitUntil(() => app.Find("PediatricsOpenAddProcedure") is null, "the Care actions to hide once the patient is cleared");
    }

    [Fact]
    public void Adding_a_new_vaccine_to_the_master_makes_it_pickable_for_vaccination()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"TestVax {suffix}";

        CreateVaccine(name);
        AppFixture.WaitUntil(
            () => app.Grid("PediatricsVaccineMasterGrid").Rows.Any(r => r.Cells[0].Value == name),
            "the new vaccine to appear in the master grid");

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");
        app.Click("PediatricsOpenRecordVaccination");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is not null, "the vaccination popup to open");

        AppFixture.WaitUntil(
            () => app.ComboBox("RecordVaccinationVaccinePicker").Items.Any(i => i.Text == name),
            "the vaccine to be pickable");
        app.Click("RecordVaccinationCancel");
    }

    [Fact]
    public void Adding_a_new_procedure_to_the_master_makes_it_billable()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"TestProc {suffix}";

        GoToMaster();
        app.SelectTab("GeneralMasterTabs", "Procedures");
        app.Click("GeneralMasterNewProcedure");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is not null, "the procedure editor to open");

        app.Type("ProcedureEditorName", name);
        app.Type("ProcedureEditorCategory", "Minor procedure");
        app.Type("ProcedureEditorPrice", "100");
        app.Click("ProcedureEditorSave");

        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is null, "the editor to close");
        AppFixture.WaitUntil(
            () => app.Grid("GeneralMasterProcedureGrid").Rows.Any(r => r.Cells[0].Value == name),
            "the new procedure to appear in the master grid");

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");
        app.Click("PediatricsOpenAddProcedure");
        AppFixture.WaitUntil(() => app.Find("AddProcedureLinePicker") is not null, "the add-procedure popup to open");

        AppFixture.WaitUntil(
            () => app.ComboBox("AddProcedureLinePicker").Items.Any(i => i.Text == name),
            "the procedure to be billable");
        app.Click("AddProcedureLineCancel");
    }

    [Fact]
    public void Billing_a_procedure_saves_and_prints()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        // A fresh procedure so this test does not depend on demo data existing.
        var procedureName = $"BillProc {suffix}";
        GoToMaster();
        app.SelectTab("GeneralMasterTabs", "Procedures");
        app.Click("GeneralMasterNewProcedure");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is not null, "the procedure editor to open");
        app.Type("ProcedureEditorName", procedureName);
        app.Type("ProcedureEditorPrice", "120");
        app.Click("ProcedureEditorSave");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is null, "the editor to close");

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");

        app.Click("PediatricsOpenAddProcedure");
        AppFixture.WaitUntil(() => app.Find("AddProcedureLinePicker") is not null, "the add-procedure popup to open");
        app.ComboBox("AddProcedureLinePicker").Select(procedureName);
        app.Click("AddProcedureLineAdd");
        AppFixture.WaitUntil(() => app.Find("AddProcedureLinePicker") is null, "the popup to close");

        AppFixture.WaitUntil(() => app.Grid("PediatricsLinesGrid").RowCount == 1, "the line to be added");

        app.Click("PediatricsSaveAndPrint");

        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;
        Assert.NotNull(preview);
        preview!.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the preview to close");

        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the save confirmation");
    }

    /// <summary>
    /// Price is never typed in Pediatrics any more — it comes straight from
    /// the batch the dose was actually drawn from, the same figure Pharmacy
    /// itself would quote for that product right now.
    /// </summary>
    [Fact]
    public void Recording_a_dose_bills_at_the_pharmacy_stocked_price()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var productName = CreateStockedProduct(suffix, mrp: 200m);

        var vaccineName = $"StockVax {suffix}";
        CreateVaccine(vaccineName);

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");
        RecordVaccination(vaccineName, productName);

        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsVaccinationStatus").Contains("added to the bill", StringComparison.OrdinalIgnoreCase),
            "the recording confirmation");

        AppFixture.WaitUntil(
            () => app.Grid("PediatricsLinesGrid").Rows.Any(r =>
                r.Cells[0].Value.Contains(vaccineName) && decimal.TryParse(r.Cells[2].Value, out var p) && p == 200m),
            "the dose to bill at the pharmacy stocked price");

        // Not on record yet — a dose only becomes a VaccinationRecord once
        // the bill it rode in on is actually saved, never at the moment it
        // was added to the cart.
        Assert.DoesNotContain(app.Grid("PediatricsVaccinationHistoryGrid").Rows, r => r.Cells[1].Value == vaccineName);

        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");

        AppFixture.WaitUntil(
            () => app.Grid("PediatricsVaccinationHistoryGrid").Rows.Any(r => r.Cells[1].Value == vaccineName),
            "the dose to land on record now that the bill has saved");
    }

    /// <summary>Clicking "Clear" — or simply never saving — must drop a
    /// pending vaccine line without ever recording it, the same as an
    /// abandoned procedure line leaves nothing behind.</summary>
    [Fact]
    public void Clearing_the_bill_without_saving_never_records_the_dose()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var productName = CreateStockedProduct(suffix);

        var vaccineName = $"DroppedVax {suffix}";
        CreateVaccine(vaccineName);

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");
        RecordVaccination(vaccineName, productName);

        AppFixture.WaitUntil(() => app.Grid("PediatricsLinesGrid").RowCount >= 1, "the line to be added");

        app.Click("PediatricsNewBill");
        AppFixture.WaitUntil(() => app.Grid("PediatricsLinesGrid").RowCount == 0, "the bill to clear");

        Assert.DoesNotContain(app.Grid("PediatricsVaccinationHistoryGrid").Rows, r => r.Cells[1].Value == vaccineName);
    }

    [Fact]
    public void Recording_a_dose_lands_on_the_same_bill_as_a_procedure()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var productName = CreateStockedProduct(suffix, mrp: 300m);

        var vaccineName = $"PaidVax {suffix}";
        CreateVaccine(vaccineName);

        var procedureName = $"ComboProc {suffix}";
        app.SelectTab("GeneralMasterTabs", "Procedures");
        app.Click("GeneralMasterNewProcedure");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is not null, "the procedure editor to open");
        app.Type("ProcedureEditorName", procedureName);
        app.Type("ProcedureEditorPrice", "100");
        app.Click("ProcedureEditorSave");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is null, "the editor to close");

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");

        // Vaccination first, priced straight from the pharmacy batch.
        RecordVaccination(vaccineName, productName);
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsVaccinationStatus").Contains("added to the bill", StringComparison.OrdinalIgnoreCase),
            "the billed confirmation");

        // Then a procedure, onto the same running bill.
        app.Click("PediatricsOpenAddProcedure");
        AppFixture.WaitUntil(() => app.Find("AddProcedureLinePicker") is not null, "the add-procedure popup to open");
        app.ComboBox("AddProcedureLinePicker").Select(procedureName);
        app.Click("AddProcedureLineAdd");
        AppFixture.WaitUntil(() => app.Find("AddProcedureLinePicker") is null, "the popup to close");

        AppFixture.WaitUntil(() => app.Grid("PediatricsLinesGrid").RowCount == 2, "both lines on the one bill");
        Assert.Contains(app.Grid("PediatricsLinesGrid").Rows, r => r.Cells[0].Value.Contains(vaccineName) && r.Cells[1].Value == "Vaccine");
        Assert.Contains(app.Grid("PediatricsLinesGrid").Rows, r => r.Cells[0].Value == procedureName && r.Cells[1].Value == "Procedure");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsFinalAmount").TrimStart('₹'), out var v) && v == 400m,
            "the combined bill to total the vaccine and the procedure together");
    }

    /// <summary>
    /// The vaccine "type" still comes from Vaccine Master, but which brand
    /// was actually given is required, and its price is read straight off
    /// the pharmacy batch it was picked from — never typed. Saving without
    /// picking a brand is refused.
    /// </summary>
    [Fact]
    public void The_brand_administered_is_required_and_its_pharmacy_price_shown()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var productName = CreateStockedProduct(suffix, mrp: 450m);

        var vaccineName = $"BrandVax {suffix}";
        CreateVaccine(vaccineName);

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");

        app.Click("PediatricsOpenRecordVaccination");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is not null, "the vaccination popup to open");
        app.ComboBox("RecordVaccinationVaccinePicker").Select(vaccineName);

        // Saving without a brand picked is refused.
        app.Click("RecordVaccinationSave");
        AppFixture.WaitUntil(
            () => app.TextOf("RecordVaccinationStatus").Contains("pick the brand", StringComparison.OrdinalIgnoreCase),
            "the missing-brand warning");
        Assert.NotNull(app.Find("RecordVaccinationVaccinePicker"));

        app.Type("RecordVaccinationProductSearch", productName);
        AppFixture.WaitUntil(
            () => app.Find("RecordVaccinationProductMatches") is not null
                  && app.ListBox("RecordVaccinationProductMatches").Items.Length == 1,
            "the stocked brand to be found");
        app.ListBox("RecordVaccinationProductMatches").Items[0].Select();

        AppFixture.WaitUntil(
            () => app.TextOf("RecordVaccinationSelectedProduct").Contains(productName),
            "the picked brand to show as selected");
        Assert.Contains("450", app.TextOf("RecordVaccinationSelectedBatchPrice"));

        app.Click("RecordVaccinationSave");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is null, "the popup to close");

        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");

        AppFixture.WaitUntil(
            () => app.Grid("PediatricsVaccinationHistoryGrid").Rows.Any(r => r.Cells[1].Value == vaccineName),
            "the dose to land on record with its brand recorded");
    }

    [Fact]
    public void Vaccination_and_growth_history_show_up_on_the_patients_screen()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var productName = CreateStockedProduct(suffix);

        var vaccineName = $"HistVax {suffix}";
        CreateVaccine(vaccineName);

        var name = RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");

        RecordVaccination(vaccineName, productName);
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsVaccinationStatus").Contains("added to the bill", StringComparison.OrdinalIgnoreCase),
            "the recording confirmation");

        // A dose only lands on record once the bill it rode in on saves.
        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");

        // Growth lives on its own tab, next to the patient panel above it.
        app.SelectTab("PediatricsTabs", "Growth");
        app.Click("PediatricsOpenRecordGrowth");
        AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is not null, "the growth popup to open");
        app.Type("RecordGrowthWeightKg", "12.5");
        app.Type("RecordGrowthHeightCm", "85");
        app.Click("RecordGrowthSave");
        AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is null, "the popup to close");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsGrowthStatus").Contains("recorded", StringComparison.OrdinalIgnoreCase),
            "the growth confirmation");

        // The chart appears once there is at least one measurement to plot —
        // read via the Y axis label, since a bare Border/Canvas (unlike a
        // TextBlock) does not reliably expose an automation peer for Find()
        // to discover even once actually visible on screen.
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsGrowthChartYTop").Split(' ')[0], out _),
            "the growth chart to render");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");
        app.Grid("PatientsGrid").Rows[0].Select();

        app.SelectTab("PatientsTabs", "Vaccination history");
        AppFixture.WaitUntil(
            () => app.Grid("PatientVaccinationHistoryGrid").Rows.Any(r => r.Cells[1].Value == vaccineName),
            "the vaccination to show on the patient record");

        app.SelectTab("PatientsTabs", "Growth chart");
        AppFixture.WaitUntil(() => app.Grid("PatientGrowthHistoryGrid").RowCount >= 1, "the growth entry to show on the patient record");
    }

    /// <summary>
    /// The chart only has something to draw once a measurement exists, and
    /// switching which metric it plots (weight/height/head circumference)
    /// has to keep working rather than only ever showing the default.
    /// </summary>
    [Fact]
    public void The_growth_chart_appears_once_a_measurement_exists_and_the_metric_can_be_switched()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Growth");

        AppFixture.WaitUntil(() => app.Find("PediatricsGrowthChartYTop") is null, "no chart before any measurement exists");

        app.Click("PediatricsOpenRecordGrowth");
        AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is not null, "the growth popup to open");
        app.Type("RecordGrowthWeightKg", "10");
        app.Type("RecordGrowthHeightCm", "72");
        app.Type("RecordGrowthHeadCircumferenceCm", "44");
        app.Click("RecordGrowthSave");
        AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is null, "the popup to close");

        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsGrowthChartYTop").Split(' ')[0], out _),
            "the chart to appear, with a numeric Y axis top label");

        app.ComboBox("PediatricsGrowthMetric").Select("Height");
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsGrowthChartYTop").Split(' ')[0], out _),
            "the chart to still render after switching metric");

        // EnumLabelConverter lowercases everything after the first letter,
        // so "HeadCircumference" reads "Head circumference" — not Title Case.
        app.ComboBox("PediatricsGrowthMetric").Select("Head circumference");
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsGrowthChartYTop").Split(' ')[0], out _),
            "the chart to still render for head circumference too");
    }

    /// <summary>Closes whatever print preview is currently open — the same
    /// modal-close sequence every other print test in this class uses.</summary>
    private void CloseAnyPrintPreview()
    {
        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;
        Assert.NotNull(preview);
        preview!.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the preview to close");
    }

    [Fact]
    public void Printing_the_vaccination_history_opens_a_preview()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var vaccineName = $"PrintVax {suffix}";
        CreateVaccine(vaccineName);

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Care");

        // The button works even with nothing recorded yet — a blank
        // certificate is still a valid thing to hand someone.
        app.Click("PediatricsPrintVaccinationHistory");
        CloseAnyPrintPreview();

        // And once there is something on record, printing still works.
        var productName = CreateStockedProduct(suffix);
        app.Navigate("NavPediatrics", "Pediatrics");
        app.SelectTab("PediatricsTabs", "Care");
        RecordVaccination(vaccineName, productName);
        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");
        AppFixture.WaitUntil(
            () => app.Grid("PediatricsVaccinationHistoryGrid").Rows.Any(r => r.Cells[1].Value == vaccineName),
            "the dose to land on record");

        app.Click("PediatricsPrintVaccinationHistory");
        CloseAnyPrintPreview();
    }

    /// <summary>
    /// What a template column's cell is actually showing — its Value is a
    /// WPF-internal placeholder rather than the real text (see AppFixture's
    /// own TextOfCell, which this mirrors), which the Immunization grid's
    /// VACCINE and STATUS columns both are.
    /// </summary>
    private static string TextOfCell(GridCell cell)
    {
        var value = cell.Value ?? "";
        if (value.Length > 0 && !value.StartsWith("Item:", StringComparison.Ordinal)) return value;

        return cell.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Select(t => t.Name ?? "")
            .FirstOrDefault(t => t.Length > 0) ?? "";
    }

    /// <summary>The Immunization tab lists this patient's own doses
    /// checked off against the vaccine master — a freshly registered test
    /// patient here has no date of birth on file, so every not-yet-given
    /// row reads Upcoming (see <c>GetImmunizationCardAsync</c>'s own
    /// remarks). Recording straight from a row's own Record button opens
    /// the same popup the "+ Record vaccination" button does, pre-selected
    /// to that row's vaccine.</summary>
    [Fact]
    public void The_immunization_tab_lists_doses_against_the_schedule_and_can_record_from_a_row()
    {
        EnsurePediatricsEnabled();
        var suffix = DateTime.Now.ToString("HHmmssfff");

        var vaccineName = $"CardVax {suffix}";
        CreateVaccine(vaccineName);

        var productName = CreateStockedProduct(suffix, mrp: 250m);

        RegisterAndSelectPatient(suffix);
        app.SelectTab("PediatricsTabs", "Immunization");

        AppFixture.WaitUntil(
            () => app.Grid("PediatricsImmunizationGrid").Rows.Any(r => TextOfCell(r.Cells[1]).Contains(vaccineName)),
            "the new vaccine to appear on the immunization card");

        var row = app.Grid("PediatricsImmunizationGrid").Rows.First(r => TextOfCell(r.Cells[1]).Contains(vaccineName));
        Assert.Equal("Upcoming", TextOfCell(row.Cells[2]));

        // No DOB on file for this patient, so the recommended-age column
        // still reads a value — it is not tied to the patient's own age.
        Assert.False(string.IsNullOrWhiteSpace(row.Cells[0].Value));

        row.FindFirstDescendant(cf => cf.ByName("Record"))!.AsButton().Invoke();
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is not null, "the vaccination popup to open");

        // Never touching the vaccine picker at all — it is proof the popup
        // opened pre-selected to this row's vaccine rather than blank, since
        // saving without a vaccine picked would otherwise be refused (see
        // The_brand_administered_is_required_and_its_pharmacy_price_shown).
        app.Type("RecordVaccinationProductSearch", productName);
        AppFixture.WaitUntil(
            () => app.Find("RecordVaccinationProductMatches") is not null
                  && app.ListBox("RecordVaccinationProductMatches").Items.Length == 1,
            "the brand to be found in pharmacy stock");
        app.ListBox("RecordVaccinationProductMatches").Items[0].Select();

        app.Click("RecordVaccinationSave");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is null, "the popup to close");

        // The dose landed on the running bill, but the bill panel itself
        // lives on the Care tab, not Immunization — recording from a row
        // does not jump there automatically.
        app.SelectTab("PediatricsTabs", "Care");
        AppFixture.WaitUntil(() => app.Find("PediatricsSaveBill") is not null, "the Care tab to render");
        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");

        app.SelectTab("PediatricsTabs", "Immunization");
        AppFixture.WaitUntil(
            () => app.Grid("PediatricsImmunizationGrid").Rows
                .Any(r => TextOfCell(r.Cells[1]).Contains(vaccineName) && TextOfCell(r.Cells[2]) == "Given"),
            "the dose to read Given on the card once the bill has saved");
    }

    /// <summary>
    /// The Immunization card fills its tab, the way Growth and Care do.
    ///
    /// It used to sit in a column capped at 820 wide and centred, so on a
    /// full-screen window the card was marooned in the middle with the
    /// schedule scrolling inside a 360px box while half the screen sat
    /// empty. Comparing against the Care tab rather than against a fixed
    /// number keeps this honest at any window size.
    /// </summary>
    [Fact]
    public void The_immunization_card_fills_the_tab_like_the_other_tabs_do()
    {
        EnsurePediatricsEnabled();
        RegisterAndSelectPatient(DateTime.Now.ToString("HHmmssfff"));

        app.SelectTab("PediatricsTabs", "Care");
        AppFixture.WaitUntil(() => app.Find("PediatricsLinesGrid") is not null, "the Care tab");
        var care = app.Require("PediatricsLinesGrid").BoundingRectangle.Width;

        app.SelectTab("PediatricsTabs", "Immunization");
        AppFixture.WaitUntil(() => app.Find("PediatricsImmunizationGrid") is not null, "the Immunization tab");
        var card = app.Require("PediatricsImmunizationGrid").BoundingRectangle;

        Assert.True(card.Width > care * 0.9,
            $"the immunization schedule should span the tab like Care does: Care is {care}px, this is {card.Width}px");

        // And the schedule itself should use the height it is given rather
        // than scrolling a dozen vaccines inside a short fixed box.
        Assert.True(card.Height > 360,
            $"the schedule should grow with the tab, not stay capped: {card.Height}px");
    }
}

