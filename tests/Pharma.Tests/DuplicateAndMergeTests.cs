using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Stopping the same medicine being entered twice, and folding together the two
/// that already exist.
///
/// A duplicate splits the stock and shows up twice at the counter — the reported
/// case was a second "Cetirizine 10mg" with nothing in it appearing in the
/// search list under the real one.
/// </summary>
public class DuplicateAndMergeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-dup-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;
    private readonly DataHealthService _health;

    public DuplicateAndMergeTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
        _health = new DataHealthService(factory, _pharmacy);
    }

    private async Task<Product> GivenAMedicineAsync(string name, string maker = "Generic", string pack = "10 TAB")
    {
        var product = new Product
        {
            Name = name, Manufacturer = maker, PackSize = pack,
            UnitsPerPack = 10, DispensingUnit = DispensingUnit.Tablet
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task GivenStockAsync(Product product, string batchNo, int packs, decimal mrp)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = batchNo,
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = packs, UnitsPerPack = 10, PurchaseRate = mrp * 0.7m, Mrp = mrp
            }]);

    [Fact]
    public async Task The_same_medicine_cannot_be_entered_twice()
    {
        await GivenAMedicineAsync("Cetirizine 10mg");

        var again = new Product { Name = "Cetirizine 10mg", Manufacturer = "Generic", PackSize = "10 TAB" };

        var refused = await Assert.ThrowsAsync<DuplicateMedicineException>(
            () => _pharmacy.SaveProductAsync(again));

        Assert.Contains("already in the catalogue", refused.Message);
        Assert.Contains("split the stock", refused.Message);
        Assert.Equal("Cetirizine 10mg", refused.Existing.Name);
    }

    [Theory]
    [InlineData("cetirizine 10mg", "generic", "10 tab")]   // case
    [InlineData("  Cetirizine 10mg  ", "Generic", "10 TAB")] // padding
    [InlineData("Cetirizine  10mg", "Generic", "10  TAB")]   // doubled spaces
    public async Task Near_misses_count_as_the_same_medicine(string name, string maker, string pack)
    {
        await GivenAMedicineAsync("Cetirizine 10mg");

        await Assert.ThrowsAsync<DuplicateMedicineException>(
            () => _pharmacy.SaveProductAsync(new Product { Name = name, Manufacturer = maker, PackSize = pack }));
    }

    [Fact]
    public async Task The_same_drug_from_a_different_maker_is_a_different_medicine()
    {
        await GivenAMedicineAsync("Cetirizine 10mg", "Cipla");
        await GivenAMedicineAsync("Cetirizine 10mg", "Sun");

        // Different MRPs, both legitimately stocked.
        Assert.Equal(2, (await _pharmacy.SearchProductsAsync("Cetirizine")).Count);
    }

    [Fact]
    public async Task Editing_a_medicine_does_not_collide_with_itself()
    {
        var product = await GivenAMedicineAsync("Cetirizine 10mg");

        product.RackLocation = "B1";
        await _pharmacy.SaveProductAsync(product);

        Assert.Equal("B1", (await _pharmacy.SearchProductsAsync("Cetirizine")).Single().RackLocation);
    }

    [Fact]
    public async Task Merging_moves_the_stock_across_and_retires_the_duplicate()
    {
        var keep = await GivenAMedicineAsync("Cetirizine 10mg");
        await GivenStockAsync(keep, "C1", packs: 5, mrp: 30m);

        // A second record, as the migration parks one.
        var dupe = new Product
        {
            Name = "Cetirizine 10mg", Manufacturer = "Generic", PackSize = "10 TAB",
            UnitsPerPack = 10, SearchKey = "parked"
        };

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Products.Add(dupe);
            await db.SaveChangesAsync();
        }

        await GivenStockAsync(dupe, "C2", packs: 3, mrp: 33m);

        var summary = await _health.MergeAsync(keep.Id, dupe.Id, by: "Yugandhar");

        Assert.Contains("1 batch(es), 30 unit(s) moved across", summary);

        // One record left, holding everything.
        var survivors = await _pharmacy.SearchProductsAsync("Cetirizine");
        var survivor = Assert.Single(survivors);

        Assert.Equal(keep.Id, survivor.Id);
        Assert.Equal(80, survivor.StockOnHand);     // 50 + 30

        // Both batches are sellable from the survivor, oldest expiry first.
        Assert.Equal(2, (await _pharmacy.GetSellableBatchesAsync(keep.Id)).Count);
    }

    [Fact]
    public async Task Merging_keeps_what_was_already_sold()
    {
        var keep = await GivenAMedicineAsync("Cetirizine 10mg");
        var dupe = await GivenAMedicineAsync("Cetirizine 10mg", "Cipla");

        await GivenStockAsync(dupe, "C2", packs: 3, mrp: 33m);

        var batch = (await _pharmacy.GetSellableBatchesAsync(dupe.Id)).Single();

        await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = dupe.Id, BatchId = batch.Id, ProductName = dupe.Name,
                BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate,
                Quantity = 9, UnitsPerPack = 10, Mrp = batch.Mrp, GstRate = 12m
            }]);

        await _health.MergeAsync(keep.Id, dupe.Id);

        // The sale history follows the medicine it was folded into, so the day
        // book and the H1 register do not lose their link.
        await using var db = await _provider
            .GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        Assert.All(await db.SaleItems.ToListAsync(), i => Assert.Equal(keep.Id, i.ProductId));
    }

    [Fact]
    public async Task A_medicine_cannot_be_merged_into_itself()
    {
        var product = await GivenAMedicineAsync("Cetirizine 10mg");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _health.MergeAsync(product.Id, product.Id));
    }

    [Fact]
    public async Task After_merging_the_name_can_be_used_again()
    {
        var keep = await GivenAMedicineAsync("Cetirizine 10mg");
        var dupe = await GivenAMedicineAsync("Cetirizine 10mg", "Cipla");

        await _health.MergeAsync(keep.Id, dupe.Id);

        // The retired record must not hold the key hostage.
        await _pharmacy.SaveProductAsync(new Product
        {
            Name = "Cetirizine 10mg", Manufacturer = "Cipla", PackSize = "10 TAB"
        });
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
