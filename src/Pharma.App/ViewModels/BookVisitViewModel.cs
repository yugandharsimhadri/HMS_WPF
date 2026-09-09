using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Booking one visit, over the shell.
///
/// Three steps in order: find who is here, pick the doctor and time, say what
/// is wrong. It was a 330px panel beside the queue, which meant scrolling to
/// reach Book visit on the screens this runs on — and a form you scroll to
/// submit is one people give up on halfway.
///
/// A new instance per booking, so nothing survives to the next one. That
/// matters most for the patient: a visit booked against whoever was still
/// selected sends the wrong child in to the doctor.
/// </summary>
public partial class BookVisitViewModel : ObservableObject
{
    private readonly OpdService _opd;
    private readonly DateTime _date;

    public BookVisitViewModel(OpdService opd, IEnumerable<Doctor> doctors, Doctor? preferred, DateTime date)
    {
        _opd = opd;
        _date = date;

        foreach (var d in doctors) Doctors.Add(d);

        // Booking from a doctor's tab should default to that doctor.
        SelectedDoctor = preferred ?? Doctors.FirstOrDefault();
        Fee = SelectedDoctor?.ConsultationFee ?? 0;
    }

    public string Header => $"Book a visit — {_date:ddd, dd MMM}";

    public ObservableCollection<Patient> Matches { get; } = [];
    public ObservableCollection<Doctor> Doctors { get; } = [];

    public Array Genders => Enum.GetValues<Gender>();

    public event Action? RequestClose;

    /// <summary>What the queue should say. Null when nothing was booked.</summary>
    public string? Outcome { get; private set; }

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
    [ObservableProperty] private string _status = "";

    /// <summary>Set when a booking was turned away for want of a patient name.</summary>
    [ObservableProperty] private bool _newNameMissing;

    partial void OnNewNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NewNameMissing = false;
    }

    /// <summary>
    /// The earlier paid visit that still covers this patient with this doctor,
    /// when the doctor runs an OPD validity window and the patient is inside it.
    /// Null the rest of the time, which is every booking until a doctor turns
    /// the scheme on in Settings.
    /// </summary>
    [ObservableProperty] private Visit? _feeCover;

    public bool HasFeeCover => FeeCover is not null;

    /// <summary>
    /// Why this booking is free, in the terms the desk would use to explain it
    /// to the person standing in front of them.
    /// </summary>
    public string FeeCoverNote => FeeCover is not { } cover
        ? ""
        : $"Review — ₹{cover.Fee:0} paid on {cover.FeePaidOn:dd MMM}" +
          (string.IsNullOrWhiteSpace(cover.FeeReceiptNo) ? "" : $" (receipt {cover.FeeReceiptNo})") +
          $", free with this doctor until {cover.FreeFollowUpUntil:dd MMM yyyy}.";

    partial void OnFeeCoverChanged(Visit? value)
    {
        OnPropertyChanged(nameof(HasFeeCover));
        OnPropertyChanged(nameof(FeeCoverNote));
    }

    partial void OnSelectedPatientChanged(Patient? value) => RefreshFeeCover();

    partial void OnSelectedDoctorChanged(Doctor? value)
    {
        if (value is not null && Fee == 0 && !HasFeeCover) Fee = value.ConsultationFee;
        RefreshFeeCover();
    }

    private void RefreshFeeCover() => RefreshFeeCoverAsync().Forget("Checking the OPD validity window");

    /// <summary>
    /// Asks whether this patient has already paid this doctor inside a live
    /// window, and if so drops the fee to zero.
    ///
    /// A default, not a lock. The desk can type a fee back in — a return inside
    /// the window but for a genuinely new complaint is usually chargeable, and
    /// that is a judgement no rule here can make. Whether the visit is recorded
    /// as a free review is decided by what the fee box actually says when Book
    /// is pressed, not by what this suggested.
    /// </summary>
    private async Task RefreshFeeCoverAsync()
    {
        var patient = SelectedPatient;
        var doctor = SelectedDoctor;

        if (patient is null || doctor is null || doctor.OpdValidDays <= 0)
        {
            FeeCover = null;
            return;
        }

        var cover = await _opd.FindFeeCoverAsync(patient.Id, doctor.Id, _date);
        FeeCover = cover;

        if (cover is not null)
        {
            Fee = 0;
            Status = FeeCoverNote;
        }
        else if (Fee == 0)
        {
            // The window closed, or a different doctor was picked — put the
            // doctor's own fee back rather than leaving a zero nobody chose.
            Fee = doctor.ConsultationFee;
        }
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        Matches.Clear();
        foreach (var p in await _opd.SearchPatientsAsync(Search, 25)) Matches.Add(p);

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
        using var log = AppLog.Enter(
            "Opd.Book",
            $"patient={SelectedPatient?.Id} new={AddingPatient} doctor={SelectedDoctor?.Id} " +
            $"date={_date:yyyy-MM-dd} time={Time} fee={Fee}");

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
                log.Skip("ambiguous patient match; none selected");
                return;
            }

            if (AddingPatient || patient is null)
            {
                if (string.IsNullOrWhiteSpace(NewName))
                {
                    NewNameMissing = true;
                    Warn("Enter the patient's name, or pick an existing patient from the list.");
                    log.Skip("new patient name missing");
                    return;
                }

                NewNameMissing = false;

                int.TryParse(NewAge, out var age);
                patient = await _opd.SavePatientAsync(new Patient
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
                log.Skip("no doctor selected");
                return;
            }

            var scheduled = _date.Date;
            if (TimeSpan.TryParse(Time, out var t)) scheduled = scheduled.Add(t);

            // Re-checked here rather than trusted from the banner: the patient
            // may have been picked before the doctor, or changed after it, and
            // an overridden fee means the desk decided to charge after all.
            var cover = SelectedDoctor.OpdValidDays > 0 && Fee <= 0
                ? await _opd.FindFeeCoverAsync(patient.Id, SelectedDoctor.Id, scheduled)
                : null;

            var visit = await _opd.BookVisitAsync(
                patient.Id, SelectedDoctor.Id, scheduled,
                string.IsNullOrWhiteSpace(Complaint) ? null : Complaint.Trim(),
                Fee, feeWaivedAgainstVisitId: cover?.Id);

            Outcome = cover is null
                ? $"Token {visit.TokenNo} booked for {patient.Name}."
                : $"Token {visit.TokenNo} booked for {patient.Name} — review, no fee due.";
            log.Ok($"token={visit.TokenNo} patient={patient.Name}");
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            log.Skip($"refused: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Empties the form without closing it. Nothing is booked until Book visit
    /// is pressed, so this loses only what was typed.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        AddingPatient = false;
        NewNameMissing = false;
        Search = NewName = NewPhone = NewAge = Complaint = "";
        SelectedPatient = null;
        Matches.Clear();
        Fee = SelectedDoctor?.ConsultationFee ?? 0;
        Time = DateTime.Now.ToString("HH:mm");
        Status = "";
    }

    /// <summary>Closes without booking anything.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "OPD", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
