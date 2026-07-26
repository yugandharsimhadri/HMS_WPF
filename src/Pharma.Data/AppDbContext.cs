using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockEntryItem> StockEntryItems => Set<StockEntryItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<H1RegisterEntry> H1Register => Set<H1RegisterEntry>();
    public DbSet<ImportProfile> ImportProfiles => Set<ImportProfile>();
    public DbSet<VendorProductCode> VendorProductCodes => Set<VendorProductCode>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Money: 12,2 is plenty for a clinic and keeps SQLite storage predictable.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(12);
            property.SetScale(2);
        }

        b.Entity<Patient>(e =>
        {
            e.HasIndex(x => x.PatientNo).IsUnique();
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Name);
            e.Ignore(x => x.Display);
        });

        b.Entity<Visit>(e =>
        {
            e.HasIndex(x => x.VisitNo).IsUnique();
            e.HasIndex(x => x.ScheduledOn);
            e.HasOne(x => x.Patient).WithMany(p => p.Visits)
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany()
                .HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Prescription).WithOne(p => p.Visit)
                .HasForeignKey(p => p.VisitId).OnDelete(DeleteBehavior.Cascade);

            e.Ignore(x => x.IsWaiting);
            e.Ignore(x => x.PatientLine);
            e.Ignore(x => x.FeeBadge);
            e.Ignore(x => x.RowSummary);
            e.Ignore(x => x.WaitedFor);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Name);
            e.Ignore(x => x.StockOnHand);
            e.Ignore(x => x.PackDescription);
        });

        b.Entity<Batch>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.BatchNo });
            e.HasOne(x => x.Product).WithMany(p => p.Batches)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.IsExpired);
            e.Ignore(x => x.Display);
            e.Ignore(x => x.UnitPrice);
            e.Ignore(x => x.OnHand);
        });

        b.Entity<StockAdjustment>(e =>
        {
            e.HasIndex(x => x.AdjustedOn);
            e.HasOne(x => x.Batch).WithMany()
                .HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Change);
            e.Ignore(x => x.Direction);
        });

        b.Entity<VendorProductCode>(e =>
        {
            e.HasIndex(x => new { x.VendorProfile, x.Code }).IsUnique();
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StockEntry>(e =>
        {
            e.HasIndex(x => x.EntryNo).IsUnique();
            e.HasIndex(x => x.SupplierInvoiceNo);
            e.Ignore(x => x.WasImported);
            e.HasMany(x => x.Items).WithOne(i => i.StockEntry)
                .HasForeignKey(i => i.StockEntryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StockEntryItem>(e =>
        {
            e.Ignore(x => x.LineTotal);
            e.Ignore(x => x.UnitsReceived);
        });

        b.Entity<SaleItem>(e => e.Ignore(x => x.QuantityDescription));

        b.Entity<Sale>(e =>
        {
            e.HasIndex(x => x.BillNo).IsUnique();
            e.HasIndex(x => x.BillDate);
            e.HasMany(x => x.Items).WithOne(i => i.Sale)
                .HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Setting>(e => e.HasIndex(x => x.Key).IsUnique());
        b.Entity<Counter>(e => e.HasIndex(x => x.Name).IsUnique());

        b.Entity<ImportProfile>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Ignore(x => x.SplitDateFormats);
            e.Ignore(x => x.SplitExpiryFormats);
        });

        // Soft delete is filtered explicitly in the services rather than by a global
        // query filter: EF warns about required navigations into filtered entities,
        // and with this few queries the explicit .Where is easier to follow.

        base.OnModelCreating(b);
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(ct);
    }

    private void Stamp()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = DateTime.Now;
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = DateTime.Now;
        }
    }
}
