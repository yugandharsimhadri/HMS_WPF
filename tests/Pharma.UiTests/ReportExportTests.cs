using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Pharma.App.Reports;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.UiTests;

/// <summary>
/// The report writers, called directly.
/// </summary>
/// <remarks>
/// The Reports screen owns offering to save under the right name, and
/// <see cref="ReportsUiTests"/> covers that. What the file actually contains is
/// this writer's job, and asserting it here rather than through the operating
/// system's Save dialog is the difference between a test that fails when
/// something is broken and one that fails when another window takes the
/// foreground.
/// </remarks>
public class ReportExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"hms-export-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public ReportExportTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "export-tests.db");
    }

    private ReportsViewModel BuildViewModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        var factory = new TestContextFactory(options);
        using (var db = factory.CreateDbContext()) db.Database.EnsureCreated();

        return new ReportsViewModel(
            new PharmacyService(factory), new OpdService(factory),
            new SettingsService(factory), new DiagnosticsService(factory), factory);
    }

    [StaFact]
    public void The_stock_register_is_written_as_a_real_workbook()
    {
        var path = Path.Combine(_dir, $"StockRegister_{DateTime.Today:yyyy-MM-dd}.xlsx");

        ReportExcelBuilder.Build(ReportKind.StockRegister, BuildViewModel(), "Twinkle Children's Hospital", path);

        var file = new FileInfo(path);
        Assert.True(file.Exists, "No workbook was written.");
        Assert.True(file.Length > 500, "The workbook is too small to be a real one.");

        // Opening it is the real proof: a file of the right size that Excel
        // cannot read would pass a length check and fail a clinic.
        using var workbook = new XLWorkbook(path);
        Assert.NotEmpty(workbook.Worksheets);
        Assert.Contains("Twinkle Children's Hospital",
                        workbook.Worksheet(1).Cell(1, 1).GetString());
    }

    [StaFact]
    public void The_stock_register_is_named_for_today_whatever_the_page_date_says()
    {
        // The stock register is a live snapshot of the shelf, so it is named
        // for today on purpose — not for the Date picker on the Reports page,
        // which belongs to the other reports. The dates passed here are
        // deliberately nowhere near today to prove they are ignored.
        var name = ReportNaming.FileName(
            ReportKind.StockRegister,
            new DateTime(2020, 1, 2), new DateTime(2020, 1, 1), new DateTime(2020, 1, 3), "xlsx");

        Assert.Equal($"StockRegister_{DateTime.Today:yyyy-MM-dd}.xlsx", name);
    }

    private sealed class TestContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (Exception) { }
    }
}
