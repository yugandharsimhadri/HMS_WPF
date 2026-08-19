using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Every clinical module — OPD and Pharmacy included — is one more toggle on
/// <see cref="GeneralSettings"/>, so a dentist-only clinic can run with
/// nothing but the Dentist module switched on. Two things make that safe:
/// OPD and Pharmacy default <c>true</c> rather than <c>false</c> like every
/// other module, so an existing install upgrading to this build keeps
/// working unchanged; and <see cref="SettingsService.SaveGeneralAsync"/>
/// refuses to write a state where every module is off at once.
/// </summary>
public class ModuleFeatureTogglesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"moduletoggles-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsService _settings;

    public ModuleFeatureTogglesTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _settings = new SettingsService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
    }

    [Fact]
    public async Task A_brand_new_installation_has_OPD_and_Pharmacy_on_and_everything_else_off()
    {
        var general = await _settings.GetGeneralAsync();

        Assert.True(general.OpdEnabled);
        Assert.True(general.PharmacyEnabled);
        Assert.False(general.DiagnosticsEnabled);
        Assert.False(general.AppointmentsEnabled);
        Assert.False(general.PediatricsEnabled);
        Assert.False(general.DentistEnabled);
        Assert.False(general.PathologyLabEnabled);
    }

    [Fact]
    public async Task Each_toggle_persists_independently_of_the_others()
    {
        await _settings.SaveGeneralAsync(new GeneralSettings
        {
            OpdEnabled = false, PharmacyEnabled = false, DiagnosticsEnabled = true,
            AppointmentsEnabled = true, PediatricsEnabled = false, DentistEnabled = true,
            PathologyLabEnabled = false
        });

        var reloaded = await _settings.GetGeneralAsync();

        Assert.False(reloaded.OpdEnabled);
        Assert.False(reloaded.PharmacyEnabled);
        Assert.True(reloaded.DiagnosticsEnabled);
        Assert.True(reloaded.AppointmentsEnabled);
        Assert.False(reloaded.PediatricsEnabled);
        Assert.True(reloaded.DentistEnabled);
        Assert.False(reloaded.PathologyLabEnabled);
    }

    /// <summary>The scenario the whole design turns on: a dentist-only
    /// clinic switches off every module except Dentist, OPD and Pharmacy
    /// included, and that has to be a legal state to save.</summary>
    [Fact]
    public async Task A_dentist_only_clinic_can_switch_off_every_other_module()
    {
        await _settings.SaveGeneralAsync(new GeneralSettings
        {
            OpdEnabled = false, PharmacyEnabled = false, DiagnosticsEnabled = false,
            AppointmentsEnabled = false, PediatricsEnabled = false, DentistEnabled = true,
            PathologyLabEnabled = false
        });

        var reloaded = await _settings.GetGeneralAsync();

        Assert.True(reloaded.DentistEnabled);
        Assert.False(reloaded.OpdEnabled);
        Assert.False(reloaded.PharmacyEnabled);
    }

    [Fact]
    public async Task Switching_off_every_module_at_once_is_refused()
    {
        var allOff = new GeneralSettings
        {
            OpdEnabled = false, PharmacyEnabled = false, DiagnosticsEnabled = false,
            AppointmentsEnabled = false, PediatricsEnabled = false, DentistEnabled = false,
            PathologyLabEnabled = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _settings.SaveGeneralAsync(allOff));
    }

    /// <summary>The refusal has to be checked before anything is written —
    /// not leave the settings table half-updated.</summary>
    [Fact]
    public async Task Refusing_the_all_off_state_leaves_the_previous_settings_untouched()
    {
        await _settings.SaveGeneralAsync(new GeneralSettings
        {
            OpdEnabled = true, PharmacyEnabled = false, DiagnosticsEnabled = false,
            AppointmentsEnabled = false, PediatricsEnabled = false, DentistEnabled = false,
            PathologyLabEnabled = false
        });

        var allOff = new GeneralSettings
        {
            OpdEnabled = false, PharmacyEnabled = false, DiagnosticsEnabled = false,
            AppointmentsEnabled = false, PediatricsEnabled = false, DentistEnabled = false,
            PathologyLabEnabled = false
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _settings.SaveGeneralAsync(allOff));

        var stillThere = await _settings.GetGeneralAsync();
        Assert.True(stillThere.OpdEnabled);
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
