using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Patient register: search, the visit history behind each patient, and
/// reprinting any of it. Adding and editing open over the shell, which is what
/// lets the register and the history have the whole window.
///
/// Booking still happens on the OPD screen — this is the record, not the queue.
/// </summary>
public partial class PatientsViewModel(
    OpdService opd, PharmacyService pharmacy, SettingsService settings, DiagnosticsService diagnostics)
    : ObservableObject, IPage
{
    public string Title => "Patients";
    public string Subtitle => $"{Patients.Count} patient(s) listed";

    public ObservableCollection<Patient> Patients { get; } = [];
    public ObservableCollection<Visit> History { get; } = [];
    public ObservableCollection<Sale> Bills { get; } = [];
    public ObservableCollection<DiagnosticBill> DiagnosticBills { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private Visit? _selectedVisit;
    [ObservableProperty] private Sale? _selectedBill;
    [ObservableProperty] private DiagnosticBill? _selectedDiagnosticBill;
    [ObservableProperty] private string _status = "";

    [NotifyPropertyChangedFor(nameof(HasPatient))]
    [ObservableProperty] private Patient? _selectedPatient;

    /// <summary>Drives Edit: there is nothing to open without a patient.</summary>
    public bool HasPatient => SelectedPatient is not null;

    /// <summary>Whether the Diagnostics module is switched on — the "New
    /// diagnostic bill" button and the history tab only make sense if it is.</summary>
    [ObservableProperty] private bool _diagnosticsEnabled;

    public async Task LoadAsync()
    {
        DiagnosticsEnabled = (await settings.GetGeneralAsync()).DiagnosticsEnabled;
        await FindAsync();
    }

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
            DiagnosticBills.Clear();
            return;
        }

        LoadHistoryAsync(value.Id).Forget("Loading patient history");
    }

    private async Task LoadHistoryAsync(Guid patientId)
    {
        History.Clear();
        foreach (var v in await opd.GetPatientHistoryAsync(patientId)) History.Add(v);

        Bills.Clear();
        foreach (var s in await pharmacy.GetSalesByPatientAsync(patientId)) Bills.Add(s);

        DiagnosticBills.Clear();
        if (DiagnosticsEnabled)
            foreach (var b in await diagnostics.GetBillsByPatientAsync(patientId)) DiagnosticBills.Add(b);
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

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => PrescriptionPrinter.Build(visit, clinic, theme), $"Prescription {visit.VisitNo}");
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

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(visit, clinic, theme, isReprint: true),
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

        var pharmacyProfile = await settings.GetPharmacyAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => BillPrinter.Build(sale, pharmacyProfile, theme, isReprint: true),
                             $"Bill {sale.BillNo} (duplicate)");
    }

    [RelayCommand]
    private async Task PrintDiagnosticBillAsync()
    {
        if (SelectedDiagnosticBill is null)
        {
            Status = "Pick a diagnostic bill first.";
            return;
        }

        var bill = await diagnostics.GetBillAsync(SelectedDiagnosticBill.Id);
        if (bill is null) return;

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => DiagnosticBillPrinter.Build(bill, clinic, theme, isReprint: true),
                             $"Bill {bill.BillNo} (duplicate)");
    }

    /// <summary>Jumps to the Diagnostics screen with this patient already
    /// selected, instead of sending the desk back to search for them again.</summary>
    [RelayCommand]
    private async Task NewDiagnosticBillAsync()
    {
        if (SelectedPatient is not { } patient) return;

        var shell = App.Services.GetRequiredService<MainViewModel>();
        var diagnosticsPage = App.Services.GetRequiredService<DiagnosticsViewModel>();

        await shell.GoCommand.ExecuteAsync("diagnostics");
        await diagnosticsPage.SelectPatientAsync(patient.Id);
    }

    // ── Adding and editing ─────────────────────────────────────────────────

    [RelayCommand]
    private Task NewPatientAsync() => EditAsync(null);

    [RelayCommand]
    private Task EditPatientAsync() => EditAsync(SelectedPatient);

    /// <summary>
    /// Opens one patient over the shell and waits for it to close.
    ///
    /// A fresh view model each time, holding that patient and nothing else. It
    /// is what retired a whole family of bugs on this screen: the register and
    /// the form used to share a selection, so the next child typed in was saved
    /// on top of whoever was still loaded.
    /// </summary>
    private async Task EditAsync(Patient? existing)
    {
        var editor = new PatientEditorViewModel(opd, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        // Cancelling says nothing, so the message from whatever happened before
        // is left where it was.
        if (editor.Outcome is not { } outcome) return;

        // Everything clears, the search box included. Leaving the register
        // filtered to the patient just saved reads as "ready for the next one"
        // and is not.
        SelectedPatient = null;
        SelectedVisit = null;
        SelectedBill = null;
        History.Clear();
        Bills.Clear();

        Search = "";
        await FindAsync();

        Status = $"{outcome} The register is clear for the next patient.";
    }

    /// <summary>Empties the search and puts the selection down.</summary>
    [RelayCommand]
    private async Task ClearAsync()
    {
        SelectedPatient = null;
        SelectedVisit = null;
        SelectedBill = null;

        Search = "";
        await FindAsync();

        Status = "";
    }
}
