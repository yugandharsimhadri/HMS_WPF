using Microsoft.EntityFrameworkCore;
using Pharma.Core;
using Pharma.Data.Security;

namespace Pharma.Data;

/// <summary>
/// Seeds the one account every installation needs regardless of whether
/// login is switched on: "Admin", so turning Settings → Security →
/// Require login on is immediately usable without a separate setup step.
/// Never overwrites — a password Admin has since changed, or a user
/// Admin has since renamed, survives every later run.
/// </summary>
public static class UserSeeder
{
    public const string DefaultAdminUsername = "Admin";
    private const string DefaultAdminPassword = "HMSAdmin@123";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Username == DefaultAdminUsername, ct)) return;

        var (hash, salt) = PasswordHasher.Hash(DefaultAdminPassword);

        db.Users.Add(new User
        {
            Username = DefaultAdminUsername,
            DisplayName = "Admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Admin,
            IsActive = true,
            MustChangePassword = true
        });

        await db.SaveChangesAsync(ct);
        AppLog.Info("Seeded the default Admin account.");
    }
}
