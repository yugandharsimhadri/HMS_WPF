using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The counter no longer asks which batch to sell from, so the software has to
/// choose — nearest expiry first, spilling into the next batch when one runs
/// short. Getting this wrong either refuses a sale that could be filled or
/// dispenses stock out of order.
/// </summary>
public class AllocationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"alloc-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public AllocationTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenProductAsync()
    {
        var product = new Product { Name = "Alloc Drug", GstRate = 12m, UnitsPerPack = 10, PackSize = "10 TAB" };
        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task StockAsync(Product product, string batchNo, int units, DateTime expiry)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry(),
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = batchNo, ExpiryDate = expiry,
                Quantity = units, UnitsPerPack = 1, PurchaseRate = 8m, Mrp = 112m
            }]);

    [Fact]
    public async Task A_request_that_one_batch_can_fill_uses_only_that_batch()
    {
        var product = await GivenProductAsync();
        await StockAsync(product, "SOON", 50, DateTime.Today.AddMonths(3));
        await StockAsync(product, "LATER", 50, DateTime.Today.AddYears(2));

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 20);

        Assert.Equal(0, shortfall);
        var only = Assert.Single(allocations);
        Assert.Equal("SOON", only.Batch.BatchNo);      // nearest expiry leaves first
        Assert.Equal(20, only.Units);
    }

    [Fact]
    public async Task A_request_larger_than_the_oldest_batch_spills_into_the_next()
    {
        var product = await GivenProductAsync();
        await StockAsync(product, "SOON", 15, DateTime.Today.AddMonths(3));
        await StockAsync(product, "LATER", 40, DateTime.Today.AddYears(2));

        // Twenty tablets, and the oldest batch only holds fifteen.
        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 20);

        Assert.Equal(0, shortfall);
        Assert.Equal(2, allocations.Count);

        Assert.Equal("SOON", allocations[0].Batch.BatchNo);
        Assert.Equal(15, allocations[0].Units);

        Assert.Equal("LATER", allocations[1].Batch.BatchNo);
        Assert.Equal(5, allocations[1].Units);
    }

    [Fact]
    public async Task A_request_bigger_than_all_stock_reports_what_is_missing()
    {
        var product = await GivenProductAsync();
        await StockAsync(product, "ONLY", 12, DateTime.Today.AddYears(1));

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 30);

        Assert.Equal(12, allocations.Sum(a => a.Units));
        Assert.Equal(18, shortfall);
    }

    [Fact]
    public async Task Expired_stock_is_never_allocated()
    {
        var product = await GivenProductAsync();
        await StockAsync(product, "DEAD", 100, DateTime.Today.AddDays(-1));
        await StockAsync(product, "GOOD", 8, DateTime.Today.AddYears(1));

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 10);

        // The expired batch is skipped even though it would have covered it.
        Assert.Single(allocations);
        Assert.Equal("GOOD", allocations[0].Batch.BatchNo);
        Assert.Equal(8, allocations[0].Units);
        Assert.Equal(2, shortfall);
    }

    [Fact]
    public async Task A_split_sale_deducts_from_both_batches_and_bills_both_lines()
    {
        var product = await GivenProductAsync();
        await StockAsync(product, "SOON", 15, DateTime.Today.AddMonths(3));
        await StockAsync(product, "LATER", 40, DateTime.Today.AddYears(2));

        var (allocations, _) = await _pharmacy.AllocateAsync(product.Id, 20);

        var sale = await _pharmacy.SaveSaleAsync(
            new Sale { IsTaxInvoice = true },
            allocations.Select(a => new SaleLine
            {
                ProductId = product.Id,
                BatchId = a.Batch.Id,
                ProductName = product.Name,
                BatchNo = a.Batch.BatchNo,
                ExpiryDate = a.Batch.ExpiryDate,
                HsnCode = product.HsnCode,
                Quantity = a.Units,
                UnitsPerPack = a.Batch.UnitsPerPack,
                PackLabel = product.PackSize,
                Mrp = a.Batch.Mrp,
                GstRate = product.GstRate
            }).ToList());

        // Two lines, so the invoice shows both batch numbers and expiries.
        var full = await _pharmacy.GetSaleAsync(sale.Id);
        Assert.Equal(2, full!.Items.Count);
        Assert.Equal(20, full.Items.Sum(i => i.Quantity));

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);
        Assert.Equal(35, batches.Sum(b => b.QtyOnHand));   // 55 less the 20 sold

        // The oldest batch is emptied and drops out of the sellable list.
        Assert.DoesNotContain(batches, b => b.BatchNo == "SOON");
    }

    [Fact]
    public async Task Nothing_is_allocated_for_a_medicine_with_no_stock()
    {
        var product = await GivenProductAsync();

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 5);

        Assert.Empty(allocations);
        Assert.Equal(5, shortfall);
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
