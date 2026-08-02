using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The Diagnostics module's data layer: test master CRUD, billing, and the
/// business rules the spec calls out explicitly — a bill keeps its own
/// billed price even if the master price changes later, a billed test
/// cannot be deleted, and nothing about a Completed bill can change.
/// </summary>
public class DiagnosticsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"diagnostics-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly DiagnosticsService _diagnostics;
    private readonly Guid _patientId = Guid.NewGuid();

    public DiagnosticsServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _diagnostics = new DiagnosticsService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        db.Patients.Add(new Patient
        {
            Id = _patientId, PatientNo = "P00001", Name = "Baby Anika", Phone = "9000000000", Age = 4
        });
        db.SaveChanges();
    }

    private static DiagnosticBillLine Line(Guid? testId, string name, decimal price, int qty = 1)
        => new() { TestId = testId, TestName = name, Price = price, Quantity = qty };

    // ── Seeding ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeding_never_runs_twice()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await DiagnosticTestSeeder.SeedAsync(db);

        var countAfterFirst = await db.DiagnosticTests.CountAsync();
        Assert.True(countAfterFirst > 0);

        // A price the clinic has since edited must survive a later seed run —
        // the application calls SeedAsync on every startup.
        var cbc = await db.DiagnosticTests.FirstAsync(t => t.Name.Contains("CBP"));
        cbc.Price = 999m;
        await db.SaveChangesAsync();

        await DiagnosticTestSeeder.SeedAsync(db);

        Assert.Equal(countAfterFirst, await db.DiagnosticTests.CountAsync());
        Assert.Equal(999m, (await db.DiagnosticTests.FirstAsync(t => t.Id == cbc.Id)).Price);
    }

    // ── Billing ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_a_bill_computes_totals_and_issues_a_sequential_number()
    {
        var bill1 = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "CBC", 300m, 2), Line(null, "ESR", 150m)]);

        Assert.Equal(750m, bill1.TotalAmount);
        Assert.Equal(750m, bill1.FinalAmount);
        Assert.Equal(DiagnosticBillStatus.Ordered, bill1.Status);
        Assert.StartsWith("DX", bill1.BillNo);

        var bill2 = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "TSH", 300m)]);

        Assert.NotEqual(bill1.BillNo, bill2.BillNo);
    }

    [Fact]
    public async Task Discount_is_subtracted_from_the_total_to_reach_the_final_amount()
    {
        var bill = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001", Discount = 50m },
            [Line(null, "CBC", 300m)]);

        Assert.Equal(300m, bill.TotalAmount);
        Assert.Equal(250m, bill.FinalAmount);
    }

    [Fact]
    public async Task A_master_price_change_never_touches_an_already_billed_line()
    {
        var test = new DiagnosticTest { Name = "CBC", Category = "Hematology", Price = 300m };
        await _diagnostics.SaveTestAsync(test);

        var bill = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(test.Id, "CBC", test.Price)]);

        test.Price = 500m;
        await _diagnostics.SaveTestAsync(test);

        var reloaded = await _diagnostics.GetBillAsync(bill.Id);
        Assert.Equal(300m, reloaded!.Items.Single().Price);
    }

    [Fact]
    public async Task Editing_or_deleting_a_completed_bill_is_refused()
    {
        var bill = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "CBC", 300m)]);

        await _diagnostics.UpdateStatusAsync(bill.Id, DiagnosticBillStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _diagnostics.SaveBillAsync(
                new DiagnosticBill { Id = bill.Id, PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
                [Line(null, "CBC", 300m)]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _diagnostics.DeleteBillAsync(bill.Id));
    }

    [Fact]
    public async Task A_bill_not_yet_completed_can_be_edited()
    {
        var bill = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "CBC", 300m)]);

        var updated = await _diagnostics.SaveBillAsync(
            new DiagnosticBill { Id = bill.Id, PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "CBC", 300m), Line(null, "ESR", 150m)]);

        Assert.Equal(bill.BillNo, updated.BillNo);
        Assert.Equal(450m, updated.TotalAmount);
        Assert.Equal(2, (await _diagnostics.GetBillAsync(bill.Id))!.Items.Count);
    }

    // ── Test master ────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite's Contains() compiles to instr(), which is case-sensitive — a
    /// clinic typing "crp" found nothing against a test saved as "CRP".
    /// SearchTestsAsync must use EF.Functions.Like instead, the same fix
    /// OpdService.SearchPatientsAsync already relies on.
    /// </summary>
    [Fact]
    public async Task Searching_is_case_insensitive()
    {
        await _diagnostics.SaveTestAsync(new DiagnosticTest { Name = "CRP", Category = "Biochemistry", Price = 400m });

        Assert.Single(await _diagnostics.SearchTestsAsync("crp"));
        Assert.Single(await _diagnostics.SearchTestsAsync("CRP"));
        Assert.Single(await _diagnostics.SearchTestsAsync("Crp"));

        // The category is searched too, and lower case there must work as well.
        Assert.Single(await _diagnostics.SearchTestsAsync("biochemistry"));
    }

    [Fact]
    public async Task A_test_that_has_never_been_billed_deletes_outright()
    {
        var test = new DiagnosticTest { Name = "Vitamin D", Category = "Vitamins", Price = 1200m };
        await _diagnostics.SaveTestAsync(test);

        await _diagnostics.DeleteTestAsync(test.Id);

        Assert.Empty(await _diagnostics.SearchTestsAsync("Vitamin D"));
    }

    [Fact]
    public async Task A_test_that_has_been_billed_cannot_be_deleted_only_deactivated()
    {
        var test = new DiagnosticTest { Name = "CBC", Category = "Hematology", Price = 300m };
        await _diagnostics.SaveTestAsync(test);

        await _diagnostics.SaveBillAsync(
            new DiagnosticBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(test.Id, "CBC", 300m)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _diagnostics.DeleteTestAsync(test.Id));

        await _diagnostics.SetActiveAsync(test.Id, false);
        var reloaded = (await _diagnostics.SearchTestsAsync("CBC")).Single();
        Assert.False(reloaded.Active);
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
