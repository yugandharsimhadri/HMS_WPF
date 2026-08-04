using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The database file was renamed from twinkle.db to ShivayaanHMS.db. A
/// clinic upgrading to a build with that rename in it must open on its own
/// records under the new name, not a fresh empty database just because the
/// filename DbBootstrapper looks for changed under it — this is the same
/// concern <see cref="DatabaseSurvivalTests"/> covers for startup in
/// general, focused on this one specific transition.
/// </summary>
public class DatabaseRenameCarryOverTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"twinkle-rename-{Guid.NewGuid():N}");
    private readonly string _newPath;
    private readonly string? _previousOverride;

    public DatabaseRenameCarryOverTests()
    {
        Directory.CreateDirectory(_dir);
        _newPath = Path.Combine(_dir, "ShivayaanHMS.db");

        _previousOverride = Environment.GetEnvironmentVariable(DbBootstrapper.PathOverrideVariable);
        Environment.SetEnvironmentVariable(DbBootstrapper.PathOverrideVariable, _newPath);
    }

    private async Task SeedOldDatabaseAsync(string doctorName)
    {
        var oldPath = Path.Combine(_dir, "twinkle.db");

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={oldPath}"));
        var factory = services.BuildServiceProvider().GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        db.Doctors.Add(new Doctor { Name = doctorName, ConsultationFee = 100m, IsActive = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task An_old_twinkle_db_beside_the_new_path_is_carried_over_once()
    {
        await SeedOldDatabaseAsync("Dr. Carried Over");

        // Reading DatabasePath is what triggers the carry-over — the same
        // property InitialiseAsync and every other caller reads.
        var resolved = DbBootstrapper.DatabasePath;

        Assert.Equal(_newPath, resolved);
        Assert.True(File.Exists(_newPath), "The new-named database was not created from the old one.");
        Assert.True(File.Exists(Path.Combine(_dir, ".carried-over-from-twinkle")), "No marker was left behind.");

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_newPath}"));
        await using var db = await services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        Assert.True(await db.Doctors.AnyAsync(d => d.Name == "Dr. Carried Over"),
            "The carried-over database does not have the old data in it.");
    }

    [Fact]
    public async Task Carrying_over_does_not_happen_twice()
    {
        await SeedOldDatabaseAsync("Dr. First");

        _ = DbBootstrapper.DatabasePath; // first read: carries over

        // The new database now has its own life — add something that only
        // exists there, then put a DIFFERENT doctor in the old file. A
        // second carry-over would silently go back to this older copy.
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_newPath}"));
        var factory = services.BuildServiceProvider().GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Doctors.Add(new Doctor { Name = "Dr. Added After The Move", ConsultationFee = 50m, IsActive = true });
            await db.SaveChangesAsync();
        }

        var oldPath = Path.Combine(_dir, "twinkle.db");
        var oldServices = new ServiceCollection();
        oldServices.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={oldPath}"));
        await using (var oldDb = await oldServices.BuildServiceProvider()
                         .GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            oldDb.Doctors.Add(new Doctor { Name = "Dr. Should Never Appear", ConsultationFee = 50m, IsActive = true });
            await oldDb.SaveChangesAsync();
        }

        _ = DbBootstrapper.DatabasePath; // second read: must not carry over again

        await using var check = await factory.CreateDbContextAsync();
        Assert.True(await check.Doctors.AnyAsync(d => d.Name == "Dr. Added After The Move"));
        Assert.False(await check.Doctors.AnyAsync(d => d.Name == "Dr. Should Never Appear"));
    }

    [Fact]
    public void With_no_old_database_the_new_path_still_resolves_for_a_fresh_install()
    {
        var resolved = DbBootstrapper.DatabasePath;

        Assert.Equal(_newPath, resolved);
        Assert.False(File.Exists(_newPath), "Nothing should be created just by resolving the path.");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DbBootstrapper.PathOverrideVariable, _previousOverride);
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
