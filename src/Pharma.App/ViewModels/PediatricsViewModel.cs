using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.Charts;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>Which measurement the growth chart is currently plotting.</summary>
public enum GrowthChartMetric
{
    Weight,
    Height,
    HeadCircumference
}

/// <summary>What a bill line was raised for — shown in the bill grid's own
/// TYPE column so a combined procedure-and-vaccine bill still reads clearly
/// at a glance.</summary>
public enum ProcedureBillRowKind
{
    Procedure,
    Vaccine
}

/// <summary>One line on a procedure bill being built — shared shape,
/// reused by Dentist's own simple billing later.</summary>
public partial class ProcedureBillRow : ObservableObject
{
    public Guid? ProcedureId { get; init; }
    public string ProcedureName { get; init; } = "";
    public ProcedureBillRowKind Kind { get; init; } = ProcedureBillRowKind.Procedure;

    /// <summary>Set only for a <see cref="ProcedureBillRowKind.Vaccine"/>
    /// line — the dose is not actually recorded until the bill this line
    /// belongs to is saved, so this carries what the eventual
    /// <see cref="VaccinationRecord"/> needs until then. Removing the line
    /// before saving simply drops it — no record, no bill entry, nothing
    /// to undo.</summary>
    public VaccinationDraft? VaccinationDraft { get; init; }

    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _quantity = 1;

    public decimal Amount => Price * Quantity;

    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(Amount));
}

/// <summary>
/// The Pediatrics "Patient" screen: pick a patient once, in the header above
/// the tabs, then Growth, Care (procedure billing and vaccination, through
/// popup forms adding straight into the same running bill) and Immunization
/// (this patient's own doses checked off against Vaccine Master's
/// recommended ages) each on their own tab. Vaccine Master, Procedure
/// Master and the rest of Pediatrics' configuration live on the shared
/// General Master nav destination — <see cref="GeneralMasterViewModel"/> —
/// configuration, not per-visit work, and never sharing a screen with it.
/// </summary>
public partial class PediatricsViewModel(
    PediatricsService pediatrics, ProcedureBillsService bills, OpdService opd, SettingsService settings,
    PharmacyService pharmacy)
    : ObservableObject, IPage
{
    public string Title => "Pediatrics";
    public string Subtitle => Lines.Count == 0 ? "No procedures on this bill" : $"{Lines.Count} item(s) · ₹{FinalAmount:0.00}";

    public async Task LoadAsync()
    {
        await LoadProceduresAsync();
        await LoadVaccinesAsync();
        NewBill();

        // Re-navigating to this page (a DI singleton) does not otherwise
        // refresh a patient already selected from a prior visit here — so
        // without this, the histories would sit stale until the patient
        // was reselected.
        if (SelectedPatient is not null)
        {
            await LoadVaccinationHistoryAsync();
            await LoadDueVaccinesAsync();
            await LoadGrowthHistoryAsync();
            await LoadImmunizationCardAsync();
        }
    }

    // ── Shared patient panel (Billing and Vaccination both bill/treat the
    //    same person on the same visit, so one selection serves both) ─────

    public ObservableCollection<Patient> PatientMatches { get; } = [];
    [ObservableProperty] private string _patientSearch = "";

    [NotifyPropertyChangedFor(nameof(HasSelectedPatient))]
    [ObservableProperty] private Patient? _selectedPatient;

    public bool HasSelectedPatient => SelectedPatient is not null;

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null) return;

        _patientSearch = "";
        OnPropertyChanged(nameof(PatientSearch));

        // Whatever metric the chart was showing for the last patient may
        // not exist at all for this one — Weight is the one every
        // measurement is most likely to carry, so that's the sane default
        // to land back on rather than showing a blank chart.
        SelectedGrowthMetric = GrowthChartMetric.Weight;

        LoadVaccinationHistoryAsync().Forget("Loading vaccination history");
        LoadDueVaccinesAsync().Forget("Loading due vaccines");
        LoadGrowthHistoryAsync().Forget("Loading growth history");
        LoadImmunizationCardAsync().Forget("Loading immunization card");
    }

    partial void OnPatientSearchChanged(string value) => FindPatientsAsync().Forget("Searching patients");

    [RelayCommand]
    private async Task FindPatientsAsync()
    {
        PatientMatches.Clear();
        if (string.IsNullOrWhiteSpace(PatientSearch)) return;

        foreach (var p in await opd.SearchPatientsAsync(PatientSearch, 20)) PatientMatches.Add(p);
        if (PatientMatches.Count == 1) SelectedPatient = PatientMatches[0];
    }

    [RelayCommand]
    private void ChangePatient()
    {
        SelectedPatient = null;
        PatientSearch = "";
        PatientMatches.Clear();
    }

    [RelayCommand]
    private async Task NewPatientAsync()
    {
        var editor = new PatientEditorViewModel(opd);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Saved is { } patient) SelectedPatient = patient;
    }

    /// <summary>Called from the Patients screen to jump here pre-filled for
    /// a specific patient, instead of typing the search again — the same
    /// idiom <see cref="DiagnosticsViewModel.SelectPatientAsync"/> uses.</summary>
    public async Task SelectPatientAsync(Guid patientId)
    {
        NewBill();
        var patients = await opd.SearchPatientsAsync(null, 1000);
        SelectedPatient = patients.FirstOrDefault(p => p.Id == patientId);
    }

    // ── Billing ────────────────────────────────────────────────────────────

    public ObservableCollection<Procedure> Procedures { get; } = [];

    private async Task LoadProceduresAsync()
    {
        Procedures.Clear();
        foreach (var p in await bills.SearchProceduresAsync(ProcedureDepartment.Pediatrics, null, activeOnly: true))
            Procedures.Add(p);
    }

    /// <summary>Opens the "Add procedure" popup and, if something was
    /// picked, adds it straight onto the running bill.</summary>
    [RelayCommand]
    private async Task OpenAddProcedureAsync()
    {
        if (SelectedPatient is null)
        {
            Warn("Select a patient first.");
            return;
        }

        var editor = new AddProcedureLineViewModel(Procedures);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Result is not { } row) return;
        if (Lines.Any(l => l.ProcedureId == row.ProcedureId))
        {
            Warn($"{row.ProcedureName} is already on this bill.");
            return;
        }

        Watch(row);
        Lines.Add(row);
        Recalculate();
    }

    public ObservableCollection<ProcedureBillRow> Lines { get; } = [];

    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _finalAmount;
    [ObservableProperty] private PaymentMode _paymentMode = PaymentMode.Cash;
    [ObservableProperty] private string _transactionNo = "";
    [ObservableProperty] private string _billingStatus = "";

    [ObservableProperty] private Guid? _currentBillId;
    [ObservableProperty] private string _billNo = "";
    [ObservableProperty] private string _referredBy = "";

    public bool ShowTransactionNo => PaymentMode is PaymentMode.Upi or PaymentMode.Card;
    partial void OnPaymentModeChanged(PaymentMode value) => OnPropertyChanged(nameof(ShowTransactionNo));

    public Array PaymentModes => Enum.GetValues<PaymentMode>();

    [RelayCommand]
    private void RemoveLine(ProcedureBillRow? row)
    {
        if (row is null) return;

        row.PropertyChanged -= OnLineChanged;
        Lines.Remove(row);
        Recalculate();
    }

    private void Watch(ProcedureBillRow row) => row.PropertyChanged += OnLineChanged;
    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Recalculate();
    partial void OnDiscountChanged(decimal value) => Recalculate();

    private void Recalculate()
    {
        TotalAmount = Lines.Sum(l => l.Amount);
        FinalAmount = Math.Max(0, TotalAmount - Discount);
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private Task SaveBillAsync() => CompleteBillAsync(print: false);

    [RelayCommand]
    private Task SaveBillAndPrintAsync() => CompleteBillAsync(print: true);

    private async Task CompleteBillAsync(bool print)
    {
        using var log = AppLog.Enter(
            "Pediatrics.SaveBill", $"print={print} lines={Lines.Count} patient={SelectedPatient?.Id}");

        if (SelectedPatient is not { } patient)
        {
            log.Skip("no patient chosen");
            Warn("Select a patient first.");
            return;
        }

        if (Lines.Count == 0)
        {
            log.Skip("nothing on the bill");
            Warn("Add at least one procedure to the bill.");
            return;
        }

        var bill = new ProcedureBill
        {
            Id = CurrentBillId ?? Guid.Empty,
            PatientId = patient.Id,
            PatientName = patient.Name,
            PatientNo = patient.PatientNo,
            PaymentMode = PaymentMode,
            TransactionNo = string.IsNullOrWhiteSpace(TransactionNo) ? null : TransactionNo.Trim(),
            Discount = Discount,
            ReferredBy = string.IsNullOrWhiteSpace(ReferredBy) ? null : ReferredBy.Trim()
        };

        var lines = Lines.Select(l => new ProcedureBillLine
        {
            ProcedureId = l.ProcedureId, ProcedureName = l.ProcedureName, Price = l.Price, Quantity = l.Quantity
        }).ToList();

        // Captured before NewBill() clears Lines below — each is only
        // actually recorded once the bill it rode in on has actually saved,
        // never at the moment it was added to the cart.
        var vaccineDrafts = Lines
            .Where(l => l.Kind == ProcedureBillRowKind.Vaccine && l.VaccinationDraft is not null)
            .Select(l => l.VaccinationDraft!)
            .ToList();

        try
        {
            var saved = await bills.SaveBillAsync(bill, lines);
            BillingStatus = $"Bill {saved.BillNo} saved · ₹{saved.FinalAmount:0.00}";

            foreach (var draft in vaccineDrafts)
            {
                await pediatrics.RecordVaccinationAsync(new VaccinationRecord
                {
                    PatientId = patient.Id, PatientName = patient.Name,
                    VaccineId = draft.Vaccine.Id, VaccineName = draft.Vaccine.Name, DoseNumber = draft.Vaccine.DoseNumber,
                    GivenOn = draft.GivenOn, BatchNo = draft.BatchNo, SiteOfInjection = draft.Site, AdministeredBy = draft.AdministeredBy,
                    ProductId = draft.ProductId, ProductName = draft.ProductName, Manufacturer = draft.Manufacturer,
                    BatchId = draft.BatchId
                });
            }

            if (vaccineDrafts.Count > 0)
            {
                await LoadVaccinationHistoryAsync();
                await LoadDueVaccinesAsync();
                await LoadImmunizationCardAsync();
            }

            if (print)
            {
                var clinic = await settings.GetClinicAsync();
                var theme = await settings.GetDocumentThemeAsync();
                var full = await bills.GetBillAsync(saved.Id);
                if (full is not null)
                    PrintService.Preview(() => ProcedureBillDocument.Build(full, clinic, theme), $"Bill {full.BillNo}");
            }

            NewBill();
            log.Ok($"{saved.BillNo} final={saved.FinalAmount:0.00} printed={print} vaccines={vaccineDrafts.Count}");
        }
        catch (Exception ex)
        {
            log.Skip($"refused: {ex.GetType().Name}: {ex.Message}");
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void NewBill()
    {
        foreach (var row in Lines) row.PropertyChanged -= OnLineChanged;

        Lines.Clear();
        Discount = 0;
        PaymentMode = PaymentMode.Cash;
        TransactionNo = "";
        CurrentBillId = null;
        BillNo = "";
        ReferredBy = "";

        Recalculate();
    }

    // ── Vaccination ────────────────────────────────────────────────────────

    public ObservableCollection<VaccineMaster> Vaccines { get; } = [];

    [ObservableProperty] private string _vaccinationStatus = "";

    public ObservableCollection<VaccinationRecord> VaccinationHistory { get; } = [];
    public ObservableCollection<DueVaccine> DueVaccines { get; } = [];
    public bool HasDueVaccines => DueVaccines.Count > 0;

    private async Task LoadVaccinesAsync()
    {
        Vaccines.Clear();
        foreach (var v in await pediatrics.SearchVaccinesAsync(null, activeOnly: true)) Vaccines.Add(v);
    }

    private async Task LoadVaccinationHistoryAsync()
    {
        VaccinationHistory.Clear();
        if (SelectedPatient is not { } patient) return;

        foreach (var r in await pediatrics.GetVaccinationHistoryAsync(patient.Id)) VaccinationHistory.Add(r);
    }

    [RelayCommand]
    private async Task PrintVaccinationHistoryAsync()
    {
        if (SelectedPatient is not { } patient)
        {
            Warn("Select a patient first.");
            return;
        }

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(
            () => VaccinationHistoryDocument.Build(patient, VaccinationHistory, clinic, theme),
            $"Vaccination record — {patient.Name}");
    }

    private async Task LoadDueVaccinesAsync()
    {
        DueVaccines.Clear();
        if (SelectedPatient is { } patient)
            foreach (var d in await pediatrics.GetDueVaccinesForPatientAsync(patient.Id, leadDays: 14)) DueVaccines.Add(d);

        OnPropertyChanged(nameof(HasDueVaccines));
    }

    /// <summary>
    /// Opens the "Record vaccination" popup and, if something was entered,
    /// adds it to the running bill — same shape as
    /// <see cref="OpenAddProcedureAsync"/>. There is no "charge or not"
    /// choice on the form: every dose given goes on the bill, at whatever
    /// price is on it (0 for a free dose). Nothing is written to
    /// <see cref="VaccinationRecord"/> yet, though — only added to
    /// <see cref="Lines"/>, exactly like a procedure line, kept as a draft
    /// until the bill this line rides in on actually saves. Clicking
    /// "Clear" or navigating away without saving drops it, the same as it
    /// would drop an added procedure — a dose is only on record once it is
    /// on a saved bill, never before.
    /// </summary>
    [RelayCommand]
    private Task OpenRecordVaccinationAsync() => OpenRecordVaccinationCoreAsync(null);

    /// <summary>Same popup, reached from a specific row on the Immunization
    /// tab instead of the generic "+ Record vaccination" button — the
    /// vaccine picker opens pre-selected to that row's type, so recording a
    /// due dose from the card doesn't ask the desk to find it again in a
    /// dropdown.</summary>
    [RelayCommand]
    private Task RecordFromCardAsync(ImmunizationCardRow? row)
    {
        var vaccine = row is null ? null : Vaccines.FirstOrDefault(v => v.Id == row.VaccineId);
        return OpenRecordVaccinationCoreAsync(vaccine);
    }

    private async Task OpenRecordVaccinationCoreAsync(VaccineMaster? preselect)
    {
        if (SelectedPatient is null)
        {
            Warn("Select a patient first.");
            return;
        }

        var editor = new RecordVaccinationViewModel(Vaccines, pharmacy);
        if (preselect is not null) editor.SelectedVaccine = preselect;

        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Result is not { } draft) return;

        var row = new ProcedureBillRow
        {
            ProcedureName = $"{draft.Vaccine.Name} (dose {draft.Vaccine.DoseNumber})",
            Price = draft.Price, Kind = ProcedureBillRowKind.Vaccine, VaccinationDraft = draft
        };
        Watch(row);
        Lines.Add(row);
        Recalculate();

        VaccinationStatus = $"{draft.Vaccine.Name} added to the bill at ₹{draft.Price:0.00} — recorded once the bill is saved.";
    }

    // ── Immunization card — this patient's own doses checked off against
    //    Vaccine Master's recommended ages (seeded from the WHO / IAP
    //    schedule), ordered the same way Vaccine Master itself is so
    //    rows naturally cluster by milestone. Read-only aside from the
    //    Record button, which just opens the same popup billing already
    //    runs through above — recording here never bypasses the bill. ────

    public ObservableCollection<ImmunizationCardRow> ImmunizationRows { get; } = [];

    [ObservableProperty] private int _immunizationGivenCount;
    [ObservableProperty] private int _immunizationOverdueCount;
    [ObservableProperty] private int _immunizationDueSoonCount;
    [ObservableProperty] private int _immunizationUpcomingCount;

    public int ImmunizationTotalCount =>
        ImmunizationGivenCount + ImmunizationOverdueCount + ImmunizationDueSoonCount + ImmunizationUpcomingCount;

    private async Task LoadImmunizationCardAsync()
    {
        ImmunizationRows.Clear();
        ImmunizationGivenCount = 0;
        ImmunizationOverdueCount = 0;
        ImmunizationDueSoonCount = 0;
        ImmunizationUpcomingCount = 0;

        if (SelectedPatient is { } patient)
        {
            var rows = await pediatrics.GetImmunizationCardAsync(patient.Id);
            foreach (var row in rows) ImmunizationRows.Add(row);

            ImmunizationGivenCount = rows.Count(r => r.Status == ImmunizationStatus.Given);
            ImmunizationOverdueCount = rows.Count(r => r.Status == ImmunizationStatus.Overdue);
            ImmunizationDueSoonCount = rows.Count(r => r.Status == ImmunizationStatus.DueSoon);
            ImmunizationUpcomingCount = rows.Count(r => r.Status == ImmunizationStatus.Upcoming);
        }

        OnPropertyChanged(nameof(ImmunizationTotalCount));
    }

    // ── Growth chart — lives on the Patient tab, right under the patient
    //    picked there, rather than buried in the Care tab with everything
    //    billable ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _growthStatus = "";

    public ObservableCollection<GrowthMeasurement> GrowthHistory { get; } = [];

    private async Task LoadGrowthHistoryAsync()
    {
        GrowthHistory.Clear();
        if (SelectedPatient is { } patient)
            foreach (var g in await pediatrics.GetGrowthHistoryAsync(patient.Id)) GrowthHistory.Add(g);

        BuildGrowthChart();
    }

    /// <summary>Opens the "Record growth" popup — never billed, so unlike
    /// vaccination this only ever records, nothing joins the bill.</summary>
    [RelayCommand]
    private async Task OpenRecordGrowthAsync()
    {
        if (SelectedPatient is not { } patient)
        {
            Warn("Select a patient first.");
            return;
        }

        var editor = new RecordGrowthViewModel();
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Result is not { } draft) return;

        await pediatrics.RecordGrowthAsync(new GrowthMeasurement
        {
            PatientId = patient.Id, MeasuredOn = draft.MeasuredOn,
            WeightKg = draft.WeightKg, HeightCm = draft.HeightCm, HeadCircumferenceCm = draft.HeadCircumferenceCm
        });

        GrowthStatus = "Measurement recorded.";
        await LoadGrowthHistoryAsync();
    }

    // ── Growth chart geometry — expected (approximate reference) vs the
    //    patient's own measurements, on shared axes so the two lines are
    //    actually comparable rather than each stretched to its own range ──

    private const double GrowthChartWidth = 480, GrowthChartHeight = 190;

    [ObservableProperty] private GrowthChartMetric _selectedGrowthMetric = GrowthChartMetric.Weight;
    public static Array GrowthChartMetrics => Enum.GetValues<GrowthChartMetric>();

    [ObservableProperty] private bool _hasGrowthChartData;
    [ObservableProperty] private string _expectedGrowthLine = "";
    [ObservableProperty] private string _patientGrowthLine = "";
    [ObservableProperty] private string _growthChartUnit = "kg";
    [ObservableProperty] private string _growthChartYAxisTop = "";
    [ObservableProperty] private string _growthChartYAxisBottom = "";
    [ObservableProperty] private string _growthChartXAxisRight = "";

    public ObservableCollection<Point> GrowthChartDots { get; } = [];

    partial void OnSelectedGrowthMetricChanged(GrowthChartMetric value) => BuildGrowthChart();

    /// <summary>Age in months at a given date — from the patient's own
    /// date of birth when it is on file (precise, which matters most for an
    /// infant under a year old), falling back to their whole-year Age
    /// otherwise (coarse, but still usable for an older child).</summary>
    private double AgeMonthsAt(DateTime on)
    {
        if (SelectedPatient?.DateOfBirth is { } dob)
            return Math.Max(0, (on - dob.Date).TotalDays / 30.4368);

        return (SelectedPatient?.Age ?? 0) * 12.0;
    }

    private void BuildGrowthChart()
    {
        GrowthChartDots.Clear();
        ExpectedGrowthLine = "";
        PatientGrowthLine = "";
        HasGrowthChartData = false;

        if (SelectedPatient is null || GrowthHistory.Count == 0) return;

        GrowthChartUnit = SelectedGrowthMetric == GrowthChartMetric.Weight ? "kg" : "cm";

        double? ValueOf(GrowthMeasurement g) => SelectedGrowthMetric switch
        {
            GrowthChartMetric.Weight => g.WeightKg is { } w ? (double)w : null,
            GrowthChartMetric.Height => g.HeightCm is { } h ? (double)h : null,
            GrowthChartMetric.HeadCircumference => g.HeadCircumferenceCm is { } c ? (double)c : null,
            _ => null
        };

        var measurements = GrowthHistory
            .OrderBy(g => g.MeasuredOn)
            .Select(g => (AgeMonths: AgeMonthsAt(g.MeasuredOn), Value: ValueOf(g)))
            .Where(m => m.Value is not null)
            .Select(m => (m.AgeMonths, Value: m.Value!.Value))
            .ToList();

        if (measurements.Count == 0) return;

        var maxAge = Math.Min(Math.Max(measurements.Max(m => m.AgeMonths), 1), GrowthReference.MaxAgeMonths);
        const double minAge = 0;

        const int samples = 24;
        var expectedPoints = Enumerable.Range(0, samples + 1)
            .Select(i => minAge + (maxAge - minAge) * i / samples)
            .Select(age => (Age: age, Value: SelectedGrowthMetric switch
            {
                GrowthChartMetric.Weight => GrowthReference.ExpectedWeightKg(age),
                GrowthChartMetric.Height => GrowthReference.ExpectedHeightCm(age),
                _ => GrowthReference.ExpectedHeadCircumferenceCm(age)
            }))
            .ToList();

        var allValues = measurements.Select(m => m.Value).Concat(expectedPoints.Select(e => e.Value)).ToList();
        var yMin = allValues.Min();
        var yMax = allValues.Max();
        if (yMax - yMin < 0.5) { yMin -= 1; yMax += 1; }
        var pad = (yMax - yMin) * 0.1;
        yMin -= pad;
        yMax += pad;

        ExpectedGrowthLine = ChartGeometry.XyLine(
            expectedPoints.Select(e => (e.Age, e.Value)).ToList(), GrowthChartWidth, GrowthChartHeight, minAge, maxAge, yMin, yMax);

        PatientGrowthLine = ChartGeometry.XyLine(
            measurements.Select(m => (m.AgeMonths, m.Value)).ToList(), GrowthChartWidth, GrowthChartHeight, minAge, maxAge, yMin, yMax);

        const double dotRadius = 3;
        foreach (var (age, value) in measurements)
        {
            var (x, y) = ChartGeometry.XyPoint(age, value, GrowthChartWidth, GrowthChartHeight, minAge, maxAge, yMin, yMax);
            GrowthChartDots.Add(new Point(x - dotRadius, y - dotRadius));
        }

        GrowthChartYAxisTop = $"{yMax:0.#} {GrowthChartUnit}";
        GrowthChartYAxisBottom = $"{yMin:0.#} {GrowthChartUnit}";
        GrowthChartXAxisRight = $"{maxAge:0} mo";
        HasGrowthChartData = true;
    }

    private void Warn(string message)
    {
        BillingStatus = message;
        Dialog.Show(message, "Pediatrics", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
