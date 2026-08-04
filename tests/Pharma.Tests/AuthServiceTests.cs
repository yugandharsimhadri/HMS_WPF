using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// Login, the forced first-time password change, Admin's user management,
/// and the EnterpriseAdmin recovery path — the business rules the login
/// design calls out explicitly: the seeded Admin always works, a new or
/// reset user must change their password before anything else, and
/// EnterpriseAdmin can reset anyone without ever being a row in Users.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"auth-{Guid.NewGuid():N}.db");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var provider = services.BuildServiceProvider();

        _factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _auth = new AuthService(_factory);

        using var db = _factory.CreateDbContext();
        db.Database.Migrate();
    }

    private async Task SeedAdminAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await UserSeeder.SeedAsync(db);
    }

    [Fact]
    public async Task Seeded_admin_signs_in_and_is_asked_to_change_password()
    {
        await SeedAdminAsync();

        var result = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "HMSAdmin@123");

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.NotNull(result.User);
        Assert.True(result.User!.MustChangePassword);
        Assert.Equal(UserRole.Admin, result.User.Role);
    }

    [Fact]
    public async Task Seeding_never_runs_twice()
    {
        await SeedAdminAsync();
        await SeedAdminAsync();

        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Username == UserSeeder.DefaultAdminUsername));
    }

    [Fact]
    public async Task Wrong_password_fails_without_revealing_which_field_was_wrong()
    {
        await SeedAdminAsync();

        var result = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "not the password");

        Assert.Equal(LoginOutcome.Failed, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Unknown_username_fails_the_same_way_as_a_wrong_password()
    {
        var result = await _auth.LoginAsync("NobodyHere", "whatever");
        Assert.Equal(LoginOutcome.Failed, result.Outcome);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("AdMiN")]
    public async Task Username_is_not_case_sensitive_at_login(string typed)
    {
        await SeedAdminAsync();

        var result = await _auth.LoginAsync(typed, "HMSAdmin@123");

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.Equal(UserSeeder.DefaultAdminUsername, result.User!.Username);
    }

    [Fact]
    public async Task A_username_differing_only_in_case_cannot_be_created_as_a_second_account()
    {
        await _auth.SaveUserAsync(
            new User { Username = "FrontDesk", DisplayName = "A", Role = UserRole.Pharmacy, IsActive = true },
            "Temp12345");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _auth.SaveUserAsync(
            new User { Username = "frontdesk", DisplayName = "B", Role = UserRole.Doctor, IsActive = true },
            "Temp12345"));
    }

    [Fact]
    public async Task Changing_the_password_clears_the_forced_change_flag_and_signs_in_next_time()
    {
        await SeedAdminAsync();

        var first = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "HMSAdmin@123");
        await _auth.ChangeOwnPasswordAsync(first.User!.Id, "NewStrongPass1");

        var second = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "NewStrongPass1");
        Assert.Equal(LoginOutcome.Success, second.Outcome);
        Assert.False(second.User!.MustChangePassword);

        // The old password no longer works.
        var stale = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "HMSAdmin@123");
        Assert.Equal(LoginOutcome.Failed, stale.Outcome);
    }

    [Fact]
    public async Task Admin_can_create_a_new_user_who_must_change_password_on_first_sign_in()
    {
        await _auth.SaveUserAsync(
            new User { Username = "FrontDesk", DisplayName = "Front Desk", Role = UserRole.Pharmacy, IsActive = true },
            "Temp12345");

        var result = await _auth.LoginAsync("FrontDesk", "Temp12345");

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.True(result.User!.MustChangePassword);
        Assert.Equal(UserRole.Pharmacy, result.User.Role);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in()
    {
        await _auth.SaveUserAsync(
            new User { Username = "OldStaff", DisplayName = "Old Staff", Role = UserRole.Doctor, IsActive = true },
            "Temp12345");

        var users = await _auth.GetUsersAsync();
        var user = users.Single(u => u.Username == "OldStaff");
        user.IsActive = false;
        await _auth.SaveUserAsync(user, null);

        var result = await _auth.LoginAsync("OldStaff", "Temp12345");
        Assert.Equal(LoginOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Two_users_cannot_share_a_username()
    {
        await _auth.SaveUserAsync(
            new User { Username = "Reception", DisplayName = "A", Role = UserRole.Pharmacy, IsActive = true },
            "Temp12345");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _auth.SaveUserAsync(
            new User { Username = "Reception", DisplayName = "B", Role = UserRole.Doctor, IsActive = true },
            "Temp12345"));
    }

    [Fact]
    public async Task EnterpriseAdmin_is_never_a_row_in_the_users_table()
    {
        await SeedAdminAsync();

        var result = await _auth.LoginAsync(AuthService.EnterpriseAdminUsername, "SivAyAAn@HMS");
        Assert.Equal(LoginOutcome.EnterpriseRecovery, result.Outcome);

        var users = await _auth.GetUsersAsync();
        Assert.DoesNotContain(users, u => u.Username == AuthService.EnterpriseAdminUsername);
    }

    [Fact]
    public async Task EnterpriseAdmin_with_the_wrong_password_fails_like_anyone_else()
    {
        var result = await _auth.LoginAsync(AuthService.EnterpriseAdminUsername, "wrong");
        Assert.Equal(LoginOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Creating_a_user_named_EnterpriseAdmin_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _auth.SaveUserAsync(
            new User { Username = AuthService.EnterpriseAdminUsername, DisplayName = "x", Role = UserRole.Admin, IsActive = true },
            "Temp12345"));
    }

    [Fact]
    public async Task EnterpriseAdmin_can_reset_a_locked_out_admins_password()
    {
        await SeedAdminAsync();

        var admin = (await _auth.GetUsersAsync()).Single(u => u.Username == UserSeeder.DefaultAdminUsername);
        await _auth.ResetPasswordAsync(admin.Id, "Reset12345");

        var result = await _auth.LoginAsync(UserSeeder.DefaultAdminUsername, "Reset12345");
        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.True(result.User!.MustChangePassword);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
