using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

/// <summary>A sale line as assembled at the counter, before it is persisted.</summary>
public class SaleLine
{
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string HsnCode { get; set; } = "3004";
    public int Quantity { get; set; }
    public decimal Mrp { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GstRate { get; set; }
    public DrugSchedule Schedule { get; set; }
}

public class PharmacyService(IDbContextFactory<AppDbContext> factory)
{
    // ── Products ───────────────────────────────────────────────────────────

    public async Task<List<Product>> SearchProductsAsync(string? term, int take = 50)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Products.Include(p => p.Batches).Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            q = q.Where(p => p.Name.Contains(term)
                          || (p.Manufacturer != null && p.Manufacturer.Contains(term))
                          || (p.RackLocation != null && p.RackLocation.Contains(term)));
        }

        return await q.OrderBy(p => p.Name).Take(take).ToListAsync();
    }

    public async Task SaveProductAsync(Product product)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (product.Id != Guid.Empty && await db.Products.AnyAsync(p => p.Id == product.Id))
        {
            var existing = await db.Products.FirstAsync(p => p.Id == product.Id);
            existing.Name = product.Name;
            existing.Manufacturer = product.Manufacturer;
            existing.PackSize = product.PackSize;
            existing.HsnCode = product.HsnCode;
            existing.GstRate = product.GstRate;
            existing.Schedule = product.Schedule;
            existing.RackLocation = product.RackLocation;
            existing.ReorderLevel = product.ReorderLevel;
            existing.IsActive = product.IsActive;
        }
        else
        {
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
    }

    // ── Stock ──────────────────────────────────────────────────────────────

    /// <summary>Batches with stock left, nearest expiry first — the order stock is dispensed in.</summary>
    public async Task<List<Batch>> GetSellableBatchesAsync(Guid productId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Batches
            .Where(b => !b.IsDeleted && b.ProductId == productId && b.QtyOnHand > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();
    }

    /// <summary>
    /// Every batch currently on the shelf — the Stock Register's source. Reads the
    /// same Batch.QtyOnHand that Product.StockOnHand, Low Stock and Expiring Soon
    /// already read, so there is only ever one place stock is calculated from.
    /// </summary>
    public async Task<List<Batch>> GetAllBatchesAsync(bool includeZeroStock = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Batches.Include(b => b.Product).Where(b => !b.IsDeleted);

        if (!includeZeroStock) q = q.Where(b => b.QtyOnHand > 0);

        return await q.OrderBy(b => b.Product.Name).ThenBy(b => b.ExpiryDate).ToListAsync();
    }

    /// <summary>Receives a supplier consignment. This is the only way stock enters the system.</summary>
    public async Task<StockEntry> ReceiveStockAsync(StockEntry entry, IEnumerable<StockEntryItem> items)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        entry.EntryNo = await NumberService.NextAsync(db, NumberService.StockEntry);
        entry.TotalAmount = 0;
        db.StockEntries.Add(entry);

        foreach (var item in items)
        {
            if (item.Quantity <= 0 && item.FreeQuantity <= 0) continue;

            item.StockEntryId = entry.Id;
            db.StockEntryItems.Add(item);
            entry.TotalAmount += item.Quantity * item.PurchaseRate;

            // Same drug + same batch number in the same consignment tops up one batch.
            var batch = await db.Batches.FirstOrDefaultAsync(
                b => b.ProductId == item.ProductId && b.BatchNo == item.BatchNo && !b.IsDeleted);

            if (batch is null)
            {
                db.Batches.Add(new Batch
                {
                    ProductId = item.ProductId,
                    BatchNo = item.BatchNo,
                    ExpiryDate = item.ExpiryDate,
                    Mrp = item.Mrp,
                    PurchaseRate = item.PurchaseRate,
                    QtyOnHand = item.Quantity + item.FreeQuantity,
                    SupplierName = entry.SupplierName,
                    ReceivedOn = entry.EntryDate
                });
            }
            else
            {
                batch.QtyOnHand += item.Quantity + item.FreeQuantity;
                batch.Mrp = item.Mrp;                 // latest printed MRP wins
                batch.PurchaseRate = item.PurchaseRate;
                batch.ExpiryDate = item.ExpiryDate;
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        AppLog.Info($"Stock entry {entry.EntryNo} received from {entry.SupplierName ?? "(no supplier)"}.");
        return entry;
    }

    public async Task AdjustStockAsync(Guid batchId, int newQuantity)
    {
        await using var db = await factory.CreateDbContextAsync();
        var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch is null) return;

        batch.QtyOnHand = Math.Max(0, newQuantity);
        await db.SaveChangesAsync();
    }

    // ── Sales ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a bill: validates stock, computes GST out of the MRP, deducts the
    /// batches and records any Schedule H1 lines. All or nothing.
    /// </summary>
    public async Task<Sale> SaveSaleAsync(Sale sale, IReadOnlyList<SaleLine> lines)
    {
        if (lines.Count == 0) throw new InvalidOperationException("Add at least one medicine to the bill.");

        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var computed = new List<LineAmounts>(lines.Count);

        foreach (var line in lines)
        {
            var batch = await db.Batches.FirstOrDefaultAsync(b => b.Id == line.BatchId)
                        ?? throw new InvalidOperationException($"Batch not found for {line.ProductName}.");

            if (line.Quantity <= 0)
                throw new InvalidOperationException($"Quantity must be at least 1 for {line.ProductName}.");

            if (batch.QtyOnHand < line.Quantity)
                throw new InvalidOperationException(
                    $"Only {batch.QtyOnHand} left of {line.ProductName} (batch {batch.BatchNo}).");

            var amounts = GstCalculator.Line(line.Mrp, line.Quantity, line.DiscountPercent, line.GstRate);
            computed.Add(amounts);

            batch.QtyOnHand -= line.Quantity;

            db.SaleItems.Add(new SaleItem
            {
                SaleId = sale.Id,
                ProductId = line.ProductId,
                BatchId = line.BatchId,
                ProductName = line.ProductName,
                BatchNo = line.BatchNo,
                ExpiryDate = line.ExpiryDate,
                HsnCode = line.HsnCode,
                Quantity = line.Quantity,
                Mrp = line.Mrp,
                DiscountPercent = line.DiscountPercent,
                GstRate = line.GstRate,
                TaxableAmount = amounts.Taxable,
                GstAmount = amounts.Gst,
                LineTotal = amounts.Net
            });

            if (line.Schedule == DrugSchedule.H1)
            {
                db.H1Register.Add(new H1RegisterEntry
                {
                    SoldOn = sale.BillDate,
                    BillNo = sale.BillNo,
                    ProductName = line.ProductName,
                    BatchNo = line.BatchNo,
                    Quantity = line.Quantity,
                    PatientName = sale.CustomerName,
                    DoctorName = sale.DoctorName
                });
            }
        }

        var bill = GstCalculator.Bill(computed);
        sale.GrossAmount = bill.Gross;
        sale.DiscountAmount = bill.Discount;
        sale.TaxableAmount = bill.Taxable;
        sale.CgstAmount = bill.Cgst;
        sale.SgstAmount = bill.Sgst;
        sale.RoundOff = bill.RoundOff;
        sale.NetAmount = bill.Net;
        sale.BillNo = await NumberService.NextAsync(db, NumberService.Bill);

        // The H1 rows were staged before the number existed — backfill them.
        foreach (var h1 in db.ChangeTracker.Entries<H1RegisterEntry>()
                             .Where(e => e.State == EntityState.Added))
        {
            h1.Entity.BillNo = sale.BillNo;
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        AppLog.Info($"Bill {sale.BillNo} saved: {lines.Count} line(s), net {sale.NetAmount:0.00}, {sale.PaymentMode}.");
        return sale;
    }

    public async Task<Sale?> GetSaleAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task<List<Sale>> GetSalesAsync(DateTime date) => GetSalesAsync(date, date);

    /// <summary>Sales whose bill date falls within [from, to], both dates inclusive.</summary>
    public async Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        var start = from.Date;
        var end = to.Date.AddDays(1);

        return await db.Sales.Include(s => s.Items)
            .Where(s => s.BillDate >= start && s.BillDate < end)
            .OrderByDescending(s => s.BillDate)
            .ToListAsync();
    }

    /// <summary>Schedule H1 statutory register entries within [from, to], both dates inclusive.</summary>
    public async Task<List<H1RegisterEntry>> GetH1RegisterAsync(DateTime from, DateTime to)
    {
        await using var db = await factory.CreateDbContextAsync();
        var start = from.Date;
        var end = to.Date.AddDays(1);

        return await db.H1Register
            .Where(h => h.SoldOn >= start && h.SoldOn < end)
            .OrderBy(h => h.SoldOn)
            .ToListAsync();
    }

    // ── Alerts ─────────────────────────────────────────────────────────────

    public async Task<List<Batch>> GetExpiringAsync(int withinDays = 90)
    {
        await using var db = await factory.CreateDbContextAsync();
        var cutoff = DateTime.Today.AddDays(withinDays);

        return await db.Batches.Include(b => b.Product)
            .Where(b => !b.IsDeleted && b.QtyOnHand > 0 && b.ExpiryDate <= cutoff)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();
    }

    public async Task<List<Product>> GetLowStockAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var products = await db.Products.Include(p => p.Batches)
            .Where(p => !p.IsDeleted && p.IsActive && p.ReorderLevel > 0)
            .ToListAsync();

        return products.Where(p => p.StockOnHand <= p.ReorderLevel)
                       .OrderBy(p => p.Name)
                       .ToList();
    }
}
