using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Generic booking, shared by every clinical module. Three tabs — Today's
/// appointments, Book appointment, Reminders — the same "one screen, a
/// handful of tabs" shape Diagnostics already uses, since each is a handful
/// of fields rather than a destination on its own.
/// </summary>
public partial class AppointmentsViewModel(
    AppointmentsService appointments, OpdService opd, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Appointments";
    public string Subtitle => TodaysAppointments.Count == 0
        ? "Nothing booked for this day"
        : $"{TodaysAppointments.Count} appointment(s)";

    public async Task LoadAsync()
    {
        await LoadEnabledContextsAsync();

        // Match the previous selection back up by Id rather than keeping the
        // old object reference — GetDoctorsAsync returns fresh AsNoTracking
        // instances every call, so on a second visit to this page the old
        // reference is no longer contained in the rebuilt Doctors list at
        // all, and the combo silently shows blank even though SelectedDoctor
        // is technically non-null.
        var previousDoctorId = SelectedDoctor?.Id;
        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);
        SelectedDoctor = Doctors.FirstOrDefault(d => d.Id == previousDoctorId) ?? Doctors.FirstOrDefault();

        await LoadTodayAsync();
        await LoadRemindersAsync();
    }

    /// <summary>
    /// Which of the three tabs is showing — Today's appointments (0), Book
    /// appointment (1), Reminders (2). <see cref="LoadAsync"/> only runs once
    /// per navigation into this page, so without this a booking made on the
    /// Book tab never shows up after switching to Today's appointments or
    /// Reminders without leaving the page and coming back.
    /// </summary>
    [ObservableProperty] private int _selectedTabIndex;

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 0) LoadTodayAsync().Forget("Refreshing today's appointments");
        else if (value == 2) LoadRemindersAsync().Forget("Refreshing reminders");
    }

    // ── Which module contexts can actually be booked against ─────────────
    //
    // Only a module that is switched on offers itself as a booking choice —
    // General only when OPD is on, and so on for the others once each one's
    // own module exists. AppointmentsService.BookAsync enforces the same
    // rule server-side; this is what keeps the picker from ever offering a
    // choice that would just be refused.

    public ObservableCollection<AppointmentModuleContext> EnabledContexts { get; } = [];

    [ObservableProperty] private AppointmentModuleContext _selectedContext = AppointmentModuleContext.General;

    private async Task LoadEnabledContextsAsync()
    {
        var general = await settings.GetGeneralAsync();

        EnabledContexts.Clear();
        if (general.OpdEnabled) EnabledContexts.Add(AppointmentModuleContext.General);
        if (general.PediatricsEnabled) EnabledContexts.Add(AppointmentModuleContext.Pediatrics);
        if (general.DentistEnabled) EnabledContexts.Add(AppointmentModuleContext.Dentist);
        if (general.PathologyLabEnabled) EnabledContexts.Add(AppointmentModuleContext.PathologyLab);

        if (!EnabledContexts.Contains(SelectedContext))
            SelectedContext = EnabledContexts.FirstOrDefault();
    }

    // ── Today's appointments ──────────────────────────────────────────────

    /// <summary>Same shape as <see cref="OpdViewModel.Date"/> — a real
    /// <c>DatePicker</c>, not a typed text field.</summary>
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;

    public ObservableCollection<Appointment> TodaysAppointments { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasSelectedAppointment))]
    [ObservableProperty] private Appointment? _selectedAppointment;

    public bool HasSelectedAppointment => SelectedAppointment is not null;

    [ObservableProperty] private string _cancelReason = "";
    [ObservableProperty] private string _rescheduleDate = "";
    [ObservableProperty] private string _rescheduleTime = "";
    [ObservableProperty] private string _dailyStatus = "";

    partial void OnSelectedDateChanged(DateTime value) => LoadTodayAsync().Forget("Loading the day's appointments");

    private async Task LoadTodayAsync()
    {
        TodaysAppointments.Clear();
        foreach (var a in await appointments.GetForDateAsync(SelectedDate)) TodaysAppointments.Add(a);
        OnPropertyChanged(nameof(Subtitle));
    }

    /// <summary>
    /// Converts a checked-in appointment into whichever module's own working
    /// record it was booked for. Only <see cref="AppointmentModuleContext.General"/>
    /// has a module to route to today — Dentist and Pathology Lab wire their
    /// own case into this same branch once their own services exist, and
    /// Pediatrics either behaves like General (when OPD is also on) or gets
    /// its own branch once it exists.
    /// </summary>
    [RelayCommand]
    private async Task CheckInAsync(Appointment? appointment)
    {
        if (appointment is null) return;

        try
        {
            switch (appointment.ModuleContext)
            {
                case AppointmentModuleContext.General:
                {
                    var doctor = Doctors.FirstOrDefault(d => d.Id == appointment.DoctorId);
                    var visit = await opd.BookVisitAsync(
                        appointment.PatientId, appointment.DoctorId, DateTime.Now,
                        appointment.Reason, doctor?.ConsultationFee ?? 0, appointment.Id);

                    await appointments.MarkCheckedInAsync(appointment.Id, visit.Id);
                    DailyStatus = $"{appointment.PatientName} checked in — token {visit.TokenNo}.";
                    break;
                }

                default:
                    Warn($"Check-in for {appointment.ModuleContext} is not available yet.");
                    return;
            }
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            return;
        }

        await LoadTodayAsync();
    }

    [RelayCommand]
    private async Task CancelAppointmentAsync(Appointment? appointment)
    {
        if (appointment is null) return;

        if (string.IsNullOrWhiteSpace(CancelReason))
        {
            Warn("Say why the appointment is being cancelled.");
            return;
        }

        try
        {
            await appointments.CancelAsync(appointment.Id, CancelReason.Trim());
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            return;
        }

        CancelReason = "";
        DailyStatus = $"{appointment.PatientName}'s appointment cancelled.";
        await LoadTodayAsync();
    }

    [RelayCommand]
    private async Task RescheduleAppointmentAsync(Appointment? appointment)
    {
        if (appointment is null) return;

        if (!DateTime.TryParse(RescheduleDate, out var date))
        {
            Warn("Enter the new date.");
            return;
        }

        var time = TimeSpan.TryParse(RescheduleTime, out var t) ? t : appointment.ScheduledOn.TimeOfDay;

        try
        {
            var moved = await appointments.RescheduleAsync(appointment.Id, date.Date.Add(time));
            DailyStatus = $"{appointment.PatientName} moved to {moved.ScheduledOn:dd MMM yyyy HH:mm} ({moved.AppointmentNo}).";
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
            return;
        }

        RescheduleDate = "";
        RescheduleTime = "";
        await LoadTodayAsync();
    }

    // ── Book appointment ───────────────────────────────────────────────────

    public ObservableCollection<Patient> PatientMatches { get; } = [];
    [ObservableProperty] private string _patientSearch = "";

    [NotifyPropertyChangedFor(nameof(HasSelectedPatient))]
    [ObservableProperty] private Patient? _selectedPatient;

    public bool HasSelectedPatient => SelectedPatient is not null;

    // See DiagnosticsViewModel.OnSelectedPatientChanged for why this goes
    // through the backing field rather than the PatientSearch property.
    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null) return;

        _patientSearch = "";
        OnPropertyChanged(nameof(PatientSearch));
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

    public ObservableCollection<Doctor> Doctors { get; } = [];
    [ObservableProperty] private Doctor? _selectedDoctor;

    [ObservableProperty] private string _bookingDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
    [ObservableProperty] private string _bookingTime = "10:00";
    [ObservableProperty] private int _durationMinutes = 15;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _bookingStatus = "";

    [RelayCommand]
    private Task BookAsync() => CompleteBookingAsync(print: false);

    [RelayCommand]
    private Task BookAndPrintAsync() => CompleteBookingAsync(print: true);

    private async Task CompleteBookingAsync(bool print)
    {
        using var log = AppLog.Enter(
            "Appointments.Book", $"patient={SelectedPatient?.Id} doctor={SelectedDoctor?.Id} context={SelectedContext} print={print}");

        if (SelectedPatient is not { } patient)
        {
            log.Skip("no patient chosen");
            Warn("Select a patient, or add a new one.");
            return;
        }

        if (SelectedDoctor is not { } doctor)
        {
            log.Skip("no doctor chosen");
            Warn("Select a doctor.");
            return;
        }

        if (!DateTime.TryParse(BookingDate, out var date))
        {
            log.Skip("bad date");
            Warn("Enter a valid date.");
            return;
        }

        var time = TimeSpan.TryParse(BookingTime, out var t) ? t : TimeSpan.Zero;

        var appointment = new Appointment
        {
            PatientId = patient.Id, PatientName = patient.Name, PatientPhone = patient.Phone,
            DoctorId = doctor.Id, DoctorName = doctor.Name,
            ScheduledOn = date.Date.Add(time),
            DurationMinutes = DurationMinutes > 0 ? DurationMinutes : 15,
            ModuleContext = SelectedContext,
            Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };

        try
        {
            var saved = await appointments.BookAsync(appointment);
            BookingStatus = $"{saved.AppointmentNo} booked for {saved.ScheduledOn:dd MMM yyyy HH:mm}.";
            log.Ok($"{saved.AppointmentNo} id={saved.Id}");

            if (print)
            {
                var clinic = await settings.GetClinicAsync();
                var theme = await settings.GetDocumentThemeAsync();
                PrintService.Preview(() => AppointmentSlipDocument.Build(saved, clinic, theme), $"Appointment {saved.AppointmentNo}");
            }
        }
        catch (Exception ex)
        {
            log.Skip($"refused: {ex.GetType().Name}: {ex.Message}");
            Warn(ex.Message);
            return;
        }

        NewBooking();
        if (SelectedDate.Date == date.Date) await LoadTodayAsync();
    }

    [RelayCommand]
    private void NewBooking()
    {
        PatientSearch = "";
        SelectedPatient = null;
        PatientMatches.Clear();
        Reason = "";
        Notes = "";
        DurationMinutes = 15;
    }

    // ── Reminders ──────────────────────────────────────────────────────────

    public ObservableCollection<ReminderItem> DueReminders { get; } = [];
    [ObservableProperty] private int _reminderLeadDays = 3;

    /// <summary>Common choices for the "Due within" picker — editable, so a
    /// day count outside this list can still be typed in directly.</summary>
    public static int[] ReminderLeadDayOptions { get; } = [1, 3, 7, 14, 30];

    [NotifyPropertyChangedFor(nameof(HasSelectedReminder))]
    [NotifyPropertyChangedFor(nameof(ReminderActionLabel))]
    [ObservableProperty] private ReminderItem? _selectedReminder;

    public bool HasSelectedReminder => SelectedReminder is not null;

    /// <summary>A reminded row stays visible rather than disappearing, so
    /// the same action has to read either way depending on what's selected —
    /// "mark done" for one still outstanding, "undo" for one already done.</summary>
    public string ReminderActionLabel => SelectedReminder?.IsReminded == true ? "Mark not reminded" : "Mark reminded";

    partial void OnReminderLeadDaysChanged(int value) => LoadRemindersAsync().Forget("Loading reminders");

    private async Task LoadRemindersAsync()
    {
        DueReminders.Clear();
        foreach (var r in await appointments.GetDueRemindersAsync(Math.Max(0, ReminderLeadDays)))
            DueReminders.Add(r);
    }

    [RelayCommand]
    private async Task ToggleReminderActionedAsync()
    {
        if (SelectedReminder is not { } reminder) return;

        if (reminder.IsReminded)
            await appointments.UnmarkReminderActionedAsync(reminder.SourceKind, reminder.SourceId);
        else
            await appointments.MarkReminderActionedAsync(
                reminder.SourceKind, reminder.SourceId, reminder.PatientId, reminder.DueOn, "front desk");

        SelectedReminder = null;
        await LoadRemindersAsync();
    }

    private void Warn(string message)
    {
        DailyStatus = message;
        Dialog.Show(message, "Appointments", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
