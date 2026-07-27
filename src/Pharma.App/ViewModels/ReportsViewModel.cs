using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Pharma.App.Reports;
using Pharma.Core;
using Pharma.Data;
using QuestPDF.Fluent;

namespace Pharma.App.ViewModels;

/// <summary>One row of the GST summary that appears on a tax invoice and in GSTR-1.</summary>
public record GstSlab(decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Total);

/// <summary>A batch on the Expiring Soon report, with the day count worked out once for display.</summary>
public class ExpiringRow(Batch batch)
{
    public Batch Batch { get; } = batch;
    public int DaysRemaining { get; } = (batch.ExpiryDate.Date - DateTime.Today).Days;
    public bool IsExpired => DaysRemaining < 0;
}

public partial class ReportsViewModel(
    PharmacyService pharmacy,
    OpdService opd,
    SettingsService settings) : ObservableObject, IPage
{
    public string Title => "Reports";
    public string Subtitle => $"{Sales.Count} bill(s) on {Date:ddd, dd MMM} · ₹{TotalCollected:0.00}";

    public ObservableCollection<Sale> Sales { get; } = [];
    public ObservableCollection<Visit> Visits { get; } = [];
    public ObservableCollection<ExpiringRow> Expiring { get; } = [];
    public ObservableCollection<Product> LowStock { get; } = [];
    public ObservableCollection<GstSlab> GstSummary { get; } = [];
    public ObservableCollection<H1RegisterEntry> H1Register { get; } = [];
    public ObservableCollection<Batch> StockRegister { get; } = [];

    public int[] ExpiringDayOptions { get; } = [30, 60, 90, 180];

    /// <summary>The seven Reports tabs, in the exact order they appear in
    /// ReportsView.xaml. SelectedTabIndex (bound to TabControl.SelectedIndex) is
    /// looked up through this array rather than a positional switch, so inserting,
    /// removing or reordering a tab can never point an export command at the
    /// wrong report — only this list has to change to match the XAML.</summary>
    private static readonly ReportKind[] TabOrder =
    [
        ReportKind.DayBook,
        ReportKind.GstSummary,
        ReportKind.OpdRegister,
        ReportKind.ExpiringSoon,
        ReportKind.LowStock,
        ReportKind.StockRegister,
        ReportKind.ScheduleH1
    ];

    // ── Filters ────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _date = DateTime.Today;
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
        await LoadDayAsync();
        await LoadRangeAsync();
        await LoadStockRegisterAsync();
        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnDateChanged(DateTime value) => LoadDayAsync().Forget("Loading reports");
    partial void OnFromDateChanged(DateTime value) => LoadRangeAsync().Forget("Loading GST/H1 range");
    partial void OnToDateChanged(DateTime value) => LoadRangeAsync().Forget("Loading GST/H1 range");
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
            MessageBox.Show("No data available to export.", "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var shop = await settings.GetAsync();
            ReportPdfBuilder.Build(kind, this, shop).GeneratePdf(dialog.FileName);
            AppLog.Info($"Exported {kind} report to PDF: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("PDF export failed.", ex);
            MessageBox.Show($"Could not create the PDF.\n\n{ex.Message}", "Reports", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportExcel))]
    private async Task ExportExcelAsync()
    {
        var (kind, hasData) = CurrentReport();
        if (!hasData)
        {
            MessageBox.Show("No data available to export.", "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var shop = await settings.GetAsync();
            ReportExcelBuilder.Build(kind, this, shop, dialog.FileName);
            AppLog.Info($"Exported {kind} report to Excel: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Excel export failed.", ex);
            MessageBox.Show($"Could not create the workbook.\n\n{ex.Message}", "Reports", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
