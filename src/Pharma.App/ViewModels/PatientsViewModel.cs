using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Patient register: search, edit, and the visit history behind each patient.
/// Booking still happens on the OPD screen — this is the record, not the queue.
/// </summary>
public partial class PatientsViewModel(OpdService opd, PharmacyService pharmacy, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Patients";
    public string Subtitle => $"{Patients.Count} patient(s) listed";

    public ObservableCollection<Patient> Patients { get; } = [];
    public ObservableCollection<Visit> History { get; } = [];
    public ObservableCollection<Sale> Bills { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private Visit? _selectedVisit;
    [ObservableProperty] private Sale? _selectedBill;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _age = "";
    [ObservableProperty] private Gender _gender = Gender.Male;
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _allergies = "";
    [ObservableProperty] private string _patientNo = "";
    [ObservableProperty] private string _status = "";

    public Array Genders => Enum.GetValues<Gender>();

    public async Task LoadAsync() => await FindAsync();

    [RelayCommand]
    private async Task FindAsync()
    {
        var selectedId = SelectedPatient?.Id;

        Patients.Clear();
        foreach (var p in await opd.SearchPatientsAsync(Search, 200)) Patients.Add(p);

        SelectedPatient = Patients.FirstOrDefault(p => p.Id == selectedId);
        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null)
        {
            History.Clear();
            Bills.Clear();
            return;
        }

        PatientNo = value.PatientNo;
        Name = value.Name;
        Phone = value.Phone;
        Age = value.Age > 0 ? value.Age.ToString() : "";
        Gender = value.Gender;
        Address = value.Address ?? "";
        Allergies = value.Allergies ?? "";

        LoadHistoryAsync(value.Id).Forget("Loading patient history");
    }

    private async Task LoadHistoryAsync(Guid patientId)
    {
        History.Clear();
        foreach (var v in await opd.GetPatientHistoryAsync(patientId)) History.Add(v);

        Bills.Clear();
        foreach (var s in await pharmacy.GetSalesByPatientAsync(patientId)) Bills.Add(s);
    }

    // ── Reprinting, however long ago it was ────────────────────────────────

    /// <summary>Prints the prescription for any past visit, not just today's.</summary>
    [RelayCommand]
    private async Task PrintPrescriptionAsync()
    {
        if (SelectedVisit is null)
        {
            Status = "Pick a visit from the history first.";
            return;
        }

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);
        if (visit is null) return;

        if (visit.Prescription.Count == 0)
        {
            Status = $"Visit {visit.VisitNo} has no prescription recorded.";
            return;
        }

        var shop = await settings.GetAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(visit, shop), $"Prescription {visit.VisitNo}");
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (SelectedVisit is null)
        {
            Status = "Pick a visit from the history first.";
            return;
        }

        if (!SelectedVisit.FeePaid)
        {
            Status = $"No consultation fee was recorded against visit {SelectedVisit.VisitNo}.";
            return;
        }

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);
        if (visit is null) return;

        var shop = await settings.GetAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(visit, shop, isReprint: true),
                             $"Receipt {visit.FeeReceiptNo} (duplicate)");
    }

    [RelayCommand]
    private async Task PrintBillAsync()
    {
        if (SelectedBill is null)
        {
            Status = "Pick a medicine bill first.";
            return;
        }

        var sale = await pharmacy.GetSaleAsync(SelectedBill.Id);
        if (sale is null) return;

        var shop = await settings.GetAsync();
        PrintService.Preview(() => BillPrinter.Build(sale, shop, isReprint: true),
                             $"Bill {sale.BillNo} (duplicate)");
    }

    [RelayCommand]
    private async Task NewPatientAsync()
    {
        await ResetAsync();

        PatientNo = "(assigned on save)";
        Status = "Enter the patient's details and save.";
    }

    /// <summary>
    /// Back to how the screen opens: nothing selected, nothing typed, nothing
    /// left in the search box.
    ///
    /// The search box matters as much as the fields. A screen still filtered to
    /// one patient, with that patient loaded, is a screen where the next name
    /// typed in overwrites them instead of being added as somebody new.
    /// </summary>
    private async Task ResetAsync()
    {
        SelectedPatient = null;
        SelectedVisit = null;
        SelectedBill = null;

        PatientNo = Name = Phone = Age = Address = Allergies = "";
        Gender = Gender.Male;

        History.Clear();
        Bills.Clear();

        Search = "";
        await FindAsync();
    }

    [RelayCommand]
    private async Task SavePatientAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Warn("Patient name is required.");
            return;
        }

        int.TryParse(Age, out var age);

        var patient = SelectedPatient ?? new Patient();
        patient.Name = Name.Trim();
        patient.Phone = Phone.Trim();
        patient.Age = age;
        patient.Gender = Gender;
        patient.Address = Empty(Address);
        patient.Allergies = Empty(Allergies);

        try
        {
            var saved = await opd.SavePatientAsync(patient);

            // Everything clears, the search box included. This used to leave the
            // saved patient selected and the list filtered to their name, which
            // reads as "ready for the next one" but is not: typing the next
            // person's details into that form saved them over this one.
            await ResetAsync();

            // The confirmation still names who was saved, so clearing the screen
            // does not also take away the evidence that the save worked.
            Status = $"{saved.Name} saved as {saved.PatientNo}. The form is clear for the next patient.";
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeletePatientAsync()
    {
        if (SelectedPatient is null) return;

        var removed = SelectedPatient.Name;

        var confirm = MessageBox.Show(
            $"Remove {removed} from the register?",
            "Patients", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var error = await opd.DeletePatientAsync(SelectedPatient.Id);
        if (error is not null)
        {
            Warn(error);
            return;
        }

        // Their details have to go off the form as well. Leaving them there means
        // Save puts the patient straight back, as a second record.
        await ResetAsync();

        Status = $"{removed} removed from the register.";
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Patients", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
