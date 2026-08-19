using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One report line in the order being built on screen, before it is
/// persisted — the UI-side counterpart of <see cref="LabOrderLine"/>.</summary>
public partial class LabOrderLineRow : ObservableObject
{
    public Guid? ReportId { get; init; }
    public string ReportName { get; init; } = "";
    [ObservableProperty] private decimal _price;
}

/// <summary>One analyte's result-entry line within an ordered report —
/// flattened across every report on the order so the whole panel is entered
/// on one scrollable list, grouped visually by <see cref="ReportName"/>.</summary>
public partial class ResultEntryRow : ObservableObject
{
    public Guid OrderReportId { get; init; }
    public string ReportName { get; init; } = "";
    public Guid? AnalyteId { get; init; }
    public string AnalyteName { get; init; } = "";
    public string Units { get; init; } = "";
    public LabResultType ResultType { get; init; }

    [ObservableProperty] private string _resultValue = "";
    [ObservableProperty] private string _referenceRangeDisplay = "";
    [ObservableProperty] private LabResultFlag _flag = LabResultFlag.Normal;
}

/// <summary>
/// The Pathology Lab "Patient" screen: pick a patient once, in the header
/// above the tabs, then orders (billed against reports or a package, with
/// per-analyte result entry and verification) on the single Orders tab.
/// The three masters behind it — analyte, report (panel) and package —
/// live on their own nav destination, <see cref="PathologyLabMasterViewModel"/>.
/// </summary>
public partial class PathologyLabViewModel(
    PathologyLabService lab, OpdService opd, SettingsService settings, CurrentUserService currentUser)
    : ObservableObject, IPage
{
    public string Title => "Pathology Lab";
    public string Subtitle => Orders.Count == 0 ? "No orders for this patient" : $"{Orders.Count} order(s)";

    public async Task LoadAsync()
    {
        await LoadReportsCatalogAsync();
        await LoadPackagesCatalogAsync();
        NewOrderForm();
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

        LoadOrdersAsync().Forget("Loading this patient's lab orders");
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
        Orders.Clear();
        SelectedOrder = null;
    }

    [RelayCommand]
    private async Task NewPatientAsync()
    {
        var editor = new PatientEditorViewModel(opd);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(editor, close => editor.RequestClose += () => close());

        if (editor.Saved is { } patient) SelectedPatient = patient;
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    public ObservableCollection<LabOrder> Orders { get; } = [];

    [NotifyPropertyChangedFor(nameof(HasSelectedOrder))]
    [ObservableProperty] private LabOrder? _selectedOrder;

    public bool HasSelectedOrder => SelectedOrder is not null;
    public bool CanPrintReport => SelectedOrder is { Status: LabOrderStatus.Verified or LabOrderStatus.Completed };

    [ObservableProperty] private string _ordersStatus = "";

    partial void OnSelectedOrderChanged(LabOrder? value)
    {
        OnPropertyChanged(nameof(CanPrintReport));
        LoadResultEntryRowsAsync().Forget("Loading result entry rows");
    }

    private async Task LoadOrdersAsync()
    {
        Orders.Clear();
        if (SelectedPatient is { } patient)
            foreach (var o in await lab.GetOrdersByPatientAsync(patient.Id)) Orders.Add(o);

        OnPropertyChanged(nameof(Subtitle));
        SelectedOrder = null;
    }

    // New order builder
    public ObservableCollection<LabReport> ReportsCatalog { get; } = [];
    public ObservableCollection<LabPackageMaster> PackagesCatalog { get; } = [];
    [ObservableProperty] private LabReport? _reportToAdd;
    [ObservableProperty] private LabPackageMaster? _packageToApply;
    public ObservableCollection<LabOrderLineRow> NewOrderLines { get; } = [];

    [ObservableProperty] private string _newOrderReferredBy = "";
    [ObservableProperty] private string _newOrderSpecimenId = "";
    [ObservableProperty] private decimal _newOrderDiscount;
    [ObservableProperty] private PaymentMode _newOrderPaymentMode = PaymentMode.Cash;
    [ObservableProperty] private string _newOrderTransactionNo = "";
    [ObservableProperty] private string _newOrderRemarks = "";
    private Guid? _newOrderPackageId;

    public static Array PaymentModes => Enum.GetValues<PaymentMode>();
    public bool ShowTransactionNo => NewOrderPaymentMode is PaymentMode.Upi or PaymentMode.Card;
    partial void OnNewOrderPaymentModeChanged(PaymentMode value) => OnPropertyChanged(nameof(ShowTransactionNo));

    public decimal NewOrderTotal => NewOrderLines.Sum(l => l.Price);
    public decimal NewOrderFinal => Math.Max(0, NewOrderTotal - NewOrderDiscount);
    partial void OnNewOrderDiscountChanged(decimal value) => OnPropertyChanged(nameof(NewOrderFinal));

    private async Task LoadReportsCatalogAsync()
    {
        ReportsCatalog.Clear();
        foreach (var r in await lab.SearchReportsAsync(null, activeOnly: true)) ReportsCatalog.Add(r);
    }

    private async Task LoadPackagesCatalogAsync()
    {
        PackagesCatalog.Clear();
        foreach (var p in await lab.SearchPackagesAsync(null, activeOnly: true)) PackagesCatalog.Add(p);
    }

    [RelayCommand]
    private void AddReportLine()
    {
        if (ReportToAdd is not { } report) return;
        if (NewOrderLines.Any(l => l.ReportId == report.Id))
        {
            Warn($"{report.Name} is already on this order.");
            return;
        }

        NewOrderLines.Add(new LabOrderLineRow { ReportId = report.Id, ReportName = report.Name, Price = report.Price });
        ReportToAdd = null;
        RaiseOrderTotalsChanged();
    }

    [RelayCommand]
    private void RemoveOrderLine(LabOrderLineRow? row)
    {
        if (row is null) return;

        NewOrderLines.Remove(row);
        RaiseOrderTotalsChanged();
    }

    /// <summary>
    /// Expands the package into one line per constituent report, at each
    /// report's own catalog price — so the printed report still shows what
    /// each one is individually worth — then discounts the order down to the
    /// package's own flat price. Same "package price wins over the sum of
    /// its parts" rule as Dentist's own packages, reached here through
    /// Discount rather than a single opaque line, since each report's own
    /// analytes still have to be known for result entry.
    /// </summary>
    [RelayCommand]
    private async Task ApplyPackageAsync()
    {
        if (PackageToApply is not { } package) return;

        var reports = await lab.GetPackageReportsAsync(package.Id);
        if (reports.Count == 0)
        {
            Warn("This package has no reports configured.");
            return;
        }

        NewOrderLines.Clear();
        foreach (var r in reports) NewOrderLines.Add(new LabOrderLineRow { ReportId = r.Id, ReportName = r.Name, Price = r.Price });

        NewOrderDiscount = Math.Max(0, reports.Sum(r => r.Price) - package.PackagePrice);
        _newOrderPackageId = package.Id;
        PackageToApply = null;
        RaiseOrderTotalsChanged();
    }

    private void RaiseOrderTotalsChanged()
    {
        OnPropertyChanged(nameof(NewOrderTotal));
        OnPropertyChanged(nameof(NewOrderFinal));
    }

    [RelayCommand]
    private void NewOrderForm()
    {
        NewOrderLines.Clear();
        ReportToAdd = null;
        PackageToApply = null;
        _newOrderPackageId = null;
        NewOrderReferredBy = "";
        NewOrderSpecimenId = "";
        NewOrderDiscount = 0;
        NewOrderPaymentMode = PaymentMode.Cash;
        NewOrderTransactionNo = "";
        NewOrderRemarks = "";
        RaiseOrderTotalsChanged();
    }

    [RelayCommand]
    private async Task SaveOrderAsync()
    {
        if (SelectedPatient is not { } patient)
        {
            Warn("Select a patient first.");
            return;
        }

        if (NewOrderLines.Count == 0)
        {
            Warn("Add at least one report, or apply a package.");
            return;
        }

        try
        {
            var lines = NewOrderLines
                .Select(l => new LabOrderLine { ReportId = l.ReportId, ReportName = l.ReportName, Price = l.Price })
                .ToList();

            var saved = await lab.SaveOrderAsync(new LabOrder
            {
                PatientId = patient.Id, PatientName = patient.Name, PatientNo = patient.PatientNo,
                PackageId = _newOrderPackageId,
                ReferredBy = string.IsNullOrWhiteSpace(NewOrderReferredBy) ? null : NewOrderReferredBy.Trim(),
                SpecimenId = string.IsNullOrWhiteSpace(NewOrderSpecimenId) ? null : NewOrderSpecimenId.Trim(),
                Discount = NewOrderDiscount, PaymentMode = NewOrderPaymentMode,
                TransactionNo = string.IsNullOrWhiteSpace(NewOrderTransactionNo) ? null : NewOrderTransactionNo.Trim(),
                Remarks = string.IsNullOrWhiteSpace(NewOrderRemarks) ? null : NewOrderRemarks.Trim()
            }, lines);

            OrdersStatus = $"{saved.OrderNo} saved — ₹{saved.FinalAmount:0.00}.";
            NewOrderForm();
            await LoadOrdersAsync();
            SelectedOrder = Orders.FirstOrDefault(o => o.Id == saved.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    // ── Result entry ───────────────────────────────────────────────────────

    public ObservableCollection<ResultEntryRow> ResultRows { get; } = [];

    private async Task LoadResultEntryRowsAsync()
    {
        ResultRows.Clear();
        if (SelectedOrder is not { } order) return;

        var reloaded = await lab.GetOrderAsync(order.Id);
        if (reloaded is null) return;

        foreach (var orderReport in reloaded.Reports)
        {
            if (orderReport.ReportId is not { } reportId) continue;

            foreach (var analyte in await lab.GetReportAnalytesAsync(reportId))
            {
                var existing = orderReport.Results.FirstOrDefault(r => r.AnalyteId == analyte.Id);
                ResultRows.Add(new ResultEntryRow
                {
                    OrderReportId = orderReport.Id, ReportName = orderReport.ReportName,
                    AnalyteId = analyte.Id, AnalyteName = analyte.Name, Units = analyte.Units, ResultType = analyte.ResultType,
                    ResultValue = existing?.ResultValue ?? "", ReferenceRangeDisplay = existing?.ReferenceRangeDisplay ?? "",
                    Flag = existing?.Flag ?? LabResultFlag.Normal
                });
            }
        }
    }

    [RelayCommand]
    private async Task SaveResultsAsync()
    {
        if (SelectedOrder is not { } order) return;

        var enteredBy = currentUser.Current?.DisplayName;
        var rows = ResultRows.Where(r => !string.IsNullOrWhiteSpace(r.ResultValue)).ToList();

        if (rows.Count == 0)
        {
            Warn("Enter at least one result before saving.");
            return;
        }

        try
        {
            foreach (var row in rows)
                await lab.SaveResultAsync(row.OrderReportId, row.AnalyteId, row.AnalyteName, row.Units, row.ResultValue, row.ResultType, enteredBy);

            if (order.Status is LabOrderStatus.Ordered or LabOrderStatus.SampleCollected)
                await lab.UpdateOrderStatusAsync(order.Id, LabOrderStatus.ResultEntered);

            OrdersStatus = "Results saved.";
            await LoadOrdersAsync();
            SelectedOrder = Orders.FirstOrDefault(o => o.Id == order.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MarkCollectedAsync()
    {
        if (SelectedOrder is not { } order) return;

        await lab.UpdateOrderStatusAsync(order.Id, LabOrderStatus.SampleCollected);
        OrdersStatus = "Sample marked collected.";
        await LoadOrdersAsync();
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == order.Id);
    }

    [RelayCommand]
    private async Task VerifyOrderAsync()
    {
        if (SelectedOrder is not { } order) return;

        try
        {
            await lab.VerifyOrderAsync(order.Id, currentUser.Current?.DisplayName ?? "Staff");
            OrdersStatus = "Order verified — the report can now be printed.";
            await LoadOrdersAsync();
            SelectedOrder = Orders.FirstOrDefault(o => o.Id == order.Id);
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (SelectedOrder is not { } order) return;

        if (order.Status is not (LabOrderStatus.Verified or LabOrderStatus.Completed))
        {
            Warn("Verify the order before printing its report.");
            return;
        }

        var reloaded = await lab.GetOrderAsync(order.Id);
        if (reloaded is null) return;

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => LabReportDocument.Build(reloaded, clinic, theme), $"Lab Report {reloaded.OrderNo}");
    }

    private void Warn(string message)
    {
        OrdersStatus = message;
        Dialog.Show(message, "Pathology Lab", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
