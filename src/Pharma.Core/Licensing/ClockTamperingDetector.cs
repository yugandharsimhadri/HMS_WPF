namespace Pharma.Core.Licensing;

/// <summary>The verdict on the machine's clock.</summary>
/// <param name="IsTampered">True when the clock has moved backwards, or the
/// stored record has been edited.</param>
/// <param name="LastRunUtc">The recorded previous run, when there was one.</param>
public readonly record struct ClockCheck(bool IsTampered, DateTime? LastRunUtc);

/// <summary>
/// Catches the obvious way to extend a time-limited evaluation: wind the
/// Windows clock back.
/// </summary>
/// <remarks>
/// The application writes down when it last ran. If it later starts and finds
/// itself apparently earlier than that, either the clock moved or the record
/// did, and neither is something to carry on through.
///
/// A small tolerance is allowed — see
/// <see cref="LicenseConstants.ClockSkewTolerance"/> — because a machine
/// correcting itself against a time server genuinely does step backwards by a
/// few seconds, and locking a clinic out for that would be a fault, not a
/// defence. A rollback that buys someone another year is not hidden by it.
/// </remarks>
public sealed class ClockTamperingDetector(ILicenseStore store, ISystemClock clock)
{
    /// <summary>
    /// Compares now against the recorded last run.
    /// </summary>
    public ClockCheck Check()
    {
        var record = store.Read();

        // An edited record is treated as tampering. Deleting it, by contrast,
        // reads as a first run — a file that is simply absent is what a fresh
        // machine looks like, and there is no way to tell the two apart.
        if (record.IntegrityFailed) return new ClockCheck(true, null);

        if (record.LastRunUtc is not { } lastRun) return new ClockCheck(false, null);

        var movedBack = clock.UtcNow < lastRun - LicenseConstants.ClockSkewTolerance;
        return new ClockCheck(movedBack, lastRun);
    }

    /// <summary>
    /// Records that the application ran, so a later rollback can be seen.
    /// </summary>
    /// <remarks>
    /// Only ever moves forward. Writing an earlier time than the one already
    /// stored would quietly forgive the rollback it is there to catch.
    /// </remarks>
    public void RecordSuccessfulRun()
    {
        var now = clock.UtcNow;
        var record = store.Read();

        if (record.LastRunUtc is { } lastRun && now <= lastRun) return;

        store.Write(now);
    }
}
