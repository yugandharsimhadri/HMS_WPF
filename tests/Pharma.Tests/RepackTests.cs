using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Repairing stock that was received under the wrong units-per-pack.
///
/// Reported from the counter: 59 strips of Paracetamol on the shelf, a child
/// needed 9 tablets, and the counter took 9 strips. The medicine had been set
/// up with a pack size of "15 TAB" but units-per-pack left at 1, so a strip and
/// a tablet were the same thing as far as the software was concerned.
///
/// Correcting the medicine is not enough on its own: a batch keeps the pack
/// size it arrived with, so stock already on the shelf goes on being sold by
/// the strip until it is re-counted too.
/// </summary>
public class RepackTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-repack-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public RepackTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    /// <summary>The shop exactly as it was reported: 59 strips, recorded as 59.</summary>
    private async Task<Product> GivenTheReportedShopAsync()
    {
        var paracetamol = new Product
        {
            Name = "Paracetamol 500mg",
            PackSize = "15 TAB",
            UnitsPerPack = 1,          // the fault
            GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(paracetamol);

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local Distributors" },
            [new StockEntryItem
            {
                ProductId = paracetamol.Id,
                BatchNo = "PC59",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 59,
                UnitsPerPack = 1,
                PurchaseRate = 22m,
                Mrp = 30m
            }]);

        return paracetamol;
    }

    [Fact]
    public async Task The_fault_is_visible_before_it_is_repaired()
    {
        var paracetamol = await GivenTheReportedShopAsync();

        var batches = await _pharmacy.GetSellableBatchesAsync(paracetamol.Id);

        // 59 strips went in and the shelf says 59 sellable units.
        Assert.Equal(59, batches.Single().QtyOnHand);

        // Which is why nine "units" cost nine strips: 9 x 30.00.
        Assert.Equal(270m, PackMath.Gross(30m, 1, 9));
    }

    [Fact]
    public async Task Repacking_turns_the_strips_into_tablets()
    {
        var paracetamol = await GivenTheReportedShopAsync();

        var preview = await _pharmacy.PreviewRepackAsync(paracetamol.Id, 15);

        Assert.True(preview.AnythingToDo);
        Assert.Equal(1, preview.Batches);
        Assert.Equal(59, preview.QuantityBefore);
        Assert.Equal(885, preview.QuantityAfter);

        var repacked = await _pharmacy.RepackAsync(paracetamol.Id, 15, by: "Test");
        Assert.Equal(1, repacked);

        var batch = (await _pharmacy.GetSellableBatchesAsync(paracetamol.Id)).Single();

        // The same 59 strips, now counted as the 885 tablets they are.
        Assert.Equal(885, batch.QtyOnHand);
        Assert.Equal(15, batch.UnitsPerPack);

        // And nine tablets now cost nine tablets: 30.00 / 15 = 2.00 each.
        Assert.Equal(18m, PackMath.Gross(batch.Mrp, batch.UnitsPerPack, 9));
    }

    [Fact]
    public async Task Nine_tablets_leave_the_shelf_and_not_nine_strips()
    {
        var paracetamol = await GivenTheReportedShopAsync();
        await _pharmacy.RepackAsync(paracetamol.Id, 15);

        var (allocations, shortfall) = await _pharmacy.AllocateAsync(paracetamol.Id, 9);

        Assert.Equal(0, shortfall);
        Assert.Equal(9, allocations.Sum(a => a.Units));

        var batch = allocations[0].Batch;

        await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = paracetamol.Id,
                BatchId = batch.Id,
                ProductName = paracetamol.Name,
                BatchNo = batch.BatchNo,
                ExpiryDate = batch.ExpiryDate,
                Quantity = 9,
                UnitsPerPack = batch.UnitsPerPack,
                PackLabel = paracetamol.PackSize,
                Mrp = batch.Mrp,
                GstRate = 12m
            }]);

        var after = (await _pharmacy.GetSellableBatchesAsync(paracetamol.Id)).Single();

        // 885 - 9. Not 885 - 135.
        Assert.Equal(876, after.QtyOnHand);
    }

    /// <summary>
    /// The counter used to total the bill through an overload that assumed one
    /// unit per pack, while the bill it saved and printed used the real one. The
    /// screen said 270.00, the printed bill said 18.00, and the operator
    /// collected whichever they were looking at.
    /// </summary>
    [Fact]
    public async Task The_total_on_screen_is_the_total_that_gets_saved()
    {
        var paracetamol = await GivenTheReportedShopAsync();
        await _pharmacy.RepackAsync(paracetamol.Id, 15);

        var batch = (await _pharmacy.GetSellableBatchesAsync(paracetamol.Id)).Single();

        // What the counter shows.
        var onScreen = GstCalculator.Bill(
        [
            GstCalculator.Line(batch.Mrp, batch.UnitsPerPack, 9, 0m, 12m)
        ]);

        var sale = await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Walk-in", PaymentMode = PaymentMode.Cash },
            [new SaleLine
            {
                ProductId = paracetamol.Id,
                BatchId = batch.Id,
                ProductName = paracetamol.Name,
                BatchNo = batch.BatchNo,
                ExpiryDate = batch.ExpiryDate,
                Quantity = 9,
                UnitsPerPack = batch.UnitsPerPack,
                PackLabel = paracetamol.PackSize,
                Mrp = batch.Mrp,
                GstRate = 12m
            }]);

        Assert.Equal(onScreen.Net, sale.NetAmount);
        Assert.Equal(18m, sale.NetAmount);
    }

    [Fact]
    public async Task Every_repacked_batch_is_recorded()
    {
        var paracetamol = await GivenTheReportedShopAsync();
        await _pharmacy.RepackAsync(paracetamol.Id, 15, by: "Yugandhar");

        var adjustment = (await _pharmacy.GetAdjustmentsAsync()).Single();

        Assert.Equal(59, adjustment.QuantityBefore);
        Assert.Equal(885, adjustment.QuantityAfter);
        Assert.Equal(AdjustmentReason.EntryError, adjustment.Reason);
        Assert.Contains("Pack size corrected", adjustment.Notes);
        Assert.Equal("Yugandhar", adjustment.AdjustedBy);
    }

    [Fact]
    public async Task Repacking_a_medicine_that_is_already_right_does_nothing()
    {
        var paracetamol = await GivenTheReportedShopAsync();
        await _pharmacy.RepackAsync(paracetamol.Id, 15);

        var preview = await _pharmacy.PreviewRepackAsync(paracetamol.Id, 15);
        Assert.False(preview.AnythingToDo);

        Assert.Equal(0, await _pharmacy.RepackAsync(paracetamol.Id, 15));
        Assert.Single(await _pharmacy.GetAdjustmentsAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
