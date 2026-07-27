using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// There is no standard strip. Tablets come 1, 3, 5, 10, 15 and more to a pack,
/// and the same drug can arrive in different pack sizes on different days. These
/// tests cover that directly, because getting it wrong either overcharges a
/// customer or gives medicine away.
/// </summary>
public class VaryingPackSizeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"packs-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public VaryingPackSizeTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenProductAsync(int unitsPerPack, DispensingUnit unit = DispensingUnit.Tablet)
    {
        var product = new Product
        {
            Name = $"Drug {unitsPerPack} per pack",
            GstRate = 12m,
            UnitsPerPack = unitsPerPack,
            DispensingUnit = unit,
            PackSize = $"{unitsPerPack} TAB"
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task ReceiveAsync(Product product, string batchNo, int packs, int unitsPerPack, decimal mrp)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry(),
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = batchNo,
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = packs, UnitsPerPack = unitsPerPack,
                PurchaseRate = mrp * 0.7m, Mrp = mrp
            }]);

    // ── Whole packs are always exactly the printed price ────────────────────

    [Theory]
    [InlineData(1, 45.00)]      // single-tablet blister
    [InlineData(3, 100.00)]     // 33.33 each, which does not divide evenly
    [InlineData(5, 87.50)]      // 17.50 each
    [InlineData(10, 112.00)]    // 11.20 each
    [InlineData(15, 87.50)]     // 5.8333 each, the worst case
    public void A_whole_pack_costs_the_printed_mrp_whatever_the_pack_size(int unitsPerPack, decimal mrp)
    {
        Assert.Equal(mrp, PackMath.Gross(mrp, unitsPerPack, unitsPerPack));

        // And two packs are exactly twice, with no drift accumulating.
        Assert.Equal(mrp * 2, PackMath.Gross(mrp, unitsPerPack, unitsPerPack * 2));
    }

    [Fact]
    public void An_odd_pack_size_prices_parts_per_unit_and_the_whole_at_mrp()
    {
        // Three to a strip at 100.00: 33.33 each.
        Assert.Equal(33.33m, PackMath.UnitPrice(100m, 3));

        Assert.Equal(33.33m, PackMath.Gross(100m, 3, 1));
        Assert.Equal(66.66m, PackMath.Gross(100m, 3, 2));
        Assert.Equal(100.00m, PackMath.Gross(100m, 3, 3));   // not 99.99

        // Four tablets is one strip plus one loose.
        Assert.Equal(133.33m, PackMath.Gross(100m, 3, 4));
    }

    [Fact]
    public void A_fifteen_tablet_strip_never_drifts_over_the_printed_price()
    {
        // 87.50 / 15 = 5.8333, rounded to 5.83. Fifteen times 5.83 is 87.45, so
        // pricing a full strip per unit would undercharge; pricing from the pack
        // MRP keeps it exact.
        Assert.Equal(5.83m, PackMath.UnitPrice(87.50m, 15));
        Assert.Equal(87.50m, PackMath.Gross(87.50m, 15, 15));

        for (var units = 1; units < 15; units++)
            Assert.True(PackMath.Gross(87.50m, 15, units) < 87.50m, $"{units} units should cost less than a strip");
    }

    // ── Receiving and selling ──────────────────────────────────────────────

    [Theory]
    [InlineData(1, 20, 20)]
    [InlineData(3, 20, 60)]
    [InlineData(5, 20, 100)]
    [InlineData(10, 20, 200)]
    [InlineData(15, 20, 300)]
    public async Task Packs_received_become_the_right_number_of_units(int unitsPerPack, int packs, int expectedUnits)
    {
        var product = await GivenProductAsync(unitsPerPack);
        await ReceiveAsync(product, "B1", packs, unitsPerPack, 100m);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        Assert.Equal(expectedUnits, batch.QtyOnHand);
        Assert.Equal(unitsPerPack, batch.UnitsPerPack);
    }

    [Fact]
    public async Task The_same_drug_can_hold_two_batches_with_different_pack_sizes()
    {
        // The distributor sent strips of ten last month and strips of fifteen this
        // month. Both sit on the shelf and each has to price against its own pack.
        var product = await GivenProductAsync(15);

        await ReceiveAsync(product, "OLD-10", 4, unitsPerPack: 10, mrp: 112m);
        await ReceiveAsync(product, "NEW-15", 4, unitsPerPack: 15, mrp: 160m);

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);

        Assert.Equal(2, batches.Count);

        var old = batches.Single(b => b.BatchNo == "OLD-10");
        var recent = batches.Single(b => b.BatchNo == "NEW-15");

        Assert.Equal(40, old.QtyOnHand);            // 4 x 10
        Assert.Equal(11.20m, old.UnitPrice);

        Assert.Equal(60, recent.QtyOnHand);         // 4 x 15
        Assert.Equal(10.67m, recent.UnitPrice);     // 160 / 15

        // 80 tablets in total, across two different pack sizes.
        await using var db = await _provider.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();

        var onHand = await db.Batches.Where(b => b.ProductId == product.Id).SumAsync(b => b.QtyOnHand);
        Assert.Equal(100, onHand);
    }

    [Fact]
    public async Task Selling_from_each_batch_charges_that_batchs_own_unit_price()
    {
        var product = await GivenProductAsync(15);

        await ReceiveAsync(product, "OLD-10", 2, unitsPerPack: 10, mrp: 112m);
        await ReceiveAsync(product, "NEW-15", 2, unitsPerPack: 15, mrp: 160m);

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);
        var old = batches.Single(b => b.BatchNo == "OLD-10");
        var recent = batches.Single(b => b.BatchNo == "NEW-15");

        var fromOld = await _pharmacy.SaveSaleAsync(new Sale(), [LineFor(product, old, 5)]);
        var fromNew = await _pharmacy.SaveSaleAsync(new Sale(), [LineFor(product, recent, 5)]);

        // Five tablets at 11.20 versus five at 10.67 — same drug, different packs.
        Assert.Equal(56.00m, fromOld.GrossAmount);
        Assert.Equal(53.35m, fromNew.GrossAmount);
    }

    [Fact]
    public async Task A_bottle_is_one_unit_and_prices_at_its_own_mrp()
    {
        var syrup = await GivenProductAsync(1, DispensingUnit.Bottle);
        await ReceiveAsync(syrup, "SYR1", 12, unitsPerPack: 1, mrp: 134.53m);

        var batch = (await _pharmacy.GetSellableBatchesAsync(syrup.Id))[0];

        Assert.Equal(12, batch.QtyOnHand);
        Assert.Equal(134.53m, batch.UnitPrice);

        var sale = await _pharmacy.SaveSaleAsync(new Sale(), [LineFor(syrup, batch, 2)]);
        Assert.Equal(269.06m, sale.GrossAmount);
    }

    [Fact]
    public void A_pack_reads_back_in_words_for_any_size()
    {
        Assert.Equal("sold as single bottle",
            new Product { UnitsPerPack = 1, DispensingUnit = DispensingUnit.Bottle }.PackDescription);

        Assert.Equal("3 tablets per pack",
            new Product { UnitsPerPack = 3, DispensingUnit = DispensingUnit.Tablet }.PackDescription);

        Assert.Equal("15 capsules per pack",
            new Product { UnitsPerPack = 15, DispensingUnit = DispensingUnit.Capsule }.PackDescription);
    }

    private static SaleLine LineFor(Product product, Batch batch, int units) => new()
    {
        ProductId = product.Id,
        BatchId = batch.Id,
        ProductName = product.Name,
        BatchNo = batch.BatchNo,
        ExpiryDate = batch.ExpiryDate,
        HsnCode = product.HsnCode,
        Quantity = units,
        UnitsPerPack = batch.UnitsPerPack,
        PackLabel = product.PackSize,
        Mrp = batch.Mrp,
        GstRate = product.GstRate
    };

    public void Dispose()
    {
        _provider.Dispose();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_dbPath + suffix); } catch (IOException) { }
        }
    }
}
