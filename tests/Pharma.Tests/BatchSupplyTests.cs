using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// What a batch remembers about where it came from.
///
/// Free packs were captured when receiving and then thrown away, so every cost
/// figure derived from a batch overstated what was paid: ten paid for and eleven
/// received makes the real cost rate x 10 / 11. The supplier's bill number was
/// only on the goods-inward document, which is most of the work of reconciling.
/// </summary>
public class BatchSupplyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pharma-supply-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly PharmacyService _pharmacy;

    public BatchSupplyTests()
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
            Name = "Supply Drug 500mg", Manufacturer = "Generic",
            PackSize = "10 TAB", UnitsPerPack = 10, GstRate = 12m
        };

        await _pharmacy.SaveProductAsync(product);
        return product;
    }

    [Fact]
    public async Task A_batch_remembers_the_scheme_it_came_in_on()
    {
        var product = await GivenAMedicineAsync();

        // The classic 10+1.
        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "SW Distributors", SupplierInvoiceNo = "SW-4471" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "S1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 10, FreeQuantity = 1, UnitsPerPack = 10,
                PurchaseRate = 22m, Mrp = 30m
            }]);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        Assert.Equal(10, batch.PacksReceived);
        Assert.Equal(1, batch.FreePacks);
        Assert.Equal(110, batch.QtyOnHand);              // eleven strips on the shelf

        // Paid for ten, got eleven: ₹22.00 becomes ₹20.00 a pack.
        Assert.Equal(20m, batch.EffectivePackCost);
    }

    [Fact]
    public async Task A_batch_knows_whose_bill_it_arrived_on()
    {
        var product = await GivenAMedicineAsync();

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "SW Distributors", SupplierInvoiceNo = "SW-4471" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "S1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5, UnitsPerPack = 10, PurchaseRate = 22m, Mrp = 30m
            }]);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        Assert.Equal("SW Distributors", batch.SupplierName);
        Assert.Equal("SW-4471", batch.SupplierInvoiceNo);
    }

    [Fact]
    public async Task A_second_delivery_of_the_same_batch_adds_to_what_it_cost()
    {
        var product = await GivenAMedicineAsync();

        for (var i = 0; i < 2; i++)
        {
            await _pharmacy.ReceiveStockAsync(
                new StockEntry { SupplierName = "SW", SupplierInvoiceNo = $"SW-{i}" },
                [new StockEntryItem
                {
                    ProductId = product.Id, BatchNo = "S1",
                    ExpiryDate = DateTime.Today.AddYears(2),
                    Quantity = 10, FreeQuantity = 1, UnitsPerPack = 10,
                    PurchaseRate = 22m, Mrp = 30m
                }]);
        }

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        // The scheme accumulates rather than being replaced, so the cost stays true.
        Assert.Equal(20, batch.PacksReceived);
        Assert.Equal(2, batch.FreePacks);
        Assert.Equal(220, batch.QtyOnHand);
        Assert.Equal(20m, batch.EffectivePackCost);
    }

    [Fact]
    public async Task No_scheme_means_the_cost_is_simply_what_was_paid()
    {
        var product = await GivenAMedicineAsync();

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierName = "SW" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "S1",
                ExpiryDate = DateTime.Today.AddYears(2),
                Quantity = 5, UnitsPerPack = 10, PurchaseRate = 22m, Mrp = 30m
            }]);

        var batch = (await _pharmacy.GetSellableBatchesAsync(product.Id)).Single();

        Assert.Equal(0, batch.FreePacks);
        Assert.Equal(22m, batch.EffectivePackCost);
    }

    [Fact]
    public async Task Composition_and_storage_survive_an_edit()
    {
        var product = await GivenAMedicineAsync();

        product.Composition = "Paracetamol 500mg + Caffeine 30mg";
        product.Storage = "Below 25°C";
        await _pharmacy.SaveProductAsync(product);

        var saved = (await _pharmacy.SearchProductsAsync("Supply Drug")).Single();

        Assert.Equal("Paracetamol 500mg + Caffeine 30mg", saved.Composition);
        Assert.Equal("Below 25°C", saved.Storage);
    }

    /// <summary>
    /// Editing a medicine has now silently dropped a field three times — first
    /// units-per-pack and the loose-sale flag, then the generic name, then
    /// composition and storage. Each time the form accepted the change and
    /// nothing happened.
    ///
    /// So rather than listing the fields anyone remembers, this sets every
    /// writable one on the record by reflection and checks they all come back.
    /// A field added later is covered without anyone thinking about it.
    /// </summary>
    [Fact]
    public async Task Editing_a_medicine_keeps_every_field_there_is()
    {
        var product = await GivenAMedicineAsync();

        // Not part of what a medicine is, or set by the service itself.
        string[] skip =
        [
            nameof(Product.Id), nameof(Product.CreatedAt), nameof(Product.UpdatedAt),
            nameof(Product.IsDeleted), nameof(Product.Batches), nameof(Product.SearchKey),
            nameof(Product.Name), nameof(Product.Manufacturer), nameof(Product.PackSize)
        ];

        var fields = typeof(Product).GetProperties()
            .Where(p => p.CanWrite && !skip.Contains(p.Name))
            .ToList();

        Assert.NotEmpty(fields);

        foreach (var field in fields) field.SetValue(product, Distinct(field.PropertyType, field));

        await _pharmacy.SaveProductAsync(product);

        var saved = (await _pharmacy.SearchProductsAsync("Supply Drug")).Single();

        foreach (var field in fields)
        {
            Assert.True(
                Equals(field.GetValue(product), field.GetValue(saved)),
                $"{field.Name} was not kept: set {field.GetValue(product)}, got back {field.GetValue(saved)}");
        }
    }

    /// <summary>A value visibly different from the default, whatever the type.</summary>
    private static object? Distinct(Type type, System.Reflection.PropertyInfo field)
    {
        if (type == typeof(string)) return $"edited {field.Name}";
        if (type == typeof(int)) return 7;
        if (type == typeof(decimal)) return 5m;
        if (type == typeof(bool)) return false;

        if (type.IsEnum)
        {
            // Anything other than what it holds now, so a dropped copy shows up.
            var values = Enum.GetValues(type).Cast<object>().ToList();
            return values[^1];
        }

        return null;
    }

    public void Dispose()
    {
        _provider.Dispose();
        try { File.Delete(_dbPath); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
