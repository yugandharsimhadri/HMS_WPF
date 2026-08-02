using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One line on the diagnostic bill being built.</summary>
public partial class DiagnosticBillRow : ObservableObject
{
    /// <summary>Null once the test it was added from has since been deleted from
    /// the master — the row still bills fine on its own name and price.</summary>
    public Guid? TestId { get; init; }
    public string TestName { get; init; } = "";

    [ObservableProperty] private decimal _price;
    [ObservableProperty] private int _quantity = 1;

    public decimal Amount => Price * Quantity;

    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(Amount));
}

/// <summary>
/// The Diagnostics module: bill a patient for lab tests, and maintain the
/// list of tests billing draws from. One screen, two tabs — Billing and Test
/// Master — since each is a handful of fields, not a destination on its own.
/// </summary>
public partial class DiagnosticsViewModel(
    DiagnosticsService diagnostics, OpdService opd, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Diagnostics";
    public string Subtitle => Lines.Count == 0 ? "No tests on this bill" : $"{Lines.Count} test(s) · ₹{FinalAmount:0.00}";

    public async Task LoadAsync()
    {
        await LoadTestsAsync();
        NewBill();
    }

    // ── Billing ────────────────────────────────────────────────────────────

    public ObservableCollection<Patient> PatientMatches { get; } = [];
    [ObservableProperty] private string _patientSearch = "";

    [NotifyPropertyChangedFor(nameof(HasSelectedPatient))]
    [ObservableProperty] private Patient? _selectedPatient;

    /// <summary>Switches the patient panel from "pick one" to "here's who was
    /// picked" — the confirmation line only shows once there is a selection.</summary>
    public bool HasSelectedPatient => SelectedPatient is not null;

    /// <summary>
    /// The search box is cleared the moment a patient is picked — left
    /// filled in, it kept showing the query that was typed even though the
    /// match list underneath it had already given way to the confirmation,
    /// which read as the search having done nothing.
    ///
    /// Set through the backing field, not the <see cref="PatientSearch"/>
    /// property. Going through the property setter fires
    /// <see cref="FindPatientsAsync"/> synchronously up to its first
    /// <c>await</c> — including its opening <c>PatientMatches.Clear()</c>.
    /// This handler runs *inside* the patient ListBox's own SelectedItem
    /// setter, on the same call stack as the click that triggered it, and
    /// <see cref="PatientMatches"/> is that same ListBox's ItemsSource:
    /// clearing it out from under WPF mid-selection reset SelectedItem
    /// straight back to null, silently undoing the very click that set it.
    /// Updating the field and notifying directly still empties the box on
    /// screen without re-running the search that causes that.
    /// </summary>
    partial void OnSelectedPatientChanged(Patient? value)
    {
        if (value is null) return;

        _patientSearch = "";
        OnPropertyChanged(nameof(PatientSearch));
    }

    /// <summary>Puts the patient panel back into search mode, for a bill
    /// started against the wrong person without starting the whole bill over.
    ///
    /// <see cref="PatientMatches"/> is cleared directly here rather than left
    /// to <see cref="PatientSearch"/>'s own setter: that setter is a no-op
    /// when the box is already empty — which it always is by this point,
    /// since selecting a patient empties it — so the stale matches from
    /// before the selection were still sitting in the list, ready to reappear
    /// the moment it became visible again.</summary>
    [RelayCommand]
    private void ChangePatient()
    {
        SelectedPatient = null;
        PatientSearch = "";
        PatientMatches.Clear();
    }

    /// <summary>
    /// Opens the same patient editor the Patients screen uses to add
    /// somebody new, over the shell — so a walk-in with no record yet does
    /// not have to be registered on a different screen before they can be
    /// billed. The patient just saved is selected the moment this closes.
    /// </summary>
    [RelayCommand]
    private async Task NewPatientAsync()
    {
        var editor = new PatientEditorViewModel(opd);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Saved is { } patient) SelectedPatient = patient;
    }

    public ObservableCollection<DiagnosticBillRow> Lines { get; } = [];

    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private decimal _finalAmount;
    [ObservableProperty] private PaymentMode _paymentMode = PaymentMode.Cash;
    [ObservableProperty] private string _remarks = "";
    [ObservableProperty] private string _status = "";

    /// <summary>Null for a bill not yet saved.</summary>
    [ObservableProperty] private Guid? _currentBillId;
    [ObservableProperty] private string _billNo = "";
    [ObservableProperty] private DiagnosticBillStatus _billStatus = DiagnosticBillStatus.Ordered;

    /// <summary>The status picker only makes sense once a bill exists to move
    /// through the workflow — a new, unsaved bill is always Ordered.</summary>
    public bool HasBillId => CurrentBillId is not null;

    partial void OnCurrentBillIdChanged(Guid? value) => OnPropertyChanged(nameof(HasBillId));

    /// <summary>Editing is refused once a bill is Completed — same rule the
    /// service enforces, mirrored here so the fields grey out rather than the
    /// operator finding out only after Save fails.</summary>
    public bool CanEdit => BillStatus != DiagnosticBillStatus.Completed;

    public Array PaymentModes => Enum.GetValues<PaymentMode>();
    public Array BillStatuses => Enum.GetValues<DiagnosticBillStatus>();

    partial void OnBillStatusChanged(DiagnosticBillStatus value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnPatientSearchChanged(string value) => FindPatientsAsync().Forget("Searching patients");

    [RelayCommand]
    private async Task FindPatientsAsync()
    {
        PatientMatches.Clear();
        if (string.IsNullOrWhiteSpace(PatientSearch)) return;

        foreach (var p in await opd.SearchPatientsAsync(PatientSearch, 20)) PatientMatches.Add(p);
        if (PatientMatches.Count == 1) SelectedPatient = PatientMatches[0];
    }

    /// <summary>Called from the Patients screen to jump here pre-filled for a
    /// specific patient, instead of typing the search again.</summary>
    public async Task SelectPatientAsync(Guid patientId)
    {
        NewBill();

        var patients = await opd.SearchPatientsAsync(null, 1000);
        SelectedPatient = patients.FirstOrDefault(p => p.Id == patientId);
    }

    /// <summary>Loads an existing bill for viewing, editing or reprinting —
    /// called from the Patients-screen diagnostics history.</summary>
    public async Task LoadBillAsync(Guid billId)
    {
        var bill = await diagnostics.GetBillAsync(billId);
        if (bill is null) return;

        CurrentBillId = bill.Id;
        BillNo = bill.BillNo;
        BillStatus = bill.Status;
        Discount = bill.Discount;
        PaymentMode = bill.PaymentMode;
        Remarks = bill.Remarks ?? "";

        var patients = await opd.SearchPatientsAsync(null, 1000);
        SelectedPatient = patients.FirstOrDefault(p => p.Id == bill.PatientId);

        Lines.Clear();
        foreach (var item in bill.Items)
        {
            var row = new DiagnosticBillRow { TestId = item.TestId, TestName = item.TestName, Price = item.Price, Quantity = item.Quantity };
            Watch(row);
            Lines.Add(row);
        }

        Recalculate();
        Status = $"Bill {bill.BillNo} loaded.";
    }

    /// <summary>
    /// Opens the test picker — a popup over the shell listing every active
    /// test, searchable, that adds straight to this bill as each one is
    /// picked. Only makes sense once there is a patient to bill, the same
    /// way the Medicines screen's editor only opens for a real record.
    /// </summary>
    [RelayCommand]
    private async Task OpenTestPickerAsync()
    {
        if (SelectedPatient is null)
        {
            Warn("Select a patient first.");
            return;
        }

        var picker = new DiagnosticTestPickerViewModel(diagnostics, this);
        await picker.LoadAsync();

        var shell = App.Services.GetRequiredService<MainViewModel>();
        await shell.ShowOverlayAsync(picker, close => picker.RequestClose += () => close());

        Status = Lines.Count == 0 ? "No tests added yet." : $"{Lines.Count} test(s) on this bill · ₹{FinalAmount:0.00}";
    }

    /// <summary>
    /// Adds one test to the bill at its master price — called by the picker
    /// for each test chosen there, so a whole shopping list can be built up
    /// while the popup stays open. The price can still be overridden
    /// afterward from the bill grid on the main screen; the quantity too —
    /// which is the intended way to bill the same test more than once, not a
    /// second click here: a test already on the bill is refused outright,
    /// rather than silently adding a duplicate line at the same price.
    /// </summary>
    public bool AddTestLine(DiagnosticTest test)
    {
        if (Lines.Any(l => l.TestId == test.Id)) return false;

        var row = new DiagnosticBillRow { TestId = test.Id, TestName = test.Name, Price = test.Price };
        Watch(row);
        Lines.Add(row);
        Recalculate();
        return true;
    }

    [RelayCommand]
    private void RemoveLine(DiagnosticBillRow? row)
    {
        if (row is null) return;

        row.PropertyChanged -= OnLineChanged;
        Lines.Remove(row);
        Recalculate();
    }

    private void Watch(DiagnosticBillRow row) => row.PropertyChanged += OnLineChanged;

    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Recalculate();

    partial void OnDiscountChanged(decimal value) => Recalculate();

    private void Recalculate()
    {
        TotalAmount = Lines.Sum(l => l.Amount);
        FinalAmount = Math.Max(0, TotalAmount - Discount);
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private Task SaveAsync() => CompleteBillAsync(print: false);

    [RelayCommand]
    private Task SaveAndPrintAsync() => CompleteBillAsync(print: true);

    private async Task CompleteBillAsync(bool print)
    {
        using var log = AppLog.Enter(
            "Diagnostics.SaveBill", $"print={print} lines={Lines.Count} patient={SelectedPatient?.Id}");

        if (SelectedPatient is not { } patient)
        {
            log.Skip("no patient chosen");
            Warn("Select a patient first.");
            return;
        }

        if (Lines.Count == 0)
        {
            log.Skip("nothing on the bill");
            Warn("Add at least one test to the bill.");
            return;
        }

        var bill = new DiagnosticBill
        {
            Id = CurrentBillId ?? Guid.Empty,
            PatientId = patient.Id,
            PatientName = patient.Name,
            PatientNo = patient.PatientNo,
            PaymentMode = PaymentMode,
            Discount = Discount,
            Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim()
        };

        var lines = Lines.Select(l => new DiagnosticBillLine
        {
            TestId = l.TestId, TestName = l.TestName, Price = l.Price, Quantity = l.Quantity
        }).ToList();

        try
        {
            var saved = await diagnostics.SaveBillAsync(bill, lines);
            Status = $"Bill {saved.BillNo} saved · ₹{saved.FinalAmount:0.00}";

            if (print)
            {
                var clinic = await settings.GetClinicAsync();
                var theme = await settings.GetDocumentThemeAsync();
                var full = await diagnostics.GetBillAsync(saved.Id);
                if (full is not null)
                    PrintService.Preview(() => DiagnosticBillPrinter.Build(full, clinic, theme), $"Bill {full.BillNo}");
            }

            NewBill();
            log.Ok($"{saved.BillNo} final={saved.FinalAmount:0.00} printed={print}");
        }
        catch (Exception ex)
        {
            log.Skip($"refused: {ex.GetType().Name}: {ex.Message}");
            AppLog.Error("Saving the diagnostic bill failed.", ex);
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void NewBill()
    {
        foreach (var row in Lines) row.PropertyChanged -= OnLineChanged;

        Lines.Clear();
        PatientSearch = "";
        SelectedPatient = null;
        PatientMatches.Clear();
        Discount = 0;
        Remarks = "";
        PaymentMode = PaymentMode.Cash;
        CurrentBillId = null;
        BillNo = "";
        BillStatus = DiagnosticBillStatus.Ordered;

        Recalculate();
    }

    // ── Test master ────────────────────────────────────────────────────────
    //
    // Add and edit both happen in a popup over the shell, the same as the
    // Medicines screen edits a product — not a permanent side panel, so the
    // grid gets the room and a test's four fields do not sit half-empty
    // beside it when nothing is selected.

    public ObservableCollection<DiagnosticTest> Tests { get; } = [];

    [ObservableProperty] private string _testMasterSearch = "";

    [NotifyPropertyChangedFor(nameof(HasMasterTest))]
    [ObservableProperty] private DiagnosticTest? _selectedMasterTest;

    /// <summary>Drives Edit: there is nothing to open without a test selected.</summary>
    public bool HasMasterTest => SelectedMasterTest is not null;

    private static readonly string[] ExampleCategories =
        ["Hematology", "Biochemistry", "Urine", "Stool", "Serology", "Thyroid", "Vitamins", "Others"];

    private async Task LoadTestsAsync()
    {
        Tests.Clear();
        foreach (var t in await diagnostics.SearchTestsAsync(TestMasterSearch)) Tests.Add(t);
    }

    [RelayCommand]
    private async Task FindTestMasterAsync() => await LoadTestsAsync();

    partial void OnTestMasterSearchChanged(string value) => FindTestMasterAsync().Forget("Searching test master");

    [RelayCommand]
    private Task NewTestAsync() => EditTestAsync(null);

    [RelayCommand]
    private Task EditTestAsync() => EditTestAsync(SelectedMasterTest);

    /// <summary>Puts the editor over the shell and waits for it to close — a
    /// fresh view model each time, the same reason ProductsViewModel.EditAsync
    /// gives for doing the same with a medicine.</summary>
    private async Task EditTestAsync(DiagnosticTest? existing)
    {
        var categories = ExampleCategories.Union(await diagnostics.GetCategoriesAsync()).OrderBy(c => c);
        var editor = new DiagnosticTestEditorViewModel(diagnostics, categories, existing);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Outcome is { } outcome) Status = outcome;

        await LoadTestsAsync();
        SelectedMasterTest = existing is null ? null : Tests.FirstOrDefault(t => t.Id == existing.Id);
    }

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
