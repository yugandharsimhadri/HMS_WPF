using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Filling a quantity that one batch cannot cover.
///
/// The whole-pack price guarantee holds per bill line, so where the split falls
/// decides what the customer pays. Twenty of a fifteen-strip taken as 12 + 8
/// prices every unit loose; taken as 15 + 5 it charges one strip at the printed
/// price and five units on top.
/// </summary>
public class SplitAllocationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-split-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public SplitAllocationTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenAMedicineAsync(int perPack)
    {
        var product = new Product
        {
            Name = $"Split Drug {perPack}", Manufacturer = "Generic",
            PackSize = $"{perPack} TAB", UnitsPerPack = perPack, GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task GivenBatchAsync(Product product, string batchNo, int units, decimal mrp, int monthsToExpiry)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = batchNo,
                ExpiryDate = DateTime.Today.AddMonths(monthsToExpiry),
                Quantity = units, UnitsPerPack = 1,          // exact units on the shelf
                PurchaseRate = mrp * 0.7m, Mrp = mrp
            }]);

    /// <summary>Receiving in exact units, then correcting the batch's pack size.</summary>
    private async Task SetPackOnBatchesAsync(Product product, int perPack)
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        foreach (var batch in await db.Batches.Where(b => b.ProductId == product.Id).ToListAsync())
            batch.UnitsPerPack = perPack;

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_split_falls_on_a_pack_boundary()
    {
        var product = await GivenAMedicineAsync(15);

        await GivenBatchAsync(product, "A", units: 17, mrp: 87.50m, monthsToExpiry: 6);
        await GivenBatchAsync(product, "B", units: 60, mrp: 87.50m, monthsToExpiry: 18);
        await SetPackOnBatchesAsync(product, 15);

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 20);

        Assert.Equal(0, shortfall);
        Assert.Equal(2, allocations.Count);

        // The oldest batch holds 17, but giving 17 would leave both lines part
        // packs. It gives one whole strip and the next batch covers the rest.
        Assert.Equal(15, allocations[0].Units);
        Assert.Equal(5, allocations[1].Units);
    }

    [Fact]
    public async Task Splitting_at_the_boundary_costs_the_same_as_not_splitting()
    {
        var product = await GivenAMedicineAsync(15);

        await GivenBatchAsync(product, "A", units: 17, mrp: 87.50m, monthsToExpiry: 6);
        await GivenBatchAsync(product, "B", units: 60, mrp: 87.50m, monthsToExpiry: 18);
        await SetPackOnBatchesAsync(product, 15);

        var (allocations, _) = await _pharmacy.AllocateAsync(product.Id, 20);

        var split = allocations.Sum(a => PackMath.Gross(a.Batch.Mrp, a.Batch.UnitsPerPack, a.Units));
        var whole = PackMath.Gross(87.50m, 15, 20);

        // ₹87.50 for the strip plus five at ₹5.83. Not ₹116.60.
        Assert.Equal(whole, split);
        Assert.Equal(116.65m, split);
    }

    [Fact]
    public async Task One_batch_that_covers_it_is_never_split()
    {
        var product = await GivenAMedicineAsync(15);

        await GivenBatchAsync(product, "A", units: 60, mrp: 87.50m, monthsToExpiry: 6);
        await SetPackOnBatchesAsync(product, 15);

        var (allocations, _) = await _pharmacy.AllocateAsync(product.Id, 20);

        // Nothing to protect — the whole quantity is one line, part pack and all.
        var single = Assert.Single(allocations);
        Assert.Equal(20, single.Units);
    }

    [Fact]
    public async Task A_batch_holding_less_than_one_pack_is_still_emptied()
    {
        var product = await GivenAMedicineAsync(15);

        await GivenBatchAsync(product, "A", units: 3, mrp: 87.50m, monthsToExpiry: 2);
        await GivenBatchAsync(product, "B", units: 60, mrp: 87.50m, monthsToExpiry: 18);
        await SetPackOnBatchesAsync(product, 15);

        var (allocations, _) = await _pharmacy.AllocateAsync(product.Id, 20);

        // Rounding this one down to a whole pack would take nothing from it and
        // strand three tablets that expire first. The oldest still goes first.
        Assert.Equal(3, allocations[0].Units);
        Assert.Equal(17, allocations[1].Units);
    }

    [Fact]
    public async Task Everything_asked_for_is_still_allocated()
    {
        var product = await GivenAMedicineAsync(10);

        await GivenBatchAsync(product, "A", units: 13, mrp: 30m, monthsToExpiry: 3);
        await GivenBatchAsync(product, "B", units: 27, mrp: 30m, monthsToExpiry: 9);
        await SetPackOnBatchesAsync(product, 10);

        foreach (var wanted in new[] { 1, 9, 10, 11, 13, 20, 25, 40 })
        {
            var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, wanted);

            Assert.Equal(0, shortfall);
            Assert.Equal(wanted, allocations.Sum(a => a.Units));
        }
    }

    [Fact]
    public async Task A_shortfall_is_still_reported_honestly()
    {
        var product = await GivenAMedicineAsync(10);

        await GivenBatchAsync(product, "A", units: 13, mrp: 30m, monthsToExpiry: 3);
        await SetPackOnBatchesAsync(product, 10);

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 20);

        // All thirteen are offered, and the seven that are missing are named.
        Assert.Equal(13, allocations.Sum(a => a.Units));
        Assert.Equal(7, shortfall);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
