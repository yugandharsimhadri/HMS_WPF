namespace Pharma.UiTests;

/// <summary>
/// Login is an optional feature — off by default, switched on from Settings
/// → Security, exactly like the Diagnostics module toggle. What this guards:
/// the toggle persists across leaving the screen, and the Users section
/// (only meaningful once accounts exist) is there for whoever is setting it
/// up. It does not drive an actual sign-in through the UI — turning
/// RequireLogin on only takes effect at the next application start, so
/// nothing here needs the running test instance to sign in, and the
/// EnterpriseAdmin/first-login/reset flows are covered at the service layer
/// in AuthServiceTests instead.
/// </summary>
public class SecurityUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void SetRequireLogin(bool value)
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Security");
        app.CheckBox("RequireLogin").IsChecked = value;
        app.Click("SecuritySave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            $"require-login ({value}) to save");
    }

    [Fact]
    public void The_require_login_toggle_survives_leaving_the_screen()
    {
        // Whatever a previous test run left it as, force a known starting
        // point so this proves the transition rather than finding it already set.
        SetRequireLogin(false);

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Security");

        AppFixture.WaitUntil(() => app.CheckBox("RequireLogin").IsChecked == false, "the toggle to reload as off");

        SetRequireLogin(true);

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Security");

        AppFixture.WaitUntil(() => app.CheckBox("RequireLogin").IsChecked == true, "the toggle to reload as on");

        // Leave it off for whatever runs the executable next — turning it on
        // for real would gate every future launch behind a sign-in.
        SetRequireLogin(false);
    }

    /// <summary>
    /// Nobody is signed in inside these UI tests — the fixture launches the
    /// application with login switched off — so the user-management section
    /// is available the same way it is on a fresh installation before login
    /// has ever been set up.
    /// </summary>
    [Fact]
    public void The_users_section_is_available_before_anyone_has_signed_in()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Security");

        AppFixture.WaitUntil(() => app.Find("UsersList") is not null, "the users list");
        AppFixture.WaitUntil(() => app.Find("UserUsername") is not null, "the user form");

        // The seeded default Admin account is always present.
        AppFixture.WaitUntil(() => app.ListBox("UsersList").Items.Length > 0, "the seeded Admin account to appear");
    }
}
