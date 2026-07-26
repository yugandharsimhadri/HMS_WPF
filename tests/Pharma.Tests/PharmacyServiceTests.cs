using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>Exercises the counter against a real (temporary) SQLite file.</summary>
public class PharmacyServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public PharmacyServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenProductAsync(decimal gstRate = 12m, DrugSchedule schedule = DrugSchedule.None)
    {
        var product = new Product { Name = "Test Drug 500mg", GstRate = gstRate, Schedule = schedule };
        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task GivenStockAsync(Product product, string batchNo, int qty, decimal mrp, DateTime? expiry = null)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Test Distributors" },
            [new StockEntryItem
            {
                ProductId = product.Id,
                BatchNo = batchNo,
                ExpiryDate = expiry ?? DateTime.Today.AddYears(2),
                Quantity = qty,
                PurchaseRate = mrp * 0.75m,
                Mrp = mrp
            }]);

    [Fact]
    public async Task Receiving_stock_creates_a_batch_that_can_be_sold()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "B1", 100, 50m);

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);

        Assert.Single(batches);
        Assert.Equal(100, batches[0].QtyOnHand);
        Assert.Equal(50m, batches[0].Mrp);
    }

    [Fact]
    public async Task Free_quantity_adds_to_stock()
    {
        var product = await GivenProductAsync();

        await _pharmacy.ReceiveStockAsync(
            new StockEntry(),
            [new StockEntryItem
            {
                ProductId = product.Id,
                BatchNo = "SCHEME",
                ExpiryDate = DateTime.Today.AddYears(1),
                Quantity = 10,
                FreeQuantity = 1,
                PurchaseRate = 40m,
                Mrp = 50m
            }]);

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);
        Assert.Equal(11, batches[0].QtyOnHand);
    }

    [Fact]
    public async Task Selling_deducts_stock_and_numbers_the_bill()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "B1", 100, 112m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        var sale = await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Cash" },
            [Line(product, batch, quantity: 10)]);

        Assert.StartsWith("INV", sale.BillNo);
        Assert.Equal(1120m, sale.NetAmount);
        Assert.Equal(1000m, sale.TaxableAmount);
        Assert.Equal(sale.CgstAmount, sale.SgstAmount);

        var after = await _pharmacy.GetSellableBatchesAsync(product.Id);
        Assert.Equal(90, after[0].QtyOnHand);
    }

    [Fact]
    public async Task Selling_more_than_is_in_stock_is_refused_and_changes_nothing()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "B1", 5, 100m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _pharmacy.SaveSaleAsync(new Sale(), [Line(product, batch, quantity: 6)]));

        Assert.Contains("Only 5 left", ex.Message);

        var after = await _pharmacy.GetSellableBatchesAsync(product.Id);
        Assert.Equal(5, after[0].QtyOnHand);
        Assert.Empty(await _pharmacy.GetSalesAsync(DateTime.Today));
    }

    [Fact]
    public async Task Nearest_expiry_batch_is_offered_first()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "LATER", 50, 100m, DateTime.Today.AddYears(3));
        await GivenStockAsync(product, "SOONER", 50, 100m, DateTime.Today.AddMonths(4));

        var batches = await _pharmacy.GetSellableBatchesAsync(product.Id);

        Assert.Equal("SOONER", batches[0].BatchNo);
    }

    [Fact]
    public async Task Schedule_H1_sales_are_written_to_the_statutory_register()
    {
        var product = await GivenProductAsync(schedule: DrugSchedule.H1);
        await GivenStockAsync(product, "H1BATCH", 20, 80m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        var sale = await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Ramesh", DoctorName = "Dr. Meera" },
            [Line(product, batch, quantity: 2, schedule: DrugSchedule.H1)]);

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.H1Register.SingleAsync();

        Assert.Equal(sale.BillNo, entry.BillNo);
        Assert.Equal("Ramesh", entry.PatientName);
        Assert.Equal("Dr. Meera", entry.DoctorName);
        Assert.Equal(2, entry.Quantity);
    }

    [Fact]
    public async Task Bill_numbers_run_in_sequence()
    {
        var product = await GivenProductAsync();
        await GivenStockAsync(product, "B1", 100, 10m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        var first = await _pharmacy.SaveSaleAsync(new Sale(), [Line(product, batch, 1)]);
        var second = await _pharmacy.SaveSaleAsync(new Sale(), [Line(product, batch, 1)]);

        Assert.Equal("INV00001", first.BillNo);
        Assert.Equal("INV00002", second.BillNo);
    }

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

    public void Dispose()
    {
        _provider.Dispose();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { }
    }
}
