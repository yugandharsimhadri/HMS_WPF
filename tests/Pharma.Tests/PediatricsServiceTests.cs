using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Vaccine master, vaccination history, growth measurements and the parent
/// details extension. The two rules worth guarding explicitly: giving a dose
/// and billing for it are separate facts (a free dose still leaves a
/// clinical record with no bill behind it), and "due" is always computed
/// from date of birth, never stored as a pending row.
/// </summary>
public class PediatricsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pediatrics-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PediatricsService _pediatrics;
    private readonly PharmacyService _pharmacy;

    public PediatricsServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _pediatrics = new PediatricsService(_factory);
        _pharmacy = new PharmacyService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
    }

    private async Task<Guid> AddPatientAsync(DateTime? dob, string name = "Baby Anika")
    {
        await using var db = await _factory.CreateDbContextAsync();
        var patient = new Patient
        {
            PatientNo = $"P{Guid.NewGuid():N}"[..6], Name = name, Phone = "9000000000",
            Age = dob is { } d ? Patient.AgeFromDob(d) : 2, DateOfBirth = dob
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    /// <summary>A stocked pharmacy product — vaccination now draws its
    /// batch straight from here rather than a second, parallel entry.</summary>
    private async Task<Batch> AddStockedProductAsync(string name, int qty, decimal mrp)
    {
        var product = new Product { Name = name };
        await _pharmacy.SaveProductAsync(product);

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Test Distributors" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = $"B{Guid.NewGuid():N}"[..8],
                ExpiryDate = DateTime.Today.AddYears(2), Quantity = qty, PurchaseRate = mrp * 0.7m, Mrp = mrp
            }]);

        return (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();
    }

    // ── Vaccine master ─────────────────────────────────────────────────────

    [Fact]
    public async Task Seeding_the_default_vaccines_never_runs_twice()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await VaccineMasterSeeder.SeedAsync(db);

        var countAfterFirst = await db.VaccineMasters.CountAsync();
        Assert.True(countAfterFirst > 0);

        var bcg = await db.VaccineMasters.FirstAsync(v => v.Name == "BCG");
        bcg.Category = "Edited by clinic";
        await db.SaveChangesAsync();

        await VaccineMasterSeeder.SeedAsync(db);

        Assert.Equal(countAfterFirst, await db.VaccineMasters.CountAsync());
        Assert.Equal("Edited by clinic", (await db.VaccineMasters.FirstAsync(v => v.Id == bcg.Id)).Category);
    }

    [Fact]
    public async Task Saving_a_vaccine_and_searching_for_it_round_trips()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "BCG", DoseNumber = 1, RecommendedAgeDays = 0,
            Category = "Universal Immunization Programme"
        });

        var found = await _pediatrics.SearchVaccinesAsync("BCG");
        Assert.Single(found);
        Assert.Equal("Universal Immunization Programme", found[0].Category);
    }

    [Fact]
    public async Task A_vaccine_that_has_been_given_cannot_be_deleted_only_deactivated()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-60));
        await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "OPV-1", DoseNumber = 1, GivenOn = DateTime.Today
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pediatrics.DeleteVaccineAsync(vaccineId));

        await _pediatrics.SetVaccineActiveAsync(vaccineId, false);
        Assert.False((await _pediatrics.SearchVaccinesAsync("OPV-1")).Single().Active);
    }

    // ── Vaccination history ────────────────────────────────────────────────

    [Fact]
    public async Task Recording_a_dose_computes_the_next_dose_due_date_from_the_series()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });
        var dose2Id = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = dose2Id, Name = "OPV", DoseNumber = 2, RecommendedAgeDays = 70, Category = "UIP"
        });

        var dob = DateTime.Today.AddDays(-45);
        var patientId = await AddPatientAsync(dob);
        var dose1 = (await _pediatrics.SearchVaccinesAsync("OPV")).First(v => v.DoseNumber == 1);

        var recorded = await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = dose1.Id, VaccineName = "OPV", DoseNumber = 1, GivenOn = DateTime.Today
        });

        Assert.Equal(dob.Date.AddDays(70), recorded.NextDueOn);
    }

    [Fact]
    public async Task Giving_a_free_dose_never_touches_billing()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "BCG", DoseNumber = 1, RecommendedAgeDays = 0, Category = "UIP"
        });
        var patientId = await AddPatientAsync(DateTime.Today);

        var record = await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "BCG", DoseNumber = 1, GivenOn = DateTime.Today
        });

        Assert.Null(record.ProcedureBillItemId);

        var history = await _pediatrics.GetVaccinationHistoryAsync(patientId);
        Assert.Single(history);
    }

    [Fact]
    public async Task Recording_a_dose_deducts_one_unit_from_the_batch_it_was_drawn_from()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "Hepatitis B", DoseNumber = 1, RecommendedAgeDays = 0, Category = "UIP"
        });
        var batch = await AddStockedProductAsync("Test Vax Brand", qty: 5, mrp: 450m);
        var patientId = await AddPatientAsync(DateTime.Today);

        await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "Hepatitis B", DoseNumber = 1, GivenOn = DateTime.Today,
            BatchId = batch.Id, ProductName = "Test Vax Brand"
        });

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(4, (await db.Batches.FirstAsync(b => b.Id == batch.Id)).QtyOnHand);
    }

    [Fact]
    public async Task Recording_a_dose_against_an_empty_batch_is_refused()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "Hepatitis B", DoseNumber = 1, RecommendedAgeDays = 0, Category = "UIP"
        });
        var batch = await AddStockedProductAsync("Empty Vax Brand", qty: 1, mrp: 450m);
        var patientId = await AddPatientAsync(DateTime.Today);

        // First dose empties the one unit of stock.
        await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "Hepatitis B", DoseNumber = 1, GivenOn = DateTime.Today,
            BatchId = batch.Id, ProductName = "Empty Vax Brand"
        });

        // A second dose against the same now-empty batch is refused, not
        // silently allowed to go negative.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "Hepatitis B", DoseNumber = 1, GivenOn = DateTime.Today,
            BatchId = batch.Id, ProductName = "Empty Vax Brand"
        }));
    }

    // ── Due vaccines ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_dose_whose_age_has_arrived_and_was_never_given_is_due()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        // 50 days old — the 42-day dose is overdue.
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-50));

        var due = await _pediatrics.GetDueVaccinesForPatientAsync(patientId);

        Assert.Single(due);
        Assert.Equal("OPV-1", due[0].VaccineName);
    }

    [Fact]
    public async Task A_dose_already_given_is_not_due_again()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-50));

        await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "OPV-1", DoseNumber = 1, GivenOn = DateTime.Today
        });

        Assert.Empty(await _pediatrics.GetDueVaccinesForPatientAsync(patientId));
    }

    [Fact]
    public async Task A_dose_not_yet_due_by_age_does_not_appear()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        // 10 days old — the 42-day dose is weeks away.
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-10));

        Assert.Empty(await _pediatrics.GetDueVaccinesForPatientAsync(patientId, leadDays: 3));
    }

    [Fact]
    public async Task A_patient_with_no_date_of_birth_on_file_is_skipped_entirely()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });
        await AddPatientAsync(dob: null);

        // Across-all-patients query must not throw or wrongly include them.
        var due = await _pediatrics.GetDueVaccinesAsync(leadDays: 365);
        Assert.Empty(due);
    }

    // ── Immunization card ──────────────────────────────────────────────────

    [Fact]
    public async Task A_dose_already_given_shows_given_regardless_of_its_recommended_age()
    {
        var vaccineId = Guid.NewGuid();
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = vaccineId, Name = "BCG", DoseNumber = 1, RecommendedAgeDays = 0, Category = "UIP"
        });
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-10));

        await _pediatrics.RecordVaccinationAsync(new VaccinationRecord
        {
            PatientId = patientId, PatientName = "Baby Anika",
            VaccineId = vaccineId, VaccineName = "BCG", DoseNumber = 1, GivenOn = DateTime.Today
        });

        var card = await _pediatrics.GetImmunizationCardAsync(patientId);

        var row = Assert.Single(card, r => r.VaccineId == vaccineId);
        Assert.Equal(ImmunizationStatus.Given, row.Status);
        Assert.NotNull(row.Given);
    }

    [Fact]
    public async Task A_dose_whose_recommended_age_has_passed_and_was_never_given_is_overdue()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        // 60 days old — the 42-day dose is 18 days overdue.
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-60));

        var card = await _pediatrics.GetImmunizationCardAsync(patientId);

        var row = Assert.Single(card, r => r.VaccineName == "OPV-1");
        Assert.Equal(ImmunizationStatus.Overdue, row.Status);
    }

    [Fact]
    public async Task A_dose_recommended_within_the_lead_window_reads_due_soon()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        // 35 days old — the 42-day dose is 7 days away, inside the default 14-day window.
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-35));

        var card = await _pediatrics.GetImmunizationCardAsync(patientId, dueSoonLeadDays: 14);

        var row = Assert.Single(card, r => r.VaccineName == "OPV-1");
        Assert.Equal(ImmunizationStatus.DueSoon, row.Status);
    }

    [Fact]
    public async Task A_dose_well_beyond_the_lead_window_reads_upcoming()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });

        // 5 days old — the 42-day dose is 37 days away, well outside the default 14-day window.
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-5));

        var card = await _pediatrics.GetImmunizationCardAsync(patientId, dueSoonLeadDays: 14);

        var row = Assert.Single(card, r => r.VaccineName == "OPV-1");
        Assert.Equal(ImmunizationStatus.Upcoming, row.Status);
    }

    [Fact]
    public async Task A_patient_with_no_date_of_birth_reads_every_not_given_dose_as_upcoming()
    {
        await _pediatrics.SaveVaccineAsync(new VaccineMaster
        {
            Id = Guid.NewGuid(), Name = "OPV-1", DoseNumber = 1, RecommendedAgeDays = 42, Category = "UIP"
        });
        var patientId = await AddPatientAsync(dob: null);

        var card = await _pediatrics.GetImmunizationCardAsync(patientId);

        var row = Assert.Single(card, r => r.VaccineName == "OPV-1");
        Assert.Equal(ImmunizationStatus.Upcoming, row.Status);
        Assert.Null(row.RecommendedOn);
    }

    // ── Growth chart ───────────────────────────────────────────────────────

    [Fact]
    public async Task Recording_growth_computes_age_in_days_and_bmi()
    {
        var dob = DateTime.Today.AddDays(-100);
        var patientId = await AddPatientAsync(dob);

        var recorded = await _pediatrics.RecordGrowthAsync(new GrowthMeasurement
        {
            PatientId = patientId, MeasuredOn = DateTime.Today, WeightKg = 6m, HeightCm = 60m
        });

        Assert.Equal(100, recorded.AgeDays);
        // 6 / (0.6*0.6) = 16.666... -> rounds to 16.7
        Assert.Equal(16.7m, recorded.BmiValue);

        var history = await _pediatrics.GetGrowthHistoryAsync(patientId);
        Assert.Single(history);
    }

    // ── Parent details ─────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_a_pediatric_profile_twice_updates_rather_than_duplicates()
    {
        var patientId = await AddPatientAsync(DateTime.Today.AddDays(-30));

        await _pediatrics.SaveProfileAsync(new PediatricProfile { PatientId = patientId, FatherName = "R. Rao" });
        await _pediatrics.SaveProfileAsync(new PediatricProfile { PatientId = patientId, FatherName = "R. Rao", MotherName = "S. Rao" });

        var profile = await _pediatrics.GetProfileAsync(patientId);
        Assert.Equal("S. Rao", profile!.MotherName);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await db.PediatricProfiles.CountAsync(p => p.PatientId == patientId));
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
