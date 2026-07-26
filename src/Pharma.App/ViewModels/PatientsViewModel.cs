using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Patient register: search, edit, and the visit history behind each patient.
/// Booking still happens on the OPD screen — this is the record, not the queue.
/// </summary>
public partial class PatientsViewModel(OpdService opd) : ObservableObject, IPage
{
    public string Title => "Patients";
    public string Subtitle => $"{Patients.Count} patient(s) listed";

    public ObservableCollection<Patient> Patients { get; } = [];
    public ObservableCollection<Visit> History { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Patient? _selectedPatient;

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
    }

    [RelayCommand]
    private void NewPatient()
    {
        SelectedPatient = null;
        PatientNo = "(assigned on save)";
        Name = Phone = Age = Address = Allergies = "";
        Gender = Gender.Male;
        History.Clear();
        Status = "Enter the patient's details and save.";
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
            Status = $"{saved.Name} saved as {saved.PatientNo}.";

            Search = saved.Name;
            await FindAsync();
            SelectedPatient = Patients.FirstOrDefault(p => p.Id == saved.Id);
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

        var confirm = MessageBox.Show(
            $"Remove {SelectedPatient.Name} from the register?",
            "Patients", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var error = await opd.DeletePatientAsync(SelectedPatient.Id);
        if (error is not null)
        {
            Warn(error);
            return;
        }

        Status = "Patient removed.";
        SelectedPatient = null;
        await FindAsync();
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "Patients", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
