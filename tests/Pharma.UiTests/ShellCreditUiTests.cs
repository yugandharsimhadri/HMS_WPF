using System.Text.RegularExpressions;

namespace Pharma.UiTests;

/// <summary>
/// The credit and the build number in the foot of the navigation.
///
/// It is there so someone on a support call can read out which build they are
/// running without being talked through a menu — which means it has to be on
/// screen whatever page they happen to be on, and it has to be a real version
/// rather than a placeholder that was never wired up.
/// </summary>
public class ShellCreditUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void The_developer_is_credited()
    {
        app.Navigate("NavOpd", "OPD");

        Assert.Equal("Developed by Sivayaan Technologies", app.TextOf("AppCredit"));
    }

    [Fact]
    public void The_version_is_the_one_the_build_produced()
    {
        app.Navigate("NavOpd", "OPD");

        var shown = app.TextOf("AppVersion");

        // Three parts, all digits — "unknown", or a version left at 0.0.0, would
        // tell the person on the phone nothing.
        Assert.Matches(new Regex(@"^Version \d+\.\d+\.\d+$"), shown);
        Assert.DoesNotContain("Version 0.0.0", shown);
    }

    [Theory]
    [InlineData("NavSale", "Pharmacy counter")]
    [InlineData("NavProducts", "Medicines")]
    [InlineData("NavReports", "Reports")]
    [InlineData("NavSettings", "Settings")]
    public void It_stays_visible_on_every_page(string nav, string title)
    {
        app.Navigate(nav, title);

        Assert.Contains("Sivayaan", app.TextOf("AppCredit"));
        Assert.StartsWith("Version ", app.TextOf("AppVersion"));
    }
}
