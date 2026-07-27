using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One row of the GST summary that appears on a tax invoice and in GSTR-1.</summary>
public record GstSlab(decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Total);

public partial class ReportsViewModel(
    PharmacyService pharmacy,
    OpdService opd,
    SettingsService settings,
    IDbContextFactory<AppDbContext> factory) : ObservableObject, IPage
{
    public string Title => "Reports";
    public string Subtitle => $"{Sales.Count} bill(s) on {Date:ddd, dd MMM} · ₹{TotalCollected:0.00}";

    public ObservableCollection<Sale> Sales { get; } = [];
    public ObservableCollection<Visit> Visits { get; } = [];
    public ObservableCollection<Batch> Expiring { get; } = [];
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

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private Sale? _selectedSale;
    [ObservableProperty] private string _billSearch = "";
    [ObservableProperty] private Visit? _selectedVisit;
    [ObservableProperty] private decimal _totalCollected;
    [ObservableProperty] private decimal _cashTotal;
    [ObservableProperty] private decimal _upiTotal;
    [ObservableProperty] private decimal _consultationTotal;
    [ObservableProperty] private int _visitCount;
    [ObservableProperty] private string _status = "";

    public async Task LoadAsync()
    {
        Sales.Clear();
        foreach (var s in await pharmacy.GetSalesAsync(Date)) Sales.Add(s);

        Visits.Clear();
        foreach (var v in await opd.GetVisitsAsync(Date)) Visits.Add(v);

        // Aggregated in memory: a day's worth of clinic data is tiny, and this
        // keeps decimal arithmetic in .NET rather than in SQLite.
        var completed = Sales.Where(s => s.Status == SaleStatus.Completed).ToList();
        TotalCollected = completed.Sum(s => s.NetAmount);
        CashTotal = completed.Where(s => s.PaymentMode == PaymentMode.Cash).Sum(s => s.NetAmount);
        UpiTotal = completed.Where(s => s.PaymentMode == PaymentMode.Upi).Sum(s => s.NetAmount);
        ConsultationTotal = Visits.Where(v => v.FeePaid).Sum(v => v.Fee);
        VisitCount = Visits.Count(v => v.Status != VisitStatus.Cancelled);

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

        Expiring.Clear();
        foreach (var b in await pharmacy.GetExpiringAsync(90)) Expiring.Add(b);

        LowStock.Clear();
        foreach (var p in await pharmacy.GetLowStockAsync()) LowStock.Add(p);

        ToReconcile.Clear();
        foreach (var b in await pharmacy.GetProvisionalBatchesAsync()) ToReconcile.Add(b);

        PartPacks.Clear();
        foreach (var b in await pharmacy.GetPartPacksAsync()) PartPacks.Add(b);

        await using var db = await factory.CreateDbContextAsync();
        var from = Date.Date;
        var to = from.AddDays(1);

        H1Register.Clear();
        foreach (var h in await db.H1Register
                     .Where(h => h.SoldOn >= from && h.SoldOn < to)
                     .OrderBy(h => h.SoldOn)
                     .ToListAsync())
        {
            H1Register.Add(h);
        }

        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnDateChanged(DateTime value) => LoadAsync().Forget("Loading reports");

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

        var shop = await settings.GetAsync();
        PrintService.Preview(() => BillPrinter.Build(sale, shop, isReprint: true),
                             $"Bill {sale.BillNo} (duplicate)");
    }

    [RelayCommand]
    private async Task ReprintReceiptAsync()
    {
        if (SelectedVisit is null || !SelectedVisit.FeePaid) return;

        var visit = await opd.GetVisitAsync(SelectedVisit.Id);
        if (visit is null) return;

        var shop = await settings.GetAsync();
        PrintService.Preview(() => FeeReceiptDocument.Build(visit, shop, isReprint: true),
                             $"Receipt {visit.FeeReceiptNo} (duplicate)");
    }
}
