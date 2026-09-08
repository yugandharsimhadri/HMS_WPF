using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The Appointments module's data layer: booking, cancellation, rescheduling
/// as a linked new row rather than an overwrite, the daily list, check-in,
/// and the reminders aggregation. Deliberately does not touch OPD, Dentist
/// or Pathology Lab's own tables — check-in only records that a link exists,
/// it never creates the record on the other end itself.
/// </summary>
public class AppointmentsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"appointments-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AppointmentsService _appointments;
    private readonly SettingsService _settings;
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public AppointmentsServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _settings = new SettingsService(_factory);
        _appointments = new AppointmentsService(_factory, _settings);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        db.Patients.Add(new Patient
        {
            Id = _patientId, PatientNo = "P00001", Name = "Aarav Rao", Phone = "9000000000", Age = 30
        });
        db.Doctors.Add(new Doctor { Id = _doctorId, Name = "Dr. Iyer", ConsultationFee = 300 });
        db.SaveChanges();
    }

    private Appointment NewAppointment(AppointmentModuleContext context = AppointmentModuleContext.General, DateTime? on = null) => new()
    {
        PatientId = _patientId, PatientName = "Aarav Rao", PatientPhone = "9000000000",
        DoctorId = _doctorId, DoctorName = "Dr. Iyer",
        ScheduledOn = on ?? DateTime.Today.AddDays(1).AddHours(10),
        ModuleContext = context
    };

    // ── Booking ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Booking_issues_a_sequential_appointment_number()
    {
        var first = await _appointments.BookAsync(NewAppointment());
        var second = await _appointments.BookAsync(NewAppointment());

        Assert.StartsWith("APT", first.AppointmentNo);
        Assert.NotEqual(first.AppointmentNo, second.AppointmentNo);
        Assert.Equal(AppointmentStatus.Scheduled, first.Status);
    }

    /// <summary>OPD is on by default in a brand new database, so a General
    /// appointment is always legal to book without touching Settings first.</summary>
    [Fact]
    public async Task A_general_appointment_can_be_booked_by_default()
    {
        var booked = await _appointments.BookAsync(NewAppointment(AppointmentModuleContext.General));
        Assert.Equal(AppointmentModuleContext.General, booked.ModuleContext);
    }

    /// <summary>The guard the design turns on this module for: a context
    /// whose own module is switched off cannot be booked against, even if
    /// the caller tries anyway.</summary>
    [Fact]
    public async Task Booking_against_a_switched_off_module_is_refused()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointments.BookAsync(NewAppointment(AppointmentModuleContext.Dentist)));

        Assert.Contains("Dentist", ex.Message);
    }

    [Fact]
    public async Task Once_its_module_is_switched_on_the_same_context_books_fine()
    {
        var general = await _settings.GetGeneralAsync();
        general.DentistEnabled = true;
        await _settings.SaveGeneralAsync(general);

        var booked = await _appointments.BookAsync(NewAppointment(AppointmentModuleContext.Dentist));
        Assert.Equal(AppointmentModuleContext.Dentist, booked.ModuleContext);
    }

    // ── Cancellation ───────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_marks_the_appointment_cancelled_and_keeps_the_row()
    {
        var booked = await _appointments.BookAsync(NewAppointment());
        await _appointments.CancelAsync(booked.Id, "Patient called to cancel");

        await using var db = await _factory.CreateDbContextAsync();
        var reloaded = await db.Appointments.FirstAsync(a => a.Id == booked.Id);

        Assert.Equal(AppointmentStatus.Cancelled, reloaded.Status);
        Assert.Contains("Patient called to cancel", reloaded.Notes);
    }

    [Fact]
    public async Task A_checked_in_appointment_cannot_be_cancelled()
    {
        var booked = await _appointments.BookAsync(NewAppointment());
        await _appointments.MarkCheckedInAsync(booked.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointments.CancelAsync(booked.Id, "too late"));
    }

    // ── Rescheduling ───────────────────────────────────────────────────────

    [Fact]
    public async Task Rescheduling_creates_a_new_linked_row_and_leaves_the_original_time_on_record()
    {
        var original = await _appointments.BookAsync(NewAppointment(on: DateTime.Today.AddDays(1).AddHours(10)));
        var newTime = DateTime.Today.AddDays(3).AddHours(11);

        var moved = await _appointments.RescheduleAsync(original.Id, newTime);

        Assert.Equal(original.Id, moved.RescheduledFromId);
        Assert.Equal(newTime, moved.ScheduledOn);
        Assert.Equal(AppointmentStatus.Scheduled, moved.Status);
        Assert.NotEqual(original.AppointmentNo, moved.AppointmentNo);

        await using var db = await _factory.CreateDbContextAsync();
        var originalReloaded = await db.Appointments.FirstAsync(a => a.Id == original.Id);
        Assert.Equal(AppointmentStatus.Rescheduled, originalReloaded.Status);
        Assert.Equal(DateTime.Today.AddDays(1).AddHours(10), originalReloaded.ScheduledOn);
    }

    [Fact]
    public async Task A_checked_in_appointment_cannot_be_rescheduled()
    {
        var booked = await _appointments.BookAsync(NewAppointment());
        await _appointments.MarkCheckedInAsync(booked.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointments.RescheduleAsync(booked.Id, DateTime.Today.AddDays(5)));
    }

    // ── Daily list ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_daily_list_excludes_other_days_and_rescheduled_originals()
    {
        var today = DateTime.Today.AddHours(9);
        var onDay = await _appointments.BookAsync(NewAppointment(on: today));
        await _appointments.BookAsync(NewAppointment(on: today.AddDays(2)));

        var moved = await _appointments.RescheduleAsync(onDay.Id, today.AddHours(1));

        var list = await _appointments.GetForDateAsync(today);

        Assert.Single(list);
        Assert.Equal(moved.Id, list[0].Id);
    }

    // ── Check-in ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Checking_in_sets_status_and_the_linked_record()
    {
        var booked = await _appointments.BookAsync(NewAppointment());
        var visitId = Guid.NewGuid();

        await _appointments.MarkCheckedInAsync(booked.Id, visitId);

        await using var db = await _factory.CreateDbContextAsync();
        var reloaded = await db.Appointments.FirstAsync(a => a.Id == booked.Id);

        Assert.Equal(AppointmentStatus.CheckedIn, reloaded.Status);
        Assert.Equal(visitId, reloaded.LinkedRecordId);
    }

    [Fact]
    public async Task Checking_in_twice_is_refused()
    {
        var booked = await _appointments.BookAsync(NewAppointment());
        await _appointments.MarkCheckedInAsync(booked.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _appointments.MarkCheckedInAsync(booked.Id, Guid.NewGuid()));
    }

    // ── Reminders ──────────────────────────────────────────────────────────

    [Fact]
    public async Task An_upcoming_appointment_and_a_visit_follow_up_both_surface_as_reminders()
    {
        await _appointments.BookAsync(NewAppointment(on: DateTime.Today.AddDays(2)));

        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Visits.Add(new Visit
            {
                VisitNo = "V00001", PatientId = _patientId, DoctorId = _doctorId,
                ScheduledOn = DateTime.Today.AddDays(-10), FollowUpOn = DateTime.Today.AddDays(1)
            });
            await db.SaveChangesAsync();
        }

        var due = await _appointments.GetDueRemindersAsync(leadDays: 3);

        Assert.Contains(due, d => d.SourceKind == ReminderSourceKind.Appointment);
        Assert.Contains(due, d => d.SourceKind == ReminderSourceKind.FollowUp);
    }

    [Fact]
    public async Task Marking_a_reminder_actioned_keeps_it_on_the_due_list_but_flags_it_reminded()
    {
        var booked = await _appointments.BookAsync(NewAppointment(on: DateTime.Today.AddDays(1)));

        var before = await _appointments.GetDueRemindersAsync(leadDays: 3);
        Assert.Contains(before, d => d.SourceId == booked.Id && !d.IsReminded);

        await _appointments.MarkReminderActionedAsync(
            ReminderSourceKind.Appointment, booked.Id, _patientId, booked.ScheduledOn, "front desk");

        var after = await _appointments.GetDueRemindersAsync(leadDays: 3);
        Assert.Contains(after, d => d.SourceId == booked.Id && d.IsReminded);
    }

    [Fact]
    public async Task Unmarking_a_reminder_flags_it_not_reminded_again()
    {
        var booked = await _appointments.BookAsync(NewAppointment(on: DateTime.Today.AddDays(1)));

        await _appointments.MarkReminderActionedAsync(
            ReminderSourceKind.Appointment, booked.Id, _patientId, booked.ScheduledOn, "front desk");
        await _appointments.UnmarkReminderActionedAsync(ReminderSourceKind.Appointment, booked.Id);

        var after = await _appointments.GetDueRemindersAsync(leadDays: 3);
        Assert.Contains(after, d => d.SourceId == booked.Id && !d.IsReminded);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }

        GC.SuppressFinalize(this);
    }
}
