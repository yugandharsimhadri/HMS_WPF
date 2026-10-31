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
    OpdService opd, PharmacyService pharmacy, DiagnosticsService diagnostics, SettingsService settings,
    ProcedureBillsService procedureBills, DentistService dentist, PathologyLabService pathologyLab)
    : ObservableObject, IPage
{
    public string Title => "Dashboard";
    public string Subtitle => IsToday ? DateTime.Today.ToString("dddd, d MMMM yyyy") : RangeText;

    /// <summary>The shortest stretch the trend line is drawn over. A line
    /// through one or two points says nothing, so a short range is widened
    /// backwards to this many days for the chart only - the figures above it
    /// stay strictly the chosen range.</summary>
    private const int MinTrendDays = 14;

    // -- The period ---------------------------------------------------------
    //
    // Today to today, which is exactly what this screen always showed, so a
    // clinic that never touches the pickers sees no change at all.
    [ObservableProperty] private DateTime _fromDate = DateTime.Today;
    [ObservableProperty] private DateTime _toDate = DateTime.Today;

    private bool _loadingRange;

    /// <summary>The range, lowest first. The pickers do not stop From being set
    /// after To, and a reversed range would quietly total nothing.</summary>
    private (DateTime From, DateTime To) Range =>
        FromDate <= ToDate ? (FromDate.Date, ToDate.Date) : (ToDate.Date, FromDate.Date);

    private int RangeDays => (Range.To - Range.From).Days + 1;
    private bool IsToday => Range.From == DateTime.Today && Range.To == DateTime.Today;

    private string RangeText
    {
        get
        {
            var (from, to) = Range;
            return from == to ? $"{from:dd MMM yyyy}" : $"{from:dd MMM yyyy} to {to:dd MMM yyyy}";
        }
    }

    /// <summary>"today" for the default view, otherwise the dates themselves.</summary>
    public string PeriodShort => IsToday ? "today" : RangeText;
    public string PatientsLabel => IsToday ? "Patients today" : $"Patients · {RangeText}";
    public string RevenueLabel => IsToday ? "Revenue today" : $"Revenue · {RangeText}";
    public string SplitTitle => IsToday ? "Today's revenue split" : "Revenue split";
    public string SplitSubtitle => IsToday ? "Where today's collection came from" : $"Where the collection came from, {RangeText}";

    /// <summary>What the arrows are measured against: yesterday for a single
    /// day, otherwise the stretch of the same length straight before it. A week
    /// against the week before is a fair comparison; a week against yesterday
    /// is not.</summary>
    public string ComparisonLabel => RangeDays == 1 ? " vs yesterday" : $" vs previous {RangeDays} days";

    private int TrendDaysShown => Math.Max(RangeDays, MinTrendDays);

    public string TrendCaption => RangeDays < MinTrendDays
        ? $"Last {MinTrendDays} days to {Range.To:dd MMM}"
        : $"{Range.From:dd MMM} to {Range.To:dd MMM}";

    public string TrendTotalCaption => $"{TrendDaysShown} days";

    partial void OnFromDateChanged(DateTime value) => RangeChanged();
    partial void OnToDateChanged(DateTime value) => RangeChanged();

    private void RangeChanged()
    {
        if (_loadingRange) return;
        NotifyPeriodText();
        LoadAsync().Forget("Loading the dashboard");
    }

    private void NotifyPeriodText()
    {
        foreach (var name in new[]
        {
            nameof(Subtitle), nameof(PeriodShort), nameof(PatientsLabel), nameof(RevenueLabel),
            nameof(SplitTitle), nameof(SplitSubtitle), nameof(ComparisonLabel),
            nameof(TrendCaption), nameof(TrendTotalCaption)
        }) OnPropertyChanged(name);
    }

    [RelayCommand]
    private void ResetToToday()
    {
        _loadingRange = true;
        FromDate = ToDate = DateTime.Today;
        _loadingRange = false;
        RangeChanged();
    }

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

    // Not part of the revenue donut/trend below — those stay the original
    // OPD/Pharmacy/Diagnostics picture so the KPI tile and the chart always
    // agree with each other. These three only gate what shows in the
    // Recent Activity feed, so today's Pediatrics/Dentist/Pathology Lab
    // transactions are visible on the landing screen without folding their
    // money into a total the chart does not also account for.
    [ObservableProperty] private bool _pediatricsEnabled;
    [ObservableProperty] private bool _dentistEnabled;
    [ObservableProperty] private bool _pathologyLabEnabled;
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
        var general = await settings.GetGeneralAsync();
        DiagnosticsEnabled = general.DiagnosticsEnabled;
        PediatricsEnabled = general.PediatricsEnabled;
        DentistEnabled = general.DentistEnabled;
        PathologyLabEnabled = general.PathologyLabEnabled;

        var (rangeFrom, to) = Range;
        var days = RangeDays;

        // The chart is drawn over the range, widened backwards if it is too
        // short to make a line; the comparison runs over the same length
        // straight before the range. One fetch covers whichever starts first.
        var trendFrom = to.AddDays(-(TrendDaysShown - 1));
        var prevFrom = rangeFrom.AddDays(-days);
        var prevTo = rangeFrom.AddDays(-1);
        var fetchFrom = trendFrom < prevFrom ? trendFrom : prevFrom;

        // Three range queries, not one per day - the same shape
        // ReportsViewModel already uses for its own ranges.
        var salesRange = await pharmacy.GetSalesAsync(fetchFrom, to);
        var visitsRange = await opd.GetVisitsAsync(fetchFrom, to);
        var diagRange = DiagnosticsEnabled ? await diagnostics.SearchBillsAsync(fetchFrom, to) : [];

        // Grouped by day once, so a year-long range is one pass over each list
        // rather than a scan per day.
        var pharmacyByDay = salesRange.Where(x => x.Status == SaleStatus.Completed)
            .GroupBy(x => x.BillDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.NetAmount));
        var opdByDay = visitsRange.Where(v => v.FeePaid && v.FeePaidOn is not null)
            .GroupBy(v => v.FeePaidOn!.Value.Date).ToDictionary(g => g.Key, g => g.Sum(v => v.Fee));
        var diagByDay = diagRange
            .GroupBy(x => x.BillDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.FinalAmount));
        var patientsByDay = visitsRange.Where(v => v.Status != VisitStatus.Cancelled)
            .GroupBy(v => v.ScheduledOn.Date).ToDictionary(g => g.Key, g => g.Count());

        static T On<T>(Dictionary<DateTime, T> map, DateTime day) => map.TryGetValue(day, out var v) ? v : default!;

        var dailyOpd = new List<decimal>();
        var dailyPharmacy = new List<decimal>();
        var dailyDiag = new List<decimal>();
        var dailyTotals = new List<decimal>();

        for (var day = trendFrom; day <= to; day = day.AddDays(1))
        {
            var o = On(opdByDay, day);
            var ph = On(pharmacyByDay, day);
            var d = On(diagByDay, day);

            dailyOpd.Add(o);
            dailyPharmacy.Add(ph);
            dailyDiag.Add(d);
            dailyTotals.Add(o + ph + d);
        }

        decimal periodOpd = 0, periodPharmacy = 0, periodDiag = 0, previousTotal = 0;
        int periodPatients = 0, previousPatients = 0;

        for (var day = rangeFrom; day <= to; day = day.AddDays(1))
        {
            periodOpd += On(opdByDay, day);
            periodPharmacy += On(pharmacyByDay, day);
            periodDiag += On(diagByDay, day);
            periodPatients += On(patientsByDay, day);
        }

        for (var day = prevFrom; day <= prevTo; day = day.AddDays(1))
        {
            previousTotal += On(opdByDay, day) + On(pharmacyByDay, day) + On(diagByDay, day);
            previousPatients += On(patientsByDay, day);
        }

        RevenueToday = periodOpd + periodPharmacy + periodDiag;
        RevenueDeltaPercent = DeltaPercent((double)RevenueToday, (double)previousTotal);
        RevenueTrendTotal = dailyTotals.Sum();

        PatientsToday = periodPatients;
        PatientsDeltaPercent = DeltaPercent(periodPatients, previousPatients);

        OpdRevenueToday = periodOpd;
        PharmacyRevenueToday = periodPharmacy;
        DiagnosticsRevenueToday = periodDiag;
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

        // The feed follows the chosen range too. Only the queue above stays
        // pinned to today: who is waiting now is a fact about this minute.
        var procedureBillsInRange = PediatricsEnabled || DentistEnabled ? await procedureBills.SearchBillsAsync(rangeFrom, to) : [];
        var dentalPaymentsInRange = DentistEnabled ? await dentist.SearchPaymentsAsync(rangeFrom, to) : [];
        var labOrdersInRange = PathologyLabEnabled ? await pathologyLab.SearchOrdersAsync(rangeFrom, to) : [];
        var visitsInRange = rangeFrom == to && to == DateTime.Today
            ? todaysVisits
            : await opd.GetVisitsAsync(rangeFrom, to);

        BuildActivityFeed(
            visitsInRange,
            salesRange.Where(x => x.BillDate.Date >= rangeFrom && x.BillDate.Date <= to),
            diagRange.Where(x => x.BillDate.Date >= rangeFrom && x.BillDate.Date <= to),
            procedureBillsInRange, dentalPaymentsInRange, labOrdersInRange);

        NotifyPeriodText();
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
        IEnumerable<Visit> todaysVisits, IEnumerable<Sale> todaysSales, IEnumerable<DiagnosticBill> todaysDiagBills,
        IEnumerable<ProcedureBill> todaysProcedureBills, IEnumerable<DentalPayment> todaysDentalPayments,
        IEnumerable<LabOrder> todaysLabOrders)
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

        // A procedure bill does not carry which department raised it — see
        // ProcedureBillsService's own note — so it reads simply "Procedure"
        // rather than guessing Pediatrics or Dentist.
        rows.AddRange(todaysProcedureBills
            .Select(b => new DashboardActivityRow(b.BillDate, b.BillNo, b.PatientName, "Procedure", b.FinalAmount)));

        rows.AddRange(todaysDentalPayments
            .Select(p => new DashboardActivityRow(p.PaidOn, p.ReceiptNo, p.DentalCase.PatientName, "Dentist", p.Amount)));

        rows.AddRange(todaysLabOrders
            .Select(o => new DashboardActivityRow(o.OrderDate, o.OrderNo, o.PatientName, "Pathology Lab", o.FinalAmount)));

        RecentActivity.Clear();
        foreach (var row in rows.OrderByDescending(r => r.When).Take(8)) RecentActivity.Add(row);
    }

    private static double DeltaPercent(double today, double yesterday)
    {
        if (yesterday <= 0) return today > 0 ? 100 : 0;
        return Math.Round((today - yesterday) / yesterday * 100, 0);
    }
}
