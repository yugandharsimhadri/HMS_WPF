using Microsoft.EntityFrameworkCore;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// AppDbContext.Stamp() — the part of the login feature that touches every
/// other entity in the system, so it gets its own focused test rather than
/// relying on some other service's test to notice if it broke. Two things
/// matter: a context given no current user (every existing caller, and
/// every installation that never turns login on) behaves exactly as it did
/// before this feature existed, and a context given one stamps it.
/// </summary>
public class AuditStampTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"auditstamp-{Guid.NewGuid():N}.db");

    private AppDbContext NewContext(ICurrentUserContext? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        return new AppDbContext(options, currentUser);
    }

    [Fact]
    public async Task With_no_current_user_context_new_rows_are_stamped_null()
    {
        await using (var db = NewContext())
            await db.Database.MigrateAsync();

        await using var write = NewContext();
        var doctor = new Doctor { Name = "Dr. No User", ConsultationFee = 100m, IsActive = true };
        write.Doctors.Add(doctor);
        await write.SaveChangesAsync();

        Assert.Null(doctor.CreatedByUserId);
        Assert.Null(doctor.UpdatedByUserId);
    }

    [Fact]
    public async Task With_a_signed_in_user_new_and_edited_rows_are_stamped()
    {
        await using (var db = NewContext())
            await db.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var currentUser = new FixedCurrentUser(userId);

        await using var write = NewContext(currentUser);
        var doctor = new Doctor { Name = "Dr. Stamped", ConsultationFee = 100m, IsActive = true };
        write.Doctors.Add(doctor);
        await write.SaveChangesAsync();

        Assert.Equal(userId, doctor.CreatedByUserId);
        Assert.Null(doctor.UpdatedByUserId);

        await using var edit = NewContext(currentUser);
        var toEdit = await edit.Doctors.FirstAsync(d => d.Id == doctor.Id);
        toEdit.Speciality = "Paediatrics";
        await edit.SaveChangesAsync();

        Assert.Equal(userId, toEdit.UpdatedByUserId);
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId => userId;
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
