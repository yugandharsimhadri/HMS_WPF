namespace Pharma.Core.Licensing;

/// <summary>
/// Reads the clock so a test can move it. Everything in licensing asks time of
/// this rather than of <see cref="DateTime"/>, which is what makes rollback
/// testable without touching the machine's own clock.
/// </summary>
public interface ISystemClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTime UtcNow { get; }
}

/// <summary>The real clock.</summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// What the store found. The three cases are deliberately distinct: never run
/// before is normal, whereas a record that fails its integrity check has been
/// edited and is not the same thing at all.
/// </summary>
/// <param name="LastRunUtc">When the application last ran, or
/// <see langword="null"/> if there is no usable record.</param>
/// <param name="IntegrityFailed">True when a record existed but did not match
/// its own signature.</param>
public readonly record struct LastRunRecord(DateTime? LastRunUtc, bool IntegrityFailed)
{
    /// <summary>Nothing recorded — a first run, or a fresh machine.</summary>
    public static LastRunRecord None => new(null, false);

    /// <summary>A record that has been tampered with.</summary>
    public static LastRunRecord Tampered => new(null, true);
}

/// <summary>
/// Where the last successful run is remembered between sessions.
/// </summary>
/// <remarks>
/// Kept as an interface so the tamper detector can be tested against an
/// in-memory store, and so the location can move without the detector caring.
/// </remarks>
public interface ILicenseStore
{
    /// <summary>Reads the recorded last run.</summary>
    LastRunRecord Read();

    /// <summary>Records a successful run.</summary>
    /// <param name="utcNow">The instant to record, in UTC.</param>
    void Write(DateTime utcNow);
}
