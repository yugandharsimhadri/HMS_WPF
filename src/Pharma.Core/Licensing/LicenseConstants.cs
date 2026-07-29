namespace Pharma.Core.Licensing;

/// <summary>
/// Every fixed value the licensing framework depends on, named once.
/// </summary>
/// <remarks>
/// The expiry itself is deliberately absent from here as a readable date. It is
/// carried as the two halves of an obfuscated 64-bit value, one half beside the
/// mask below and the other in <see cref="EmbeddedEvaluationLicenseProvider"/>,
/// so that neither a strings dump of the executable nor a glance at one file
/// gives the date away. See the provider for what that does and does not buy.
/// </remarks>
public static class LicenseConstants
{
    /// <summary>Product name shown on the About dialog.</summary>
    public const string ProductName = "Hospital Management System";

    /// <summary>Who to contact when a licence check fails.</summary>
    public const string Vendor = "Sivayaan Technologies";

    // ── Storage ────────────────────────────────────────────────────────────

    /// <summary>Vendor folder under %ProgramData%. Never under Program Files,
    /// which a standard user cannot write to.</summary>
    public const string StorageVendorFolder = "Sivayaan";

    /// <summary>Product folder inside <see cref="StorageVendorFolder"/>.</summary>
    public const string StorageProductFolder = "HMS";

    /// <summary>File holding the last successful run, for rollback detection.</summary>
    public const string RuntimeStateFileName = "runtime.dat";

    // ── Timing ─────────────────────────────────────────────────────────────

    /// <summary>How often a running application re-checks its licence.</summary>
    public static readonly TimeSpan ValidationInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How far behind the recorded run the clock may sit before it is called
    /// tampering. A machine correcting itself against NTP can legitimately step
    /// back a few seconds, and a clinic locked out by that would rightly be
    /// unimpressed; a rollback used to extend an evaluation is measured in
    /// months, so nothing is given away by allowing this much.
    /// </summary>
    public static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    // ── Messages ───────────────────────────────────────────────────────────

    /// <summary>Shown when the system clock has been moved backwards.</summary>
    public const string ClockTamperedMessage =
        "System date appears to have been modified. " +
        "Please correct your system clock or contact Sivayaan Technologies.";

    /// <summary>Shown when the evaluation period has run out.</summary>
    public const string ExpiredMessage =
        "This evaluation copy of the Hospital Management System has expired. " +
        "Please contact Sivayaan Technologies to continue using it.";

    /// <summary>Shown when the licence cannot be read at all.</summary>
    public const string UnreadableMessage =
        "The licence could not be verified. " +
        "Please contact Sivayaan Technologies.";

    /// <summary>Shown when a licence expires while the application is open.</summary>
    public const string ExpiredWhileRunningMessage =
        "This evaluation copy has just expired. Finish what you are doing and " +
        "close the application; it will not open again until the licence is renewed. " +
        "Please contact Sivayaan Technologies.";

    // ── Obfuscation ────────────────────────────────────────────────────────

    /// <summary>
    /// XOR mask applied to the expiry ticks. Paired with the two halves held by
    /// the embedded provider.
    /// </summary>
    internal const ulong ExpiryMask = 0x5A17E9C3D4B18F27UL;

    /// <summary>High 32 bits of the masked expiry.</summary>
    internal const uint ExpiryHigh = 0x52F40A6AU;
}
