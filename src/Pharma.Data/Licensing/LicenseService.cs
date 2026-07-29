using Pharma.Core.Licensing;

namespace Pharma.Data.Licensing;

/// <summary>
/// Decides whether this copy may run, from whatever licence the provider hands
/// over and whatever the clock says.
/// </summary>
/// <remarks>
/// The order of the checks matters. The clock is judged first: if it has been
/// wound back, the expiry it would be compared against means nothing, so
/// reporting "still valid, 300 days left" from a rolled-back clock would be
/// worse than saying nothing at all.
/// </remarks>
public sealed class LicenseService(
    ILicenseProvider provider,
    ILicenseStore store,
    ISystemClock clock) : ILicenseService
{
    private readonly ClockTamperingDetector _clockCheck = new(store, clock);

    /// <inheritdoc />
    public LicenseValidationResult Validate()
    {
        var clockCheck = _clockCheck.Check();

        if (clockCheck.IsTampered)
        {
            AppLog.Error(
                "Licence: clock tampering detected — " +
                $"now {clock.UtcNow:o}, last run {clockCheck.LastRunUtc:o}.");

            return new LicenseValidationResult(
                LicenseStatus.ClockTampered, LicenseConstants.ClockTamperedMessage);
        }

        if (provider.TryGetLicense() is not { } licence)
        {
            AppLog.Error("Licence: validation failed — no licence could be read.");

            return new LicenseValidationResult(
                LicenseStatus.Unreadable, LicenseConstants.UnreadableMessage);
        }

        if (clock.UtcNow > licence.ExpiryUtc)
        {
            AppLog.Warn($"Licence: expired on {licence.ExpiryUtc:o}.");

            return new LicenseValidationResult(
                LicenseStatus.Expired, LicenseConstants.ExpiredMessage);
        }

        // Only a run that was allowed is worth remembering, and only now that
        // the clock has been believed.
        _clockCheck.RecordSuccessfulRun();

        AppLog.Info(
            $"Licence valid: {licence.Edition} for {licence.CustomerName} " +
            $"({licence.CustomerId}), expires {licence.ExpiryUtc:dd MMM yyyy}, " +
            $"{RemainingDays(licence.ExpiryUtc)} day(s) left.");

        return LicenseValidationResult.Valid();
    }

    /// <inheritdoc />
    public bool IsExpired()
        => provider.TryGetLicense() is not { } licence || clock.UtcNow > licence.ExpiryUtc;

    /// <inheritdoc />
    public int GetRemainingDays()
        => provider.TryGetLicense() is { } licence ? RemainingDays(licence.ExpiryUtc) : 0;

    /// <inheritdoc />
    public LicenseInfo GetLicenseInfo()
    {
        var clockCheck = _clockCheck.Check();
        var licence = provider.TryGetLicense();

        // Nothing readable still has to answer, because the About dialog asks
        // this and a dialog that throws is worse than one saying "unknown".
        if (licence is not { } l)
        {
            return new LicenseInfo
            {
                CustomerName = "Unknown",
                CustomerId = "Unknown",
                Edition = "Unknown",
                ExpiryDate = DateTime.MinValue,
                DaysRemaining = 0,
                IsExpired = true,
                IsClockTampered = clockCheck.IsTampered
            };
        }

        return new LicenseInfo
        {
            CustomerName = l.CustomerName,
            CustomerId = l.CustomerId,
            Edition = l.Edition,
            ExpiryDate = l.ExpiryUtc,
            DaysRemaining = RemainingDays(l.ExpiryUtc),
            IsExpired = clock.UtcNow > l.ExpiryUtc,
            IsClockTampered = clockCheck.IsTampered
        };
    }

    /// <summary>Whole days between now and the expiry, floored at zero.</summary>
    private int RemainingDays(DateTime expiryUtc)
    {
        var left = expiryUtc - clock.UtcNow;
        return left <= TimeSpan.Zero ? 0 : (int)left.TotalDays;
    }
}
