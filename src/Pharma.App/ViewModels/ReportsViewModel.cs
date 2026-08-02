using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Pharma.App.Printing;
using Microsoft.Win32;
using Pharma.App.Reports;
using Pharma.Core;
using Pharma.Data;
using QuestPDF.Fluent;

namespace Pharma.App.ViewModels;

/// <summary>One row of the GST summary that appears on a tax invoice and in GSTR-1.</summary>
public record GstSlab(decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Total);

/// <summary>One day's diagnostic revenue, for the Date-wise Revenue section.</summary>
public record DiagnosticsRevenueRow(DateTime Date, int Bills, decimal Revenue);

/// <summary>One test's billing frequency, for the Most Frequently Ordered Tests section.</summary>
public record DiagnosticsTestFrequencyRow(string TestName, int TimesOrdered, decimal Revenue);

/// <summary>A batch on the Expiring Soon report, with the day count worked out once for display.</summary>
public class ExpiringRow(Batch batch)
{
    public Batch Batch { get; } = batch;
    public int DaysRemaining { get; } = (batch.ExpiryDate.Date - DateTime.Today).Days;
    public bool IsExpired => DaysRemaining < 0;

    /// <summary>
    /// Read out in words rather than left for the red row colour to say alone —
    /// a colour-blind or low-vision user gets the same "this is expired"
    /// signal a sighted one gets from the tint.
    /// </summary>
    public string StatusLabel => IsExpired
        ? $"⚠ Expired {-DaysRemaining} day(s) ago"
        : $"Expires in {DaysRemaining} day(s)";
}

public partial class ReportsViewModel(
    PharmacyService pharmacy,
    OpdService opd,
    SettingsService settings,
    DiagnosticsService diagnostics,
    IDbContextFactory<AppDbContext> factory) : ObservableObject, IPage
{
    public string Title => "Reports";
    public string Subtitle => $"{Sales.Count} bill(s) on {Date:ddd, dd MMM} · ₹{TotalCollected:0.00}";

    public ObservableCollection<Sale> Sales { get; } = [];
    public ObservableCollection<Visit> Visits { get; } = [];
    public ObservableCollection<ExpiringRow> Expiring { get; } = [];
    public ObservableCollection<Product> LowStock { get; } = [];

    /// <summary>
    /// Stock put on the shelf at the counter with no supplier bill behind it.
    /// Purchases will not tie out against sales until each of these is matched
    /// to the real bill, so the list is the reconciliation worklist.
    /// </summary>
    public ObservableCollection<Batch> ToReconcile { get; } = [];

    /// <summary>
    /// Tail ends of opened strips — less than one full pack left. They expire
    /// where they sit unless someone pushes them.
    /// </summary>
    public ObservableCollection<Batch> PartPacks { get; } = [];
    public ObservableCollection<GstSlab> GstSummary { get; } = [];
    public ObservableCollection<H1RegisterEntry> H1Register { get; } = [];
    public ObservableCollection<Batch> StockRegister { get; } = [];

    // ── Diagnostics (optional module — empty when it is switched off) ─────
    public ObservableCollection<DiagnosticBill> DiagnosticsTodayBills { get; } = [];
    public ObservableCollection<DiagnosticsRevenueRow> DiagnosticsRevenue { get; } = [];
    public ObservableCollection<DiagnosticsTestFrequencyRow> DiagnosticsTopTests { get; } = [];
    [ObservableProperty] private decimal _diagnosticsTodayTotal;
    [ObservableProperty] private bool _diagnosticsEnabled;

    public int[] ExpiringDayOptions { get; } = [30, 60, 90, 180];

    /// <summary>The seven Reports tabs, in the exact order they appear in
    /// ReportsView.xaml. SelectedTabIndex (bound to TabControl.SelectedIndex) is
    /// looked up through this array rather than a positional switch, so inserting,
    /// removing or reordering a tab can never point an export command at the
    /// wrong report — only this list has to change to match the XAML.</summary>
    /// <summary>
    /// Must match the order of the tabs in ReportsView, position for position —
    /// the selected index is looked up here to decide what Export writes.
    /// Part packs and Stock to reconcile have no export of their own, so they
    /// hold their places with None rather than shifting everything after them.
    /// </summary>
    private static readonly ReportKind[] TabOrder =
    [
        ReportKind.DayBook,
        ReportKind.GstSummary,
        ReportKind.OpdRegister,
        ReportKind.ExpiringSoon,
        ReportKind.None,            // Part packs
        ReportKind.None,            // Stock to reconcile
        ReportKind.LowStock,
        ReportKind.StockRegister,
        ReportKind.ScheduleH1,
        ReportKind.None            // Diagnostics — no PDF/Excel export in v1
    ];

    // ── Filters ────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private Sale? _selectedSale;
    [ObservableProperty] private string _billSearch = "";
    [ObservableProperty] private Visit? _selectedVisit;
    [ObservableProperty] private DiagnosticBill? _selectedDiagnosticsBill;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today;
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private int _expiringDays = 90;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _includeZeroStock;
    [ObservableProperty] private string _stockSearch = "";

    // ── Summary cards (day-based) ─────────────────────────────────────────
    [ObservableProperty] private decimal _totalCollected;
    [ObservableProperty] private decimal _cashTotal;
    [ObservableProperty] private decimal _upiTotal;
    [ObservableProperty] private decimal _consultationTotal;
    [ObservableProperty] private int _visitCount;
    [ObservableProperty] private string _status = "";

    // ── Day book totals ───────────────────────────────────────────────────
    [ObservableProperty] private decimal _dayBookTaxableTotal;
    [ObservableProperty] private decimal _dayBookCgstTotal;
    [ObservableProperty] private decimal _dayBookSgstTotal;
    [ObservableProperty] private decimal _dayBookNetTotal;

    // ── GST summary grand totals (range-based) ────────────────────────────
    [ObservableProperty] private decimal _gstGrandTaxable;
    [ObservableProperty] private decimal _gstGrandCgst;
    [ObservableProperty] private decimal _gstGrandSgst;
    [ObservableProperty] private decimal _gstGrandTotal;

    // ── Schedule H1 totals (range-based) ──────────────────────────────────
    [ObservableProperty] private int _h1TotalQuantity;

    // ── Stock register totals (live snapshot) ─────────────────────────────
    [ObservableProperty] private int _stockTotalProducts;
    [ObservableProperty] private int _stockTotalBatches;
    [ObservableProperty] private int _stockTotalUnits;
    [ObservableProperty] private decimal _stockTotalCostValue;
    [ObservableProperty] private decimal _stockTotalMrpValue;

    public async Task LoadAsync()
    {
        DiagnosticsEnabled = (await settings.GetGeneralAsync()).DiagnosticsEnabled;

        await LoadDayAsync();
        await LoadRangeAsync();
        await LoadStockRegisterAsync();
        if (DiagnosticsEnabled) await LoadDiagnosticsReportAsync();
        OnPropertyChanged(nameof(Subtitle));
    }

    /// <summary>Today's bills off the Date picker; revenue-by-day and the most
    /// frequently ordered tests off the From/To range — the same split the
    /// day book and the GST summary already use.</summary>
    private async Task LoadDiagnosticsReportAsync()
    {
        var todays = (await diagnostics.SearchBillsAsync(Date, Date))
            .OrderByDescending(b => b.BillDate).ToList();

        DiagnosticsTodayBills.Clear();
        foreach (var b in todays) DiagnosticsTodayBills.Add(b);
        DiagnosticsTodayTotal = todays.Sum(b => b.FinalAmount);

        var from = FromDate <= ToDate ? FromDate : ToDate;
        var to = FromDate <= ToDate ? ToDate : FromDate;
        var ranged = await diagnostics.SearchBillsAsync(from, to);

        DiagnosticsRevenue.Clear();
        foreach (var day in ranged.GroupBy(b => b.BillDate.Date).OrderByDescending(g => g.Key))
            DiagnosticsRevenue.Add(new DiagnosticsRevenueRow(day.Key, day.Count(), day.Sum(b => b.FinalAmount)));

        DiagnosticsTopTests.Clear();
        foreach (var test in ranged.SelectMany(b => b.Items)
                     .GroupBy(i => i.TestName)
                     .OrderByDescending(g => g.Sum(i => i.Quantity))
                     .Take(15))
            DiagnosticsTopTests.Add(new DiagnosticsTestFrequencyRow(
                test.Key, test.Sum(i => i.Quantity), test.Sum(i => i.Amount)));
    }

    partial void OnDateChanged(DateTime value)
    {
        LoadDayAsync().Forget("Loading reports");
        if (DiagnosticsEnabled) LoadDiagnosticsReportAsync().Forget("Loading diagnostics report");
    }

    partial void OnFromDateChanged(DateTime value)
    {
        LoadRangeAsync().Forget("Loading GST/H1 range");
        if (DiagnosticsEnabled) LoadDiagnosticsReportAsync().Forget("Loading diagnostics report");
    }

    partial void OnToDateChanged(DateTime value)
    {
        LoadRangeAsync().Forget("Loading GST/H1 range");
        if (DiagnosticsEnabled) LoadDiagnosticsReportAsync().Forget("Loading diagnostics report");
    }
    partial void OnExpiringDaysChanged(int value) => LoadExpiringAsync().Forget("Loading expiring stock");
    partial void OnIncludeZeroStockChanged(bool value) => LoadStockRegisterAsync().Forget("Loading stock register");
    partial void OnStockSearchChanged(string value) => ApplyStockSearch();

    partial void OnSelectedTabIndexChanged(int value) => NotifyExportCanExecute();

    /// <summary>Everything driven by the single Date picker: cards, day book, OPD
    /// register, expiring soon and low stock.</summary>
    private async Task LoadDayAsync()
    {
        // Day book must only ever show completed bills — a cancelled or returned
        // sale is not revenue, and leaving it in would double count against the
        // summary cards (which already exclude it).
        var completed = (await pharmacy.GetSalesAsync(Date))
            .Where(s => s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.BillDate)
            .ToList();

        Sales.Clear();
        foreach (var s in completed) Sales.Add(s);

        var visits = await opd.GetVisitsAsync(Date);
        Visits.Clear();
        foreach (var v in visits) Visits.Add(v);

        TotalCollected = completed.Sum(s => s.NetAmount);
        CashTotal = completed.Where(s => s.PaymentMode == PaymentMode.Cash).Sum(s => s.NetAmount);
        UpiTotal = completed.Where(s => s.PaymentMode == PaymentMode.Upi).Sum(s => s.NetAmount);

        DayBookTaxableTotal = completed.Sum(s => s.TaxableAmount);
        DayBookCgstTotal = completed.Sum(s => s.CgstAmount);
        DayBookSgstTotal = completed.Sum(s => s.SgstAmount);
        DayBookNetTotal = TotalCollected;

        ConsultationTotal = visits.Where(v => v.FeePaid).Sum(v => v.Fee);
        VisitCount = visits.Count(v => v.Status != VisitStatus.Cancelled);

        await LoadExpiringAsync();

        LowStock.Clear();
        foreach (var p in await pharmacy.GetLowStockAsync()) LowStock.Add(p);

        OnPropertyChanged(nameof(Subtitle));
        NotifyExportCanExecute();
    }

    private async Task LoadExpiringAsync()
    {
        Expiring.Clear();
        foreach (var b in await pharmacy.GetExpiringAsync(ExpiringDays)) Expiring.Add(new ExpiringRow(b));
        NotifyExportCanExecute();
    }

    /// <summary>GST summary and the Schedule H1 register, over the From/To range.</summary>
    private async Task LoadRangeAsync()
    {
        var from = FromDate <= ToDate ? FromDate : ToDate;
        var to = FromDate <= ToDate ? ToDate : FromDate;

        var completed = (await pharmacy.GetSalesAsync(from, to))
            .Where(s => s.Status == SaleStatus.Completed)
            .ToList();

        GstSummary.Clear();
        foreach (var slab in completed
                     .SelectMany(s => s.Items)
                     .GroupBy(i => i.GstRate)
                     .OrderBy(g => g.Key))
        {
            var taxable = slab.Sum(i => i.TaxableAmount);
            var gst = slab.Sum(i => i.GstAmount);
            var half = Math.Round(gst / 2m, 2, MidpointRounding.AwayFromZero);
            GstSummary.Add(new GstSlab(slab.Key, taxable, gst - half, half, taxable + gst));
        }

        GstGrandTaxable = GstSummary.Sum(g => g.Taxable);
        GstGrandCgst = GstSummary.Sum(g => g.Cgst);
        GstGrandSgst = GstSummary.Sum(g => g.Sgst);
        GstGrandTotal = GstSummary.Sum(g => g.Total);

        H1Register.Clear();
        foreach (var h in await pharmacy.GetH1RegisterAsync(from, to)) H1Register.Add(h);
        H1TotalQuantity = H1Register.Sum(h => h.Quantity);

        ToReconcile.Clear();
        foreach (var b in await pharmacy.GetProvisionalBatchesAsync()) ToReconcile.Add(b);

        PartPacks.Clear();
        foreach (var b in await pharmacy.GetPartPacksAsync()) PartPacks.Add(b);

        NotifyExportCanExecute();
    }

    /// <summary>Every batch currently on the shelf — a live snapshot, not tied to
    /// the Date/From/To pickers. Reloaded whenever "Include Zero Stock" flips;
    /// the search box then just re-filters the already-loaded rows in memory.</summary>
    private List<Batch> _stockRegisterRaw = [];

    private async Task LoadStockRegisterAsync()
    {
        _stockRegisterRaw = await pharmacy.GetAllBatchesAsync(IncludeZeroStock);
        ApplyStockSearch();
    }

    private void ApplyStockSearch()
    {
        StockRegister.Clear();
        foreach (var b in _stockRegisterRaw.Where(b => StockRegisterFilter.Matches(b, StockSearch)))
            StockRegister.Add(b);

        var summary = StockSummary.From(StockRegister);
        StockTotalProducts = summary.TotalProducts;
        StockTotalBatches = summary.TotalBatches;
        StockTotalUnits = summary.TotalUnits;
        StockTotalCostValue = summary.TotalCostValue;
        StockTotalMrpValue = summary.TotalMrpValue;

        NotifyExportCanExecute();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    /// <summary>
    /// Looks a bill up across every date. A walk-in customer coming back for a
    /// copy rarely remembers which day they bought on, only the name or the number.
    /// </summary>
    [RelayCommand]
    private async Task FindBillAsync()
    {
        if (string.IsNullOrWhiteSpace(BillSearch))
        {
            await LoadAsync();
            return;
        }

        Sales.Clear();
        foreach (var s in await pharmacy.SearchSalesAsync(BillSearch)) Sales.Add(s);

        Status = Sales.Count == 0
            ? $"No bill matches '{BillSearch}'."
            : $"{Sales.Count} bill(s) matching '{BillSearch}', across all dates.";
    }

    /// <summary>Reprints a bill from the day book, marked as a duplicate.</summary>
    [RelayCommand]
    private async Task ReprintBillAsync()
    {
        if (SelectedSale is null) return;

        var sale = await pharmacy.GetSaleAsync(SelectedSale.Id);
        if (sale is null) return;

        var pharmacyProfile = await settings.GetPharmacyAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => BillPrinter.Build(sale, pharmacyProfile, theme, isReprint: true),
                             $"Bill {sale.BillNo} (duplicate)");
    }

    [RelayCommand]
    private async Task ReprintReceiptAsync()
    {
        if (SelectedVisit is null || !SelectedVisit.FeePaid) return;

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);
        if (visit is null) return;

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(visit, clinic, theme, isReprint: true),
                             $"Receipt {visit.FeeReceiptNo} (duplicate)");
    }

    /// <summary>Reprints a diagnostic bill from Today's Diagnostic Bills,
    /// marked as a duplicate — same shape as <see cref="ReprintBillAsync"/>.</summary>
    [RelayCommand]
    private async Task ReprintDiagnosticBillAsync()
    {
        if (SelectedDiagnosticsBill is null) return;

        var bill = await diagnostics.GetBillAsync(SelectedDiagnosticsBill.Id);
        if (bill is null) return;

        var clinic = await settings.GetClinicAsync();
        var theme = await settings.GetDocumentThemeAsync();
        PrintService.Preview(() => DiagnosticBillPrinter.Build(bill, clinic, theme, isReprint: true),
                             $"Bill {bill.BillNo} (duplicate)");
    }

    // ── Export ─────────────────────────────────────────────────────────────

    private (ReportKind Kind, bool HasData) CurrentReport()
    {
        var kind = TabOrder[Math.Clamp(SelectedTabIndex, 0, TabOrder.Length - 1)];

        var hasData = kind switch
        {
            ReportKind.DayBook => Sales.Count > 0,
            ReportKind.GstSummary => GstSummary.Count > 0,
            ReportKind.OpdRegister => Visits.Count > 0,
            ReportKind.ExpiringSoon => Expiring.Count > 0,
            ReportKind.LowStock => LowStock.Count > 0,
            ReportKind.StockRegister => StockRegister.Count > 0,
            ReportKind.ScheduleH1 => H1Register.Count > 0,
            _ => false
        };

        return (kind, hasData);
    }

    // Stock Register is Excel-only by design (see ReportPdfBuilder) — the PDF
    // button disables on that tab rather than exporting a wide analysis table
    // as an unreadable printout.
    private bool CanExportPdf() => CurrentReport() is { HasData: true, Kind: not ReportKind.StockRegister };
    private bool CanExportExcel() => CurrentReport().HasData;

    private void NotifyExportCanExecute()
    {
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanExportPdf))]
    private async Task ExportPdfAsync()
    {
        var (kind, hasData) = CurrentReport();
        if (!hasData)
        {
            Dialog.Show("No data available to export.", "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = ReportNaming.FileName(kind, Date, FromDate, ToDate, "pdf"),
            Filter = "PDF file (*.pdf)|*.pdf",
            DefaultExt = "pdf"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var shopName = (await settings.GetClinicAsync()).Name;
            ReportPdfBuilder.Build(kind, this, shopName).GeneratePdf(dialog.FileName);
            AppLog.Info($"Exported {kind} report to PDF: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("PDF export failed.", ex);
            Dialog.Show($"Could not create the PDF.\n\n{ex.Message}", "Reports", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportExcel))]
    private async Task ExportExcelAsync()
    {
        var (kind, hasData) = CurrentReport();
        if (!hasData)
        {
            Dialog.Show("No data available to export.", "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = ReportNaming.FileName(kind, Date, FromDate, ToDate, "xlsx"),
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var shopName = (await settings.GetClinicAsync()).Name;
            ReportExcelBuilder.Build(kind, this, shopName, dialog.FileName);
            AppLog.Info($"Exported {kind} report to Excel: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Excel export failed.", ex);
            Dialog.Show($"Could not create the workbook.\n\n{ex.Message}", "Reports", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
