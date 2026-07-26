using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// OPD desk: today's queue on the left, a three-step booking panel on the right.
/// Find patient → choose doctor → Book.
/// </summary>
public partial class OpdViewModel(OpdService opd) : ObservableObject, IPage
{
    public string Title => "OPD";
    public string Subtitle => $"{Visits.Count(v => v.Status != VisitStatus.Cancelled)} visits on {Date:ddd, dd MMM}";

    public ObservableCollection<Visit> Visits { get; } = [];
    public ObservableCollection<Patient> Matches { get; } = [];
    public ObservableCollection<Doctor> Doctors { get; } = [];

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private Visit? _selectedVisit;

    // Step 1 — find or add the patient
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private bool _addingPatient;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newPhone = "";
    [ObservableProperty] private string _newAge = "";
    [ObservableProperty] private Gender _newGender = Gender.Male;

    // Step 2 — doctor and slot
    [ObservableProperty] private Doctor? _selectedDoctor;
    [ObservableProperty] private string _time = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _complaint = "";
    [ObservableProperty] private decimal _fee;

    [ObservableProperty] private string _status = "";

    public Array Genders => Enum.GetValues<Gender>();

    public async Task LoadAsync()
    {
        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);
        SelectedDoctor ??= Doctors.FirstOrDefault();

        await RefreshAsync();
        await FindAsync();
    }

    partial void OnDateChanged(DateTime value) => RefreshAsync().Forget("Refreshing the OPD queue");

    partial void OnSelectedDoctorChanged(Doctor? value)
    {
        if (value is not null && Fee == 0) Fee = value.ConsultationFee;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Visits.Clear();
        foreach (var v in await opd.GetVisitsAsync(Date)) Visits.Add(v);
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        Matches.Clear();
        foreach (var p in await opd.SearchPatientsAsync(Search, 25)) Matches.Add(p);

        if (Matches.Count == 0 && !string.IsNullOrWhiteSpace(Search))
        {
            AddingPatient = true;
            // Typing a phone number into search is the common case, so pre-fill it.
            if (Search.All(char.IsDigit) && Search.Length >= 6) NewPhone = Search;
            else NewName = Search;
        }
    }

    [RelayCommand]
    private void StartNewPatient()
    {
        AddingPatient = true;
        SelectedPatient = null;
    }

    [RelayCommand]
    private void CancelNewPatient()
    {
        AddingPatient = false;
        NewName = NewPhone = NewAge = "";
    }

    [RelayCommand]
    private async Task BookAsync()
    {
        try
        {
            var patient = SelectedPatient;

            if (AddingPatient || patient is null)
            {
                if (string.IsNullOrWhiteSpace(NewName))
                {
                    Warn("Enter the patient's name, or pick an existing patient from the list.");
                    return;
                }

                int.TryParse(NewAge, out var age);
                patient = await opd.SavePatientAsync(new Patient
                {
                    Name = NewName.Trim(),
                    Phone = NewPhone.Trim(),
                    Age = age,
                    Gender = NewGender
                });
            }

            if (SelectedDoctor is null)
            {
                Warn("Add a doctor under Settings before booking a visit.");
                return;
            }

            var scheduled = Date.Date;
            if (TimeSpan.TryParse(Time, out var t)) scheduled = scheduled.Add(t);

            var visit = await opd.BookVisitAsync(
                patient.Id, SelectedDoctor.Id, scheduled,
                string.IsNullOrWhiteSpace(Complaint) ? null : Complaint.Trim(),
                Fee);

            Status = $"Token {visit.TokenNo} booked for {patient.Name}.";
            ResetBookingPanel();
            await RefreshAsync();
            await FindAsync();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    private void ResetBookingPanel()
    {
        AddingPatient = false;
        Search = NewName = NewPhone = NewAge = Complaint = "";
        SelectedPatient = null;
        Fee = SelectedDoctor?.ConsultationFee ?? 0;
        Time = DateTime.Now.ToString("HH:mm");
    }

    [RelayCommand]
    private async Task ArrivedAsync()
    {
        if (SelectedVisit is null) return;
        await opd.SetStatusAsync(SelectedVisit.Id, VisitStatus.Waiting);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CollectFeeAsync()
    {
        if (SelectedVisit is null) return;
        await opd.CollectFeeAsync(SelectedVisit.Id);
        Status = $"Consultation fee received for token {SelectedVisit.TokenNo}.";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task CancelVisitAsync()
    {
        if (SelectedVisit is null) return;

        var confirm = MessageBox.Show(
            $"Cancel token {SelectedVisit.TokenNo} for {SelectedVisit.Patient.Name}?",
            "Cancel visit", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        await opd.SetStatusAsync(SelectedVisit.Id, VisitStatus.Cancelled);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ConsultAsync()
    {
        if (SelectedVisit is null) return;

        await opd.SetStatusAsync(SelectedVisit.Id, VisitStatus.InConsultation);

        var window = new Views.ConsultationWindow(SelectedVisit.Id)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();

        await RefreshAsync();
    }

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "OPD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
