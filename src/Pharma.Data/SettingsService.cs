using Microsoft.EntityFrameworkCore;

namespace Pharma.Data;

/// <summary>
/// The clinic's own identity — printed on the prescription and the fee
/// receipt, never on the pharmacy bill.
/// </summary>
public class ClinicProfile
{
    public string Name { get; set; } = "Twinkle Children's Hospital";
    public string AddressLine { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string Phone { get; set; } = "";

    /// <summary>
    /// Off by default. A consultation is usually not a taxable supply, and
    /// printing a GSTIN on a prescription that is not one would be a false
    /// claim — same reasoning as the pharmacy's own switch, applied separately.
    /// </summary>
    public bool GstRegistered { get; set; }
    public string Gstin { get; set; } = "";

    // When the doctor actually sits. Indian clinics run two sittings with the
    // afternoon off, and the desk thinks in those terms — "who is left this
    // evening" is a real question and "who is left today" is not.
    public TimeSpan MorningFrom { get; set; } = new(10, 0, 0);
    public TimeSpan MorningTo { get; set; } = new(13, 0, 0);
    public TimeSpan EveningFrom { get; set; } = new(16, 0, 0);
    public TimeSpan EveningTo { get; set; } = new(20, 0, 0);

    /// <summary>
    /// The hours a session covers, or null for the whole day. The end is
    /// exclusive, so a morning ending at 13:00 does not also claim the one
    /// o'clock patient.
    /// </summary>
    public (TimeSpan From, TimeSpan To)? Window(ClinicSession session) => session switch
    {
        ClinicSession.Morning => (MorningFrom, MorningTo),
        ClinicSession.Evening => (EveningFrom, EveningTo),
        _ => null
    };

    /// <summary>Whether a visit at this time belongs to the chosen session.</summary>
    public bool IsIn(ClinicSession session, DateTime when)
    {
        if (Window(session) is not { } window) return true;

        var time = when.TimeOfDay;
        return time >= window.From && time < window.To;
    }

    /// <summary>"10:00 to 13:00", for the line under the OPD heading.</summary>
    public string Describe(ClinicSession session) =>
        Window(session) is { } window
            ? $"{window.From:hh\\:mm} to {window.To:hh\\:mm}"
            : "the whole day";
}

/// <summary>
/// The pharmacy's own identity — printed on the medicine bill. Can differ
/// from the clinic's: a pharmacy sometimes trades under its own name even
/// when it sits inside the same building.
/// </summary>
public class PharmacyProfile
{
    public string Name { get; set; } = "Twinkle Pharmacy";
    public string AddressLine { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string Phone { get; set; } = "";

    /// <summary>
    /// Whether the pharmacy is registered for GST. Off means no tax is charged
    /// and bills print as a plain invoice — issuing a "tax invoice" without
    /// being registered is not something to leave switched on by default.
    /// </summary>
    public bool GstRegistered { get; set; }
    public string Gstin { get; set; } = "";
    public string DrugLicenceNo { get; set; } = "";
    public string PharmacistName { get; set; } = "";
}

/// <summary>
/// How every printed document is branded. One theme today — a single logo and
/// a single footer message used everywhere — with room to grow into
/// per-document themes later without another migration: this is already its
/// own settings group rather than folded into Clinic or Pharmacy.
/// </summary>
public class DocumentTheme
{
    public string Footer { get; set; } = "Get well soon. Medicines once sold are not returnable.";

    /// <summary>
    /// Base64, not a file path and not a database column of its own. It rides
    /// inside the same Settings row as everything else, so it travels with
    /// every backup automatically — the same reasoning
    /// <see cref="Pharma.App.Printing.DocumentBuilder"/>'s compiled-in
    /// letterhead is written for, applied to a logo the clinic supplies
    /// instead of one that ships with the application.
    /// </summary>
    public string? LogoBase64 { get; set; }
    public string? LogoContentType { get; set; }

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoBase64);
}

/// <summary>How the application itself behaves, as opposed to who it is speaking for.</summary>
public class GeneralSettings
{
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

/// <summary>
/// Which sitting the OPD screen is showing. Full day is the safe default: it
/// hides nobody, which matters because a visit booked outside both windows —
/// two in the afternoon, say — belongs to neither session.
/// </summary>
public enum ClinicSession
{
    FullDay = 0,
    Morning = 1,
    Evening = 2
}

/// <summary>
/// Everything the application remembers between sessions: who the clinic and
/// the pharmacy are, how documents are branded, and how the software itself
/// behaves. Backed by one key/value table, so every field here is a row, not
/// a column — which is what let the clinic/pharmacy split ship without an
/// EF Core migration at all.
/// </summary>
public class SettingsService(IDbContextFactory<AppDbContext> factory)
{
    // ── Clinic ─────────────────────────────────────────────────────────────
    private const string KeyClinicName = "clinic.name";
    private const string KeyClinicAddress = "clinic.address";
    private const string KeyClinicAddress2 = "clinic.address2";
    private const string KeyClinicPhone = "clinic.phone";
    private const string KeyClinicGstRegistered = "clinic.gstregistered";
    private const string KeyClinicGstin = "clinic.gstin";
    private const string KeyClinicMorningFrom = "clinic.morningfrom";
    private const string KeyClinicMorningTo = "clinic.morningto";
    private const string KeyClinicEveningFrom = "clinic.eveningfrom";
    private const string KeyClinicEveningTo = "clinic.eveningto";

    // ── Pharmacy ───────────────────────────────────────────────────────────
    private const string KeyPharmacyName = "pharmacy.name";
    private const string KeyPharmacyAddress = "pharmacy.address";
    private const string KeyPharmacyAddress2 = "pharmacy.address2";
    private const string KeyPharmacyPhone = "pharmacy.phone";
    private const string KeyPharmacyGstRegistered = "pharmacy.gstregistered";
    private const string KeyPharmacyGstin = "pharmacy.gstin";
    private const string KeyPharmacyLicence = "pharmacy.druglicence";
    private const string KeyPharmacyPharmacist = "pharmacy.pharmacist";

    // ── Document branding ─────────────────────────────────────────────────
    private const string KeyDocsFooter = "docs.footer";
    private const string KeyDocsLogoBase64 = "docs.logo.base64";
    private const string KeyDocsLogoContentType = "docs.logo.contenttype";

    // ── General ────────────────────────────────────────────────────────────
    private const string KeyQueueLayout = "opd.queuelayout";
    private const string KeyTheme = "ui.theme";

    // ── The old, merged identity. Read-only from here on — kept so a build
    //    older than the split still finds its data exactly where it left it,
    //    and so the one-time seed below has somewhere to read from. ────────
    private const string KeyOldName = "shop.name";
    private const string KeyOldAddress = "shop.address";
    private const string KeyOldPhone = "shop.phone";
    private const string KeyOldGstin = "shop.gstin";
    private const string KeyOldLicence = "shop.licence";
    private const string KeyOldPharmacist = "shop.pharmacist";
    private const string KeyOldFooter = "shop.footer";
    private const string KeyOldGstRegistered = "shop.gstregistered";
    private const string KeyOldMorningFrom = "opd.morning.from";
    private const string KeyOldMorningTo = "opd.morning.to";
    private const string KeyOldEveningFrom = "opd.evening.from";
    private const string KeyOldEveningTo = "opd.evening.to";

    private const string KeySplitSeeded = "migrations.settingssplit.done";

    public async Task<ClinicProfile> GetClinicAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        var fallback = new ClinicProfile();
        return new ClinicProfile
        {
            Name = Get(map, KeyClinicName, fallback.Name),
            AddressLine = Get(map, KeyClinicAddress, fallback.AddressLine),
            AddressLine2 = Get(map, KeyClinicAddress2, fallback.AddressLine2),
            Phone = Get(map, KeyClinicPhone, fallback.Phone),
            GstRegistered = Bool(map, KeyClinicGstRegistered),
            Gstin = Get(map, KeyClinicGstin, fallback.Gstin),
            MorningFrom = Time(map, KeyClinicMorningFrom, fallback.MorningFrom),
            MorningTo = Time(map, KeyClinicMorningTo, fallback.MorningTo),
            EveningFrom = Time(map, KeyClinicEveningFrom, fallback.EveningFrom),
            EveningTo = Time(map, KeyClinicEveningTo, fallback.EveningTo)
        };
    }

    public async Task SaveClinicAsync(ClinicProfile profile)
    {
        await using var db = await factory.CreateDbContextAsync();

        await SetAsync(db, KeyClinicName, profile.Name);
        await SetAsync(db, KeyClinicAddress, profile.AddressLine);
        await SetAsync(db, KeyClinicAddress2, profile.AddressLine2);
        await SetAsync(db, KeyClinicPhone, profile.Phone);
        await SetAsync(db, KeyClinicGstRegistered, profile.GstRegistered.ToString());
        await SetAsync(db, KeyClinicGstin, profile.Gstin);

        // "hh\:mm" rather than the default, which would write 10:00:00 and read
        // back the same — correct, but nobody hand-editing the table wants to
        // count the colons.
        await SetAsync(db, KeyClinicMorningFrom, profile.MorningFrom.ToString(@"hh\:mm"));
        await SetAsync(db, KeyClinicMorningTo, profile.MorningTo.ToString(@"hh\:mm"));
        await SetAsync(db, KeyClinicEveningFrom, profile.EveningFrom.ToString(@"hh\:mm"));
        await SetAsync(db, KeyClinicEveningTo, profile.EveningTo.ToString(@"hh\:mm"));

        await db.SaveChangesAsync();
    }

    public async Task<PharmacyProfile> GetPharmacyAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        var fallback = new PharmacyProfile();
        return new PharmacyProfile
        {
            Name = Get(map, KeyPharmacyName, fallback.Name),
            AddressLine = Get(map, KeyPharmacyAddress, fallback.AddressLine),
            AddressLine2 = Get(map, KeyPharmacyAddress2, fallback.AddressLine2),
            Phone = Get(map, KeyPharmacyPhone, fallback.Phone),
            GstRegistered = Bool(map, KeyPharmacyGstRegistered),
            Gstin = Get(map, KeyPharmacyGstin, fallback.Gstin),
            DrugLicenceNo = Get(map, KeyPharmacyLicence, fallback.DrugLicenceNo),
            PharmacistName = Get(map, KeyPharmacyPharmacist, fallback.PharmacistName)
        };
    }

    public async Task SavePharmacyAsync(PharmacyProfile profile)
    {
        await using var db = await factory.CreateDbContextAsync();

        await SetAsync(db, KeyPharmacyName, profile.Name);
        await SetAsync(db, KeyPharmacyAddress, profile.AddressLine);
        await SetAsync(db, KeyPharmacyAddress2, profile.AddressLine2);
        await SetAsync(db, KeyPharmacyPhone, profile.Phone);
        await SetAsync(db, KeyPharmacyGstRegistered, profile.GstRegistered.ToString());
        await SetAsync(db, KeyPharmacyGstin, profile.Gstin);
        await SetAsync(db, KeyPharmacyLicence, profile.DrugLicenceNo);
        await SetAsync(db, KeyPharmacyPharmacist, profile.PharmacistName);

        await db.SaveChangesAsync();
    }

    public async Task<DocumentTheme> GetDocumentThemeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        var fallback = new DocumentTheme();
        return new DocumentTheme
        {
            Footer = Get(map, KeyDocsFooter, fallback.Footer),
            LogoBase64 = map.TryGetValue(KeyDocsLogoBase64, out var b64) && !string.IsNullOrWhiteSpace(b64) ? b64 : null,
            LogoContentType = map.TryGetValue(KeyDocsLogoContentType, out var ct) && !string.IsNullOrWhiteSpace(ct) ? ct : null
        };
    }

    public async Task SaveDocumentThemeAsync(DocumentTheme theme)
    {
        await using var db = await factory.CreateDbContextAsync();

        await SetAsync(db, KeyDocsFooter, theme.Footer);
        await SetAsync(db, KeyDocsLogoBase64, theme.LogoBase64 ?? "");
        await SetAsync(db, KeyDocsLogoContentType, theme.LogoContentType ?? "");

        await db.SaveChangesAsync();
    }

    public async Task<GeneralSettings> GetGeneralAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        var fallback = new GeneralSettings();
        return new GeneralSettings
        {
            QueueLayout = Enum.TryParse<QueueLayout>(Get(map, KeyQueueLayout, ""), out var layout)
                ? layout : fallback.QueueLayout,
            Theme = Enum.TryParse<Pharma.Core.AppThemeKind>(Get(map, KeyTheme, ""), out var theme)
                ? theme : fallback.Theme
        };
    }

    public async Task SaveGeneralAsync(GeneralSettings settings)
    {
        await using var db = await factory.CreateDbContextAsync();

        await SetAsync(db, KeyQueueLayout, settings.QueueLayout.ToString());
        await SetAsync(db, KeyTheme, settings.Theme.ToString());

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Splits the one identity a build before this shipped into the two this
    /// one asks for, the first time settings are ever read after the update —
    /// and never again. Not an EF migration: <c>Settings</c> is already a
    /// key/value table, so this is new rows, and it runs from application
    /// code rather than <c>Migrate()</c>.
    ///
    /// Both new identities start from the same old one, because that is what
    /// is actually true today — one clinic, one pharmacy, one set of details
    /// covering both. The old <c>shop.*</c> rows are never touched again, but
    /// never deleted either: a rollback to a build before the split finds its
    /// data exactly where it left it.
    /// </summary>
    public async Task EnsureSettingsSplitSeedAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var map = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        if (Bool(map, KeySplitSeeded)) return;

        var oldFallback = new ClinicProfile();

        var name = Get(map, KeyOldName, oldFallback.Name);
        var address = Get(map, KeyOldAddress, oldFallback.AddressLine);
        var phone = Get(map, KeyOldPhone, oldFallback.Phone);
        var gstin = Get(map, KeyOldGstin, "");
        var gstRegistered = Bool(map, KeyOldGstRegistered);

        // Clinic gets the shared identity, but starts un-registered for GST —
        // most consultations are not a taxable supply, and that is a decision
        // worth making deliberately rather than inheriting from the pharmacy
        // side of the old single switch. See ClinicProfile.GstRegistered.
        await SetAsync(db, KeyClinicName, name);
        await SetAsync(db, KeyClinicAddress, address);
        await SetAsync(db, KeyClinicPhone, phone);
        await SetAsync(db, KeyClinicGstRegistered, "False");
        await SetAsync(db, KeyClinicGstin, "");

        await SetAsync(db, KeyClinicMorningFrom, Get(map, KeyOldMorningFrom, oldFallback.MorningFrom.ToString(@"hh\:mm")));
        await SetAsync(db, KeyClinicMorningTo, Get(map, KeyOldMorningTo, oldFallback.MorningTo.ToString(@"hh\:mm")));
        await SetAsync(db, KeyClinicEveningFrom, Get(map, KeyOldEveningFrom, oldFallback.EveningFrom.ToString(@"hh\:mm")));
        await SetAsync(db, KeyClinicEveningTo, Get(map, KeyOldEveningTo, oldFallback.EveningTo.ToString(@"hh\:mm")));

        // Pharmacy keeps the GST registration and the licence details — those
        // were always pharmacy facts, even when they lived on the shared type.
        await SetAsync(db, KeyPharmacyName, name);
        await SetAsync(db, KeyPharmacyAddress, address);
        await SetAsync(db, KeyPharmacyPhone, phone);
        await SetAsync(db, KeyPharmacyGstRegistered, gstRegistered.ToString());
        await SetAsync(db, KeyPharmacyGstin, gstin);
        await SetAsync(db, KeyPharmacyLicence, Get(map, KeyOldLicence, ""));
        await SetAsync(db, KeyPharmacyPharmacist, Get(map, KeyOldPharmacist, ""));

        await SetAsync(db, KeyDocsFooter, Get(map, KeyOldFooter, new DocumentTheme().Footer));

        await SetAsync(db, KeySplitSeeded, "True");

        await db.SaveChangesAsync();

        AppLog.Info("Settings: split the clinic and pharmacy identity from the old shared one.");
    }

    private static string Get(Dictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static bool Bool(Dictionary<string, string> map, string key)
        => bool.TryParse(Get(map, key, ""), out var value) && value;

    /// <summary>
    /// A stored time, or the default when the row is missing or unreadable. An
    /// unparseable session window would otherwise hide the whole queue.
    /// </summary>
    private static TimeSpan Time(Dictionary<string, string> map, string key, TimeSpan fallback)
        => TimeSpan.TryParse(Get(map, key, ""), out var parsed) ? parsed : fallback;

    private static async Task SetAsync(AppDbContext db, string key, string? value)
    {
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
            db.Settings.Add(new Core.Setting { Key = key, Value = value ?? "" });
        else
            row.Value = value ?? "";
    }
}
