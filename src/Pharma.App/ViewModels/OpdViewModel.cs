using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// The OPD desk. One tab per doctor, waiting and completed side by side as tiles,
/// and a tile that moves from waiting to completed when the consultation is done.
///
/// The doctor is a tab rather than a column because repeating it on every row was
/// the bulk of the duplication on the old screen.
/// </summary>
public partial class OpdViewModel(OpdService opd, SettingsService settings) : ObservableObject, IPage
{
    public string Title => "OPD";
    public string Subtitle => $"{Waiting.Count} waiting · {Completed.Count} completed · {Date:ddd, dd MMM}";

    public ObservableCollection<Visit> Waiting { get; } = [];
    public ObservableCollection<Visit> Completed { get; } = [];
    public ObservableCollection<Patient> Matches { get; } = [];
    public ObservableCollection<Doctor> Doctors { get; } = [];

    /// <summary>Null means every doctor — the "All" tab.</summary>
    [ObservableProperty] private Doctor? _doctorTab;

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _status = "";

    /// <summary>Chosen in Settings; re-read every time the screen is opened.</summary>
    [ObservableProperty] private bool _useTiles = true;
    [ObservableProperty] private PaymentMode _feePaymentMode = PaymentMode.Cash;

    // Booking is a panel now, not a permanent third of the screen.
    [ObservableProperty] private bool _isBooking;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Patient? _selectedPatient;
    [ObservableProperty] private bool _addingPatient;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newPhone = "";
    [ObservableProperty] private string _newAge = "";
    [ObservableProperty] private Gender _newGender = Gender.Male;
    [ObservableProperty] private Doctor? _selectedDoctor;
    [ObservableProperty] private string _time = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _complaint = "";
    [ObservableProperty] private decimal _fee;

    private readonly List<Visit> _all = [];

    public Array Genders => Enum.GetValues<Gender>();
    public Array PaymentModes => Enum.GetValues<PaymentMode>();

    public async Task LoadAsync()
    {
        UseTiles = (await settings.GetAsync()).QueueLayout == QueueLayout.Tiles;

        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);
        SelectedDoctor ??= Doctors.FirstOrDefault();

        await RefreshAsync();
    }

    partial void OnDateChanged(DateTime value) => RefreshAsync().Forget("Refreshing the OPD queue");

    partial void OnDoctorTabChanged(Doctor? value) => Regroup();

    partial void OnSelectedDoctorChanged(Doctor? value)
    {
        if (value is not null && Fee == 0) Fee = value.ConsultationFee;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _all.Clear();
        _all.AddRange(await opd.GetVisitsAsync(Date));
        Regroup();
    }

    /// <summary>Splits the day's visits into the two columns for the chosen doctor.</summary>
    private void Regroup()
    {
        Waiting.Clear();
        Completed.Clear();

        foreach (var visit in _all.Where(v => DoctorTab is null || v.DoctorId == DoctorTab.Id))
        {
            if (visit.IsWaiting) Waiting.Add(visit);
            else if (visit.Status == VisitStatus.Completed) Completed.Add(visit);
        }

        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void ShowAllDoctors() => DoctorTab = null;

    [RelayCommand]
    private void SelectDoctorTab(Doctor? doctor) => DoctorTab = doctor;

    // ── Booking ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void NewVisit()
    {
        IsBooking = true;
        Status = "";
        Fee = SelectedDoctor?.ConsultationFee ?? 0;

        // Booking from a doctor's tab should default to that doctor.
        if (DoctorTab is not null) SelectedDoctor = DoctorTab;
    }

    [RelayCommand]
    private void CloseBooking()
    {
        IsBooking = false;
        ResetBookingPanel();
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        Matches.Clear();
        foreach (var p in await opd.SearchPatientsAsync(Search, 25)) Matches.Add(p);

        if (Matches.Count == 0 && !string.IsNullOrWhiteSpace(Search))
        {
            AddingPatient = true;

            if (OpdService.LooksLikePhone(Search)) NewPhone = Search.Trim();
            else NewName = Search.Trim();

            Status = $"No one matches '{Search}'. Add them as a new patient.";
            return;
        }

        AddingPatient = false;

        // One number, several children — say so, because the operator has to pick.
        Status = Matches.Count switch
        {
            0 => "",
            1 => $"{Matches[0].Name} found. Select and book.",
            _ when OpdService.LooksLikePhone(Search) =>
                $"{Matches.Count} people are registered on this number. Select which one is here.",
            _ => $"{Matches.Count} matches. Select which one is here."
        };
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

            // Siblings share a phone number, so a search can return several people.
            // Creating a new record because none was picked would quietly duplicate
            // a child who is already registered.
            if (patient is null && !AddingPatient && Matches.Count > 0)
            {
                Warn(Matches.Count == 1
                    ? $"Select {Matches[0].Name} from the list, or choose + New patient."
                    : $"{Matches.Count} people match that. Select which one, or choose + New patient.");
                return;
            }

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

            ResetBookingPanel();
            IsBooking = false;
            await RefreshAsync();

            // Set last: refreshing writes its own status, and the confirmation is
            // what the operator needs left on screen.
            Status = $"Token {visit.TokenNo} booked for {patient.Name}.";
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
        Matches.Clear();
        Fee = SelectedDoctor?.ConsultationFee ?? 0;
        Time = DateTime.Now.ToString("HH:mm");
    }

    // ── Tile actions ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ArrivedAsync(Visit? visit)
    {
        if (visit is null) return;

        await Safely.RunAsync(async () =>
        {
            await opd.SetStatusAsync(visit.Id, VisitStatus.Waiting);
            await RefreshAsync();
        }, "Marking arrived", m => Status = m);
    }

    /// <summary>Takes the fee, issues a numbered receipt, and offers to print it.</summary>
    [RelayCommand]
    private async Task CollectFeeAsync(Visit? visit)
    {
        if (visit is null) return;

        if (visit.FeePaid)
        {
            Status = $"Token {visit.TokenNo} has already paid — use the receipt button to reprint.";
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var paid = await opd.CollectFeeAsync(visit.Id, FeePaymentMode);
            if (paid is null) return;

            Status = $"Receipt {paid.FeeReceiptNo} — ₹{paid.Fee:0.00} received from {paid.Patient.Name}.";
            await RefreshAsync();

            var shop = await settings.GetAsync();
            PrintService.Preview(() => FeeReceiptDocument.Build(paid, shop), $"Receipt {paid.FeeReceiptNo}");
        }, "Taking the fee", m => Status = m);
    }

    /// <summary>Moves the tile out of waiting and into completed.</summary>
    [RelayCommand]
    private async Task CompleteAsync(Visit? visit)
    {
        if (visit is null) return;

        await Safely.RunAsync(async () =>
        {
            await opd.SetStatusAsync(visit.Id, VisitStatus.Completed);
            await RefreshAsync();
            Status = $"Token {visit.TokenNo} moved to completed.";
        }, "Moving to completed", m => Status = m);
    }

    [RelayCommand]
    private async Task ReopenAsync(Visit? visit)
    {
        if (visit is null) return;

        await Safely.RunAsync(async () =>
        {
            await opd.SetStatusAsync(visit.Id, VisitStatus.Waiting);
            await RefreshAsync();
            Status = $"Token {visit.TokenNo} moved back to waiting.";
        }, "Moving back to waiting", m => Status = m);
    }

    [RelayCommand]
    private async Task CancelVisitAsync(Visit? visit)
    {
        if (visit is null) return;

        var confirm = MessageBox.Show(
            $"Cancel token {visit.TokenNo} for {visit.Patient.Name}?",
            "Cancel visit", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        await opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ConsultAsync(Visit? visit)
    {
        if (visit is null) return;

        await Safely.RunAsync(async () =>
        {
            await opd.SetStatusAsync(visit.Id, VisitStatus.InConsultation);

            var window = new Views.ConsultationWindow(visit.Id)
            {
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();

            // Completing the consultation sets the status, so the tile lands in
            // the other column as soon as this refresh runs.
            await RefreshAsync();
        }, "Opening the consultation", m => Status = m);
    }

    [RelayCommand]
    private async Task PrintReceiptAsync(Visit? visit)
    {
        if (visit is null) return;

        if (!visit.FeePaid)
        {
            Status = $"Token {visit.TokenNo} has not paid yet — there is no receipt to print.";
            return;
        }

        var full = await opd.GetVisitAsync(visit.Id);
        if (full is null) return;

        var shop = await settings.GetAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(full, shop, isReprint: true),
                             $"Receipt {full.FeeReceiptNo} (duplicate)");
    }

    [RelayCommand]
    private async Task PrintPrescriptionAsync(Visit? visit)
    {
        if (visit is null) return;

        var full = await opd.GetVisitAsync(visit.Id);
        if (full is null) return;

        if (full.Prescription.Count == 0)
        {
            Status = $"Token {full.TokenNo} has no prescription yet.";
            return;
        }

        var shop = await settings.GetAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(full, shop), $"Prescription {full.VisitNo}");
    }

    private void Warn(string message)
    {
        Status = message;
        MessageBox.Show(message, "OPD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
