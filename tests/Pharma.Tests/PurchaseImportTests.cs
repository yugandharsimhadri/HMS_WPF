using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;
using Pharma.Data.Import;

namespace Pharma.Tests;

/// <summary>
/// Imports the suppliers' real bills into a real database. What matters most here
/// is that stock is added to the shelf and never replaces what is on it.
/// </summary>
public class PurchaseImportTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"import-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PharmacyService _pharmacy;
    private readonly PurchaseImportService _import;

    public PurchaseImportTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _provider = services.BuildServiceProvider();

        _factory = _provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = _factory.CreateDbContext()) db.Database.Migrate();

        _pharmacy = new PharmacyService(_factory);
        _import = new PurchaseImportService(_factory, _pharmacy);
    }

    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static ImportProfile ProfileB() => new()
    {
        Name = "Profile B",
        ColumnMap = StandardMap,
        DateFormats = "dd-MM-yyyy|d-M-yyyy",
        ExpiryFormats = "MMM-yy|MMM-yyyy",
        DefaultGstRate = 5m
    };

    private const string StandardMap = """
        BillNo=FeedNo
        BillDate=FeedDate
        CustomerName=CustName
        SubTotal=SubTotal
        DiscountPercent=DisPer
        TotalDiscount=SumDis
        TaxableValue=GstVal1
        TaxAmount=Gst1
        RoundOff=Rounding
        NetAmount=NetAmt
        ProductCode=ProdCode
        ProductName=ProdName
        PackSize=Packing
        BatchNo=BatchNo
        Quantity=Qty
        FreeQuantity=Free
        Rate=Rate
        Mrp=Mrp
        LineValue=ProValue
        GstPercent=IGstPer
        Expiry=Expiry
        Manufacturer=ComName
        HsnCode=HsnCode
        PreviousMrp=Mrp_Old
        """;

    private async Task<ImportPreview> PreviewProfileB()
    {
        var profile = ProfileB();
        var bill = new VendorBillParser(profile).Parse(Fixture("Profile_B.csv"));
        var preview = await _import.PreviewAsync(bill, profile, "Profile_B.csv");
        preview.SupplierName = "Test Distributors";
        return preview;
    }

    // ── Preview ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unseen_bill_lists_every_line_as_a_new_medicine()
    {
        var preview = await PreviewProfileB();

        Assert.True(preview.CanImport);
        Assert.Equal(9, preview.Lines.Count);
        Assert.Equal(9, preview.NewMedicines);   // lines, not distinct medicines
        Assert.Equal(0, preview.NeedsChecking);
    }

    [Fact]
    public async Task Units_per_pack_is_read_where_the_packing_says_so_and_flagged_where_it_does_not()
    {
        var preview = await PreviewProfileB();

        var gummies = preview.Lines.Single(l => l.ProductName.StartsWith("FERRO POPS"));
        Assert.Equal(30, gummies.UnitsPerPack);        // "30s"
        Assert.False(gummies.UnitsAssumed);

        var syrup = preview.Lines.First(l => l.ProductName.StartsWith("CALCIMAX"));
        Assert.Equal(1, syrup.UnitsPerPack);           // "200ML" is one bottle
        Assert.True(syrup.UnitsAssumed);               // flagged for the user to correct
    }

    // ── Commit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Importing_creates_the_medicines_and_puts_the_stock_on_the_shelf()
    {
        var result = await _import.CommitAsync(await PreviewProfileB());

        Assert.Equal(9, result.Lines);

        // Nine lines but eight medicines — CALPOL arrives on two batches.
        Assert.Equal(8, result.ProductsCreated);

        // 143 paid packs + 29 free, with the gummies counting 30 units per pack.
        Assert.Equal(172 - 30 + (30 * 30), result.UnitsAdded);

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(8, await db.Products.CountAsync());
        Assert.Equal(9, await db.Batches.CountAsync());
    }

    [Fact]
    public async Task A_countable_pack_lands_as_loose_sellable_units()
    {
        await _import.CommitAsync(await PreviewProfileB());

        await using var db = await _factory.CreateDbContextAsync();
        var gummies = await db.Products.Include(p => p.Batches)
            .FirstAsync(p => p.Name.StartsWith("FERRO POPS"));

        // 20 paid + 10 free = 30 jars, each holding 30 gummies.
        Assert.Equal(30, gummies.UnitsPerPack);
        Assert.True(gummies.AllowLooseSale);
        Assert.Equal(900, gummies.Batches.Single().QtyOnHand);
    }

    [Fact]
    public async Task A_syrup_stays_one_unit_per_bottle()
    {
        await _import.CommitAsync(await PreviewProfileB());

        await using var db = await _factory.CreateDbContextAsync();
        var syrup = await db.Products.Include(p => p.Batches)
            .FirstAsync(p => p.Name.StartsWith("CALCIMAX"));

        Assert.Equal(1, syrup.UnitsPerPack);
        Assert.False(syrup.AllowLooseSale);
        Assert.Equal(10, syrup.Batches.Single().QtyOnHand);   // 9 paid + 1 free
    }

    [Fact]
    public async Task The_same_medicine_on_two_batches_becomes_two_batches()
    {
        await _import.CommitAsync(await PreviewProfileB());

        await using var db = await _factory.CreateDbContextAsync();
        var calpol = await db.Products.Include(p => p.Batches)
            .FirstAsync(p => p.Name == "CALPOL PED DROPS");

        Assert.Equal(2, calpol.Batches.Count);
        Assert.Equal(["NA497", "NA504"], calpol.Batches.Select(b => b.BatchNo).Order());
    }

    // ── Appending, not overwriting ──────────────────────────────────────────

    [Fact]
    public async Task A_second_delivery_of_the_same_batch_adds_to_the_shelf()
    {
        // Manually stocked first, the way a pharmacist keys in an opening count.
        var product = new Product { Name = "CALPOL PED DROPS", PackSize = "15ML", GstRate = 5m };
        await _pharmacy.SaveProductAsync(product);

        await _pharmacy.ReceiveStockAsync(
            new StockEntry { SupplierInvoiceNo = "MANUAL-1", SupplierName = "Counted by hand" },
            [new StockEntryItem
            {
                ProductId = product.Id, BatchNo = "NA497",
                ExpiryDate = new DateTime(2028, 1, 31),
                Quantity = 12, PurchaseRate = 20m, Mrp = 30.98m
            }]);

        var before = (await _pharmacy.GetSellableBatchesAsync(product.Id))
            .Single(b => b.BatchNo == "NA497").QtyOnHand;

        Assert.Equal(12, before);

        // Now the vendor bill arrives carrying the same batch.
        await _import.CommitAsync(await PreviewProfileB());

        var after = (await _pharmacy.GetSellableBatchesAsync(product.Id))
            .Single(b => b.BatchNo == "NA497").QtyOnHand;

        // 12 counted by hand plus 5 on the bill — not replaced by 5.
        Assert.Equal(17, after);
    }

    [Fact]
    public async Task Importing_matches_the_medicine_that_is_already_in_the_catalogue()
    {
        var product = new Product { Name = "MOKTEL DROP", PackSize = "30ml", GstRate = 5m };
        await _pharmacy.SaveProductAsync(product);

        var preview = await PreviewProfileB();
        var line = preview.Lines.Single(l => l.Source.ProductName == "MOKTEL DROP");

        Assert.Equal(MatchKind.ByName, line.Match);
        Assert.Equal(product.Id, line.Product!.Id);
        Assert.Equal(8, preview.NewMedicines);          // one fewer line to create
    }

    // ── Guards ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_same_bill_cannot_be_imported_twice()
    {
        await _import.CommitAsync(await PreviewProfileB());

        var second = await PreviewProfileB();

        Assert.True(second.AlreadyImported);
        Assert.False(second.CanImport);
        Assert.Contains("would double the stock", second.BlockedReason);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _import.CommitAsync(second));

        // Nothing was added the second time.
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(9, await db.Batches.CountAsync());
    }

    [Fact]
    public async Task The_vendors_own_code_is_remembered_for_next_time()
    {
        await _import.CommitAsync(await PreviewProfileB());

        await using var db = await _factory.CreateDbContextAsync();
        var codes = await db.VendorProductCodes.ToListAsync();

        Assert.Equal(8, codes.Count);                    // 9 lines, CALPOL twice
        Assert.All(codes, c => Assert.Equal("Profile B", c.VendorProfile));
        Assert.Contains(codes, c => c.Code == "55932");   // FERRO POPS
    }

    [Fact]
    public async Task A_later_bill_from_the_same_vendor_matches_on_that_code()
    {
        await _import.CommitAsync(await PreviewProfileB());

        // The same file stands in for the vendor's next bill; only the number
        // differs in reality, and matching is what is under test.
        var profile = ProfileB();
        var bill = new VendorBillParser(profile).Parse(Fixture("Profile_B.csv"));
        bill.BillNo = "SW09999";

        var preview = await _import.PreviewAsync(bill, profile, "next.csv");

        Assert.True(preview.CanImport);
        Assert.Equal(0, preview.NewMedicines);
        Assert.All(preview.Lines, l => Assert.Equal(MatchKind.ByVendorCode, l.Match));
    }

    [Fact]
    public async Task Nothing_is_written_when_the_file_could_not_be_read()
    {
        // Profile A's numeric expiry rules against Profile B's month names.
        var wrong = new ImportProfile
        {
            Name = "Profile A", ColumnMap = StandardMap,
            DateFormats = "dd/MM/yyyy", ExpiryFormats = "M/yyyy"
        };

        var bill = new VendorBillParser(wrong).Parse(Fixture("Profile_B.csv"));
        var preview = await _import.PreviewAsync(bill, wrong, "Profile_B.csv");

        Assert.False(preview.CanImport);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _import.CommitAsync(preview));

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Batches.CountAsync());
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
