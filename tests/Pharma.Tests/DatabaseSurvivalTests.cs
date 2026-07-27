using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Starting the application must never cost a clinic its records.
///
/// The database lives outside the build output, so publishing a new version
/// cannot overwrite it — but the startup path still opens, migrates and seeds,
/// and any of those could in principle throw data away. These hold it to
/// opening what is there, creating one only when there is nothing, and leaving
/// the contents alone either way.
/// </summary>
public class DatabaseSurvivalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"twinkle-db-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public DatabaseSurvivalTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "twinkle.db");
    }

    /// <summary>One application start, against the database at this path.</summary>
    private async Task<T> StartAsync<T>(Func<PharmacyService, AppDbContext, Task<T>> work)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await DbBootstrapper.InitialiseAsync(factory);

        await using var db = await factory.CreateDbContextAsync();
        return await work(new PharmacyService(factory), db);
    }

    [Fact]
    public async Task A_first_start_creates_the_database_and_seeds_it()
    {
        Assert.False(File.Exists(_dbPath));

        var medicines = await StartAsync(async (_, db) => await db.Products.CountAsync());

        Assert.True(File.Exists(_dbPath));
        Assert.Equal(DbBootstrapper.StarterCatalogue().Length, medicines);
    }

    [Fact]
    public async Task A_second_start_opens_what_is_there_and_changes_nothing()
    {
        // A clinic's first day: a medicine of their own, and a patient.
        await StartAsync(async (pharmacy, db) =>
        {
            await pharmacy.SaveProductAsync(new Product
            {
                Name = "Their Own Medicine", Manufacturer = "Local", PackSize = "10 TAB", UnitsPerPack = 10
            });

            db.Patients.Add(new Patient { PatientNo = "P-1", Name = "Aarav", Phone = "9440011223", Age = 4 });
            await db.SaveChangesAsync();

            return 0;
        });

        var written = new FileInfo(_dbPath).LastWriteTimeUtc;

        // The next morning, the application starts again.
        var (medicines, patients, theirs) = await StartAsync(async (pharmacy, db) => (
            await db.Products.CountAsync(),
            await db.Patients.CountAsync(),
            (await pharmacy.SearchProductsAsync("Their Own")).Count));

        // Everything they entered is still there, and the seed did not run again.
        Assert.Equal(DbBootstrapper.StarterCatalogue().Length + 1, medicines);
        Assert.Equal(1, patients);
        Assert.Equal(1, theirs);

        Assert.True(File.Exists(_dbPath));
        Assert.True(new FileInfo(_dbPath).Length > 0);
        Assert.True(written <= new FileInfo(_dbPath).LastWriteTimeUtc);
    }

    [Fact]
    public async Task Starting_repeatedly_never_multiplies_the_starter_catalogue()
    {
        for (var start = 0; start < 4; start++)
            await StartAsync(async (_, db) => await db.Products.CountAsync());

        var medicines = await StartAsync(async (_, db) => await db.Products.CountAsync());

        Assert.Equal(DbBootstrapper.StarterCatalogue().Length, medicines);
    }

    [Fact]
    public async Task Stock_and_bills_survive_a_restart()
    {
        Guid productId = Guid.Empty;

        await StartAsync(async (pharmacy, _) =>
        {
            var product = new Product
            {
                Name = "Survivor 500mg", Manufacturer = "Local", PackSize = "10 TAB", UnitsPerPack = 10
            };

            await pharmacy.SaveProductAsync(product);
            productId = product.Id;

            await pharmacy.ReceiveStockAsync(
                new StockEntry { SupplierName = "Local" },
                [new StockEntryItem
                {
                    ProductId = product.Id, BatchNo = "S1",
                    ExpiryDate = DateTime.Today.AddYears(2),
                    Quantity = 5, UnitsPerPack = 10, PurchaseRate = 21m, Mrp = 30m
                }]);

            var batch = (await pharmacy.GetSellableBatchesAsync(product.Id)).Single();

            await pharmacy.SaveSaleAsync(
                new Sale { CustomerName = "Aarav", PaymentMode = PaymentMode.Cash },
                [new SaleLine
                {
                    ProductId = product.Id, BatchId = batch.Id, ProductName = product.Name,
                    BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate,
                    Quantity = 9, UnitsPerPack = 10, Mrp = 30m, GstRate = 12m
                }]);

            return 0;
        });

        var (onHand, bills) = await StartAsync(async (pharmacy, db) => (
            (await pharmacy.GetSellableBatchesAsync(productId)).Single().QtyOnHand,
            await db.Sales.CountAsync()));

        // Fifty received, nine sold, one bill — exactly as it was left.
        Assert.Equal(41, onHand);
        Assert.Equal(1, bills);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
