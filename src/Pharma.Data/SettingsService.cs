using Microsoft.EntityFrameworkCore;

namespace Pharma.Data;

/// <summary>Shop identity, printed on every bill and prescription.</summary>
public class ShopProfile
{
    public string Name { get; set; } = "Twinkle Children's Hospital";
    public string AddressLine { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>
    /// Whether the clinic is registered for GST. Off means no tax is charged and
    /// bills print as a plain invoice — issuing a "tax invoice" without being
    /// registered is not something to leave switched on by default.
    /// </summary>
    public bool GstRegistered { get; set; }

    public string Gstin { get; set; } = "";
    public string DrugLicenceNo { get; set; } = "";
    public string PharmacistName { get; set; } = "";
    public string BillFooter { get; set; } = "Get well soon. Medicines once sold are not returnable.";

    /// <summary>
    /// How the OPD queue is drawn. Tiles suit a short list read at a glance; rows
    /// fit more people on screen. Which is better depends on how busy the clinic
    /// is, so it is the user's choice rather than ours.
    /// </summary>
    public QueueLayout QueueLayout { get; set; } = QueueLayout.Tiles;

    /// <summary>
    /// Light or dark. A counter under fluorescent light and a desk in a dim back
    /// room want different things, and it is the same person switching between
    /// them, so it is a setting rather than something we decide.
    /// </summary>
    public Pharma.Core.AppThemeKind Theme { get; set; } = Pharma.Core.AppThemeKind.Light;
}

public enum QueueLayout
{
    Tiles = 0,
    Rows = 1
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
    private const string KeyQueueLayout = "opd.queuelayout";
    private const string KeyTheme = "ui.theme";
    private const string KeyGstRegistered = "shop.gstregistered";

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
            BillFooter = Get(map, KeyFooter, fallback.BillFooter),
            QueueLayout = Enum.TryParse<QueueLayout>(Get(map, KeyQueueLayout, ""), out var layout)
                ? layout
                : fallback.QueueLayout,

            Theme = Enum.TryParse<Pharma.Core.AppThemeKind>(Get(map, KeyTheme, ""), out var theme)
                ? theme
                : fallback.Theme,
            GstRegistered = bool.TryParse(Get(map, KeyGstRegistered, ""), out var registered) && registered
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
        await SetAsync(db, KeyQueueLayout, profile.QueueLayout.ToString());
        await SetAsync(db, KeyTheme, profile.Theme.ToString());
        await SetAsync(db, KeyGstRegistered, profile.GstRegistered.ToString());

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
