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
    [ObservableProperty] private Product? _newMedicine;
    [ObservableProperty] private string _newMedicineText = "";
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
    partial void OnNewMedicineChanged(Product? value)
    {
        if (value is not null) NewMedicineText = value.Name;
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
        var name = NewMedicine?.Name ?? NewMedicineText;

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

        Status = $"{name} added.";

        // Keep the frequency and days: a course is usually repeated across a
        // prescription, and retyping them for every line is the slow part.
        NewMedicine = null;
        NewMedicineText = "";
        NewInstructions = "";
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
