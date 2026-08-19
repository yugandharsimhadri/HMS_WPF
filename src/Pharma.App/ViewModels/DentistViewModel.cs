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
/// The Dentist "Patient" screen: pick a patient once, in the header above
/// the tabs, then Cases (open a case, see this patient's case list, mark
/// one completed/cancelled), Treatment (sittings and replacements against
/// whichever case is selected) and Payments (record and see payment
/// history for that same case) each on their own tab — one task per tab,
/// same shape as Pediatrics' Growth/Care/Immunization split. Treatment and
/// Payments both carry a read-only case summary strip at the top so which
/// case is active is never lost switching tabs. The four masters behind
/// this screen — procedure, replacement, anesthesia type, package — live
/// on the shared General Master nav destination.
/// </summary>
public partial class DentistViewModel(
    DentistService dentist, ProcedureBillsService bills, OpdService opd, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Dentist";
    public string Subtitle => Cases.Count == 0 ? "No cases for this patient" : $"{Cases.Count} case(s)";

    [ObservableProperty] private int _selectedTabIndex;

    /// <summary>Jumps back to the Cases tab — the "Switch case" link on the
    /// Treatment and Payments tabs' case strip uses this rather than
    /// leaving the user to hunt for the tab themselves.</summary>
    [RelayCommand]
    private void GoToCasesTab() => SelectedTabIndex = 0;

    public async Task LoadAsync()
    {
        await LoadProceduresAsync();
        await LoadReplacementsAsync();
        await LoadAnesthesiaTypesAsync();
        await LoadPackagesAsync();
        NewCaseForm();
    }

    // ── Shared patient panel ───────────────────────────────────────────────

    public ObservableCollection<Patient> PatientMatches { get; } = [];
    [ObservableProperty] private string _patientSearch = "";

    [NotifyPropertyChangedFor(nameof(HasSelectedPatient))]
    [ObservableProperty] private Patient? _selectedPatient;

    public bool HasSelectedPatient => SelectedPatient is not null;

    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null) return;

        _patientSearch = "";
        OnPropertyChanged(nameof(PatientSearch));

        LoadCasesAsync().Forget("Loading this patient's dental cases");
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
        Cases.Clear();
        SelectedCase = null;
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

    // ── Cases ──────────────────────────────────────────────────────────────

    public ObservableCollection<DentalCase> Cases { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasSelectedCase))]
    [ObservableProperty] private DentalCase? _selectedCase;

    public bool HasSelectedCase => SelectedCase is not null;

    [ObservableProperty] private string _casesStatus = "";

    private async Task LoadCasesAsync()
    {
        Cases.Clear();
        if (SelectedPatient is { } patient)
            foreach (var c in await dentist.GetCasesByPatientAsync(patient.Id)) Cases.Add(c);

        OnPropertyChanged(nameof(Subtitle));
        SelectedCase = null;
    }

    // Opening a new case
    public ObservableCollection<Procedure> Procedures { get; } = [];
    public ObservableCollection<DentalPackageMaster> Packages { get; } = [];

    [ObservableProperty] private Procedure? _newCaseProcedure;
    [ObservableProperty] private DentalPackageMaster? _newCasePackage;
    [ObservableProperty] private string _newCaseTooth = "";
    [ObservableProperty] private string _newCaseNotes = "";

    private async Task LoadProceduresAsync()
    {
        Procedures.Clear();
        foreach (var p in await bills.SearchProceduresAsync(ProcedureDepartment.Dentist, null, activeOnly: true)) Procedures.Add(p);

        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);
        if (SelectedDoctor is null) SelectedDoctor = Doctors.FirstOrDefault();
    }

    private async Task LoadPackagesAsync()
    {
        Packages.Clear();
        foreach (var p in await dentist.SearchPackagesAsync(null, activeOnly: true)) Packages.Add(p);
    }

    [RelayCommand]
    private async Task OpenCaseAsync()
    {
        if (SelectedPatient is not { } patient)
        {
            Warn("Select a patient first.");
            return;
        }

        if (SelectedDoctor is not { } doctor)
        {
            Warn("Select a doctor.");
            return;
        }

        if (NewCaseProcedure is null && NewCasePackage is null)
        {
            Warn("Pick a procedure or a package to open the case against.");
            return;
        }

        if (NewCaseProcedure is not null && NewCasePackage is not null)
        {
            Warn("Pick either a procedure or a package, not both.");
            return;
        }

        try
        {
            var opened = await dentist.OpenCaseAsync(new DentalCase
            {
                PatientId = patient.Id, PatientName = patient.Name, DoctorId = doctor.Id,
                ProcedureId = NewCaseProcedure?.Id, PackageId = NewCasePackage?.Id,
                ToothNumber = string.IsNullOrWhiteSpace(NewCaseTooth) ? null : NewCaseTooth.Trim(),
                Notes = string.IsNullOrWhiteSpace(NewCaseNotes) ? null : NewCaseNotes.Trim()
            });

            CasesStatus = $"Case opened against {opened.ProcedureName ?? opened.PackageName}, base cost ₹{opened.BaseCost:0.00}.";
            NewCaseForm();
            await LoadCasesAsync();
            SelectedCase = Cases.FirstOrDefault(c => c.Id == opened.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void NewCaseForm()
    {
        NewCaseProcedure = null;
        NewCasePackage = null;
        NewCaseTooth = "";
        NewCaseNotes = "";
    }

    [RelayCommand]
    private async Task CompleteCaseAsync()
    {
        if (SelectedCase is not { } dentalCase) return;

        await dentist.UpdateCaseStatusAsync(dentalCase.Id, DentalCaseStatus.Completed);
        CasesStatus = "Case marked completed.";
        await LoadCasesAsync();
    }

    [RelayCommand]
    private async Task CancelCaseAsync()
    {
        if (SelectedCase is not { } dentalCase) return;

        await dentist.UpdateCaseStatusAsync(dentalCase.Id, DentalCaseStatus.Cancelled);
        CasesStatus = "Case cancelled.";
        await LoadCasesAsync();
    }

    // Sittings
    public ObservableCollection<AnesthesiaTypeMaster> AnesthesiaTypes { get; } = [];
    [ObservableProperty] private string _sittingWorkDone = "";
    [ObservableProperty] private AnesthesiaTypeMaster? _selectedAnesthesiaType;
    [ObservableProperty] private decimal? _sittingAnesthesiaCost;
    [ObservableProperty] private string _nextSittingOnText = "";

    private async Task LoadAnesthesiaTypesAsync()
    {
        AnesthesiaTypes.Clear();
        foreach (var a in await dentist.SearchAnesthesiaTypesAsync(activeOnly: true)) AnesthesiaTypes.Add(a);
    }

    partial void OnSelectedAnesthesiaTypeChanged(AnesthesiaTypeMaster? value)
    {
        if (value is not null) SittingAnesthesiaCost = value.DefaultCost;
    }

    [RelayCommand]
    private async Task AddSittingAsync()
    {
        if (SelectedCase is not { } dentalCase) return;

        DateTime? nextOn = DateTime.TryParse(NextSittingOnText, out var parsed) ? parsed : null;

        try
        {
            await dentist.AddSittingAsync(new DentalSitting
            {
                DentalCaseId = dentalCase.Id,
                DoctorId = dentalCase.DoctorId,
                WorkDone = string.IsNullOrWhiteSpace(SittingWorkDone) ? null : SittingWorkDone.Trim(),
                AnesthesiaTypeId = SelectedAnesthesiaType?.Id,
                AnesthesiaTypeName = SelectedAnesthesiaType?.Name,
                AnesthesiaCost = SittingAnesthesiaCost,
                NextSittingOn = nextOn
            });

            CasesStatus = "Sitting added.";
            SittingWorkDone = "";
            SelectedAnesthesiaType = null;
            SittingAnesthesiaCost = null;
            NextSittingOnText = "";

            await LoadCasesAsync();
            SelectedCase = Cases.FirstOrDefault(c => c.Id == dentalCase.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    // Replacements
    public ObservableCollection<DentalReplacementMaster> Replacements { get; } = [];
    [ObservableProperty] private DentalReplacementMaster? _selectedReplacementToAdd;
    [ObservableProperty] private int _replacementQuantity = 1;

    private async Task LoadReplacementsAsync()
    {
        Replacements.Clear();
        foreach (var r in await dentist.SearchReplacementsAsync(null, activeOnly: true)) Replacements.Add(r);
    }

    [RelayCommand]
    private async Task AddReplacementAsync()
    {
        if (SelectedCase is not { } dentalCase) return;
        if (SelectedReplacementToAdd is not { } replacement)
        {
            Warn("Pick a replacement first.");
            return;
        }

        try
        {
            await dentist.AddReplacementAsync(dentalCase.Id, replacement.Id, replacement.Name, replacement.UnitCost, ReplacementQuantity);

            CasesStatus = $"{replacement.Name} added.";
            SelectedReplacementToAdd = null;
            ReplacementQuantity = 1;

            await LoadCasesAsync();
            SelectedCase = Cases.FirstOrDefault(c => c.Id == dentalCase.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    // Payments
    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private PaymentMode _paymentMode = PaymentMode.Cash;
    [ObservableProperty] private string _paymentTransactionNo = "";

    public Array PaymentModes => Enum.GetValues<PaymentMode>();
    public bool ShowTransactionNo => PaymentMode is PaymentMode.Upi or PaymentMode.Card;
    partial void OnPaymentModeChanged(PaymentMode value) => OnPropertyChanged(nameof(ShowTransactionNo));

    [RelayCommand]
    private Task RecordPaymentAsync() => CompletePaymentAsync(print: false);

    [RelayCommand]
    private Task RecordPaymentAndPrintAsync() => CompletePaymentAsync(print: true);

    private async Task CompletePaymentAsync(bool print)
    {
        if (SelectedCase is not { } dentalCase) return;

        try
        {
            var payment = await dentist.RecordPaymentAsync(new DentalPayment
            {
                DentalCaseId = dentalCase.Id, Amount = PaymentAmount, PaymentMode = PaymentMode,
                TransactionNo = string.IsNullOrWhiteSpace(PaymentTransactionNo) ? null : PaymentTransactionNo.Trim()
            });

            CasesStatus = $"{payment.ReceiptNo} · ₹{payment.Amount:0.00} recorded.";

            if (print)
            {
                var reloaded = await dentist.GetCaseAsync(dentalCase.Id);
                if (reloaded is not null)
                {
                    var clinic = await settings.GetClinicAsync();
                    var theme = await settings.GetDocumentThemeAsync();
                    PrintService.Preview(() => DentalReceiptDocument.Build(payment, reloaded, clinic, theme), $"Receipt {payment.ReceiptNo}");
                }
            }

            PaymentAmount = 0;
            PaymentTransactionNo = "";

            await LoadCasesAsync();
            SelectedCase = Cases.FirstOrDefault(c => c.Id == dentalCase.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    private void Warn(string message)
    {
        CasesStatus = message;
        Dialog.Show(message, "Dentist", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
