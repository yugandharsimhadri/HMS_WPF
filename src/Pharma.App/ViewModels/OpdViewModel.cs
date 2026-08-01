using Microsoft.Extensions.DependencyInjection;
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

    public string Subtitle
    {
        get
        {
            var line = $"{Waiting.Count} waiting · {Completed.Count} completed · {Date:ddd, dd MMM}";

            if (Session == ClinicSession.FullDay) return line;

            // Naming the hours matters: "3 waiting" under a session filter is a
            // different number from "3 waiting" today, and the desk has to know
            // which one it is looking at.
            line += $" · {Session} sitting, {_clinic.Describe(Session)}";

            // And say when the filter is hiding people, or the afternoon walk-in
            // who belongs to neither sitting simply vanishes.
            if (Hidden > 0) line += $" · {Hidden} more today outside these hours";

            return line;
        }
    }

    public ObservableCollection<Visit> Waiting { get; } = [];
    public ObservableCollection<Visit> Completed { get; } = [];
    public ObservableCollection<Doctor> Doctors { get; } = [];

    /// <summary>Null means every doctor — the "All" tab.</summary>
    [ObservableProperty] private Doctor? _doctorTab;

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _status = "";

    /// <summary>Chosen in Settings; re-read every time the screen is opened.</summary>
    [ObservableProperty] private bool _useTiles = true;

    /// <summary>
    /// Which sitting to show. Doctors sit mornings and evenings with the
    /// afternoon off, and "who is left this evening" is the question actually
    /// being asked at the desk — "who is left today" answers it only by
    /// accident, on a day with one sitting.
    /// </summary>
    [ObservableProperty] private ClinicSession _session = ClinicSession.FullDay;

    /// <summary>How many of today's visits the session filter is holding back.</summary>
    public int Hidden { get; private set; }

    private readonly List<Visit> _all = [];

    /// <summary>The session hours, re-read whenever the screen is opened.</summary>
    private ClinicProfile _clinic = new();

    public Array Sessions => Enum.GetValues<ClinicSession>();

    public async Task LoadAsync()
    {
        _clinic = await settings.GetClinicAsync();
        UseTiles = (await settings.GetGeneralAsync()).QueueLayout == QueueLayout.Tiles;

        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);

        await RefreshAsync();
    }

    partial void OnDateChanged(DateTime value) => RefreshAsync().Forget("Refreshing the OPD queue");

    partial void OnDoctorTabChanged(Doctor? value) => Regroup();

    partial void OnSessionChanged(ClinicSession value) => Regroup();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _all.Clear();
        _all.AddRange(await opd.GetVisitsAsync(Date));
        Regroup();
    }

    /// <summary>
    /// Splits the day's visits into the two columns, for the chosen doctor and
    /// the chosen sitting.
    /// </summary>
    private void Regroup()
    {
        Waiting.Clear();
        Completed.Clear();
        Hidden = 0;

        foreach (var visit in _all.Where(v => DoctorTab is null || v.DoctorId == DoctorTab.Id))
        {
            // Counted rather than dropped. A visit booked at two in the
            // afternoon belongs to neither sitting, and a queue that quietly
            // loses somebody is worse than one that says it is filtered.
            if (!_clinic.IsIn(Session, visit.ScheduledOn))
            {
                if (visit.IsWaiting || visit.Status == VisitStatus.Completed) Hidden++;
                continue;
            }

            if (visit.IsWaiting) Waiting.Add(visit);
            else if (visit.Status == VisitStatus.Completed) Completed.Add(visit);
        }

        OnPropertyChanged(nameof(Hidden));
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void ShowAllDoctors() => DoctorTab = null;

    [RelayCommand]
    private void SelectDoctorTab(Doctor? doctor) => DoctorTab = doctor;

    // ── Booking ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the booking form over the shell and waits for it to close.
    ///
    /// A fresh view model per booking, so nothing survives to the next one. That
    /// matters most for the patient: a visit booked against whoever happened to
    /// be still selected sends the wrong child in to the doctor.
    /// </summary>
    [RelayCommand]
    private async Task NewVisitAsync()
    {
        var booking = new BookVisitViewModel(opd, Doctors, DoctorTab ?? Doctors.FirstOrDefault(), Date);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(booking, close => booking.RequestClose += () => close());

        // Refresh either way: the form may have added a patient before being
        // closed, and the queue should agree with the register.
        await RefreshAsync();

        // Set last — refreshing writes its own status, and the confirmation is
        // what the operator needs left on screen.
        if (booking.Outcome is { } outcome) Status = outcome;
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

    /// <summary>
    /// Opens the fee form over the shell. It used to take the money on the
    /// press — at whatever payment mode a combo at the top of the screen was
    /// left on, and straight into a print preview. A receipt is numbered and
    /// dated as it is written, so a fee taken wrongly is a fee reversed on
    /// paper. Now the amount and the mode are shown, and asked about, first.
    /// </summary>
    [RelayCommand]
    private async Task CollectFeeAsync(Visit? visit)
    {
        if (visit is null) return;

        if (visit.FeePaid)
        {
            Status = $"Token {visit.TokenNo} has already paid — use the receipt button to reprint.";
            return;
        }

        var collecting = new CollectFeeViewModel(opd, settings, visit);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(collecting, close => collecting.RequestClose += () => close());

        await RefreshAsync();

        // Set last: refreshing writes its own status, and the receipt number is
        // what the desk needs left on screen.
        if (collecting.Outcome is { } outcome) Status = outcome;
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

        var confirm = Dialog.Show(
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

            var consultation = new ConsultationViewModel(
                visit.Id,
                opd,
                App.Services.GetRequiredService<PharmacyService>(),
                App.Services.GetRequiredService<SettingsService>());

            var shell = App.Services.GetRequiredService<MainViewModel>();

            // Shown over the shell rather than in its own window: a window can
            // get lost behind another application, and then the doctor is
            // clicking a main window that cannot answer.
            var showing = shell.ShowOverlayAsync(consultation, close => consultation.RequestClose += () => close());

            await consultation.LoadAsync();
            await showing;

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

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(full, clinic, theme, isReprint: true),
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

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(full, clinic, theme), $"Prescription {full.VisitNo}");
    }
}
