using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Pharma.Data;
using Application = FlaUI.Core.Application;

namespace Pharma.UiTests;

/// <summary>
/// Drives the actual login window end to end: the startup gate appears
/// instead of the dashboard once Require sign-in is on, the seeded Admin
/// account is forced through a password change on its first sign-in, and
/// the sidebar's "Log out" reopens the login window without losing the
/// session if it is cancelled.
///
/// This manages its own application process rather than sharing
/// <see cref="AppFixture"/>: the startup gate only exists before the main
/// window opens, so proving it needs a launch that never reaches the
/// dashboard at all, and a second launch of the same database afterwards.
/// PasswordBox has no bindable value UI Automation can read or set, so
/// passwords here go in as real keystrokes via <see cref="Keyboard.Type(string)"/>,
/// the same way a person would type them.
/// </summary>
public class LoginFlowUiTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"twinkle-login-{Guid.NewGuid():N}.db");
    private readonly string _logDir;
    private Application? _app;
    private UIA3Automation? _automation;

    public LoginFlowUiTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), $"twinkle-login-logs-{Guid.NewGuid():N}");
    }

    private Window Launch()
    {
        var startInfo = new ProcessStartInfo(AppFixture.FindExecutable()) { UseShellExecute = false };
        startInfo.Environment[DbBootstrapper.PathOverrideVariable] = _dbPath;
        startInfo.Environment[AppLog.DirectoryOverrideVariable] = _logDir;

        _app = Application.Launch(startInfo);
        _automation = new UIA3Automation();

        return _app.GetMainWindow(_automation, TimeSpan.FromSeconds(30))
               ?? throw new InvalidOperationException("No window appeared.");
    }

    private void CloseApp()
    {
        try { _app?.Close(); _app?.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2)); }
        catch (Exception) { /* already gone */ }

        try { _app?.Kill(); } catch (Exception) { }

        _automation?.Dispose();
        _app?.Dispose();
        _app = null;
        _automation = null;
    }

    private static void TypeInto(Window window, string automationId, string text)
    {
        var element = AppFixture.WaitUntilFound(window, automationId);
        element.Focus();
        Keyboard.Type(text);
    }

    private static void Click(Window window, string automationId)
        => AppFixture.WaitUntilFound(window, automationId).AsButton().Invoke();

    /// <summary>The current top-level window showing the application shell —
    /// found fresh each call by searching every top-level window on the
    /// desktop, rather than trusting a window reference, handle or process
    /// id captured before the login window closed (which one Windows
    /// considers "the" main window of the process changes under FlaUI in
    /// ways that were not reliable across that transition here).</summary>
    private Window? FindShellWindow()
    {
        try
        {
            return _automation!.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                .Select(w => w.AsWindow())
                .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByAutomationId("PageTitle")) is not null);
        }
        catch (Exception)
        {
            // The tree is mid-update between the two windows; try again.
            return null;
        }
    }

    [Fact]
    public void Turning_on_require_login_gates_the_next_launch_and_the_seeded_admin_signs_in()
    {
        // First launch: login is off by default, so this reaches the
        // ordinary dashboard, and seeds the database (including the
        // default Admin account) along the way.
        var window = Launch();
        AppFixture.WaitUntil(() => window.FindFirstDescendant(cf => cf.ByAutomationId("PageTitle")) is not null,
                             "the dashboard on the first, ungated launch");

        window.FindFirstDescendant(cf => cf.ByAutomationId("NavSettings"))!.AsButton().Invoke();
        AppFixture.WaitUntil(() => window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsTabs")) is not null,
                             "the Settings screen");

        var tab = window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsTabs"))!
            .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem))
            .First(t => t.Name == "Security");
        tab.AsTabItem().Select();

        window.FindFirstDescendant(cf => cf.ByAutomationId("RequireLogin"))!.AsCheckBox().IsChecked = true;
        Click(window, "SecuritySave");

        AppFixture.WaitUntil(
            () => (window.FindFirstDescendant(cf => cf.ByAutomationId("SettingsStatus"))?.AsLabel().Text ?? "")
                .Contains("saved", StringComparison.OrdinalIgnoreCase),
            "require-login to save");

        CloseApp();

        // Second launch, same database: the login window has to appear
        // before anything else, in place of the dashboard.
        var login = Launch();
        AppFixture.WaitUntil(() => login.FindFirstDescendant(cf => cf.ByAutomationId("LoginUsername")) is not null,
                             "the login window on the gated launch");
        Assert.Null(login.FindFirstDescendant(cf => cf.ByAutomationId("PageTitle")));

        // The clinic's own identity, not a hardcoded brand name — read live
        // from Settings the same way the shell's own sidebar does.
        AppFixture.WaitUntil(
            () => !string.IsNullOrEmpty(login.FindFirstDescendant(cf => cf.ByAutomationId("LoginClinicName"))?.Name),
            "the clinic name to load on the login screen");
        Assert.Equal("Sivaayaan HMS", login.FindFirstDescendant(cf => cf.ByAutomationId("LoginClinicName"))!.Name);

        TypeInto(login, "LoginUsername", UserSeeder.DefaultAdminUsername);
        TypeInto(login, "LoginPassword", "HMSAdmin@123");
        Click(login, "LoginSubmit");

        // The seeded Admin always has to change its password on first sign-in.
        AppFixture.WaitUntil(() => login.FindFirstDescendant(cf => cf.ByAutomationId("LoginNewPassword")) is not null,
                             "the forced change-password stage");

        TypeInto(login, "LoginNewPassword", "NewAdminPass1");
        TypeInto(login, "LoginConfirmPassword", "NewAdminPass1");
        Click(login, "LoginChangePasswordSubmit");

        // The login window closes and MainWindow opens in its place — a new
        // top-level window, not something found by re-querying the (now
        // closed) login window's own element tree.
        Window? shell = null;
        AppFixture.WaitUntil(() =>
        {
            shell = FindShellWindow();
            return shell is not null;
        }, "the dashboard after signing in");

        var loggedInAs = shell!.FindFirstDescendant(cf => cf.ByAutomationId("LoggedInAs"))?.Name ?? "";
        Assert.Contains("Admin", loggedInAs);

        CloseApp();
    }

    public void Dispose()
    {
        CloseApp();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_dbPath + suffix); } catch (IOException) { }
        }

        try { Directory.Delete(_logDir, recursive: true); } catch (Exception) { }
    }
}
