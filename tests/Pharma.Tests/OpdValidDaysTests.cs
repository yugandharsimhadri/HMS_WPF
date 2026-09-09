using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The OPD validity window: one consultation fee covering a doctor for a set
/// number of days, which is how most Indian clinics actually charge.
///
/// The arithmetic is day-wise and deliberately done once — at the moment the
/// money is taken — rather than recomputed from the doctor's current setting on
/// every later visit. These tests pin both halves of that: the window a payment
/// opens, and what counts as being inside it.
/// </summary>
public class OpdValidDaysTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"opd-valid-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly OpdService _opd;
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _otherPatientId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();
    private readonly Guid _otherDoctorId = Guid.NewGuid();

    public OpdValidDaysTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _opd = new OpdService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();

        db.Patients.Add(new Patient
        {
            Id = _patientId, PatientNo = "P00001", Name = "Ramesh Kumar", Phone = "9000000001", Age = 41
        });
        db.Patients.Add(new Patient
        {
            Id = _otherPatientId, PatientNo = "P00002", Name = "Sita Devi", Phone = "9000000002", Age = 35
        });

        // Seven-day cover, the common arrangement.
        db.Doctors.Add(new Doctor
        {
            Id = _doctorId, Name = "Dr Priya Nair", Speciality = "Paediatrics",
            Qualification = "MBBS, MD (Paediatrics)", ConsultationFee = 300m, OpdValidDays = 7
        });

        // No cover at all — every visit charged, which is the default and what
        // every doctor already on file gets when this upgrade is applied.
        db.Doctors.Add(new Doctor
        {
            Id = _otherDoctorId, Name = "Dr Anand Rao", Speciality = "General",
            ConsultationFee = 250m, OpdValidDays = 0
        });

        db.SaveChanges();
    }

    private Task<Visit> BookAsync(Guid? patientId = null, Guid? doctorId = null,
                                  DateTime? on = null, decimal fee = 300m, Guid? waivedAgainst = null)
        => _opd.BookVisitAsync(patientId ?? _patientId, doctorId ?? _doctorId,
                               on ?? DateTime.Today.AddHours(10), "Fever", fee,
                               feeWaivedAgainstVisitId: waivedAgainst);

    private async Task<Visit> ReloadAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Visits.FirstAsync(v => v.Id == id);
    }

    /// <summary>Backdates a payment, so a window can be tested near its edges
    /// without waiting a week.</summary>
    private async Task PaidDaysAgo(Guid visitId, int days)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var v = await db.Visits.FirstAsync(x => x.Id == visitId);
        var paidOn = DateTime.Now.AddDays(-days);
        v.FeePaidOn = paidOn;
        v.FreeFollowUpUntil = paidOn.Date.AddDays(7);
        await db.SaveChangesAsync();
    }

    // ── The window a payment opens ────────────────────────────────────────

    [Fact]
    public async Task Taking_the_fee_opens_a_window_of_the_doctors_own_length()
    {
        var visit = await BookAsync();
        await _opd.CollectFeeAsync(visit.Id);

        var paid = await ReloadAsync(visit.Id);

        // Seven days from the day the money arrived, inclusive of that seventh
        // day — "paid on the 1st, free until the 8th".
        Assert.Equal(DateTime.Today.AddDays(7), paid.FreeFollowUpUntil);
    }

    [Fact]
    public async Task A_doctor_with_no_window_opens_none()
    {
        var visit = await BookAsync(doctorId: _otherDoctorId, fee: 250m);
        await _opd.CollectFeeAsync(visit.Id);

        Assert.Null((await ReloadAsync(visit.Id)).FreeFollowUpUntil);
    }

    /// <summary>
    /// The window is dated from the payment, not the booking. A patient who
    /// books on Monday and pays on Thursday was told "seven days" on Thursday.
    /// </summary>
    [Fact]
    public async Task The_window_runs_from_the_day_the_money_arrived()
    {
        var visit = await BookAsync(on: DateTime.Today.AddDays(-3).AddHours(10));
        await _opd.CollectFeeAsync(visit.Id);

        var paid = await ReloadAsync(visit.Id);
        Assert.Equal(DateTime.Today.AddDays(7), paid.FreeFollowUpUntil);
    }

    // ── Who is inside it ──────────────────────────────────────────────────

    [Fact]
    public async Task A_return_inside_the_window_is_covered()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        var cover = await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(3));

        Assert.NotNull(cover);
        Assert.Equal(first.Id, cover!.Id);
    }

    /// <summary>The seventh day is inside a seven-day window; the eighth is not.
    /// This is the boundary the desk argues about, so it is pinned.</summary>
    [Fact]
    public async Task The_last_day_is_covered_and_the_day_after_is_not()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        Assert.NotNull(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(7)));
        Assert.Null(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(8)));
    }

    /// <summary>Day-wise, not clock-wise: a fee paid late in the evening still
    /// covers the whole of the final day.</summary>
    [Fact]
    public async Task The_time_of_day_never_enters_into_it()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);
        await PaidDaysAgo(first.Id, 7);

        // Paid seven days ago at this hour; today is the last covered day, and
        // it is covered no matter what the clock says.
        Assert.NotNull(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today));
        Assert.Null(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(1)));
    }

    [Fact]
    public async Task A_fee_paid_to_one_doctor_does_not_cover_another()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        Assert.Null(await _opd.FindFeeCoverAsync(_patientId, _otherDoctorId, DateTime.Today));
    }

    [Fact]
    public async Task One_patients_fee_does_not_cover_another_patient()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        Assert.Null(await _opd.FindFeeCoverAsync(_otherPatientId, _doctorId, DateTime.Today));
    }

    [Fact]
    public async Task An_unpaid_visit_covers_nothing()
    {
        await BookAsync();

        Assert.Null(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today));
    }

    /// <summary>
    /// The rule that stops the scheme running away: only a payment opens a
    /// window. If a free review re-anchored it, a patient returning on day six
    /// would be covered to day thirteen, then day twenty, and never pay again.
    /// </summary>
    [Fact]
    public async Task A_review_does_not_extend_the_window()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);
        var paidUntil = (await ReloadAsync(first.Id)).FreeFollowUpUntil;

        var review = await BookAsync(on: DateTime.Today.AddDays(6).AddHours(10),
                                     fee: 0m, waivedAgainst: first.Id);

        var reloaded = await ReloadAsync(review.Id);
        Assert.True(reloaded.IsReview);
        Assert.Null(reloaded.FreeFollowUpUntil);

        // Day nine is past the original window and nothing has moved it.
        Assert.Equal(DateTime.Today.AddDays(7), paidUntil);
        Assert.Null(await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(9)));
    }

    /// <summary>A second payment inside a live window restarts the clock from
    /// the new payment — the desk charged, so the patient gets the cover.</summary>
    [Fact]
    public async Task Paying_again_inside_the_window_restarts_it()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);
        await PaidDaysAgo(first.Id, 5);

        var second = await BookAsync();
        await _opd.CollectFeeAsync(second.Id);

        var cover = await _opd.FindFeeCoverAsync(_patientId, _doctorId, DateTime.Today.AddDays(6));

        // The older payment expired two days ago; the newer one answers.
        Assert.NotNull(cover);
        Assert.Equal(second.Id, cover!.Id);
    }

    // ── What the visit then reads as ──────────────────────────────────────

    [Fact]
    public async Task A_review_is_settled_without_being_paid()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        var review = await BookAsync(fee: 0m, waivedAgainst: first.Id);
        var reloaded = await ReloadAsync(review.Id);

        Assert.True(reloaded.IsReview);
        Assert.True(reloaded.FeeSettled);
        Assert.False(reloaded.FeePaid);
        Assert.False(reloaded.CanCollectFee);
        Assert.Equal("Review", reloaded.VisitKind);
    }

    [Fact]
    public async Task An_ordinary_paid_visit_reads_as_new()
    {
        var visit = await BookAsync();
        await _opd.CollectFeeAsync(visit.Id);

        var paid = await ReloadAsync(visit.Id);
        Assert.Equal("New", paid.VisitKind);
        Assert.False(paid.IsReview);
        Assert.True(paid.FeeSettled);
    }

    /// <summary>A review is still just a booking with no money on it, so it can
    /// be called off — unlike the payment it rides on.</summary>
    [Fact]
    public async Task A_review_can_still_be_cancelled()
    {
        var first = await BookAsync();
        await _opd.CollectFeeAsync(first.Id);

        var review = await BookAsync(fee: 0m, waivedAgainst: first.Id);
        Assert.True((await ReloadAsync(review.Id)).CanCancel);

        await _opd.SetStatusAsync(review.Id, VisitStatus.Cancelled);
        Assert.Equal(VisitStatus.Cancelled, (await ReloadAsync(review.Id)).Status);
    }

    [Fact]
    public void The_doctors_degrees_print_beside_the_name()
    {
        var withDegrees = new Doctor { Name = "Dr Priya Nair", Qualification = "MBBS, MD (Paediatrics)" };
        Assert.Equal("Dr Priya Nair, MBBS, MD (Paediatrics)", withDegrees.NameWithQualification);

        // Left blank it must not leave a dangling comma on the receipt.
        var without = new Doctor { Name = "Dr Anand Rao" };
        Assert.Equal("Dr Anand Rao", without.NameWithQualification);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { File.Delete(_dbPath); } catch { /* a temp file is not worth failing a test over */ }
    }
}
