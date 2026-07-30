using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Upgrading a database that is already in use.
///
/// A version of this is now installed at a clinic, and every release after it
/// has to add to that database rather than replace it. Migrations do that
/// already — the application calls Migrate() at startup, which applies only the
/// ones the file has not seen and records them in __EFMigrationsHistory.
///
/// What was not covered is that it works. Every other test in this project
/// migrates a file that does not exist yet, so the migrations were only ever
/// exercised as "create the whole schema"; the path a clinic actually takes —
/// an old database, with records in it, gaining a new column — had nothing
/// standing behind it.
/// </summary>
public class UpgradeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"upgrade-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UpgradeTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _factory = services.BuildServiceProvider().GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    /// <summary>The first migration this application ever shipped.</summary>
    private const string TheBeginning = "20260725165349_InitialCreate";

    /// <summary>
    /// The one that matters most, and the cheapest to run: if somebody changes an
    /// entity and forgets to add a migration, the change is in the code, absent
    /// from the database, and nothing complains until a clinic's copy is missing
    /// the column. The application would come up and then fail on the screen that
    /// uses it.
    ///
    /// This compares the model the code describes against the snapshot the last
    /// migration left behind. It is `dotnet ef migrations
    /// has-pending-model-changes`, run where it cannot be skipped.
    /// </summary>
    [Fact]
    public void Every_change_to_the_model_has_a_migration_behind_it()
    {
        using var db = _factory.CreateDbContext();

        var differ = db.GetService<IMigrationsModelDiffer>();
        var initializer = db.GetService<IModelRuntimeInitializer>();

        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot
                       ?? throw new InvalidOperationException("There is no model snapshot. Has a migration ever been added?");

        // The snapshot is a design-time model and has to be run through the
        // runtime initialiser before it can be compared with the live one.
        var was = initializer.Initialize(snapshot.Model, designTime: false, validationLogger: null);

        // The design-time model, not db.Model: the runtime one is read-optimised
        // and throws when the differ asks it for things it dropped.
        var now = db.GetService<IDesignTimeModel>().Model;

        var differences = differ.GetDifferences(was.GetRelationalModel(), now.GetRelationalModel());

        Assert.True(differences.Count == 0,
            $"The model has {differences.Count} change(s) with no migration behind them. " +
            $"Run: dotnet ef migrations add <NameTheChange> --project src\\Pharma.Data --startup-project src\\Pharma.Data");
    }

    /// <summary>
    /// A fresh installation gets the whole schema in one go, which is what the
    /// rest of the suite relies on.
    /// </summary>
    [Fact]
    public void A_new_installation_is_created_at_the_latest_version()
    {
        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        Assert.Empty(db.Database.GetPendingMigrations());

        // The newest migration by name, so this keeps meaning "the latest" after
        // the next one is added rather than naming a version that will age.
        var latest = db.Database.GetMigrations().Last();
        Assert.Contains(latest, db.Database.GetAppliedMigrations());

        // And a column from that newest migration really is there. The count is
        // zero on an empty database; the point is that the query runs at all
        // rather than failing with "no such column".
        Assert.Equal(0, db.Products.Count(p => p.Composition != null));
    }

    /// <summary>
    /// The clinic's path: a database built by an older version, with records in
    /// it, brought up to date.
    ///
    /// The rows are written with plain SQL rather than through the context on
    /// purpose. At that point the code's model is the new one and would try to
    /// write columns the old file does not have yet — which is precisely the
    /// situation being tested, so it cannot also be the way the fixture is set
    /// up.
    /// </summary>
    [Fact]
    public void An_old_database_keeps_its_records_when_it_is_upgraded()
    {
        using (var old = _factory.CreateDbContext())
        {
            old.GetService<IMigrator>().Migrate(TheBeginning);

            Assert.NotEmpty(old.Database.GetPendingMigrations());   // there is an upgrade to do

            old.Database.ExecuteSqlRaw(
                """
                INSERT INTO Patients (Id, PatientNo, Name, Phone, Gender, Age, CreatedAt, IsDeleted)
                VALUES ('11111111-1111-1111-1111-111111111111', 'P00001', 'Aarav Before', '9160494923', 0, 4, '2026-07-01', 0)
                """);
        }

        using (var upgraded = _factory.CreateDbContext())
        {
            upgraded.Database.Migrate();

            Assert.Empty(upgraded.Database.GetPendingMigrations());

            // The child booked in on the old version is still on the register.
            var patient = upgraded.Patients.Single(p => p.PatientNo == "P00001");
            Assert.Equal("Aarav Before", patient.Name);
            Assert.Equal(4, patient.Age);

            // And the columns added since are there to be written to.
            patient.Allergies = "penicillin";
            upgraded.SaveChanges();
        }
    }

    /// <summary>
    /// Migrating a database that is already current does nothing, so a clinic
    /// that reinstalls the same version — or opens the application twice — is
    /// not a special case anybody has to think about.
    /// </summary>
    [Fact]
    public void Upgrading_twice_is_the_same_as_upgrading_once()
    {
        using (var db = _factory.CreateDbContext()) db.Database.Migrate();

        using (var again = _factory.CreateDbContext())
        {
            var applied = again.Database.GetAppliedMigrations().Count();

            again.Database.Migrate();

            Assert.Equal(applied, again.Database.GetAppliedMigrations().Count());
            Assert.Empty(again.Database.GetPendingMigrations());
        }
    }

    /// <summary>
    /// Every migration applies on top of the one before it, one at a time.
    ///
    /// Migrate() in one go proves the chain works from empty. Stepping through it
    /// proves each link works from wherever the previous one left the file, which
    /// is the only thing a clinic three versions behind will ever do.
    /// </summary>
    [Fact]
    public void The_migrations_apply_one_at_a_time_in_order()
    {
        using var db = _factory.CreateDbContext();

        var all = db.Database.GetMigrations().ToList();
        Assert.True(all.Count > 1, "There is only one migration; there is no chain to test.");

        var migrator = db.GetService<IMigrator>();

        foreach (var migration in all)
        {
            migrator.Migrate(migration);
            Assert.Contains(migration, db.Database.GetAppliedMigrations());
        }

        Assert.Empty(db.Database.GetPendingMigrations());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }

        GC.SuppressFinalize(this);
    }
}
