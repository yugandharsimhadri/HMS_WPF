using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>Exercises the data reports are built from: date-range sales/H1 queries
/// and the low-stock/shortage calculation.</summary>
public class ReportsDataTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-reports-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public ReportsDataTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenProductAsync(decimal gstRate = 12m, DrugSchedule schedule = DrugSchedule.None, int reorderLevel = 0)
    {
        var product = new Product { Name = "Test Drug 500mg", GstRate = gstRate, Schedule = schedule, ReorderLevel = reorderLevel };
        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task GivenStockAsync(Product product, string batchNo, int qty, decimal mrp)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Test Distributors" },
            [new StockEntryItem
            {
                ProductId = product.Id,
                BatchNo = batchNo,
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = qty,
                PurchaseRate = mrp * 0.75m,
                Mrp = mrp
            }]);

    private static SaleLine Line(Product product, Batch batch, int quantity, DrugSchedule schedule = DrugSchedule.None)
        => new()
        {
            ProductId = product.Id,
            BatchId = batch.Id,
            ProductName = product.Name,
            BatchNo = batch.BatchNo,
            ExpiryDate = batch.ExpiryDate,
            HsnCode = product.HsnCode,
            Quantity = quantity,
            Mrp = batch.Mrp,
            GstRate = product.GstRate,
            Schedule = schedule
        };

    [Fact]
    public async Task Sales_range_query_includes_both_endpoints_and_excludes_outside_dates()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "B1", 100, 50m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        var today = await _pharmacy.SaveSaleAsync(new Sale { BillDate = DateTime.Today.AddHours(10) }, [Line(product, batch, 1)]);
        var weekAgo = await _pharmacy.SaveSaleAsync(new Sale { BillDate = DateTime.Today.AddDays(-7).AddHours(10) }, [Line(product, batch, 1)]);
        _ = await _pharmacy.SaveSaleAsync(new Sale { BillDate = DateTime.Today.AddDays(-30).AddHours(10) }, [Line(product, batch, 1)]);

        var range = await _pharmacy.GetSalesAsync(DateTime.Today.AddDays(-7), DateTime.Today);

        Assert.Equal(2, range.Count);
        Assert.Contains(range, s => s.Id == today.Id);
        Assert.Contains(range, s => s.Id == weekAgo.Id);
    }

    [Fact]
    public async Task H1_register_range_query_matches_only_sales_within_the_window()
    {
        var product = await GivenProductAsync(schedule: DrugSchedule.H1);
        await GivenStockAsync(product, "H1", 20, 80m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        await _pharmacy.SaveSaleAsync(
            new Sale { BillDate = DateTime.Today.AddDays(-2).AddHours(9), CustomerName = "Ramesh" },
            [Line(product, batch, 2, DrugSchedule.H1)]);

        await _pharmacy.SaveSaleAsync(
            new Sale { BillDate = DateTime.Today.AddDays(-20).AddHours(9), CustomerName = "Suresh" },
            [Line(product, batch, 3, DrugSchedule.H1)]);

        var range = await _pharmacy.GetH1RegisterAsync(DateTime.Today.AddDays(-5), DateTime.Today);

        var entry = Assert.Single(range);
        Assert.Equal("Ramesh", entry.PatientName);
        Assert.Equal(2, entry.Quantity);
    }

    [Fact]
    public async Task Low_stock_only_lists_products_at_or_below_their_reorder_level()
    {
        var low = await GivenProductAsync(reorderLevel: 10);
        await GivenStockAsync(low, "B1", 5, 50m);

        var healthy = await GivenProductAsync(reorderLevel: 10);
        await GivenStockAsync(healthy, "B1", 50, 50m);

        var noThreshold = await GivenProductAsync(reorderLevel: 0);
        await GivenStockAsync(noThreshold, "B1", 0, 50m);

        var lowStock = await _pharmacy.GetLowStockAsync();

        Assert.Single(lowStock);
        Assert.Equal(low.Id, lowStock[0].Id);
        Assert.Equal(5, lowStock[0].Shortage);
    }

    [Fact]
    public void Shortage_never_goes_negative_when_stock_is_healthy()
    {
        var product = new Product { ReorderLevel = 10 };
        product.Batches.Add(new Batch { QtyOnHand = 40 });

        Assert.Equal(0, product.Shortage);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { }
    }
}
