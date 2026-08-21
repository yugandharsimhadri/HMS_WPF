using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// When a visit may still be cancelled.
///
/// Cancelling is the desk undoing a booking made by mistake, and it is only
/// honest while nothing has happened yet. Once the fee is taken there is a
/// numbered receipt in the patient's hand, and once the doctor has finished
/// there is a consultation on the record — cancelling either would leave a
/// paid or written-up visit sitting in the register as though it never
/// occurred. The desk refunds or reissues on paper instead.
///
/// The button on the tile is disabled for these cases, but the rule lives in
/// the service, because a tile drawn before the fee was taken elsewhere is
/// out of date by the time it is clicked.
/// </summary>
public class OpdCancelTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"opd-cancel-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly OpdService _opd;
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _doctorId = Guid.NewGuid();

    public OpdCancelTests()
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
        db.Doctors.Add(new Doctor { Id = _doctorId, Name = "Dr Priya Nair", Speciality = "General" });
        db.SaveChanges();
    }

    private Task<Visit> BookAsync(decimal fee = 300m)
        => _opd.BookVisitAsync(_patientId, _doctorId, DateTime.Today.AddHours(10), "Fever", fee);

    private async Task<Visit> ReloadAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Visits.FirstAsync(v => v.Id == id);
    }

    [Fact]
    public async Task A_fresh_unpaid_visit_can_be_cancelled()
    {
        var visit = await BookAsync();
        Assert.True(visit.CanCancel);

        await _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled);

        Assert.Equal(VisitStatus.Cancelled, (await ReloadAsync(visit.Id)).Status);
    }

    [Fact]
    public async Task A_paid_visit_cannot_be_cancelled()
    {
        var visit = await BookAsync();
        await _opd.CollectFeeAsync(visit.Id);

        var paid = await ReloadAsync(visit.Id);
        Assert.True(paid.FeePaid);
        Assert.False(paid.CanCancel);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled));

        // The desk needs to know which receipt to reverse, not just that it
        // was refused.
        Assert.Contains("fee has already been taken", refused.Message);
        Assert.Contains(paid.FeeReceiptNo!, refused.Message);

        Assert.NotEqual(VisitStatus.Cancelled, (await ReloadAsync(visit.Id)).Status);
    }

    [Fact]
    public async Task A_completed_visit_cannot_be_cancelled()
    {
        var visit = await BookAsync(fee: 0m);
        await _opd.SetStatusAsync(visit.Id, VisitStatus.Completed);

        Assert.False((await ReloadAsync(visit.Id)).CanCancel);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled));

        Assert.Contains("already completed", refused.Message);
        Assert.Equal(VisitStatus.Completed, (await ReloadAsync(visit.Id)).Status);
    }

    /// <summary>
    /// A visit part-way through — the doctor has called the patient in but has
    /// not finished — is still cancellable if no money changed hands. The
    /// patient who walks out of the room mid-consultation is a real case.
    /// </summary>
    [Fact]
    public async Task An_unpaid_visit_in_consultation_can_still_be_cancelled()
    {
        var visit = await BookAsync(fee: 0m);
        await _opd.SetStatusAsync(visit.Id, VisitStatus.InConsultation);

        await _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled);

        Assert.Equal(VisitStatus.Cancelled, (await ReloadAsync(visit.Id)).Status);
    }

    [Fact]
    public async Task Cancelling_twice_is_refused_the_second_time()
    {
        var visit = await BookAsync(fee: 0m);
        await _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _opd.SetStatusAsync(visit.Id, VisitStatus.Cancelled));
    }

    /// <summary>The guard is about cancelling only — a paid visit still has to
    /// be able to move through the queue to completed.</summary>
    [Fact]
    public async Task A_paid_visit_can_still_be_completed()
    {
        var visit = await BookAsync();
        await _opd.CollectFeeAsync(visit.Id);

        await _opd.SetStatusAsync(visit.Id, VisitStatus.Completed);

        Assert.Equal(VisitStatus.Completed, (await ReloadAsync(visit.Id)).Status);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { File.Delete(_dbPath); } catch { /* the temp file is not worth failing a test over */ }
    }
}
