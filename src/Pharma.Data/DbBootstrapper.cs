using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

public static class DbBootstrapper
{
    /// <summary>
    /// Set CLINICDESK_DB to run against a different database file. The UI tests use
    /// this to drive a throwaway database instead of the live one in ProgramData.
    /// </summary>
    public const string PathOverrideVariable = "CLINICDESK_DB";

    /// <summary>Database file lives in ProgramData so every Windows user of the PC shares it.</summary>
    public static string DatabasePath
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(PathOverrideVariable);
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(overridden)!);
                return overridden;
            }

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "TwinkleHMS");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, "twinkle.db");
            MoveLegacyDatabase(path);
            return path;
        }
    }

    /// <summary>
    /// The application was called ClinicDesk before it was branded. Carry an
    /// existing database over on first launch rather than silently starting empty.
    /// </summary>
    private static void MoveLegacyDatabase(string newPath)
    {
        if (File.Exists(newPath)) return;

        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ClinicDesk", "clinicdesk.db");

        if (!File.Exists(legacy)) return;

        try
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                if (File.Exists(legacy + suffix)) File.Copy(legacy + suffix, newPath + suffix);
            }
        }
        catch (IOException)
        {
            // Nothing carried over; the app starts with a freshly seeded database.
        }
    }

    public static string ConnectionString => $"Data Source={DatabasePath}";

    /// <summary>Applies migrations, takes a dated backup first, and seeds a usable starter set.</summary>
    public static async Task InitialiseAsync(IDbContextFactory<AppDbContext> factory)
    {
        BackupOnce();

        await using var db = await factory.CreateDbContextAsync();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0) AppLog.Info($"Applying migrations: {string.Join(", ", pending)}");

        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await Import.ImportProfileSeeder.SeedAsync(db);
    }

    private static void BackupOnce()
    {
        if (!File.Exists(DatabasePath)) return;

        var backupDir = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "backups");
        Directory.CreateDirectory(backupDir);

        var target = Path.Combine(backupDir, $"twinkle-{DateTime.Now:yyyyMMdd}.db");
        if (File.Exists(target)) return;   // one backup per day is enough

        try
        {
            File.Copy(DatabasePath, target);

            // Keep the last 14 daily backups.
            foreach (var stale in new DirectoryInfo(backupDir)
                         .GetFiles("twinkle-*.db")
                         .OrderByDescending(f => f.Name)
                         .Skip(14))
            {
                stale.Delete();
            }
        }
        catch (IOException)
        {
            // A failed backup must never stop the shop from opening.
        }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Doctors.AnyAsync())
        {
            db.Doctors.Add(new Doctor
            {
                Name = "Dr. A. Kumar",
                Speciality = "Paediatrics",
                RegistrationNo = "REG-00000",
                ConsultationFee = 300m,
                IsActive = true
            });
        }

        if (!await db.Products.AnyAsync())
        {
            // A handful of everyday drugs so the counter is usable on first launch.
            db.Products.AddRange(
                new Product { Name = "Paracetamol 500mg", Manufacturer = "Generic", PackSize = "15 TAB", GstRate = 12m, RackLocation = "A1", ReorderLevel = 100 },
                new Product { Name = "Amoxicillin 500mg", Manufacturer = "Generic", PackSize = "10 CAP", GstRate = 12m, Schedule = DrugSchedule.H, RackLocation = "A2", ReorderLevel = 50 },
                new Product { Name = "Cetirizine 10mg", Manufacturer = "Generic", PackSize = "10 TAB", GstRate = 12m, RackLocation = "B1", ReorderLevel = 50 },
                new Product { Name = "Pantoprazole 40mg", Manufacturer = "Generic", PackSize = "15 TAB", GstRate = 12m, RackLocation = "B2", ReorderLevel = 50 },
                new Product { Name = "ORS Powder", Manufacturer = "Generic", PackSize = "21.8 G", GstRate = 5m, RackLocation = "C1", ReorderLevel = 30 },
                new Product { Name = "Cough Syrup 100ml", Manufacturer = "Generic", PackSize = "100 ML", GstRate = 12m, RackLocation = "C2", ReorderLevel = 20 });
        }

        await db.SaveChangesAsync();
    }
}
