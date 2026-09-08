using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The shared procedure master and flat billing pair Pediatrics and Dentist
/// both use — the same business rules <see cref="DiagnosticsServiceTests"/>
/// already covers for Diagnostics: a bill keeps its own billed price even if
/// the master price changes, a billed procedure cannot be deleted, nothing
/// about a Completed bill can change. Also covers the one thing this shared
/// table adds over Diagnostics' own: a Department filter that keeps
/// Pediatrics and Dentist rows apart on the same catalogue.
/// </summary>
public class ProcedureBillsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"procedurebills-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ProcedureBillsService _bills;
    private readonly Guid _patientId = Guid.NewGuid();

    public ProcedureBillsServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _bills = new ProcedureBillsService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        db.Patients.Add(new Patient
        {
            Id = _patientId, PatientNo = "P00001", Name = "Baby Anika", Phone = "9000000000", Age = 2
        });
        db.SaveChanges();
    }

    private static ProcedureBillLine Line(Guid? procedureId, string name, decimal price, int qty = 1)
        => new() { ProcedureId = procedureId, ProcedureName = name, Price = price, Quantity = qty };

    [Fact]
    public async Task Saving_a_bill_computes_totals_and_issues_a_sequential_number()
    {
        var bill1 = await _bills.SaveBillAsync(
            new ProcedureBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "BCG vaccination", 200m, 2), Line(null, "Ear piercing", 100m)]);

        Assert.Equal(500m, bill1.TotalAmount);
        Assert.Equal(ProcedureBillStatus.Ordered, bill1.Status);
        Assert.StartsWith("PRC", bill1.BillNo);

        var bill2 = await _bills.SaveBillAsync(
            new ProcedureBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "OPV", 150m)]);

        Assert.NotEqual(bill1.BillNo, bill2.BillNo);
    }

    [Fact]
    public async Task A_completed_bill_refuses_further_edits()
    {
        var bill = await _bills.SaveBillAsync(
            new ProcedureBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "BCG", 200m)]);

        await _bills.UpdateStatusAsync(bill.Id, ProcedureBillStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _bills.SaveBillAsync(
            new ProcedureBill { Id = bill.Id, PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(null, "OPV", 150m)]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _bills.DeleteBillAsync(bill.Id));
    }

    [Fact]
    public async Task A_billed_procedure_cannot_be_deleted_only_deactivated()
    {
        await _bills.SaveProcedureAsync(new Procedure
        {
            Id = Guid.NewGuid(), Name = "BCG", Category = "Vaccination",
            Department = ProcedureDepartment.Pediatrics, Price = 200m
        });
        var procedure = (await _bills.SearchProceduresAsync(ProcedureDepartment.Pediatrics, "BCG")).Single();

        await _bills.SaveBillAsync(
            new ProcedureBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(procedure.Id, procedure.Name, procedure.Price)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _bills.DeleteProcedureAsync(procedure.Id));

        await _bills.SetActiveAsync(procedure.Id, false);
        var reloaded = (await _bills.SearchProceduresAsync(ProcedureDepartment.Pediatrics, "BCG")).Single();
        Assert.False(reloaded.Active);
    }

    [Fact]
    public async Task A_later_price_change_never_changes_an_already_billed_lines_price()
    {
        await _bills.SaveProcedureAsync(new Procedure
        {
            Id = Guid.NewGuid(), Name = "Ear piercing", Category = "Minor procedure",
            Department = ProcedureDepartment.Pediatrics, Price = 100m
        });
        var procedure = (await _bills.SearchProceduresAsync(ProcedureDepartment.Pediatrics, "Ear")).Single();

        var bill = await _bills.SaveBillAsync(
            new ProcedureBill { PatientId = _patientId, PatientName = "Baby Anika", PatientNo = "P00001" },
            [Line(procedure.Id, procedure.Name, procedure.Price)]);

        procedure.Price = 250m;
        await _bills.SaveProcedureAsync(procedure);

        var reloadedBill = await _bills.GetBillAsync(bill.Id);
        Assert.Equal(100m, reloadedBill!.Items.Single().Price);
    }

    [Fact]
    public async Task Searching_by_department_never_returns_the_other_departments_rows()
    {
        await _bills.SaveProcedureAsync(new Procedure
        {
            Id = Guid.NewGuid(), Name = "BCG", Category = "Vaccination",
            Department = ProcedureDepartment.Pediatrics, Price = 200m
        });
        await _bills.SaveProcedureAsync(new Procedure
        {
            Id = Guid.NewGuid(), Name = "Tooth extraction", Category = "Minor surgery",
            Department = ProcedureDepartment.Dentist, Price = 500m
        });

        var pediatrics = await _bills.SearchProceduresAsync(ProcedureDepartment.Pediatrics, null);
        var dentist = await _bills.SearchProceduresAsync(ProcedureDepartment.Dentist, null);

        Assert.Contains(pediatrics, p => p.Name == "BCG");
        Assert.DoesNotContain(pediatrics, p => p.Name == "Tooth extraction");
        Assert.Contains(dentist, p => p.Name == "Tooth extraction");
        Assert.DoesNotContain(dentist, p => p.Name == "BCG");
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
