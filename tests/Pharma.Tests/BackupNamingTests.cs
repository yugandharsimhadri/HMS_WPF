using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Backup files are named after the clinic, not the product — "clinic that
/// takes it and hands it to whoever is helping" only works if the file
/// itself says whose data it is. Covers the naming, the fallback when
/// nothing has been typed into Settings yet, and that the permanent
/// pre-upgrade copies are never mistaken for an ordinary rotation.
/// </summary>
public class BackupNamingTests : IDisposable
{
    private readonly string _dbDir = Path.Combine(Path.GetTempPath(), $"backup-naming-{Guid.NewGuid():N}");
    private readonly string _backupDir;
    private readonly string _dbPath;
    private readonly string? _previousDbOverride;
    private readonly string? _previousBackupOverride;

    public BackupNamingTests()
    {
        Directory.CreateDirectory(_dbDir);
        _dbPath = Path.Combine(_dbDir, "twinkle.db");
        _backupDir = Path.Combine(_dbDir, "backups");

        // DbBootstrapper reads its paths from these two environment variables
        // before anything else, so every static call in this test — even the
        // ones that do not take an IDbContextFactory — stays inside this
        // test's own throwaway folder rather than a real clinic's C:\HMS.
        _previousDbOverride = Environment.GetEnvironmentVariable(DbBootstrapper.PathOverrideVariable);
        _previousBackupOverride = Environment.GetEnvironmentVariable(DbBootstrapper.BackupDirectoryOverrideVariable);

        Environment.SetEnvironmentVariable(DbBootstrapper.PathOverrideVariable, _dbPath);
        Environment.SetEnvironmentVariable(DbBootstrapper.BackupDirectoryOverrideVariable, _backupDir);
    }

    private async Task<IDbContextFactory<AppDbContext>> InitAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await DbBootstrapper.InitialiseAsync(factory);

        return factory;
    }

    [Fact]
    public async Task Manual_backup_is_named_after_the_given_clinic_name()
    {
        await InitAsync();

        var file = DbBootstrapper.BackupNow("Sunrise Kids Clinic");

        Assert.StartsWith("Sunrise-Kids-Clinic-", file.Name);
        Assert.EndsWith(".db", file.Name);
    }

    [Fact]
    public async Task Manual_backup_strips_characters_that_are_not_valid_in_a_filename()
    {
        await InitAsync();

        // A slash would otherwise be read as a path separator, splitting the
        // name into a subfolder that does not exist and failing the copy.
        // An apostrophe is left alone — it is a perfectly legal Windows
        // filename character, and "Dr. Rao's Clinic" is a real clinic name.
        var file = DbBootstrapper.BackupNow("Dr. Rao's Clinic / Lab");

        Assert.DoesNotContain('/', file.Name);
        Assert.Contains('\'', file.Name);
        Assert.True(File.Exists(file.FullName));
    }

    [Fact]
    public async Task With_no_name_given_it_reads_the_clinics_own_saved_name()
    {
        var factory = await InitAsync();
        await new SettingsService(factory).SaveClinicAsync(new ClinicProfile { Name = "Rainbow Clinic" });

        var file = DbBootstrapper.BackupNow();

        Assert.StartsWith("Rainbow-Clinic-", file.Name);
    }

    [Fact]
    public async Task With_nothing_saved_yet_it_falls_back_to_a_generic_name_rather_than_failing()
    {
        // A fresh database has a Settings table but no clinic.name row until
        // something actually saves one — this must still produce a backup.
        await InitAsync();

        var file = DbBootstrapper.BackupNow();

        Assert.StartsWith("clinic-", file.Name);
    }

    [Fact]
    public async Task LastBackup_ignores_the_permanent_pre_upgrade_copies()
    {
        await InitAsync();
        var routine = DbBootstrapper.BackupNow("Clinic");

        // Deliberately newer than the routine backup, so a naive "most
        // recently written file" search would wrongly report this instead.
        var preUpgrade = Path.Combine(_backupDir, "pre-upgrade-99999999-999999.db");
        File.Copy(_dbPath, preUpgrade);
        File.SetLastWriteTime(preUpgrade, DateTime.Now.AddMinutes(5));

        Assert.Equal(routine.Name, DbBootstrapper.LastBackup!.Name);
    }

    [Fact]
    public async Task LastBackup_finds_the_newest_routine_backup_even_after_the_clinic_renames_itself()
    {
        await InitAsync();

        DbBootstrapper.BackupNow("Old Name");
        await Task.Delay(1100); // distinct LastWriteTime, not just a distinct filename
        var renamed = DbBootstrapper.BackupNow("New Name");

        Assert.Equal(renamed.Name, DbBootstrapper.LastBackup!.Name);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DbBootstrapper.PathOverrideVariable, _previousDbOverride);
        Environment.SetEnvironmentVariable(DbBootstrapper.BackupDirectoryOverrideVariable, _previousBackupOverride);

        try { Directory.Delete(_dbDir, recursive: true); } catch (IOException) { }
    }
}
