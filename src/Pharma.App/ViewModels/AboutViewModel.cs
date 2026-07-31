using Pharma.Core.Licensing;

namespace Pharma.App.ViewModels;

/// <summary>
/// What the About dialog shows: the product, who it is licensed to, and how
/// long is left.
/// </summary>
/// <remarks>
/// Read once when the dialog opens. Nothing here changes while a modal dialog
/// is on screen, so there is nothing to notify about and no need for the
/// observable machinery the editing screens use.
/// </remarks>
public sealed class AboutViewModel(ILicenseService licence)
{
    private readonly LicenseInfo _info = licence.GetLicenseInfo();

    /// <summary>Product name, as the vendor sells it.</summary>
    public string ProductName => LicenseConstants.ProductName;

    /// <summary>The edition this copy is licensed as.</summary>
    public string Edition => _info.Edition;

    /// <summary>Who the licence is issued to.</summary>
    public string LicensedTo => _info.CustomerName;

    /// <summary>Customer identifier on the licence.</summary>
    public string CustomerId => _info.CustomerId;

    /// <summary>
    /// Expiry, written the way a clinic reads a date.
    /// </summary>
    /// <remarks>
    /// Shown as the UTC date the licence actually states, not the local one.
    /// The licence runs to 23:59:59 UTC on 31 December 2030, which in India is
    /// half past five on New Year's morning — so converting to local time put
    /// "01-Jan-2031" on the dialog and made it disagree with the licence terms
    /// and with every other document about this evaluation.
    /// </remarks>
    public string ExpiryDate => _info.ExpiryDate == DateTime.MinValue
        ? "Unknown"
        : _info.ExpiryDate.ToString("dd-MMM-yyyy");

    /// <summary>Days left, or why there are none.</summary>
    public string RemainingDays => _info.IsClockTampered
        ? "Unknown — check the system clock"
        : _info.IsExpired
            ? "Expired"
            : $"{_info.DaysRemaining:N0} day(s)";

    /// <summary>
    /// The build this is, for support to read back — commit included, because
    /// two builds of the same version behave differently and the version alone
    /// cannot tell them apart.
    /// </summary>
    public string BuildVersion => AppInfo.FullVersion;

    /// <summary>Who wrote it.</summary>
    public string Vendor => LicenseConstants.Vendor;

    /// <summary>Where the vendor's name links to.</summary>
    public string VendorUrl => AppInfo.DeveloperUrl;

    /// <summary>True when the licence needs attention, which the dialog says loudly.</summary>
    public bool NeedsAttention => _info.IsExpired || _info.IsClockTampered;

    /// <summary>The sentence shown when <see cref="NeedsAttention"/> is set.</summary>
    public string AttentionMessage => _info.IsClockTampered
        ? LicenseConstants.ClockTamperedMessage
        : LicenseConstants.ExpiredMessage;
}
