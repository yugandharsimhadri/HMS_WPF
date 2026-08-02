using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Charts;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One row of the mixed OPD/Pharmacy/Diagnostics activity feed.</summary>
public record DashboardActivityRow(DateTime When, string BillNo, string PatientName, string Department, decimal Amount);

/// <summary>
/// The landing screen: what a doctor or owner would want to see in the first
/// five seconds — today's numbers, which way revenue is moving, where it's
/// coming from, what needs restocking, and what just happened. Nothing here
/// is a destination in its own right; every figure already has a real screen
/// behind it (OPD, Reports, Inventory) for the detail.
///
/// Line and donut shapes only, deliberately — a dashboard read at a glance
/// wants "is this going up" and "how does it split", which a line and a
/// donut answer directly; a bar-per-day grid asks the eye to compare heights
/// across seven separate marks first.
/// </summary>
public partial class DashboardViewModel(
    OpdService opd, PharmacyService pharmacy, DiagnosticsService diagnostics, SettingsService settings)
    : ObservableObject, IPage
{
    public string Title => "Dashboard";
    public string Subtitle => DateTime.Today.ToString("dddd, d MMMM yyyy");

    private const int TrendDays = 14;

    // ── KPI tiles ──────────────────────────────────────────────────────────
    [ObservableProperty] private int _patientsToday;

    [NotifyPropertyChangedFor(nameof(PatientsDeltaLabel))]
    [NotifyPropertyChangedFor(nameof(PatientsDeltaIsUp))]
    [ObservableProperty] private double _patientsDeltaPercent;

    [ObservableProperty] private int _inQueueNow;
    [ObservableProperty] private decimal _revenueToday;

    [NotifyPropertyChangedFor(nameof(RevenueDeltaLabel))]
    [NotifyPropertyChangedFor(nameof(RevenueDeltaIsUp))]
    [ObservableProperty] private double _revenueDeltaPercent;

    [ObservableProperty] private int _lowStockCount;

    /// <summary>"▲ 8%" or "▼ 5%" — the direction reads from the glyph, not
    /// colour alone, since colour here is decorative reinforcement.</summary>
    public string PatientsDeltaLabel => FormatDelta(PatientsDeltaPercent);
    public bool PatientsDeltaIsUp => PatientsDeltaPercent >= 0;
    public string RevenueDeltaLabel => FormatDelta(RevenueDeltaPercent);
    public bool RevenueDeltaIsUp => RevenueDeltaPercent >= 0;

    private static string FormatDelta(double percent) => $"{(percent >= 0 ? "▲" : "▼")} {Math.Abs(percent):0}%";

    // ── Today's revenue split (donut) ────────────────────────────────────
    [ObservableProperty] private bool _diagnosticsEnabled;
    [ObservableProperty] private decimal _opdRevenueToday;
    [ObservableProperty] private decimal _pharmacyRevenueToday;
    [ObservableProperty] private decimal _diagnosticsRevenueToday;
    [ObservableProperty] private string _opdSegmentPath = "";
    [ObservableProperty] private string _pharmacySegmentPath = "";
    [ObservableProperty] private string _diagnosticsSegmentPath = "";
    [ObservableProperty] private int _opdPercent;
    [ObservableProperty] private int _pharmacyPercent;
    [ObservableProperty] private int _diagnosticsPercent;

    // ── Revenue trend — one line per category, sharing one scale so the
    // three are honestly comparable rather than each stretched to its own
    // range. Colors match the donut and its legend exactly. ────────────────
    [ObservableProperty] private string _opdTrendLine = "";
    [ObservableProperty] private double _opdTrendEndX;
    [ObservableProperty] private double _opdTrendEndY;
    [ObservableProperty] private string _pharmacyTrendLine = "";
    [ObservableProperty] private double _pharmacyTrendEndX;
    [ObservableProperty] private double _pharmacyTrendEndY;
    [ObservableProperty] private string _diagnosticsTrendLine = "";
    [ObservableProperty] private double _diagnosticsTrendEndX;
    [ObservableProperty] private double _diagnosticsTrendEndY;
    [ObservableProperty] private decimal _revenueTrendTotal;

    public ObservableCollection<DashboardActivityRow> RecentActivity { get; } = [];
    public ObservableCollection<Product> LowStock { get; } = [];

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    public async Task LoadAsync()
    {
        DiagnosticsEnabled = (await settings.GetGeneralAsync()).DiagnosticsEnabled;

        var from = DateTime.Today.AddDays(-(TrendDays - 1));
        var to = DateTime.Today;

        // Three range queries, not fourteen daily ones — the same shape
        // ReportsViewModel already uses for its own GST-summary and
        // diagnostics-revenue ranges.
        var salesRange = await pharmacy.GetSalesAsync(from, to);
        var visitsRange = await opd.GetVisitsAsync(from, to);
        var diagRange = DiagnosticsEnabled ? await diagnostics.SearchBillsAsync(from, to) : [];

        var dailyOpd = new List<decimal>(TrendDays);
        var dailyPharmacy = new List<decimal>(TrendDays);
        var dailyDiag = new List<decimal>(TrendDays);
        var dailyTotals = new List<decimal>(TrendDays);
        var dailyPatients = new List<int>(TrendDays);
        decimal todayOpd = 0, todayPharmacy = 0, todayDiag = 0;
        decimal yesterdayTotal = 0;
        int todayPatients = 0, yesterdayPatients = 0;

        for (var i = 0; i < TrendDays; i++)
        {
            var day = from.AddDays(i).Date;

            var dayPharmacy = salesRange
                .Where(s => s.Status == SaleStatus.Completed && s.BillDate.Date == day)
                .Sum(s => s.NetAmount);

            var dayOpd = visitsRange
                .Where(v => v.FeePaid && v.FeePaidOn?.Date == day)
                .Sum(v => v.Fee);

            var dayDiag = diagRange.Where(b => b.BillDate.Date == day).Sum(b => b.FinalAmount);

            var dayPatients = visitsRange.Count(v => v.ScheduledOn.Date == day && v.Status != VisitStatus.Cancelled);

            dailyOpd.Add(dayOpd);
            dailyPharmacy.Add(dayPharmacy);
            dailyDiag.Add(dayDiag);
            dailyTotals.Add(dayPharmacy + dayOpd + dayDiag);
            dailyPatients.Add(dayPatients);

            if (day == to) { todayOpd = dayOpd; todayPharmacy = dayPharmacy; todayDiag = dayDiag; todayPatients = dayPatients; }
            if (day == to.AddDays(-1)) { yesterdayTotal = dayPharmacy + dayOpd + dayDiag; yesterdayPatients = dayPatients; }
        }

        RevenueToday = todayOpd + todayPharmacy + todayDiag;
        RevenueDeltaPercent = DeltaPercent((double)RevenueToday, (double)yesterdayTotal);
        RevenueTrendTotal = dailyTotals.Sum();

        PatientsToday = todayPatients;
        PatientsDeltaPercent = DeltaPercent(todayPatients, yesterdayPatients);

        OpdRevenueToday = todayOpd;
        PharmacyRevenueToday = todayPharmacy;
        DiagnosticsRevenueToday = todayDiag;
        BuildDonut();

        // One shared scale across all three lines — each stretched to its
        // own range would make a tiny category look exactly as busy as the
        // whole clinic, which is precisely the thing a "which department is
        // moving" chart must not do.
        const double w = 100, h = 100;
        var sharedMax = (double)new[] { dailyOpd.DefaultIfEmpty(0).Max(), dailyPharmacy.DefaultIfEmpty(0).Max(), dailyDiag.DefaultIfEmpty(0).Max() }.Max();

        OpdTrendLine = ChartGeometry.Trend(dailyOpd.Select(v => (double)v).ToList(), w, h, 0, sharedMax).Line;
        (OpdTrendEndX, OpdTrendEndY) = ChartGeometry.TrendEndPoint(dailyOpd.Select(v => (double)v).ToList(), w, h, 0, sharedMax);

        PharmacyTrendLine = ChartGeometry.Trend(dailyPharmacy.Select(v => (double)v).ToList(), w, h, 0, sharedMax).Line;
        (PharmacyTrendEndX, PharmacyTrendEndY) = ChartGeometry.TrendEndPoint(dailyPharmacy.Select(v => (double)v).ToList(), w, h, 0, sharedMax);

        DiagnosticsTrendLine = ChartGeometry.Trend(dailyDiag.Select(v => (double)v).ToList(), w, h, 0, sharedMax).Line;
        (DiagnosticsTrendEndX, DiagnosticsTrendEndY) = ChartGeometry.TrendEndPoint(dailyDiag.Select(v => (double)v).ToList(), w, h, 0, sharedMax);

        // Today's own richer query — includes Patient, which the range query
        // above deliberately skips since a trend never shows a name.
        var todaysVisits = await opd.GetVisitsAsync(DateTime.Today);
        InQueueNow = todaysVisits.Count(v => v.Status is VisitStatus.Booked or VisitStatus.Waiting or VisitStatus.InConsultation);

        var lowStock = await pharmacy.GetLowStockAsync();
        LowStockCount = lowStock.Count;
        LowStock.Clear();
        foreach (var p in lowStock.Take(5)) LowStock.Add(p);

        BuildActivityFeed(todaysVisits, salesRange.Where(s => s.BillDate.Date == to), diagRange.Where(b => b.BillDate.Date == to));

        OnPropertyChanged(nameof(Subtitle));
    }

    private void BuildDonut()
    {
        var total = OpdRevenueToday + PharmacyRevenueToday + DiagnosticsRevenueToday;

        if (total <= 0)
        {
            OpdSegmentPath = PharmacySegmentPath = DiagnosticsSegmentPath = "";
            OpdPercent = PharmacyPercent = DiagnosticsPercent = 0;
            return;
        }

        const double cx = 62, cy = 62, rOuter = 58, rInner = 36, gapDeg = 2.2;
        double angle = -90;

        (string Path, int Pct) Segment(decimal value)
        {
            if (value <= 0) return ("", 0);

            var sweep = (double)(value / total) * 360;
            var start = angle;
            var end = angle + sweep - gapDeg;
            angle += sweep;

            return (ChartGeometry.DonutSegment(cx, cy, rOuter, rInner, start, Math.Max(end, start + 0.5)),
                    (int)Math.Round((double)(value / total) * 100));
        }

        (OpdSegmentPath, OpdPercent) = Segment(OpdRevenueToday);
        (PharmacySegmentPath, PharmacyPercent) = Segment(PharmacyRevenueToday);
        (DiagnosticsSegmentPath, DiagnosticsPercent) = DiagnosticsEnabled ? Segment(DiagnosticsRevenueToday) : ("", 0);
    }

    private void BuildActivityFeed(
        IEnumerable<Visit> todaysVisits, IEnumerable<Sale> todaysSales, IEnumerable<DiagnosticBill> todaysDiagBills)
    {
        var rows = new List<DashboardActivityRow>();

        rows.AddRange(todaysVisits
            .Where(v => v.FeePaid)
            .Select(v => new DashboardActivityRow(v.FeePaidOn ?? v.ScheduledOn, v.FeeReceiptNo ?? "", v.Patient.Name, "OPD", v.Fee)));

        rows.AddRange(todaysSales
            .Where(s => s.Status == SaleStatus.Completed)
            .Select(s => new DashboardActivityRow(s.BillDate, s.BillNo, s.CustomerName, "Pharmacy", s.NetAmount)));

        rows.AddRange(todaysDiagBills
            .Select(b => new DashboardActivityRow(b.BillDate, b.BillNo, b.PatientName, "Diagnostics", b.FinalAmount)));

        RecentActivity.Clear();
        foreach (var row in rows.OrderByDescending(r => r.When).Take(8)) RecentActivity.Add(row);
    }

    private static double DeltaPercent(double today, double yesterday)
    {
        if (yesterday <= 0) return today > 0 ? 100 : 0;
        return Math.Round((today - yesterday) / yesterday * 100, 0);
    }
}
