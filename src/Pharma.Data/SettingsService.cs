using Microsoft.EntityFrameworkCore;

namespace Pharma.Data;

/// <summary>Shop identity, printed on every bill and prescription.</summary>
public class ShopProfile
{
    public string Name { get; set; } = "Twinkle Children's Hospital";
    public string AddressLine { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string DrugLicenceNo { get; set; } = "";
    public string PharmacistName { get; set; } = "";
    public string BillFooter { get; set; } = "Get well soon. Medicines once sold are not returnable.";
}

public class SettingsService(IDbContextFactory<AppDbContext> factory)
{
    private const string KeyName = "shop.name";
    private const string KeyAddress = "shop.address";
    private const string KeyPhone = "shop.phone";
    private const string KeyGstin = "shop.gstin";
    private const string KeyLicence = "shop.licence";
    private const string KeyPharmacist = "shop.pharmacist";
    private const string KeyFooter = "shop.footer";

    public async Task<ShopProfile> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        var fallback = new ShopProfile();
        return new ShopProfile
        {
            Name = Get(map, KeyName, fallback.Name),
            AddressLine = Get(map, KeyAddress, fallback.AddressLine),
            Phone = Get(map, KeyPhone, fallback.Phone),
            Gstin = Get(map, KeyGstin, fallback.Gstin),
            DrugLicenceNo = Get(map, KeyLicence, fallback.DrugLicenceNo),
            PharmacistName = Get(map, KeyPharmacist, fallback.PharmacistName),
            BillFooter = Get(map, KeyFooter, fallback.BillFooter)
        };
    }

    public async Task SaveAsync(ShopProfile profile)
    {
        await using var db = await factory.CreateDbContextAsync();

        await SetAsync(db, KeyName, profile.Name);
        await SetAsync(db, KeyAddress, profile.AddressLine);
        await SetAsync(db, KeyPhone, profile.Phone);
        await SetAsync(db, KeyGstin, profile.Gstin);
        await SetAsync(db, KeyLicence, profile.DrugLicenceNo);
        await SetAsync(db, KeyPharmacist, profile.PharmacistName);
        await SetAsync(db, KeyFooter, profile.BillFooter);

        await db.SaveChangesAsync();
    }

    private static string Get(Dictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static async Task SetAsync(AppDbContext db, string key, string? value)
    {
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
            db.Settings.Add(new Core.Setting { Key = key, Value = value ?? "" });
        else
            row.Value = value ?? "";
    }
}
