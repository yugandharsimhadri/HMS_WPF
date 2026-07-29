namespace Pharma.Core.Licensing;

/// <summary>
/// The evaluation licence that ships inside the executable: Professional
/// edition, issued to "Evaluation Version", expiring at the end of 2030.
/// </summary>
/// <remarks>
/// <para>
/// The expiry is not written here as a date. It is the XOR of its tick count
/// with a mask, split so that one half sits in <see cref="LicenseConstants"/>
/// and the other here, and it is checksummed on the way back out. A strings
/// dump of the executable therefore shows no date, and neither file read alone
/// gives one.
/// </para>
/// <para>
/// <b>This is obfuscation, not protection.</b> Anyone willing to run a
/// decompiler can find this method and change what it returns, and no amount of
/// arithmetic here would stop them. It is worth exactly what it claims: the
/// date is not discoverable by accident or by curiosity. The five hospitals
/// this evaluation goes to are not the threat model; a commercial release
/// wanting real assurance needs a signed licence file, which is why
/// <see cref="ILicenseProvider"/> exists.
/// </para>
/// </remarks>
public sealed class EmbeddedEvaluationLicenseProvider : ILicenseProvider
{
    /// <summary>Low 32 bits of the masked expiry; the high half is in <see cref="LicenseConstants"/>.</summary>
    private const uint ExpiryLow = 0xEE1B66A7U;

    /// <summary>Guards against either half being edited without the other.</summary>
    private const uint ExpiryCheck = 34U;

    /// <summary>Name the evaluation is issued to.</summary>
    public const string EvaluationCustomerName = "Evaluation Version";

    /// <summary>Customer identifier the evaluation is issued under.</summary>
    public const string EvaluationCustomerId = "EVAL";

    /// <inheritdoc />
    public LicenseDescriptor? TryGetLicense()
    {
        var expiry = DecodeExpiry();

        // A failed checksum means the two halves no longer agree — the constants
        // have been edited. Report no licence rather than an arbitrary date.
        return expiry is null
            ? null
            : new LicenseDescriptor(
                EvaluationCustomerName,
                EvaluationCustomerId,
                LicenseEditions.Professional,
                expiry.Value);
    }

    /// <summary>
    /// Rebuilds the expiry from its two halves.
    /// </summary>
    /// <returns>The expiry in UTC, or <see langword="null"/> if the halves fail
    /// their checksum or do not describe a usable date.</returns>
    private static DateTime? DecodeExpiry()
    {
        var masked = ((ulong)LicenseConstants.ExpiryHigh << 32) | ExpiryLow;
        var ticks = masked ^ LicenseConstants.ExpiryMask;

        if (ticks > (ulong)DateTime.MaxValue.Ticks) return null;
        if (ticks % 97UL + 11UL != ExpiryCheck) return null;

        return new DateTime((long)ticks, DateTimeKind.Utc);
    }
}
