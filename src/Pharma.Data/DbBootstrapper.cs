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

    /// <summary>Set CLINICDESK_BACKUPDIR to write backups somewhere harmless —
    /// same reasoning as <see cref="PathOverrideVariable"/>, so a test never
    /// writes into a real clinic's C:\HMS\DBBackup.</summary>
    public const string BackupDirectoryOverrideVariable = "CLINICDESK_BACKUPDIR";

    /// <summary>Database file lives in ProgramData so every Windows user of the PC shares it.</summary>
    public static string DatabasePath
    {
        get
        {
            // Environment first so a test can point somewhere harmless, then the
            // config file, then the default under ProgramData.
            var overridden = Environment.GetEnvironmentVariable(PathOverrideVariable);
            if (string.IsNullOrWhiteSpace(overridden)) overridden = AppConfig.Current.DatabasePath;

            string path;

            if (!string.IsNullOrWhiteSpace(overridden))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(overridden)!);
                path = overridden;
            }
            else
            {
                // C:\HMS keeps the database, its backups and the logs together, where
                // a clinic can find them, copy them to a pen drive and hand them to
                // whoever is helping. ProgramData is hidden and nobody goes there.
                var dir = DefaultRoot("DB");
                Directory.CreateDirectory(dir);

                path = Path.Combine(dir, "ShivayaanHMS.db");
                MoveLegacyDatabase(path);
            }

            // Whichever path this resolved to — the shipped config's explicit
            // path, same as every existing installation actually uses today, or
            // the computed default above — a database still sitting next to it
            // under the old twinkle.db name is carried over to the new one.
            CarryOverFromTwinkleDb(path);

            return path;
        }
    }

    /// <summary>Where backups are written. Beside the database unless configured.</summary>
    public static string BackupDirectory
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(BackupDirectoryOverrideVariable);
            if (string.IsNullOrWhiteSpace(overridden)) overridden = AppConfig.Current.BackupDirectory;

            var dir = string.IsNullOrWhiteSpace(overridden) ? DefaultRoot("DBBackup") : overridden;
            Directory.CreateDirectory(dir);

            return dir;
        }
    }

    private static string DefaultRoot(string leaf)
    {
        var root = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "HMS");
        return Path.Combine(root, leaf);
    }

    /// <summary>
    /// The application was called ClinicDesk before it was branded. Carry an
    /// existing database over on first launch rather than silently starting empty.
    /// </summary>
    /// <remarks>
    /// It carries over <b>once</b>, and leaves a marker saying so. Without the
    /// marker it fired every time the database was missing, so deleting the
    /// database to start again silently resurrected the old one instead —
    /// complete with the records the shop was trying to be rid of, and with the
    /// copied file's original timestamp, which made it look untouched.
    /// </remarks>
    private static void MoveLegacyDatabase(string newPath)
    {
        if (File.Exists(newPath)) return;

        var carried = Path.Combine(Path.GetDirectoryName(newPath)!, ".carried-over");
        if (File.Exists(carried)) return;

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

            File.WriteAllText(carried, $"Carried over from {legacy} on {DateTime.Now:yyyy-MM-dd HH:mm}.");
            AppLog.Info($"Carried the ClinicDesk database over from {legacy}.");
        }
        catch (IOException)
        {
            // Nothing carried over; the app starts with a freshly seeded database.
        }
    }

    /// <summary>
    /// The database file was named twinkle.db before the product was renamed
    /// to Sivaayaan HMS. Carried over once, the same way and for the same
    /// reason as <see cref="MoveLegacyDatabase"/> — a clinic upgrading to a
    /// build with this rename in it must open on its own data, not a fresh,
    /// empty one just because the filename it is looking for changed under it.
    /// </summary>
    /// <remarks>
    /// Looked for beside <paramref name="newPath"/> rather than at a fixed
    /// location: every real installation's database path is whatever
    /// appsettings.json says (today, and for as long as that file has shipped
    /// with an explicit path in it), so the old file to carry over is
    /// wherever that same folder actually is — not assumed to be under
    /// C:\HMS\DB, which nothing enforces.
    /// </remarks>
    private static void CarryOverFromTwinkleDb(string newPath)
    {
        if (File.Exists(newPath)) return;

        var carried = Path.Combine(Path.GetDirectoryName(newPath)!, ".carried-over-from-twinkle");
        if (File.Exists(carried)) return;

        var legacy = Path.Combine(Path.GetDirectoryName(newPath)!, "twinkle.db");
        if (!File.Exists(legacy)) return;

        try
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                if (File.Exists(legacy + suffix)) File.Copy(legacy + suffix, newPath + suffix);
            }

            File.WriteAllText(carried, $"Carried over from {legacy} on {DateTime.Now:yyyy-MM-dd HH:mm}.");
            AppLog.Info($"Carried the database over from {legacy} to {newPath}.");
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
        // Said plainly, because "did it use my data or start a new one?" is the
        // first question after any new version is installed.
        var existed = File.Exists(DatabasePath);

        AppLog.Info(existed
            ? $"Opening the existing database at {DatabasePath}."
            : $"No database at {DatabasePath} — creating one.");

        BackupOnce();

        await using var db = await factory.CreateDbContextAsync();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count > 0)
        {
            AppLog.Info($"Applying migrations: {string.Join(", ", pending)}");

            // A copy of the file as it was, before the schema is touched.
            //
            // BackupOnce is not enough on the day of an upgrade: it takes one
            // backup per day and skips if today's already exists, so a clinic
            // that opened the application before installing the new version has
            // already spent it. And the daily ones are pruned on a rotation, so
            // the only copy of the old shape could be deleted a week later —
            // which is exactly when somebody asks for it.
            if (existed) BackupBeforeUpgrade(pending[^1]);
        }

        await db.Database.MigrateAsync();
        await SeedAsync(db);
        await Import.ImportProfileSeeder.SeedAsync(db);
        await DiagnosticTestSeeder.SeedAsync(db);
        await UserSeeder.SeedAsync(db);
    }

    /// <summary>The newest backup on disk, or null if there has never been one.</summary>
    public static FileInfo? LastBackup
    {
        get
        {
            try
            {
                return RoutineBackups()
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Copies the database now, whatever has already been taken today. This is
    /// the button a clinic presses before closing up, and before anything they
    /// are nervous about.
    /// </summary>
    /// <param name="clinicName">The clinic's current name, if the caller already
    /// has it to hand (Settings does) — saved a re-read. Falls back to reading
    /// it from the database itself when not given.</param>
    public static FileInfo BackupNow(string? clinicName = null)
    {
        if (!File.Exists(DatabasePath))
            throw new InvalidOperationException("There is no database to back up yet.");

        var target = Path.Combine(BackupDirectory, $"{ClinicSlug(clinicName)}-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        File.Copy(DatabasePath, target, overwrite: true);
        Prune();

        AppLog.Info($"Backup taken: {target}");
        return new FileInfo(target);
    }

    private static void Prune()
    {
        try
        {
            foreach (var stale in RoutineBackups()
                         .OrderByDescending(f => f.LastWriteTime)
                         .Skip(Math.Max(1, AppConfig.Current.BackupsToKeep)))
            {
                stale.Delete();
            }
        }
        catch (IOException)
        {
            // A backup that could not be pruned is still a backup.
        }
    }

    /// <summary>
    /// Every ordinary backup — daily and manual alike — whatever name the
    /// clinic was trading under on the day each one was taken. Matched by
    /// extension and by excluding the "pre-upgrade-" prefix rather than by
    /// a fixed name prefix of its own, so a clinic that renames itself
    /// between backups does not orphan the older ones from pruning or from
    /// being found as the most recent.
    /// </summary>
    private static IEnumerable<FileInfo> RoutineBackups()
        => new DirectoryInfo(BackupDirectory).GetFiles("*.db")
            .Where(f => !f.Name.StartsWith("pre-upgrade-", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Copies the database before any migration runs, under a name the daily
    /// rotation never touches. Kept for good: it is the only thing that can put
    /// a clinic back where it was if an upgrade goes wrong.
    /// </summary>
    private static void BackupBeforeUpgrade(string upgradingTo)
    {
        // "pre-upgrade-", not the clinic's name: RoutineBackups() excludes
        // this prefix by name specifically so these are never pruned or
        // reported as the newest ordinary backup.
        var target = Path.Combine(BackupDirectory, $"pre-upgrade-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        try
        {
            File.Copy(DatabasePath, target);
            AppLog.Info($"Backed up before upgrading to {upgradingTo}: {target}");
        }
        catch (IOException ex)
        {
            // Said loudly, because carrying on means migrating a database with
            // no copy of how it looked beforehand.
            AppLog.Error($"Could not back up before upgrading to {upgradingTo}. The upgrade will still be attempted.", ex);
        }
    }

    private static void BackupOnce()
    {
        if (!File.Exists(DatabasePath)) return;

        var target = Path.Combine(BackupDirectory, $"{ClinicSlug()}-{DateTime.Now:yyyyMMdd}.db");
        if (File.Exists(target)) return;   // one automatic backup per day is enough

        try
        {
            File.Copy(DatabasePath, target);
            Prune();
        }
        catch (IOException)
        {
            // A failed backup must never stop the shop from opening.
        }
    }

    /// <summary>
    /// The clinic's own name, made safe to put in a filename. Read with a
    /// plain SQL query against the database file rather than through
    /// AppDbContext: <see cref="BackupOnce"/> runs before migrations, when
    /// EF's model cannot be trusted to match whatever shape the database is
    /// actually in — a query for one column of one table survives schema
    /// changes that a full context would not.
    /// </summary>
    private static string ClinicSlug(string? providedName = null)
    {
        var name = providedName;

        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Value FROM Settings WHERE Key = 'clinic.name' LIMIT 1";
                name = command.ExecuteScalar() as string;
            }
            catch (Exception)
            {
                // No Settings table yet, no row yet, or the file is not
                // readable as a database at all — the backup still has to
                // happen, just without the clinic's name on it.
            }
        }

        if (string.IsNullOrWhiteSpace(name)) return "clinic";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim(), @"\s+", "-");

        return string.IsNullOrWhiteSpace(cleaned) ? "clinic" : cleaned;
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
            var catalogue = StarterCatalogue();

            // The catalogue is keyed on brand, maker and pack, and a unique index
            // enforces it. Seeding is a way into the catalogue like any other, so
            // it sets the key — without it all six collide on an empty one and
            // the application cannot open its own database.
            foreach (var product in catalogue) product.SearchKey = product.BuildKey();

            db.Products.AddRange(catalogue);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A handful of everyday drugs so the counter is usable on first launch.
    ///
    /// UnitsPerPack is stated on every one of them. Leaving it at its default of
    /// 1 while the pack size says "15 TAB" makes the shop sell whole strips to
    /// anyone who asks for tablets, at fifteen times the price, and nothing
    /// anywhere reports an error.
    /// </summary>
    public static Product[] StarterCatalogue() =>
    [
        new Product { Name = "Paracetamol 500mg", GenericName = "Paracetamol", Manufacturer = "Generic",
                      PackSize = "15 TAB", UnitsPerPack = 15, AllowLooseSale = true,
                      GstRate = 12m, RackLocation = "A1", ReorderLevel = 100 },

        new Product { Name = "Amoxicillin 500mg", GenericName = "Amoxicillin", Manufacturer = "Generic",
                      PackSize = "10 CAP", UnitsPerPack = 10, AllowLooseSale = true,
                      DispensingUnit = DispensingUnit.Capsule,
                      GstRate = 12m, Schedule = DrugSchedule.H, RackLocation = "A2", ReorderLevel = 50 },

        new Product { Name = "Cetirizine 10mg", GenericName = "Cetirizine", Manufacturer = "Generic",
                      PackSize = "10 TAB", UnitsPerPack = 10, AllowLooseSale = true,
                      GstRate = 12m, RackLocation = "B1", ReorderLevel = 50 },

        new Product { Name = "Pantoprazole 40mg", GenericName = "Pantoprazole", Manufacturer = "Generic",
                      PackSize = "15 TAB", UnitsPerPack = 15, AllowLooseSale = true,
                      GstRate = 12m, RackLocation = "B2", ReorderLevel = 50 },

        // A sachet and a bottle really are one unit each — half of either is
        // not something a shop can hand over.
        new Product { Name = "ORS Powder", GenericName = "Oral rehydration salts", Manufacturer = "Generic",
                      PackSize = "21.8 G", UnitsPerPack = 1, AllowLooseSale = false,
                      DispensingUnit = DispensingUnit.Sachet,
                      GstRate = 5m, RackLocation = "C1", ReorderLevel = 30 },

        new Product { Name = "Cough Syrup 100ml", GenericName = "Dextromethorphan", Manufacturer = "Generic",
                      PackSize = "100 ML", UnitsPerPack = 1, AllowLooseSale = false,
                      DispensingUnit = DispensingUnit.Bottle,
                      GstRate = 12m, RackLocation = "C2", ReorderLevel = 20 }
    ];
}
