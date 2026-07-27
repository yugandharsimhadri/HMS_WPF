using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Stock put on the shelf from the counter, for a medicine that is physically
/// there but not in the system.
///
/// V1 trades a tidy purchase ledger for a counter people will actually use: the
/// supplier's data is not always usable and nobody is doing a goods-inward with
/// a patient waiting. What it must not trade is the audit trail — every one of
/// these has to be findable later, or the books can never be squared.
/// </summary>
public class QuickStockTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-quick-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public QuickStockTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    private async Task<Product> GivenAMedicineAsync(int unitsPerPack = 10, DispensingUnit unit = DispensingUnit.Tablet)
    {
        var product = new Product
        {
            Name = "Counter Drug 500mg",
            PackSize = $"{unitsPerPack} TAB",
            UnitsPerPack = unitsPerPack,
            DispensingUnit = unit,
            GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    [Fact]
    public async Task Packs_and_an_mrp_are_enough_to_get_stock_on_the_shelf()
    {
        var product = await GivenAMedicineAsync();

        var batch = await _pharmacy.QuickAddStockAsync(product.Id, packs: 5, mrp: 30m, by: "Counter");

        // Five strips of ten is fifty tablets — the same arithmetic as a
        // proper goods-inward, so the counter cannot end up selling strips.
        Assert.Equal(50, batch.QtyOnHand);
        Assert.Equal(10, batch.UnitsPerPack);
        Assert.Equal(30m, batch.Mrp);
    }

    [Fact]
    public async Task A_bottle_counts_as_one()
    {
        var product = await GivenAMedicineAsync(unitsPerPack: 1, unit: DispensingUnit.Bottle);

        var batch = await _pharmacy.QuickAddStockAsync(product.Id, packs: 5, mrp: 85m);

        Assert.Equal(5, batch.QtyOnHand);
    }

    [Fact]
    public async Task A_missing_batch_number_gets_a_traceable_one()
    {
        var product = await GivenAMedicineAsync();

        var batch = await _pharmacy.QuickAddStockAsync(product.Id, packs: 2, mrp: 30m);

        // Prefixed so nobody mistakes it for something printed on the pack.
        Assert.StartsWith("CTR-", batch.BatchNo);
        Assert.Equal(DateTime.Today.AddYears(2), batch.ExpiryDate);
    }

    [Fact]
    public async Task What_the_operator_does_know_is_kept()
    {
        var product = await GivenAMedicineAsync();
        var expiry = new DateTime(2027, 9, 30);

        var batch = await _pharmacy.QuickAddStockAsync(
            product.Id, packs: 3, mrp: 30m, batchNo: "AB1234", expiry: expiry, purchaseRate: 21m);

        Assert.Equal("AB1234", batch.BatchNo);
        Assert.Equal(expiry, batch.ExpiryDate);
        Assert.Equal(21m, batch.PurchaseRate);
    }

    [Fact]
    public async Task It_can_be_sold_immediately()
    {
        var product = await GivenAMedicineAsync();
        await _pharmacy.QuickAddStockAsync(product.Id, packs: 5, mrp: 30m);

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(product.Id, 9);

        Assert.Equal(0, shortfall);
        Assert.Equal(9, allocations.Sum(a => a.Units));

        // Nine tablets out of a strip of ten, priced per tablet.
        Assert.Equal(27m, PackMath.Gross(30m, 10, 9));
    }

    [Fact]
    public async Task Every_entry_is_findable_for_reconciliation()
    {
        var product = await GivenAMedicineAsync();

        await _pharmacy.QuickAddStockAsync(product.Id, packs: 5, mrp: 30m, by: "Yugandhar");

        var toReconcile = await _pharmacy.GetProvisionalBatchesAsync();
        var batch = Assert.Single(toReconcile);

        Assert.True(batch.IsProvisional);
        Assert.Equal(product.Id, batch.ProductId);
        Assert.Equal(DateTime.Today, batch.ReceivedOn);
    }

    [Fact]
    public async Task Stock_received_the_normal_way_is_not_on_the_reconciliation_list()
    {
        var product = await GivenAMedicineAsync();

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Real Distributors", SupplierInvoiceNo = "INV-9" },
            [new StockEntryItem
            {
                ProductId = product.Id,
                BatchNo = "B1",
                ExpiryDate = DateTime.Today.AddYears(1),
                Quantity = 4,
                UnitsPerPack = 10,
                PurchaseRate = 21m,
                Mrp = 30m
            }]);

        Assert.Empty(await _pharmacy.GetProvisionalBatchesAsync());
    }

    [Fact]
    public async Task The_entry_itself_records_who_and_why()
    {
        var product = await GivenAMedicineAsync();
        await _pharmacy.QuickAddStockAsync(product.Id, packs: 5, mrp: 30m, by: "Yugandhar");

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        var entry = await db.StockEntries.Include(e => e.Items).SingleAsync();

        Assert.True(entry.IsProvisional);
        Assert.Equal("Yugandhar", entry.EnteredBy);
        Assert.Null(entry.SupplierName);
        Assert.Contains("no supplier bill", entry.Notes);

        // It is a real goods-inward document, not a loose adjustment, so the
        // quantity can be compared against the bill when it turns up.
        var item = Assert.Single(entry.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(50, item.UnitsReceived);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(5, 0)]
    public async Task Nonsense_is_refused(int packs, decimal mrp)
    {
        var product = await GivenAMedicineAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pharmacy.QuickAddStockAsync(product.Id, packs, mrp));

        Assert.Empty(await _pharmacy.GetProvisionalBatchesAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
