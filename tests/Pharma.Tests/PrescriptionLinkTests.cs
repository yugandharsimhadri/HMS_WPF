using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// A prescription can name a medicine we stock or one we do not. The first links
/// to our catalogue so the counter can dispense it; the second is written on the
/// paper and nothing else — the parent buys it outside, and it must never appear
/// among our own medicines.
/// </summary>
public class PrescriptionLinkTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rx-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly OpdService _opd;
    private readonly PharmacyService _pharmacy;

    public PrescriptionLinkTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        _factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = _factory.CreateDbContext()) db.Database.Migrate();

        _opd = new OpdService(_factory);
        _pharmacy = new PharmacyService(_factory);
    }

    private async Task<Visit> GivenVisitAsync()
    {
        var patient = await _opd.SavePatientAsync(new Patient { Name = "Baby Anika", Age = 4 });

        var doctor = new Doctor { Name = "Dr. A. Kumar", ConsultationFee = 300m };
        await _opd.SaveDoctorAsync(doctor);

        return await _opd.BookVisitAsync(patient.Id, doctor.Id, DateTime.Now, "Fever", 300m);
    }

    [Fact]
    public async Task A_stocked_medicine_is_linked_to_the_catalogue()
    {
        var visit = await GivenVisitAsync();

        var product = new Product { Name = "Calpol Syrup 60ml", GstRate = 12m };
        await _pharmacy.SaveProductAsync(product);

        await _opd.SaveConsultationAsync(visit, [new PrescriptionItem
        {
            ProductId = product.Id,
            MedicineName = product.Name,
            Frequency = "1-0-1", Days = 3, Quantity = 6
        }], [], complete: false);

        var saved = await _opd.GetVisitAsync(visit.Id);
        var line = Assert.Single(saved!.Prescription);

        Assert.Equal(product.Id, line.ProductId);
        Assert.Equal(6, line.Quantity);
    }

    [Fact]
    public async Task A_medicine_we_do_not_stock_is_prescribed_without_being_added_to_the_pharmacy()
    {
        var visit = await GivenVisitAsync();

        await using (var db = await _factory.CreateDbContextAsync())
            Assert.Equal(0, await db.Products.CountAsync());

        // Typed by the doctor, sold by some other chemist.
        await _opd.SaveConsultationAsync(visit, [new PrescriptionItem
        {
            ProductId = null,
            MedicineName = "Some Imported Ointment 20g",
            Frequency = "1-0-1", Days = 5, Quantity = 10
        }], [], complete: false);

        var saved = await _opd.GetVisitAsync(visit.Id);
        var line = Assert.Single(saved!.Prescription);

        Assert.Null(line.ProductId);
        Assert.Equal("Some Imported Ointment 20g", line.MedicineName);

        // The important part: our catalogue is untouched.
        await using var check = await _factory.CreateDbContextAsync();
        Assert.Equal(0, await check.Products.CountAsync());
        Assert.Equal(0, await check.Batches.CountAsync());
    }

    [Fact]
    public async Task One_prescription_can_mix_stocked_and_outside_medicines()
    {
        var visit = await GivenVisitAsync();

        var stocked = new Product { Name = "Calpol Syrup 60ml", GstRate = 12m };
        await _pharmacy.SaveProductAsync(stocked);

        await _opd.SaveConsultationAsync(visit,
        [
            new PrescriptionItem { ProductId = stocked.Id, MedicineName = stocked.Name, Quantity = 1 },
            new PrescriptionItem { ProductId = null, MedicineName = "Outside Nasal Spray", Quantity = 1 }
        ], [], complete: true);

        var saved = await _opd.GetVisitAsync(visit.Id);

        Assert.Equal(2, saved!.Prescription.Count);
        Assert.Single(saved.Prescription, l => l.ProductId is not null);
        Assert.Single(saved.Prescription, l => l.ProductId is null);

        // Still only the one medicine we actually stock.
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Rewriting_a_prescription_never_creates_medicines_either()
    {
        var visit = await GivenVisitAsync();

        await _opd.SaveConsultationAsync(visit,
            [new PrescriptionItem { MedicineName = "First Draft Syrup", Quantity = 1 }], [], complete: false);

        await _opd.SaveConsultationAsync(visit,
            [new PrescriptionItem { MedicineName = "Second Draft Syrup", Quantity = 1 }], [], complete: false);

        var saved = await _opd.GetVisitAsync(visit.Id);
        Assert.Equal("Second Draft Syrup", Assert.Single(saved!.Prescription).MedicineName);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Products.CountAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_dbPath + suffix); } catch (IOException) { }
        }
    }
}
