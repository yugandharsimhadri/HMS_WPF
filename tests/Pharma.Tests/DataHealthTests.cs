using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The health check, against the shop as it was actually reported:
/// Paracetamol 500mg, pack "15 TAB", units-per-pack 1, 59 strips on the shelf,
/// dispensing unit never set. Nine tablets billed ₹1,080.
/// </summary>
public class DataHealthTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-health-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;
    private readonly DataHealthService _health;

    public DataHealthTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
        _health = new DataHealthService(factory, _pharmacy);
    }

    private async Task<Product> GivenTheReportedShopAsync()
    {
        var paracetamol = new Product
        {
            Name = "Paracetamol 500mg",
            Manufacturer = "Generic",
            PackSize = "15 TAB",
            UnitsPerPack = 1,                        // the fault
            DispensingUnit = (DispensingUnit)0,      // never set
            GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(paracetamol);

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = paracetamol.Id,
                BatchNo = "123456",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 59,
                UnitsPerPack = 1,
                PurchaseRate = 84m,
                Mrp = 120m
            }]);

        return paracetamol;
    }

    [Fact]
    public async Task It_finds_the_pack_size_disagreement_and_says_what_it_costs()
    {
        await GivenTheReportedShopAsync();

        var pack = (await _health.ScanAsync()).Single(f => f.Problem == HealthProblem.PackSizeDisagrees);

        Assert.Equal("Paracetamol 500mg", pack.ProductName);
        Assert.Equal("pack says 15, medicine says 1 per pack", pack.Current);
        Assert.Equal(15, pack.UnitsPerPack);

        // 59 strips are 885 tablets. The packs on the shelf do not move.
        Assert.Equal(59, pack.QuantityBefore);
        Assert.Equal(885, pack.QuantityAfter);
        Assert.Contains("15 times the price", pack.Explanation);
    }

    [Fact]
    public async Task It_finds_the_missing_dispensing_unit_and_infers_it_from_the_pack()
    {
        await GivenTheReportedShopAsync();

        var unit = (await _health.ScanAsync()).Single(f => f.Problem == HealthProblem.UnitNotSet);

        Assert.Equal("not set", unit.Current);
        Assert.Equal(DispensingUnit.Tablet, unit.InferredUnit);
        Assert.Contains("read as \"units\"", unit.Explanation);
    }

    [Theory]
    [InlineData("15 TAB", DispensingUnit.Tablet)]
    [InlineData("10 CAP", DispensingUnit.Capsule)]
    [InlineData("100 ML", DispensingUnit.Bottle)]
    [InlineData("21.8 G", DispensingUnit.Sachet)]
    public void The_unit_is_taken_from_what_is_printed_on_the_pack(string pack, DispensingUnit expected)
        => Assert.Equal(expected, DataHealthService.InferUnit(pack));

    [Fact]
    public async Task Repairing_makes_nine_tablets_cost_nine_tablets()
    {
        var paracetamol = await GivenTheReportedShopAsync();

        await _health.RepairAsync(await _health.ScanAsync(), by: "Test");

        var batch = (await _pharmacy.GetSellableBatchesAsync(paracetamol.Id)).Single();

        Assert.Equal(885, batch.QtyOnHand);
        Assert.Equal(15, batch.UnitsPerPack);

        // ₹120.00 for 15 is ₹8.00 each. Nine is ₹72.00, not ₹1,080.00.
        Assert.Equal(72m, PackMath.Gross(batch.Mrp, batch.UnitsPerPack, 9));
    }

    [Fact]
    public async Task A_repaired_shop_is_clean_on_the_next_scan()
    {
        await GivenTheReportedShopAsync();

        await _health.RepairAsync(await _health.ScanAsync());

        Assert.Empty(await _health.ScanAsync());
    }

    [Fact]
    public async Task Re_counting_the_stock_leaves_a_trail()
    {
        await GivenTheReportedShopAsync();
        await _health.RepairAsync(await _health.ScanAsync(), by: "Yugandhar");

        var adjustment = (await _pharmacy.GetAdjustmentsAsync()).Single();

        Assert.Equal(59, adjustment.QuantityBefore);
        Assert.Equal(885, adjustment.QuantityAfter);
        Assert.Contains("Pack size corrected", adjustment.Notes);
        Assert.Equal("Yugandhar", adjustment.AdjustedBy);
    }

    [Fact]
    public async Task Duplicates_are_reported_but_never_merged_on_their_own()
    {
        await _pharmacy.SaveProductAsync(new Product
        {
            Name = "Cetirizine 10mg", Manufacturer = "Generic", PackSize = "10 TAB", UnitsPerPack = 10
        });

        // Same medicine keyed again — different spacing and case, as it happens.
        await _pharmacy.SaveProductAsync(new Product
        {
            Name = "cetirizine  10MG", Manufacturer = "generic", PackSize = "10 tab", UnitsPerPack = 10
        });

        var duplicate = Assert.Single((await _health.ScanAsync())
            .Where(f => f.Problem == HealthProblem.Duplicate));

        Assert.False(duplicate.CanRepairAutomatically);
        Assert.Contains("by hand", duplicate.Explanation);

        // Repairing everything else must leave both records standing.
        await _health.RepairAsync(await _health.ScanAsync());

        Assert.Equal(2, (await _pharmacy.SearchProductsAsync("cetirizine")).Count);
    }

    [Fact]
    public async Task A_healthy_shop_reports_nothing()
    {
        await _pharmacy.SaveProductAsync(new Product
        {
            Name = "Amoxicillin 500mg",
            Manufacturer = "Generic",
            PackSize = "10 CAP",
            UnitsPerPack = 10,
            DispensingUnit = DispensingUnit.Capsule
        });

        Assert.Empty(await _health.ScanAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
