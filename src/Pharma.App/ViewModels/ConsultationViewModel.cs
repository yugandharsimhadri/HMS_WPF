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

        Lines.Clear();
        foreach (var item in Visit.Prescription)
        {
            Lines.Add(new PrescriptionRow
            {
                Medicine = item.MedicineName,
                Dosage = item.Dosage ?? "",
                Frequency = item.Frequency ?? "",
                Days = item.Days,
                Quantity = item.Quantity,
                Instructions = item.Instructions,
                ProductId = item.ProductId
            });
        }

        if (Lines.Count == 0) Lines.Add(new PrescriptionRow());

        Products.Clear();
        foreach (var p in await _pharmacy.SearchProductsAsync(null, 500)) Products.Add(p);
    }

    [RelayCommand]
    private void AddLine() => Lines.Add(new PrescriptionRow());

    [RelayCommand]
    private void RemoveLine(PrescriptionRow? row)
    {
        if (row is not null) Lines.Remove(row);
        if (Lines.Count == 0) Lines.Add(new PrescriptionRow());
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
