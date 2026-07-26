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

public partial class ConsultationViewModel : ObservableObject
{
    private readonly OpdService _opd;
    private readonly PharmacyService _pharmacy;
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
    [ObservableProperty] private decimal _fee;
    [ObservableProperty] private DateTime? _followUpOn;
    [ObservableProperty] private string _status = "";

    // The entry row. Filling a form and pressing Add is far easier than editing
    // cells in a grid, which needs a click to start and swallows the Tab key.

    /// <summary>Matches for what has been typed. Empty once one is chosen.</summary>
    public ObservableCollection<Product> Matches { get; } = [];

    /// <summary>What was typed. Also the medicine name when nothing is chosen.</summary>
    [ObservableProperty] private string _medicineSearch = "";

    /// <summary>The catalogue medicine chosen, or null for one we do not stock.</summary>
    [ObservableProperty] private Product? _newMedicine;

    [ObservableProperty] private string _medicineHint = "";
    [ObservableProperty] private string _newDosage = "1 tab";
    [ObservableProperty] private string _newFrequency = "1-0-1";
    [ObservableProperty] private int _newDays = 3;
    [ObservableProperty] private int _newQuantity;
    [ObservableProperty] private string _newInstructions = "";
    [ObservableProperty] private string _courseHint = "";

    public event Action? RequestClose;

    public ConsultationViewModel(Guid visitId, OpdService opd, PharmacyService pharmacy, SettingsService settings)
    {
        _visitId = visitId;
        _opd = opd;
        _pharmacy = pharmacy;
        _settings = settings;
    }

    public async Task LoadAsync()
    {
        Visit = await _opd.GetVisitAsync(_visitId);
        if (Visit is null) return;

        Header = $"Token {Visit.TokenNo} · {Visit.Patient.Name} · {Visit.Patient.Age}{Visit.Patient.Gender.ToString()[0]} · {Visit.Doctor.Name}";
        Complaint = Visit.Complaint ?? "";
        Diagnosis = Visit.Diagnosis ?? "";
        Notes = Visit.Notes ?? "";
        Weight = Visit.WeightKg?.ToString("0.#") ?? "";
        BloodPressure = Visit.BloodPressure ?? "";
        Temperature = Visit.TemperatureF?.ToString("0.#") ?? "";
        Fee = Visit.Fee;
        FollowUpOn = Visit.FollowUpOn;

        Products.Clear();
        foreach (var p in await _pharmacy.SearchProductsAsync(null, 500)) Products.Add(p);

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
    }

    // Recompute the course whenever anything it depends on changes.
    partial void OnNewFrequencyChanged(string value) => RecalculateCourse();
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
            CourseHint = string.IsNullOrWhiteSpace(NewFrequency)
                ? ""
                : $"'{NewFrequency}' has no fixed daily dose — enter the quantity yourself.";
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
            Status = "Choose a medicine, or type its name.";
            return;
        }

        if (NewQuantity <= 0)
        {
            Status = "Enter how many units to dispense.";
            return;
        }

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

        // Keep the frequency and days: a course is usually repeated across a
        // prescription, and retyping them for every line is the slow part.
        NewMedicine = null;
        MedicineSearch = "";
        NewInstructions = "";
        Matches.Clear();
        UpdateMedicineHint();
        RecalculateCourse();
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

    [RelayCommand]
    private async Task PrintAsync()
    {
        await PersistAsync(complete: false, null);

        var visit = await _opd.GetVisitAsync(_visitId);
        if (visit is null) return;

        var shop = await _settings.GetAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(visit, shop), $"Prescription {visit.VisitNo}");
    }

    private async Task PersistAsync(bool complete, string? message)
    {
        if (Visit is null) return;

        Visit.Complaint = Trim(Complaint);
        Visit.Diagnosis = Trim(Diagnosis);
        Visit.Notes = Trim(Notes);
        Visit.WeightKg = ParseDecimal(Weight);
        Visit.BloodPressure = Trim(BloodPressure);
        Visit.TemperatureF = ParseDecimal(Temperature);
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

        try
        {
            await _opd.SaveConsultationAsync(Visit, items, complete);
            if (message is not null) Status = message;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Consultation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ParseDecimal(string? value)
        => decimal.TryParse(value, out var d) ? d : null;
}
