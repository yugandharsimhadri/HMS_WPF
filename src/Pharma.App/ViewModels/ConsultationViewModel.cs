using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>An editable prescription line.</summary>
public partial class PrescriptionRow : ObservableObject
{
    [ObservableProperty] private string _medicine = "";
    [ObservableProperty] private string _dosage = "1 tab";
    [ObservableProperty] private string _frequency = "1-0-1";
    [ObservableProperty] private int _days = 3;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private string? _instructions;

    public Guid? ProductId { get; set; }

    /// <summary>Units in one pack, so the line can show what that is in strips.</summary>
    public int UnitsPerPack { get; set; } = 1;
    public string? PackLabel { get; set; }

    /// <summary>
    /// The course in words: "6 tablets · 1 × 10 TAB minus 4". The doctor writes
    /// individual units; the pharmacy hands over strips.
    /// </summary>
    public string Course
    {
        get
        {
            if (Quantity <= 0) return "";
            if (UnitsPerPack <= 1) return $"{Quantity}";

            return $"{Quantity} · {PackMath.Describe(Quantity, UnitsPerPack, PackLabel)}";
        }
    }

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(Course));
}

/// <summary>One test requested for this consultation, picked from the
/// catalogue or typed free-text — the diagnosis-tab equivalent of a
/// <see cref="PrescriptionRow"/>. Nothing is billed here; this is only a
/// list for the Diagnostics desk to load later.</summary>
public partial class DiagnosticRequestRow : ObservableObject
{
    public Guid? TestId { get; init; }
    [ObservableProperty] private string _testName = "";
}

public partial class ConsultationViewModel : ObservableObject
{
    private readonly OpdService _opd;
    private readonly PharmacyService _pharmacy;
    private readonly DiagnosticsService _diagnostics;
    private readonly SettingsService _settings;
    private readonly Guid _visitId;

    public ObservableCollection<PrescriptionRow> Lines { get; } = [];
    public ObservableCollection<Product> Products { get; } = [];

    [ObservableProperty] private Visit? _visit;
    [ObservableProperty] private string _header = "";
    [ObservableProperty] private string _complaint = "";
    [ObservableProperty] private string _diagnosis = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _weight = "";
    [ObservableProperty] private string _bloodPressure = "";
    [ObservableProperty] private string _temperature = "";
    [ObservableProperty] private string _height = "";
    [ObservableProperty] private string _heartRate = "";
    [ObservableProperty] private string _spo2 = "";
    [ObservableProperty] private decimal _fee;
    [ObservableProperty] private DateTime? _followUpOn;
    [ObservableProperty] private string _status = "";

    // ── Diagnosis tab: tests requested ────────────────────────────────────

    public ObservableCollection<DiagnosticRequestRow> RequestedTests { get; } = [];
    public ObservableCollection<DiagnosticTest> TestMatches { get; } = [];

    [ObservableProperty] private bool _diagnosticsEnabled;
    [ObservableProperty] private string _testSearch = "";
    [ObservableProperty] private DiagnosticTest? _newTest;
    [ObservableProperty] private string _testHint = "";
    [ObservableProperty] private bool _testMissing;

    private List<DiagnosticTest> _testCatalogue = [];

    partial void OnTestSearchChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) TestMissing = false;

        if (NewTest is not null && !string.Equals(NewTest.Name, value, StringComparison.OrdinalIgnoreCase))
            NewTest = null;

        TestMatches.Clear();
        var term = value?.Trim() ?? "";

        if (term.Length >= 2 && NewTest is null)
        {
            foreach (var t in _testCatalogue
                         .Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                         .Take(8))
            {
                TestMatches.Add(t);
            }
        }

        TestHint = NewTest is not null
            ? "From our test catalogue."
            : string.IsNullOrWhiteSpace(value)
                ? ""
                : !DiagnosticsEnabled
                    ? "Written down as free text — it will print on the prescription. Turn on Diagnostics in Settings to bill from a catalogue instead."
                    : TestMatches.Count > 0
                        ? "Pick one from the list, or keep typing for a test we do not run in-house."
                        : "Not in our catalogue — it will still be listed for the diagnostics desk.";
    }

    [RelayCommand]
    private void PickTest(DiagnosticTest? test)
    {
        if (test is null) return;

        NewTest = test;
        TestSearch = test.Name;
        TestMatches.Clear();
    }

    [RelayCommand]
    private void AddTestRequest()
    {
        var name = NewTest?.Name ?? TestSearch;

        if (string.IsNullOrWhiteSpace(name))
        {
            TestMissing = true;
            Status = "Choose a test, or type its name.";
            return;
        }

        TestMissing = false;

        if (RequestedTests.Any(r => r.TestName.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            Status = $"{name} is already on the list.";
            return;
        }

        RequestedTests.Add(new DiagnosticRequestRow { TestId = NewTest?.Id, TestName = name.Trim() });

        NewTest = null;
        TestSearch = "";
        TestMatches.Clear();
        TestHint = "";
        Status = $"{name} added to the diagnosis list.";
    }

    [RelayCommand]
    private void RemoveTestRequest(DiagnosticRequestRow? row)
    {
        if (row is not null) RequestedTests.Remove(row);
    }

    // The entry row. Filling a form and pressing Add is far easier than editing
    // cells in a grid, which needs a click to start and swallows the Tab key.

    /// <summary>Matches for what has been typed. Empty once one is chosen.</summary>
    public ObservableCollection<Product> Matches { get; } = [];

    /// <summary>What was typed. Also the medicine name when nothing is chosen.</summary>
    [ObservableProperty] private string _medicineSearch = "";

    /// <summary>The catalogue medicine chosen, or null for one we do not stock.</summary>
    [ObservableProperty] private Product? _newMedicine;

    [ObservableProperty] private string _medicineHint = "";
    // Deliberately empty. A pre-filled dose is a clinical decision made by the
    // software, and one that is easy to leave in place by accident.
    [ObservableProperty] private string _newDosage = "";
    [ObservableProperty] private int _newDays;

    // Morning, afternoon and night, chosen rather than typed. A prescription is
    // written this way, and picking from a list rules out "1-0-l" and "1_0_1".
    [ObservableProperty] private string _morningDose = "0";
    [ObservableProperty] private string _afternoonDose = "0";
    [ObservableProperty] private string _nightDose = "0";

    /// <summary>Half and quarter doses are normal on a paediatric prescription.</summary>
    public string[] DoseOptions { get; } = ["0", "1/4", "1/2", "1", "2"];

    /// <summary>The "1-0-1" that gets saved and printed, built from the three boxes.</summary>
    public string NewFrequency => $"{MorningDose}-{AfternoonDose}-{NightDose}";

    partial void OnMorningDoseChanged(string value) => FrequencyChanged();
    partial void OnAfternoonDoseChanged(string value) => FrequencyChanged();
    partial void OnNightDoseChanged(string value) => FrequencyChanged();

    private void FrequencyChanged()
    {
        OnPropertyChanged(nameof(NewFrequency));
        RecalculateCourse();
    }
    [ObservableProperty] private int _newQuantity;

    // A prescription line needs a medicine and a number of units before it can
    // be added; both are marked so the message names a box rather than a rule.
    [ObservableProperty] private bool _medicineMissing;
    [ObservableProperty] private bool _quantityMissing;

    partial void OnNewQuantityChanged(int value)
    {
        if (value > 0) QuantityMissing = false;
    }
    [ObservableProperty] private string _newInstructions = "";
    [ObservableProperty] private string _courseHint = "";

    public event Action? RequestClose;

    /// <summary>The form as it was last read from or written to the database.</summary>
    private string _savedSnapshot = "";

    public ConsultationViewModel(
        Guid visitId, OpdService opd, PharmacyService pharmacy, DiagnosticsService diagnostics, SettingsService settings)
    {
        _visitId = visitId;
        _opd = opd;
        _pharmacy = pharmacy;
        _diagnostics = diagnostics;
        _settings = settings;
    }

    public async Task LoadAsync()
    {
        using var log = AppLog.Enter("Consultation.Load", $"visit={_visitId}");

        Visit = await _opd.GetVisitAsync(_visitId);

        if (Visit is null)
        {
            log.Skip("visit not found");
            return;
        }

        Header = $"Token {Visit.TokenNo} · {Visit.Patient.Name} · {Visit.Patient.Age}{Visit.Patient.Gender.ToString()[0]} · {Visit.Doctor.Name}";
        Complaint = Visit.Complaint ?? "";
        Diagnosis = Visit.Diagnosis ?? "";
        Notes = Visit.Notes ?? "";
        Weight = Visit.WeightKg?.ToString("0.#") ?? "";
        BloodPressure = Visit.BloodPressure ?? "";
        Temperature = Visit.TemperatureF?.ToString("0.#") ?? "";
        Height = Visit.HeightCm?.ToString("0.#") ?? "";
        HeartRate = Visit.HeartRateBpm?.ToString() ?? "";
        Spo2 = Visit.Spo2Percent?.ToString() ?? "";
        Fee = Visit.Fee;
        FollowUpOn = Visit.FollowUpOn;

        Products.Clear();
        foreach (var p in await _pharmacy.SearchProductsAsync(null, 500)) Products.Add(p);

        DiagnosticsEnabled = (await _settings.GetGeneralAsync()).DiagnosticsEnabled;

        _testCatalogue = DiagnosticsEnabled ? await _diagnostics.SearchTestsAsync(null, activeOnly: true) : [];

        RequestedTests.Clear();
        foreach (var req in Visit.DiagnosticRequests)
            RequestedTests.Add(new DiagnosticRequestRow { TestId = req.TestId, TestName = req.TestName });

        Lines.Clear();
        foreach (var item in Visit.Prescription)
        {
            var product = item.ProductId is { } id ? Products.FirstOrDefault(p => p.Id == id) : null;

            Lines.Add(new PrescriptionRow
            {
                Medicine = item.MedicineName,
                Dosage = item.Dosage ?? "",
                Frequency = item.Frequency ?? "",
                Days = item.Days,
                Quantity = item.Quantity,
                Instructions = item.Instructions,
                ProductId = item.ProductId,
                UnitsPerPack = product?.UnitsPerPack ?? 1,
                PackLabel = product?.PackSize
            });
        }

        RecalculateCourse();
        _savedSnapshot = Snapshot();

        log.Ok($"{Visit.VisitNo} '{Visit.Patient.Name}' rx={Lines.Count} catalogue={Products.Count}");
    }

    // Recompute the course whenever anything it depends on changes.
    partial void OnNewDaysChanged(int value) => RecalculateCourse();
    partial void OnNewMedicineChanged(Product? value) => RecalculateCourse();

    /// <summary>
    /// Filters the catalogue as the doctor types. The whole list is already in
    /// memory, so this costs nothing and needs no database round trip.
    ///
    /// Typing something we do not stock is allowed on purpose — the parent buys
    /// it outside, and it must not be added to our own medicine records.
    /// </summary>
    partial void OnMedicineSearchChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) MedicineMissing = false;

        // Typing on after choosing one means they are choosing something else.
        if (NewMedicine is not null &&
            !string.Equals(NewMedicine.Name, value, StringComparison.OrdinalIgnoreCase))
        {
            NewMedicine = null;
        }

        Matches.Clear();

        var term = value?.Trim() ?? "";

        if (term.Length >= 2 && NewMedicine is null)
        {
            foreach (var product in Products
                         .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                                  || (p.Manufacturer ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                         .Take(8))
            {
                Matches.Add(product);
            }
        }

        UpdateMedicineHint();
        RecalculateCourse();
    }

    private void UpdateMedicineHint()
    {
        if (NewMedicine is not null)
        {
            var stock = NewMedicine.StockOnHand;
            MedicineHint = stock > 0
                ? $"In our pharmacy · {stock} in stock"
                : "In our pharmacy · out of stock";
            return;
        }

        MedicineHint = string.IsNullOrWhiteSpace(MedicineSearch)
            ? ""
            : Matches.Count > 0
                ? "Pick one from the list, or keep typing for a medicine we do not stock."
                : "Not in our pharmacy — it will be written on the prescription only.";
    }

    /// <summary>Chooses a catalogue medicine, linking the line to our stock.</summary>
    [RelayCommand]
    private void PickMedicine(Product? product)
    {
        if (product is null) return;

        NewMedicine = product;
        MedicineSearch = product.Name;
        Matches.Clear();

        UpdateMedicineHint();
        RecalculateCourse();
    }

    /// <summary>
    /// Works out how many individual units the course needs — "1-0-1" for 3 days
    /// is 6 tablets — and says what that is in strips, because the pharmacy
    /// dispenses from strips.
    /// </summary>
    private void RecalculateCourse()
    {
        var units = DoseMath.UnitsForCourse(NewFrequency, NewDays);

        if (units is null)
        {
            // Nothing chosen yet is not the same as an as-needed dose.
            var nothingChosen = MorningDose == "0" && AfternoonDose == "0" && NightDose == "0";

            CourseHint = nothingChosen
                ? ""
                : $"'{NewFrequency}' for {NewDays} day(s) — enter the quantity yourself.";
            return;
        }

        NewQuantity = units.Value;

        var perPack = NewMedicine?.UnitsPerPack ?? 1;
        CourseHint = perPack > 1
            ? $"{units} units · {PackMath.Describe(units.Value, perPack, NewMedicine?.PackSize)}"
            : $"{units} units";
    }

    [RelayCommand]
    private void AddLine()
    {
        // Either a catalogue medicine, or whatever was typed. A typed name is
        // written on the prescription and nowhere else — it never becomes a
        // medicine in our pharmacy.
        var name = NewMedicine?.Name ?? MedicineSearch;

        if (string.IsNullOrWhiteSpace(name))
        {
            MedicineMissing = true;
            Status = "Choose a medicine, or type its name.";
            return;
        }

        MedicineMissing = false;

        if (NewQuantity <= 0)
        {
            QuantityMissing = true;
            Status = "Enter how many units to dispense.";
            return;
        }

        QuantityMissing = false;

        Lines.Add(new PrescriptionRow
        {
            Medicine = name.Trim(),
            Dosage = NewDosage,
            Frequency = NewFrequency,
            Days = NewDays,
            Quantity = NewQuantity,
            Instructions = string.IsNullOrWhiteSpace(NewInstructions) ? null : NewInstructions.Trim(),
            ProductId = NewMedicine?.Id,
            UnitsPerPack = NewMedicine?.UnitsPerPack ?? 1,
            PackLabel = NewMedicine?.PackSize
        });

        Status = NewMedicine is null
            ? $"{name} added — not stocked here, so the parent buys it outside."
            : $"{name} added.";

        // Emptied in full, dose and days included — a dose left behind from
        // the last medicine reads as chosen for this one, and a wrong dose
        // that nobody typed is worse than retyping a right one.
        NewMedicine = null;
        MedicineSearch = "";
        NewDosage = "";
        NewInstructions = "";
        MorningDose = AfternoonDose = NightDose = "0";
        NewDays = 0;
        NewQuantity = 0;

        Matches.Clear();
        UpdateMedicineHint();
        RecalculateCourse();
    }

    /// <summary>Empties the entry row. Lines already added to the
    /// prescription are untouched.</summary>
    [RelayCommand]
    private void ClearLine()
    {
        NewMedicine = null;
        MedicineSearch = "";
        NewDosage = "";
        NewInstructions = "";
        MorningDose = AfternoonDose = NightDose = "0";
        NewDays = 0;
        NewQuantity = 0;

        Matches.Clear();
        UpdateMedicineHint();
        RecalculateCourse();

        Status = "";
    }

    [RelayCommand]
    private void RemoveLine(PrescriptionRow? row)
    {
        if (row is not null) Lines.Remove(row);
    }

    [RelayCommand]
    private Task SaveAsync() => PersistAsync(complete: false, "Consultation saved.");

    [RelayCommand]
    private async Task CompleteAsync()
    {
        await PersistAsync(complete: true, "Consultation completed.");
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Leaves the consultation. Anything typed but not saved is worth one
    /// question — a half-entered prescription is not recoverable.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        if (HasUnsavedWork())
        {
            var answer = Dialog.Show(
                "This consultation has changes that have not been saved.\n\nClose it and lose them?",
                "Consultation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;
        }

        RequestClose?.Invoke();
    }

    /// <summary>
    /// Compares the form against how it looked when it was last read or written.
    /// Cheaper and harder to get wrong than a dirty flag on every field.
    /// </summary>
    private bool HasUnsavedWork() => Snapshot() != _savedSnapshot;

    private string Snapshot()
    {
        var lines = string.Join("|", Lines.Select(
            l => $"{l.Medicine}~{l.Dosage}~{l.Frequency}~{l.Days}~{l.Quantity}~{l.Instructions}"));

        var tests = string.Join("|", RequestedTests.Select(r => r.TestName));

        return string.Join("~", Complaint, Diagnosis, Notes, Weight, BloodPressure, Temperature,
                           Height, HeartRate, Spo2, Fee, FollowUpOn, lines, tests);
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        await PersistAsync(complete: false, null);

        var visit = await _opd.GetVisitAsync(_visitId);
        if (visit is null) return;

        var clinic = await _settings.GetClinicAsync();
        var theme = await _settings.GetDocumentThemeAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(visit, clinic, theme), $"Prescription {visit.VisitNo}");
    }

    private async Task PersistAsync(bool complete, string? message)
    {
        using var log = AppLog.Enter(
            "Consultation.Save",
            $"visit={_visitId} complete={complete} lines={Lines.Count}");

        if (Visit is null)
        {
            log.Skip("no visit loaded");
            return;
        }

        Visit.Complaint = Trim(Complaint);
        Visit.Diagnosis = Trim(Diagnosis);
        Visit.Notes = Trim(Notes);
        Visit.WeightKg = ParseDecimal(Weight);
        Visit.BloodPressure = Trim(BloodPressure);
        Visit.TemperatureF = ParseDecimal(Temperature);
        Visit.HeightCm = ParseDecimal(Height);
        Visit.HeartRateBpm = ParseInt(HeartRate);
        Visit.Spo2Percent = ParseInt(Spo2);
        Visit.Fee = Fee;
        Visit.FollowUpOn = FollowUpOn;

        var items = Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Medicine))
            .Select(l => new PrescriptionItem
            {
                ProductId = l.ProductId,
                MedicineName = l.Medicine.Trim(),
                Dosage = Trim(l.Dosage),
                Frequency = Trim(l.Frequency),
                Days = l.Days,
                Quantity = l.Quantity,
                Instructions = Trim(l.Instructions)
            })
            .ToList();

        var tests = RequestedTests
            .Where(r => !string.IsNullOrWhiteSpace(r.TestName))
            .Select(r => new VisitDiagnosticRequest { TestId = r.TestId, TestName = r.TestName.Trim() })
            .ToList();

        try
        {
            await _opd.SaveConsultationAsync(Visit, items, tests, complete);
            _savedSnapshot = Snapshot();
            if (message is not null) Status = message;

            log.Ok($"{Visit.VisitNo} {items.Count} prescribed line(s), {tests.Count} test(s) requested");
        }
        catch (Exception ex)
        {
            log.Skip($"failed: {ex.GetType().Name}: {ex.Message}");
            AppLog.Error("Saving the consultation failed.", ex);

            Dialog.Show(ex.Message, "Consultation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ParseDecimal(string? value)
        => decimal.TryParse(value, out var d) ? d : null;

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var i) ? i : null;
}
