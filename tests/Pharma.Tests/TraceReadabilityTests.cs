using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The trace has to be good enough that a bill can be reconstructed from the log
/// alone — which medicine, which batch, what quantity, what price, and what the
/// stock went from and to. That is the whole point of it: a clinic PC has no
/// debugger, and the log is the only account of what happened.
/// </summary>
[Collection("Logging")]
public class TraceReadabilityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"twinkle-readable-{Guid.NewGuid():N}");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-trace-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public TraceReadabilityTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, _dir);

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        var factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(factory);
    }

    [Fact]
    public async Task A_bill_can_be_reconstructed_from_the_log_alone()
    {
        var paracetamol = new Product
        {
            Name = "Paracetamol 500mg",
            PackSize = "10 TAB",
            UnitsPerPack = 10,
            GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(paracetamol);

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "Local Distributors", SupplierInvoiceNo = "INV-42" },
            [new StockEntryItem
            {
                ProductId = paracetamol.Id,
                BatchNo = "PC1234",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5,
                UnitsPerPack = 10,
                PurchaseRate = 21m,
                Mrp = 30m
            }]);

        var (allocations, _) = await _pharmacy.AllocateAsync(paracetamol.Id, 9);
        var batch = allocations[0].Batch;

        await _pharmacy.SaveSaleAsync(
            new Sale { CustomerName = "Aarav Sharma", PaymentMode = PaymentMode.Cash },
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

        var log = File.ReadAllText(AppLog.CurrentFile);

        // Who the bill was for, and how big.
        Assert.Contains("→ SaveSaleAsync", log);
        Assert.Contains("customer='Aarav Sharma'", log);

        // Every input to the arithmetic.
        Assert.Contains("line 'Paracetamol 500mg' batch=PC1234 qty=9 perPack=10 mrp=30.00", log);

        // What it came to, and what left the shelf.
        Assert.Contains("net=27.00", log);
        Assert.Contains("batch PC1234 50 → 41", log);

        // The bill number, so the log ties to the record on the counter's screen.
        Assert.Matches(@"← SaveSaleAsync#\d+ \d+ms \[INV", log);

        // Receiving is just as traceable: packs in, units out.
        Assert.Contains("new batch PC1234", log);
        Assert.Contains("packs=5+0 perPack=10 = 50 unit(s)", log);

        // And the allocation that chose the batch.
        Assert.Contains("→ AllocateAsync", log);
        Assert.Contains("PC1234×9", log);
    }

    public void Dispose()
    {
        _provider.Dispose();

        Environment.SetEnvironmentVariable(AppLog.DirectoryOverrideVariable, null);

        try { File.Delete(_dbPath); } catch (IOException) { }
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }

        GC.SuppressFinalize(this);
    }
}
