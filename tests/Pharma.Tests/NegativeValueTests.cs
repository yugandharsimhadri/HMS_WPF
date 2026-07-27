using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Negative numbers, pushed at every place a number is taken.
///
/// A minus sign typed into a quantity or a price is not a rounding problem: it
/// pays money out of the till, takes stock off a shelf that never had it, or
/// prints a bill the shop owes the customer. Nothing may accept one.
/// </summary>
public class NegativeValueTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-neg-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public NegativeValueTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenAMedicineAsync()
    {
        var product = new Product
        {
            Name = "Negative Test 500mg", PackSize = "10 TAB",
            UnitsPerPack = 10, GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    private Task GivenStockAsync(Product product, int packs = 5)
        => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "N1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = packs, UnitsPerPack = 10, PurchaseRate = 21m, Mrp = 30m
            }]);

    // ── Receiving ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Receiving_a_negative_quantity_is_refused()
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "N1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = -5, UnitsPerPack = 10, PurchaseRate = 21m, Mrp = 30m
            }]));
    }

    /// <summary>
    /// The one that slipped through: a positive quantity with a negative free
    /// quantity passed the "nothing on this line" check, and the units received
    /// came out negative — a delivery that took stock off the shelf.
    /// </summary>
    [Fact]
    public async Task A_negative_free_quantity_cannot_drain_the_shelf()
    {
        var product = await GivenAMedicineAsync();
        await GivenStockAsync(product);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "N1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5, FreeQuantity = -10, UnitsPerPack = 10, PurchaseRate = 21m, Mrp = 30m
            }]));

        // Still the fifty it started with.
        Assert.Equal(50, (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single().QtyOnHand);
    }

    [Fact]
    public async Task Receiving_at_a_negative_price_is_refused()
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "N1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5, UnitsPerPack = 10, PurchaseRate = 21m, Mrp = -30m
            }]));
    }

    [Fact]
    public async Task Receiving_at_a_negative_cost_is_refused()
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "N1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5, UnitsPerPack = 10, PurchaseRate = -21m, Mrp = 30m
            }]));
    }

    // ── Counter stock ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(-5, 30)]
    [InlineData(5, -30)]
    [InlineData(-5, -30)]
    public async Task Counter_stock_refuses_negatives(int packs, decimal mrp)
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pharmacy.QuickAddStockAsync(product.Id, packs, mrp));
    }

    [Fact]
    public async Task Counter_stock_refuses_a_negative_rate()
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pharmacy.QuickAddStockAsync(product.Id, 5, 30m, purchaseRate: -21m));
    }

    // ── Selling ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_negative_quantity_cannot_be_sold()
    {
        var product = await GivenAMedicineAsync();
        await GivenStockAsync(product);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = product.Id, BatchId = batch.Id, ProductName = product.Name,
                BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate,
                Quantity = -9, UnitsPerPack = 10, Mrp = 30m, GstRate = 12m
            }]));
    }

    /// <summary>A minus in the price would hand money over with the medicine.</summary>
    [Fact]
    public async Task A_negative_price_cannot_be_billed()
    {
        var product = await GivenAMedicineAsync();
        await GivenStockAsync(product);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = product.Id, BatchId = batch.Id, ProductName = product.Name,
                BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate,
                Quantity = 9, UnitsPerPack = 10, Mrp = -30m, GstRate = 12m
            }]));
    }

    [Fact]
    public async Task A_negative_discount_cannot_inflate_a_bill()
    {
        var product = await GivenAMedicineAsync();
        await GivenStockAsync(product);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = product.Id, BatchId = batch.Id, ProductName = product.Name,
                BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate,
                Quantity = 9, UnitsPerPack = 10, Mrp = 30m,
                DiscountPercent = -50m, GstRate = 12m
            }]));
    }

    // ── Corrections ────────────────────────────────────────────────────────

    [Fact]
    public async Task Stock_cannot_be_corrected_to_a_negative_count()
    {
        var product = await GivenAMedicineAsync();
        await GivenStockAsync(product);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pharmacy.AdjustStockAsync(batch.Id, -1, AdjustmentReason.Recount));
    }

    // ── Arithmetic ─────────────────────────────────────────────────────────

    [Fact]
    public void Pricing_a_negative_quantity_never_returns_money()
    {
        foreach (var quantity in new[] { -1, -9, -100 })
        foreach (var perPack in new[] { 1, 10, 15 })
            Assert.Equal(0m, PackMath.Gross(30m, perPack, quantity));
    }

    [Fact]
    public void Describing_a_negative_quantity_does_not_invent_packs()
    {
        Assert.Equal("0", PackMath.Describe(-5, 1, "10 TAB"));
        Assert.DoesNotContain("-", PackMath.Describe(-5, 10, "10 TAB"));
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
