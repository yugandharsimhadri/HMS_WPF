namespace Pharma.Core.Licensing;

/// <summary>
/// The editions the product can be licensed as. Held as constants rather than
/// an enum because a licence file written by a later version may name an
/// edition this build has never heard of, and refusing to read it would be
/// worse than showing the name through.
/// </summary>
public static class LicenseEditions
{
    /// <summary>The edition the evaluation ships as.</summary>
    public const string Professional = "Professional";

    /// <summary>Reserved for a later, larger deployment.</summary>
    public const string Enterprise = "Enterprise";

    /// <summary>Reserved for a time-limited trial issued per customer.</summary>
    public const string Trial = "Trial";
}

/// <summary>
/// A licence exactly as its issuer states it, with nothing worked out yet.
/// This is what every <see cref="ILicenseProvider"/> returns, whether it read
/// the licence from inside the executable, from a signed file, or from a server.
/// </summary>
/// <param name="CustomerName">Who the licence is issued to.</param>
/// <param name="CustomerId">The issuer's identifier for that customer.</param>
/// <param name="Edition">One of <see cref="LicenseEditions"/>, or a later name.</param>
/// <param name="ExpiryUtc">The instant the licence stops being valid, in UTC.</param>
public readonly record struct LicenseDescriptor(
    string CustomerName,
    string CustomerId,
    string Edition,
    DateTime ExpiryUtc);

/// <summary>What a licence check concluded.</summary>
public enum LicenseStatus
{
    /// <summary>In date, and the clock is believable.</summary>
    Valid = 0,

    /// <summary>Past its expiry.</summary>
    Expired = 1,

    /// <summary>The clock has been moved back since the last run.</summary>
    ClockTampered = 2,

    /// <summary>The licence itself could not be read or failed its checksum.</summary>
    Unreadable = 3
}

/// <summary>
/// The outcome of <see cref="ILicenseService.Validate"/>: what was decided, and
/// the sentence to show the user if the answer was no.
/// </summary>
/// <param name="Status">What the check concluded.</param>
/// <param name="Message">Empty when valid; otherwise what to show the user.</param>
public readonly record struct LicenseValidationResult(LicenseStatus Status, string Message)
{
    /// <summary>True when the application may start or keep running.</summary>
    public bool IsValid => Status == LicenseStatus.Valid;

    /// <summary>A passing result, which carries no message.</summary>
    public static LicenseValidationResult Valid() => new(LicenseStatus.Valid, string.Empty);
}

/// <summary>
/// Everything worth telling the user about their licence, resolved against the
/// clock at the moment it was asked for.
/// </summary>
public sealed record LicenseInfo
{
    /// <summary>Who the licence is issued to.</summary>
    public required string CustomerName { get; init; }

    /// <summary>The issuer's identifier for that customer.</summary>
    public required string CustomerId { get; init; }

    /// <summary>The edition this copy is licensed as.</summary>
    public required string Edition { get; init; }

    /// <summary>The instant the licence stops being valid, in UTC.</summary>
    public required DateTime ExpiryDate { get; init; }

    /// <summary>Whole days left. Zero once expired — never negative.</summary>
    public required int DaysRemaining { get; init; }

    /// <summary>True once past <see cref="ExpiryDate"/>.</summary>
    public required bool IsExpired { get; init; }

    /// <summary>True when the clock has been moved back since the last run.</summary>
    public required bool IsClockTampered { get; init; }
}
