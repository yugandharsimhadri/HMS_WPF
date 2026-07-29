using Pharma.Core.Licensing;
using Pharma.Data.Licensing;

namespace Pharma.Tests;

/// <summary>A clock the test moves.</summary>
internal sealed class FakeClock(DateTime utcNow) : ISystemClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

/// <summary>A store held in memory, so nothing touches %ProgramData%.</summary>
internal sealed class FakeStore : ILicenseStore
{
    public DateTime? LastRun { get; set; }
    public bool IntegrityFailed { get; set; }
    public int Writes { get; private set; }

    public LastRunRecord Read()
        => IntegrityFailed ? LastRunRecord.Tampered : new LastRunRecord(LastRun, false);

    public void Write(DateTime utcNow)
    {
        LastRun = utcNow;
        Writes++;
    }
}

/// <summary>
/// Stands in for the licence sources that do not exist yet — a signed file, an
/// activation server. It exists to prove the application depends on the
/// interface and not on the embedded evaluation licence.
/// </summary>
internal sealed class FakeProvider(LicenseDescriptor? licence) : ILicenseProvider
{
    public LicenseDescriptor? TryGetLicense() => licence;
}

public class LicensingTests
{
    private static readonly DateTime Expiry = new(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private static LicenseService Build(ISystemClock clock, ILicenseStore store, ILicenseProvider? provider = null)
        => new(provider ?? new EmbeddedEvaluationLicenseProvider(), store, clock);

    // ── The embedded evaluation licence ────────────────────────────────────

    [Fact]
    public void Embedded_licence_carries_the_agreed_terms()
    {
        var licence = new EmbeddedEvaluationLicenseProvider().TryGetLicense();

        Assert.NotNull(licence);
        Assert.Equal("Evaluation Version", licence!.Value.CustomerName);
        Assert.Equal("EVAL", licence.Value.CustomerId);
        Assert.Equal(LicenseEditions.Professional, licence.Value.Edition);
        Assert.Equal(Expiry, licence.Value.ExpiryUtc);
        Assert.Equal(DateTimeKind.Utc, licence.Value.ExpiryUtc.Kind);
    }

    // ── Valid licence ──────────────────────────────────────────────────────

    [Fact]
    public void A_licence_in_date_is_valid_and_records_the_run()
    {
        var clock = new FakeClock(new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc));
        var store = new FakeStore();

        var result = Build(clock, store).Validate();

        Assert.True(result.IsValid);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(clock.UtcNow, store.LastRun);
    }

    [Fact]
    public void The_last_second_before_expiry_is_still_valid()
    {
        var clock = new FakeClock(Expiry.AddSeconds(-1));

        Assert.True(Build(clock, new FakeStore()).Validate().IsValid);
    }

    // ── Expired licence ────────────────────────────────────────────────────

    [Fact]
    public void A_licence_past_its_expiry_is_refused()
    {
        var clock = new FakeClock(Expiry.AddSeconds(2));
        var store = new FakeStore();

        var result = Build(clock, store).Validate();

        Assert.False(result.IsValid);
        Assert.Equal(LicenseStatus.Expired, result.Status);
        Assert.Equal(LicenseConstants.ExpiredMessage, result.Message);
    }

    [Fact]
    public void An_expired_run_is_not_recorded()
    {
        var store = new FakeStore();

        Build(new FakeClock(Expiry.AddDays(1)), store).Validate();

        Assert.Null(store.LastRun);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void IsExpired_follows_the_clock()
    {
        var clock = new FakeClock(Expiry.AddDays(-1));
        var service = Build(clock, new FakeStore());

        Assert.False(service.IsExpired());

        clock.UtcNow = Expiry.AddDays(1);
        Assert.True(service.IsExpired());
    }

    // ── Clock rollback ─────────────────────────────────────────────────────

    [Fact]
    public void Winding_the_clock_back_is_caught()
    {
        var store = new FakeStore { LastRun = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = Build(clock, store).Validate();

        Assert.False(result.IsValid);
        Assert.Equal(LicenseStatus.ClockTampered, result.Status);
        Assert.Equal(LicenseConstants.ClockTamperedMessage, result.Message);
    }

    [Fact]
    public void Rollback_is_reported_even_when_the_licence_is_still_in_date()
    {
        // The point of the check: a rolled-back clock makes "in date" meaningless.
        var store = new FakeStore { LastRun = new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var clock = new FakeClock(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(LicenseStatus.ClockTampered, Build(clock, store).Validate().Status);
    }

    [Fact]
    public void A_few_seconds_of_clock_correction_is_not_tampering()
    {
        // NTP legitimately steps a machine back. Locking a clinic out for that
        // would be a fault, not a defence.
        var now = new DateTime(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var store = new FakeStore { LastRun = now.AddSeconds(30) };

        Assert.True(Build(new FakeClock(now), store).Validate().IsValid);
    }

    [Fact]
    public void An_edited_state_file_is_treated_as_tampering()
    {
        var store = new FakeStore { IntegrityFailed = true };

        Assert.Equal(
            LicenseStatus.ClockTampered,
            Build(new FakeClock(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)), store).Validate().Status);
    }

    [Fact]
    public void A_first_run_with_no_record_is_allowed()
    {
        var store = new FakeStore();

        Assert.True(Build(new FakeClock(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)), store).Validate().IsValid);
    }

    [Fact]
    public void The_recorded_run_never_moves_backwards()
    {
        var later = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new FakeStore { LastRun = later };

        // Inside the skew tolerance, so it is not tampering — but it must not
        // overwrite the later stamp with an earlier one either.
        var clock = new FakeClock(later.AddMinutes(-2));
        Build(clock, store).Validate();

        Assert.Equal(later, store.LastRun);
    }

    // ── Remaining days ─────────────────────────────────────────────────────

    [Fact]
    public void Remaining_days_counts_whole_days_left()
    {
        var clock = new FakeClock(Expiry.AddDays(-10));

        Assert.Equal(10, Build(clock, new FakeStore()).GetRemainingDays());
    }

    [Fact]
    public void Remaining_days_never_goes_negative()
    {
        var clock = new FakeClock(Expiry.AddDays(400));

        Assert.Equal(0, Build(clock, new FakeStore()).GetRemainingDays());
    }

    [Fact]
    public void Remaining_days_rounds_down_to_the_day()
    {
        var clock = new FakeClock(Expiry.AddDays(-5).AddHours(-13));

        Assert.Equal(5, Build(clock, new FakeStore()).GetRemainingDays());
    }

    // ── Licence information ────────────────────────────────────────────────

    [Fact]
    public void Licence_info_reports_every_field_the_dialog_shows()
    {
        var clock = new FakeClock(Expiry.AddDays(-30));

        var info = Build(clock, new FakeStore()).GetLicenseInfo();

        Assert.Equal("Evaluation Version", info.CustomerName);
        Assert.Equal("EVAL", info.CustomerId);
        Assert.Equal(LicenseEditions.Professional, info.Edition);
        Assert.Equal(Expiry, info.ExpiryDate);
        Assert.Equal(30, info.DaysRemaining);
        Assert.False(info.IsExpired);
        Assert.False(info.IsClockTampered);
    }

    [Fact]
    public void Licence_info_reports_tampering_without_throwing()
    {
        var store = new FakeStore { IntegrityFailed = true };

        var info = Build(new FakeClock(Expiry.AddDays(-1)), store).GetLicenseInfo();

        Assert.True(info.IsClockTampered);
    }

    [Fact]
    public void An_unreadable_licence_still_answers_the_dialog()
    {
        var service = Build(new FakeClock(Expiry), new FakeStore(), new FakeProvider(null));

        var info = service.GetLicenseInfo();

        Assert.True(info.IsExpired);
        Assert.Equal(0, info.DaysRemaining);
        Assert.Equal(LicenseStatus.Unreadable, service.Validate().Status);
    }

    [Fact]
    public void The_expiry_is_the_last_moment_of_31_December_2030_in_utc()
    {
        // Guards the About dialog, which showed "01-Jan-2031" while it rendered
        // this in local time: 23:59:59 UTC is half past five the next morning
        // in India, so the dialog disagreed with the licence terms.
        var expiry = new EmbeddedEvaluationLicenseProvider().TryGetLicense()!.Value.ExpiryUtc;

        Assert.Equal("31-Dec-2030", expiry.ToString("dd-MMM-yyyy"));
        Assert.Equal(new TimeSpan(23, 59, 59), expiry.TimeOfDay);
    }

    // ── Future provider compatibility ──────────────────────────────────────

    [Fact]
    public void A_later_provider_needs_no_change_to_the_service()
    {
        // Stands in for the JSON or online provider: a different customer, a
        // different edition, a different expiry, and nothing else moves.
        var future = new LicenseDescriptor(
            "Twinkle Children's Hospital", "CUST-0001",
            LicenseEditions.Enterprise,
            new DateTime(2035, 6, 30, 23, 59, 59, DateTimeKind.Utc));

        var clock = new FakeClock(new DateTime(2035, 6, 20, 0, 0, 0, DateTimeKind.Utc));
        var service = Build(clock, new FakeStore(), new FakeProvider(future));

        Assert.True(service.Validate().IsValid);

        var info = service.GetLicenseInfo();
        Assert.Equal("Twinkle Children's Hospital", info.CustomerName);
        Assert.Equal(LicenseEditions.Enterprise, info.Edition);
        Assert.Equal(10, info.DaysRemaining);
    }

    [Fact]
    public void An_edition_this_build_has_never_heard_of_is_carried_through()
    {
        var future = new LicenseDescriptor(
            "Some Hospital", "CUST-0002", "Subscription",
            new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var service = Build(
            new FakeClock(new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new FakeStore(), new FakeProvider(future));

        Assert.Equal("Subscription", service.GetLicenseInfo().Edition);
    }

    // ── The real file-backed store ─────────────────────────────────────────

    [Fact]
    public void The_file_store_round_trips_a_run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hms-licence-" + Guid.NewGuid().ToString("N"));

        try
        {
            var store = new LicenseStorage(dir);
            Assert.Null(store.Read().LastRunUtc);

            var stamp = new DateTime(2027, 5, 4, 9, 30, 0, DateTimeKind.Utc);
            store.Write(stamp);

            var read = store.Read();
            Assert.Equal(stamp, read.LastRunUtc);
            Assert.False(read.IntegrityFailed);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void The_file_store_notices_an_edited_record()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hms-licence-" + Guid.NewGuid().ToString("N"));

        try
        {
            var store = new LicenseStorage(dir);
            store.Write(new DateTime(2027, 5, 4, 9, 30, 0, DateTimeKind.Utc));

            // Wind the stamp back by hand, exactly as somebody would with Notepad.
            var file = Path.Combine(dir, LicenseConstants.RuntimeStateFileName);
            var signature = File.ReadAllText(file).Split('|')[1];
            File.WriteAllText(file, $"2020-01-01T00:00:00.0000000Z|{signature}");

            Assert.True(store.Read().IntegrityFailed);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
