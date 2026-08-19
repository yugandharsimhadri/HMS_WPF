using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The Dentist module's data layer: a case tracks one procedure or package
/// across however many sittings and payments it takes, and the three rules
/// that make that safe — a case is against a procedure or a package, never
/// both or neither; the total cost is always computed fresh from the case's
/// own sittings, replacements and payments, never stored; and a case that is
/// Completed or Cancelled refuses more clinical work but a Completed one
/// still accepts a late payment.
/// </summary>
public class DentistServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dentist-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly DentistService _dentist;
    private readonly ProcedureBillsService _bills;
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public DentistServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _dentist = new DentistService(_factory);
        _bills = new ProcedureBillsService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        db.Patients.Add(new Patient { Id = _patientId, PatientNo = "P00001", Name = "Aarav Rao", Phone = "9000000000", Age = 30 });
        db.Doctors.Add(new Doctor { Id = _doctorId, Name = "Dr. Iyer", ConsultationFee = 300 });
        db.SaveChanges();
    }

    private async Task<Procedure> AddProcedureAsync(string name, decimal price)
    {
        var procedure = new Procedure { Id = Guid.NewGuid(), Name = name, Category = "Restorative", Department = ProcedureDepartment.Dentist, Price = price };
        await _bills.SaveProcedureAsync(procedure);
        return procedure;
    }

    private DentalCase NewCase(Guid? procedureId = null, Guid? packageId = null) => new()
    {
        PatientId = _patientId, PatientName = "Aarav Rao", DoctorId = _doctorId,
        ProcedureId = procedureId, PackageId = packageId, ToothNumber = "36"
    };

    // ── Seeding ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeding_the_default_procedures_never_runs_twice()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await DentistProcedureSeeder.SeedAsync(db);

        var countAfterFirst = await db.Procedures.CountAsync(p => p.Department == ProcedureDepartment.Dentist);
        Assert.True(countAfterFirst > 0);

        var scaling = await db.Procedures.FirstAsync(p => p.Name == "Scaling & Polishing");
        scaling.Price = 999m;
        await db.SaveChangesAsync();

        await DentistProcedureSeeder.SeedAsync(db);

        Assert.Equal(countAfterFirst, await db.Procedures.CountAsync(p => p.Department == ProcedureDepartment.Dentist));
        Assert.Equal(999m, (await db.Procedures.FirstAsync(p => p.Id == scaling.Id)).Price);
    }

    // ── Opening a case ─────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_a_case_against_a_procedure_snapshots_its_price_as_the_base_cost()
    {
        var procedure = await AddProcedureAsync("Root Canal", 5000m);

        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));

        Assert.Equal(5000m, opened.BaseCost);
        Assert.Equal("Root Canal", opened.ProcedureName);
        Assert.Equal(DentalCaseStatus.Planned, opened.Status);
    }

    [Fact]
    public async Task Opening_a_case_against_a_package_bills_the_package_price_not_a_sum_of_its_items()
    {
        var crown = await AddProcedureAsync("Crown", 3000m);
        var package = await _dentist.SavePackageAsync(
            new DentalPackageMaster { Name = "Full Mouth", PackagePrice = 20000m },
            [new DentalPackageItem { ProcedureId = crown.Id, ProcedureName = "Crown", Quantity = 4 }]);

        var opened = await _dentist.OpenCaseAsync(NewCase(packageId: package.Id));

        // 4 crowns at 3000 would be 12000 if summed — the package price wins.
        Assert.Equal(20000m, opened.BaseCost);
        Assert.Equal("Full Mouth", opened.PackageName);
    }

    [Fact]
    public async Task A_case_cannot_be_opened_against_both_a_procedure_and_a_package()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var package = await _dentist.SavePackageAsync(new DentalPackageMaster { Name = "Pkg", PackagePrice = 5000m }, []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id, packageId: package.Id)));
    }

    [Fact]
    public async Task A_case_cannot_be_opened_against_neither_a_procedure_nor_a_package()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => _dentist.OpenCaseAsync(NewCase()));

    // ── Sittings ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Sittings_are_numbered_in_sequence_and_move_the_case_to_in_progress()
    {
        var procedure = await AddProcedureAsync("Root Canal", 5000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));

        var first = await _dentist.AddSittingAsync(new DentalSitting { DentalCaseId = opened.Id, WorkDone = "Access opening" });
        var second = await _dentist.AddSittingAsync(new DentalSitting { DentalCaseId = opened.Id, WorkDone = "Obturation" });

        Assert.Equal(1, first.SittingNumber);
        Assert.Equal(2, second.SittingNumber);

        var reloaded = await _dentist.GetCaseAsync(opened.Id);
        Assert.Equal(DentalCaseStatus.InProgress, reloaded!.Status);
    }

    [Fact]
    public async Task A_completed_case_refuses_another_sitting()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));
        await _dentist.UpdateCaseStatusAsync(opened.Id, DentalCaseStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dentist.AddSittingAsync(new DentalSitting { DentalCaseId = opened.Id }));
    }

    // ── Cost, replacements, payments ──────────────────────────────────────

    [Fact]
    public async Task Total_cost_sums_base_cost_anesthesia_and_replacements()
    {
        var procedure = await AddProcedureAsync("Root Canal", 5000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));

        await _dentist.AddSittingAsync(new DentalSitting { DentalCaseId = opened.Id, AnesthesiaCost = 300m });
        await _dentist.AddSittingAsync(new DentalSitting { DentalCaseId = opened.Id, AnesthesiaCost = 300m });
        await _dentist.AddReplacementAsync(opened.Id, null, "Crown", 3000m, 1);

        var reloaded = await _dentist.GetCaseAsync(opened.Id);

        // 5000 base + 300 + 300 anesthesia + 3000 crown = 8600
        Assert.Equal(8600m, reloaded!.TotalCost);
    }

    [Fact]
    public async Task Balance_reflects_partial_payments_against_the_running_total()
    {
        var procedure = await AddProcedureAsync("Root Canal", 5000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));

        var payment1 = await _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 2000m });
        var payment2 = await _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 1500m });

        Assert.NotEqual(payment1.ReceiptNo, payment2.ReceiptNo);
        Assert.StartsWith("DPR", payment1.ReceiptNo);

        var reloaded = await _dentist.GetCaseAsync(opened.Id);
        Assert.Equal(3500m, reloaded!.AmountPaid);
        Assert.Equal(1500m, reloaded.Balance);
    }

    [Fact]
    public async Task A_completed_case_still_accepts_a_late_payment()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));
        await _dentist.UpdateCaseStatusAsync(opened.Id, DentalCaseStatus.Completed);

        var payment = await _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 1000m });
        Assert.Equal(1000m, payment.Amount);
    }

    [Fact]
    public async Task A_cancelled_case_refuses_further_payment()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));
        await _dentist.UpdateCaseStatusAsync(opened.Id, DentalCaseStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 500m }));
    }

    [Fact]
    public async Task Searching_payments_by_date_finds_them_regardless_of_which_case_they_belong_to()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));
        await _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 750m });

        var today = DateTime.Today;
        var found = await _dentist.SearchPaymentsAsync(today, today);

        Assert.Single(found);
        Assert.Equal(750m, found[0].Amount);
        Assert.Equal("Aarav Rao", found[0].DentalCase.PatientName);
    }

    [Fact]
    public async Task Searching_payments_outside_the_date_range_finds_nothing()
    {
        var procedure = await AddProcedureAsync("Filling", 1000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));
        await _dentist.RecordPaymentAsync(new DentalPayment { DentalCaseId = opened.Id, Amount = 750m });

        var yesterday = DateTime.Today.AddDays(-1);
        var found = await _dentist.SearchPaymentsAsync(yesterday, yesterday);

        Assert.Empty(found);
    }

    // ── Masters ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_replacement_already_billed_on_a_case_cannot_be_deleted()
    {
        var procedure = await AddProcedureAsync("Root Canal", 5000m);
        var opened = await _dentist.OpenCaseAsync(NewCase(procedureId: procedure.Id));

        await _dentist.SaveReplacementAsync(new DentalReplacementMaster { Id = Guid.NewGuid(), Name = "Crown", UnitCost = 3000m });
        var crown = (await _dentist.SearchReplacementsAsync("Crown")).Single();

        await _dentist.AddReplacementAsync(opened.Id, crown.Id, crown.Name, crown.UnitCost, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dentist.DeleteReplacementAsync(crown.Id));
    }

    [Fact]
    public async Task A_package_already_used_to_open_a_case_cannot_be_deleted()
    {
        var package = await _dentist.SavePackageAsync(new DentalPackageMaster { Name = "Pkg", PackagePrice = 5000m }, []);
        await _dentist.OpenCaseAsync(NewCase(packageId: package.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dentist.DeletePackageAsync(package.Id));
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
