using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The Stock Register reads Batch.QtyOnHand directly — the same field
/// Product.StockOnHand, Low Stock and Expiring Soon already read — so these tests
/// exercise the real PharmacyService.GetAllBatchesAsync query rather than a
/// second, parallel stock calculation.
/// </summary>
public class StockRegisterTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-stock-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public StockRegisterTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenProductAsync(string name, int reorderLevel = 0)
    {
        var product = new Product { Name = name, GstRate = 12m, ReorderLevel = reorderLevel };
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
    public async Task Product_with_positive_stock_appears()
    {
        var product = await GivenProductAsync("Paracetamol 500mg");
        await GivenStockAsync(product, "B1", 50, 12m);

        var register = await _pharmacy.GetAllBatchesAsync();

        Assert.Contains(register, b => b.ProductId == product.Id && b.QtyOnHand == 50);
    }

    [Fact]
    public async Task Zero_stock_batch_excluded_by_default()
    {
        // Receiving zero units is refused by ReceiveStockAsync, so the only
        // legitimate way a batch reaches zero is being adjusted or sold down —
        // AdjustStockAsync is the app's own mechanism for that.
        var product = await GivenProductAsync("Cough Syrup");
        await GivenStockAsync(product, "B1", 10, 60m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];
        await _pharmacy.AdjustStockAsync(batch.Id, 0, AdjustmentReason.Recount, "Sold down to nothing.");

        var register = await _pharmacy.GetAllBatchesAsync(includeZeroStock: false);

        Assert.DoesNotContain(register, b => b.ProductId == product.Id);
    }

    [Fact]
    public async Task Zero_stock_batch_appears_when_include_zero_stock_is_enabled()
    {
        var product = await GivenProductAsync("Cough Syrup");
        await GivenStockAsync(product, "B1", 10, 60m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];
        await _pharmacy.AdjustStockAsync(batch.Id, 0, AdjustmentReason.Recount, "Sold down to nothing.");

        var register = await _pharmacy.GetAllBatchesAsync(includeZeroStock: true);

        Assert.Contains(register, b => b.ProductId == product.Id && b.QtyOnHand == 0);
    }

    [Fact]
    public async Task Multiple_batches_of_the_same_medicine_remain_separate_rows()
    {
        var product = await GivenProductAsync("Amoxicillin 250mg");
        await GivenStockAsync(product, "A123", 50, 20m, DateTime.Today.AddMonths(6));
        await GivenStockAsync(product, "B456", 100, 22m, DateTime.Today.AddMonths(10));

        var register = await _pharmacy.GetAllBatchesAsync();
        var rows = register.Where(b => b.ProductId == product.Id).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, b => b.BatchNo == "A123" && b.QtyOnHand == 50);
        Assert.Contains(rows, b => b.BatchNo == "B456" && b.QtyOnHand == 100);
        // Not combined into a single 150-unit row — batch/expiry detail must survive.
        Assert.DoesNotContain(rows, b => b.QtyOnHand == 150);
    }

    [Fact]
    public async Task Current_quantity_matches_the_authoritative_stock_on_hand_calculation()
    {
        var product = await GivenProductAsync("Vitamin C");
        await GivenStockAsync(product, "B1", 30, 15m);
        await GivenStockAsync(product, "B2", 20, 15m);

        var register = await _pharmacy.GetAllBatchesAsync();
        var registerTotal = register.Where(b => b.ProductId == product.Id).Sum(b => b.QtyOnHand);

        var catalogue = await _pharmacy.SearchProductsAsync(product.Name, 10);
        var authoritative = catalogue.Single(p => p.Id == product.Id).StockOnHand;

        Assert.Equal(50, registerTotal);
        Assert.Equal(authoritative, registerTotal);
    }

    [Fact]
    public async Task Low_stock_product_still_appears_in_the_stock_register()
    {
        var product = await GivenProductAsync("Ibuprofen 400mg", reorderLevel: 20);
        await GivenStockAsync(product, "B1", 5, 9m);

        var lowStock = await _pharmacy.GetLowStockAsync();
        var register = await _pharmacy.GetAllBatchesAsync();

        Assert.Contains(lowStock, p => p.Id == product.Id);
        Assert.Contains(register, b => b.ProductId == product.Id && b.QtyOnHand == 5);
    }

    [Fact]
    public async Task Out_of_stock_batch_is_tracked_but_hidden_until_include_zero_stock_is_on()
    {
        var product = await GivenProductAsync("Paracetamol 650mg");
        await GivenStockAsync(product, "B1", 10, 15m);
        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id))[0];

        // Sell every unit — the batch is depleted, not deleted.
        await _pharmacy.SaveSaleAsync(new Sale(), [new SaleLine
        {
            ProductId = product.Id, BatchId = batch.Id, ProductName = product.Name, BatchNo = batch.BatchNo,
            ExpiryDate = batch.ExpiryDate, HsnCode = product.HsnCode, Quantity = 10, Mrp = batch.Mrp, GstRate = product.GstRate
        }]);

        var hidden = await _pharmacy.GetAllBatchesAsync(includeZeroStock: false);
        var shown = await _pharmacy.GetAllBatchesAsync(includeZeroStock: true);

        Assert.DoesNotContain(hidden, b => b.ProductId == product.Id);
        Assert.Contains(shown, b => b.ProductId == product.Id && b.QtyOnHand == 0);
    }

    [Fact]
    public async Task Expired_batch_with_stock_remaining_is_not_silently_removed()
    {
        var product = await GivenProductAsync("Expired Antibiotic");
        await GivenStockAsync(product, "OLD1", 12, 30m, DateTime.Today.AddDays(-10));

        var register = await _pharmacy.GetAllBatchesAsync();

        var row = Assert.Single(register, b => b.ProductId == product.Id);
        Assert.Equal(12, row.QtyOnHand);
        Assert.True(row.IsExpired);
    }

    [Fact]
    public async Task Search_matches_by_product_name_or_batch_number_case_insensitively()
    {
        var product = await GivenProductAsync("Paracetamol 500mg");
        await GivenStockAsync(product, "PARA-XYZ", 25, 12m);
        var batch = (await _pharmacy.GetAllBatchesAsync()).Single(b => b.ProductId == product.Id);

        Assert.True(StockRegisterFilter.Matches(batch, "paracetamol"));
        Assert.True(StockRegisterFilter.Matches(batch, "para-xyz"));
        Assert.True(StockRegisterFilter.Matches(batch, ""));
        Assert.True(StockRegisterFilter.Matches(batch, null));
        Assert.False(StockRegisterFilter.Matches(batch, "ibuprofen"));
    }

    [Fact]
    public async Task Stock_summary_totals_reconcile_with_the_underlying_batches()
    {
        var a = await GivenProductAsync("Drug A");
        await GivenStockAsync(a, "A1", 10, 50m); // cost 37.50 each
        await GivenStockAsync(a, "A2", 5, 55m);

        var b = await GivenProductAsync("Drug B");
        await GivenStockAsync(b, "B1", 20, 8m);

        var register = await _pharmacy.GetAllBatchesAsync();
        var relevant = register.Where(x => x.ProductId == a.Id || x.ProductId == b.Id).ToList();

        var summary = StockSummary.From(relevant);

        Assert.Equal(2, summary.TotalProducts);
        Assert.Equal(3, summary.TotalBatches);
        Assert.Equal(35, summary.TotalUnits); // 10 + 5 + 20
        Assert.Equal(10 * 37.5m + 5 * 41.25m + 20 * 6m, summary.TotalCostValue);
        Assert.Equal(10 * 50m + 5 * 55m + 20 * 8m, summary.TotalMrpValue);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch (IOException) { }
    }
}
