using System.IO;

namespace Pharma.App.Reports;

public enum ReportKind
{
    /// <summary>A tab with nothing to export — it holds its place in the tab order.</summary>
    None = -1,

    DayBook,
    GstSummary,
    OpdRegister,
    ExpiringSoon,
    LowStock,
    StockRegister,
    ScheduleH1
}

/// <summary>Display name and file-naming rules shared by the PDF and Excel exporters,
/// so both always agree with what is on screen.</summary>
public static class ReportNaming
{
    public static string Title(ReportKind kind) => kind switch
    {
        ReportKind.DayBook => "Day Book",
        ReportKind.GstSummary => "GST Summary",
        ReportKind.OpdRegister => "OPD Register",
        ReportKind.ExpiringSoon => "Expiring Soon",
        ReportKind.LowStock => "Low Stock",
        ReportKind.StockRegister => "Stock Register",
        ReportKind.ScheduleH1 => "Schedule H1 Register",
        _ => "Report"
    };

    /// <summary>GST summary and the Schedule H1 register are read over a From/To range;
    /// everything else follows the single Date picker.</summary>
    public static bool IsRangeBased(ReportKind kind) => kind is ReportKind.GstSummary or ReportKind.ScheduleH1;

    public static string DateLabel(ReportKind kind, DateTime date, DateTime from, DateTime to)
    {
        // Stock Register is a live snapshot, not tied to the Date picker at all.
        if (kind == ReportKind.StockRegister) return $"As of {DateTime.Now:dd MMM yyyy, HH:mm}";

        if (!IsRangeBased(kind)) return date.ToString("dd MMM yyyy");

        var (start, end) = from <= to ? (from, to) : (to, from);
        return start.Date == end.Date
            ? start.ToString("dd MMM yyyy")
            : $"{start:dd MMM yyyy} to {end:dd MMM yyyy}";
    }

    public static string FileName(ReportKind kind, DateTime date, DateTime from, DateTime to, string extension)
    {
        var stem = kind switch
        {
            ReportKind.DayBook => "DayBook",
            ReportKind.GstSummary => "GSTSummary",
            ReportKind.OpdRegister => "OPDRegister",
            ReportKind.ExpiringSoon => "ExpiringSoon",
            ReportKind.LowStock => "LowStock",
            ReportKind.StockRegister => "StockRegister",
            ReportKind.ScheduleH1 => "ScheduleH1",
            _ => "Report"
        };

        string suffix;
        if (kind == ReportKind.StockRegister)
        {
            // A live snapshot — the filename is today's date, not whatever the
            // page's (unrelated) Date picker happens to be set to.
            suffix = DateTime.Today.ToString("yyyy-MM-dd");
        }
        else if (IsRangeBased(kind))
        {
            var (start, end) = from <= to ? (from, to) : (to, from);
            suffix = start.Date == end.Date
                ? start.ToString("yyyy-MM-dd")
                : $"{start:yyyy-MM-dd}_to_{end:yyyy-MM-dd}";
        }
        else
        {
            suffix = date.ToString("yyyy-MM-dd");
        }

        return Sanitize($"{stem}_{suffix}.{extension}");
    }

    private static string Sanitize(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
        return fileName;
    }
}
