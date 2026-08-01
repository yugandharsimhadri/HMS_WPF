using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Splitting one shared clinic/pharmacy identity into two, for a database that
/// already has the old one saved.
///
/// Not an EF migration — <c>Settings</c> is already a key/value table, so the
/// split is new rows, written by <see cref="SettingsService.EnsureSettingsSplitSeedAsync"/>
/// from application code rather than <c>Migrate()</c>. What has to hold is the
/// same thing every EF migration in this project is held to: it runs once, it
/// runs safely on top of whatever a clinic already saved, and running it twice
/// is the same as running it once.
/// </summary>
public class SettingsSplitTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"settingssplit-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsService _settings;

    public SettingsSplitTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _settings = new SettingsService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
    }

    /// <summary>Writes a row the way a build before the split would have.</summary>
    private async Task WriteOldRowAsync(string key, string value)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Settings.Add(new Pharma.Core.Setting { Key = key, Value = value });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Seeding_splits_the_old_shared_identity_into_clinic_and_pharmacy()
    {
        await WriteOldRowAsync("shop.name", "Twinkle Children's Hospital");
        await WriteOldRowAsync("shop.address", "12 Main Road, Hyderabad");
        await WriteOldRowAsync("shop.phone", "040-1234567");
        await WriteOldRowAsync("shop.gstin", "36ABCDE1234F1Z5");
        await WriteOldRowAsync("shop.gstregistered", "True");
        await WriteOldRowAsync("shop.licence", "AP/21B/2024/1234");
        await WriteOldRowAsync("shop.pharmacist", "S. Rao");
        await WriteOldRowAsync("shop.footer", "Get well soon.");

        await _settings.EnsureSettingsSplitSeedAsync();

        var clinic = await _settings.GetClinicAsync();
        Assert.Equal("Twinkle Children's Hospital", clinic.Name);
        Assert.Equal("12 Main Road, Hyderabad", clinic.AddressLine);
        Assert.Equal("040-1234567", clinic.Phone);

        // The clinic starts un-registered even though the old shared switch was
        // on — that switch was always describing the pharmacy's tax invoices,
        // not a doctor's prescription, and the split is the first chance to say
        // so honestly rather than carry the old value across by accident.
        Assert.False(clinic.GstRegistered);
        Assert.Equal("", clinic.Gstin);

        var pharmacy = await _settings.GetPharmacyAsync();
        Assert.Equal("Twinkle Children's Hospital", pharmacy.Name);
        Assert.True(pharmacy.GstRegistered);
        Assert.Equal("36ABCDE1234F1Z5", pharmacy.Gstin);
        Assert.Equal("AP/21B/2024/1234", pharmacy.DrugLicenceNo);
        Assert.Equal("S. Rao", pharmacy.PharmacistName);

        var theme = await _settings.GetDocumentThemeAsync();
        Assert.Equal("Get well soon.", theme.Footer);
    }

    [Fact]
    public async Task Seeding_never_runs_twice()
    {
        await WriteOldRowAsync("shop.name", "Original Name");
        await _settings.EnsureSettingsSplitSeedAsync();

        // Something changes the clinic afterwards, the way Settings normally would.
        var clinic = await _settings.GetClinicAsync();
        clinic.Name = "Renamed After The Split";
        await _settings.SaveClinicAsync(clinic);

        // Seeding again — the application calls this on every startup — must not
        // walk back over that change.
        await _settings.EnsureSettingsSplitSeedAsync();

        Assert.Equal("Renamed After The Split", (await _settings.GetClinicAsync()).Name);
    }

    [Fact]
    public async Task A_brand_new_installation_needs_no_seed()
    {
        // No shop.* rows at all — a fresh install that has never seen the old
        // shared identity. The seed has nothing to copy and must not fail.
        await _settings.EnsureSettingsSplitSeedAsync();

        var clinic = await _settings.GetClinicAsync();
        var pharmacy = await _settings.GetPharmacyAsync();

        Assert.False(string.IsNullOrWhiteSpace(clinic.Name));
        Assert.False(string.IsNullOrWhiteSpace(pharmacy.Name));
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
