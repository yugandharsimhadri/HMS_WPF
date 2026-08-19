using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The Pathology Lab module's data layer: analyte and panel masters,
/// packages, order billing, and result entry with automatic flagging. The
/// rules worth guarding explicitly: a numeric result is flagged against the
/// patient's own matched reference range (by age and sex), a qualitative
/// one is not auto-flagged since it has no numeric range to compare
/// against, and a report does not verify until every result on it has been
/// entered.
/// </summary>
public class PathologyLabServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pathologylab-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PathologyLabService _lab;

    public PathologyLabServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _lab = new PathologyLabService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
    }

    private async Task<Guid> AddPatientAsync(int age, Gender gender = Gender.Male)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var patient = new Patient
        {
            PatientNo = $"P{Guid.NewGuid():N}"[..6], Name = "Aarav Rao", Phone = "9000000000",
            Age = age, Gender = gender
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    // ── Seeding ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeding_the_default_analytes_and_reports_never_runs_twice()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await PathologyLabSeeder.SeedAsync(db);

        var analyteCountAfterFirst = await db.LabAnalytes.CountAsync();
        var reportCountAfterFirst = await db.LabReports.CountAsync();
        Assert.True(analyteCountAfterFirst > 0);
        Assert.True(reportCountAfterFirst > 0);

        var cbc = await db.LabReports.FirstAsync(r => r.Name == "Complete Blood Picture (CBC)");
        cbc.Price = 999m;
        var hemoglobin = await db.LabAnalytes.FirstAsync(a => a.Name == "Hemoglobin");
        hemoglobin.Units = "edited";
        await db.SaveChangesAsync();

        await PathologyLabSeeder.SeedAsync(db);

        Assert.Equal(analyteCountAfterFirst, await db.LabAnalytes.CountAsync());
        Assert.Equal(reportCountAfterFirst, await db.LabReports.CountAsync());
        Assert.Equal(999m, (await db.LabReports.FirstAsync(r => r.Id == cbc.Id)).Price);
        Assert.Equal("edited", (await db.LabAnalytes.FirstAsync(a => a.Id == hemoglobin.Id)).Units);

        // The report's analyte membership actually links to real rows, not
        // just a name match — this is what CBC is billed and reported for.
        var members = await db.LabReportAnalytes.Where(x => x.ReportId == cbc.Id).ToListAsync();
        Assert.Equal(5, members.Count);
        Assert.Contains(members, m => m.AnalyteId == hemoglobin.Id);
    }

    // ── Analyte master ─────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_an_analyte_and_searching_for_it_round_trips()
    {
        await _lab.SaveAnalyteAsync(new LabAnalyte
        {
            Id = Guid.NewGuid(), Name = "Sodium", Category = "Electrolytes", Units = "mmol/L", DecimalPlaces = 0
        });

        var found = await _lab.SearchAnalytesAsync("Sodium");
        Assert.Single(found);
        Assert.Equal("mmol/L", found[0].Units);
    }

    [Fact]
    public async Task An_analyte_used_in_a_report_cannot_be_deleted()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Sodium", Units = "mmol/L" });
        await _lab.SaveReportAsync(new LabReport { Name = "Electrolytes" }, [analyteId]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _lab.DeleteAnalyteAsync(analyteId));
    }

    // ── Reference ranges ───────────────────────────────────────────────────

    [Fact]
    public async Task The_matching_range_is_picked_by_gender_and_age()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Hemoglobin", Units = "g/dL" });

        await _lab.SaveReferenceRangeAsync(new LabAnalyteReferenceRange
        {
            AnalyteId = analyteId, Gender = Gender.Male, MinAgeYears = 18, MaxAgeYears = 120,
            LowValue = 13.0m, HighValue = 17.0m, Label = "Adult Male"
        });
        await _lab.SaveReferenceRangeAsync(new LabAnalyteReferenceRange
        {
            AnalyteId = analyteId, Gender = Gender.Female, MinAgeYears = 18, MaxAgeYears = 120,
            LowValue = 12.0m, HighValue = 15.0m, Label = "Adult Female"
        });

        var maleRange = await _lab.MatchReferenceRangeAsync(analyteId, Gender.Male, 30);
        var femaleRange = await _lab.MatchReferenceRangeAsync(analyteId, Gender.Female, 30);

        Assert.Equal("Adult Male", maleRange!.Label);
        Assert.Equal("Adult Female", femaleRange!.Label);
    }

    // ── Report / package master ────────────────────────────────────────────

    [Fact]
    public async Task Saving_a_report_replaces_its_analyte_membership_wholesale()
    {
        var sodium = Guid.NewGuid();
        var potassium = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = sodium, Name = "Sodium", Units = "mmol/L" });
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = potassium, Name = "Potassium", Units = "mmol/L" });

        var report = await _lab.SaveReportAsync(new LabReport { Name = "Electrolytes" }, [sodium]);
        var firstMembership = await _lab.GetReportAnalytesAsync(report.Id);
        Assert.Single(firstMembership);

        await _lab.SaveReportAsync(new LabReport { Id = report.Id, Name = "Electrolytes" }, [sodium, potassium]);
        var secondMembership = await _lab.GetReportAnalytesAsync(report.Id);
        Assert.Equal(2, secondMembership.Count);
    }

    [Fact]
    public async Task A_package_already_ordered_cannot_be_deleted()
    {
        var report = await _lab.SaveReportAsync(new LabReport { Name = "CBP", Price = 300m }, []);
        var package = await _lab.SavePackageAsync(new LabPackageMaster { Name = "Full Body", PackagePrice = 1000m }, [report.Id]);

        var patientId = await AddPatientAsync(30);
        await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001", PackageId = package.Id },
            [new LabOrderLine { ReportId = report.Id, ReportName = "CBP", Price = 1000m }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _lab.DeletePackageAsync(package.Id));
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_an_order_computes_totals_and_issues_a_sequential_number()
    {
        var patientId = await AddPatientAsync(30);

        var order1 = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "CBP", Price = 300m }, new LabOrderLine { ReportName = "ESR", Price = 150m }]);

        Assert.Equal(450m, order1.TotalAmount);
        Assert.StartsWith("LAB", order1.OrderNo);

        var order2 = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "TSH", Price = 300m }]);

        Assert.NotEqual(order1.OrderNo, order2.OrderNo);
    }

    // ── Result entry and flagging ──────────────────────────────────────────

    [Fact]
    public async Task A_numeric_result_outside_the_range_is_flagged_high_or_low()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Hemoglobin", Units = "g/dL" });
        await _lab.SaveReferenceRangeAsync(new LabAnalyteReferenceRange
        {
            AnalyteId = analyteId, LowValue = 13.0m, HighValue = 17.0m, Label = "Adult"
        });

        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "CBP", Price = 300m }]);
        var orderReportId = order.Reports.Single().Id;

        var low = await _lab.SaveResultAsync(orderReportId, analyteId, "Hemoglobin", "g/dL", "10.5", LabResultType.Numeric, "tech");
        Assert.Equal(LabResultFlag.Low, low.Flag);
        Assert.Equal("13.0 – 17.0", low.ReferenceRangeDisplay);

        var high = await _lab.SaveResultAsync(orderReportId, analyteId, "Hemoglobin", "g/dL", "18.2", LabResultType.Numeric, "tech");
        Assert.Equal(LabResultFlag.High, high.Flag);

        var normal = await _lab.SaveResultAsync(orderReportId, analyteId, "Hemoglobin", "g/dL", "15.0", LabResultType.Numeric, "tech");
        Assert.Equal(LabResultFlag.Normal, normal.Flag);
    }

    [Fact]
    public async Task A_qualitative_result_is_not_auto_flagged_and_shows_the_text_range()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "HBsAg", Units = "", ResultType = LabResultType.Text });
        await _lab.SaveReferenceRangeAsync(new LabAnalyteReferenceRange
        {
            AnalyteId = analyteId, TextRange = "Non-reactive", Label = "All"
        });

        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "Hepatitis Screen", Price = 500m }]);
        var orderReportId = order.Reports.Single().Id;

        var result = await _lab.SaveResultAsync(orderReportId, analyteId, "HBsAg", "", "Reactive", LabResultType.Text, "tech");

        Assert.Equal(LabResultFlag.Normal, result.Flag);
        Assert.Equal("Non-reactive", result.ReferenceRangeDisplay);
    }

    [Fact]
    public async Task Saving_a_result_twice_for_the_same_analyte_updates_rather_than_duplicates()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Glucose", Units = "mg/dL" });

        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "Glucose Fasting", Price = 100m }]);
        var orderReportId = order.Reports.Single().Id;

        await _lab.SaveResultAsync(orderReportId, analyteId, "Glucose", "mg/dL", "90", LabResultType.Numeric, "tech");
        await _lab.SaveResultAsync(orderReportId, analyteId, "Glucose", "mg/dL", "95", LabResultType.Numeric, "tech");

        var reloaded = await _lab.GetOrderAsync(order.Id);
        var results = reloaded!.Reports.Single().Results;

        Assert.Single(results);
        Assert.Equal("95", results.Single().ResultValue);
    }

    // ── Verification ───────────────────────────────────────────────────────

    [Fact]
    public async Task Verifying_an_order_with_no_results_is_refused()
    {
        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "CBP", Price = 300m }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _lab.VerifyOrderAsync(order.Id, "Dr. Iyer"));
    }

    [Fact]
    public async Task Verifying_an_order_marks_every_result_verified_and_moves_the_order_status()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Glucose", Units = "mg/dL" });

        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "Glucose Fasting", Price = 100m }]);
        var orderReportId = order.Reports.Single().Id;

        await _lab.SaveResultAsync(orderReportId, analyteId, "Glucose", "mg/dL", "90", LabResultType.Numeric, "tech");
        await _lab.VerifyOrderAsync(order.Id, "Dr. Iyer");

        var reloaded = await _lab.GetOrderAsync(order.Id);
        Assert.Equal(LabOrderStatus.Verified, reloaded!.Status);
        Assert.All(reloaded.Reports.SelectMany(r => r.Results), r => Assert.NotNull(r.VerifiedOn));
    }

    [Fact]
    public async Task A_verified_order_can_no_longer_be_edited()
    {
        var analyteId = Guid.NewGuid();
        await _lab.SaveAnalyteAsync(new LabAnalyte { Id = analyteId, Name = "Glucose", Units = "mg/dL" });

        var patientId = await AddPatientAsync(30);
        var order = await _lab.SaveOrderAsync(
            new LabOrder { PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "Glucose Fasting", Price = 100m }]);
        var orderReportId = order.Reports.Single().Id;

        await _lab.SaveResultAsync(orderReportId, analyteId, "Glucose", "mg/dL", "90", LabResultType.Numeric, "tech");
        await _lab.VerifyOrderAsync(order.Id, "Dr. Iyer");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _lab.SaveOrderAsync(
            new LabOrder { Id = order.Id, PatientId = patientId, PatientName = "Aarav Rao", PatientNo = "P00001" },
            [new LabOrderLine { ReportName = "TSH", Price = 300m }]));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }

        GC.SuppressFinalize(this);
    }
}
