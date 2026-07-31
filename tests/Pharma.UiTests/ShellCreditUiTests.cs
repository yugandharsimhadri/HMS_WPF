using System.Diagnostics;
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

    /// <summary>
    /// The number on screen has to be the one the build stamped on the exe, not
    /// a near-miss. It used to be read off the assembly version, which is always
    /// four parts with the last forced to zero and any suffix discarded, so a
    /// build of 1.2.3-beta showed up as 1.2.3 and nobody could tell.
    /// </summary>
    [Fact]
    public void The_version_is_the_one_the_build_stamped_on_the_exe()
    {
        app.Navigate("NavOpd", "OPD");

        var shown = app.TextOf("AppVersion");

        // major.minor.patch.publish, and nothing after it. "unknown", or a
        // version left at 0.0.0.0, would tell the person on the phone nothing —
        // and neither would the commit hash that used to sit beside it, which
        // only gave them a second number to read out the wrong one of.
        Assert.Matches(new Regex(@"^Version \d+\.\d+\.\d+\.\d+(-\S+)?$"), shown);
        Assert.DoesNotContain("Version 0.0.0", shown);

        // And it is the version the compiler stamped, character for character,
        // rather than something that merely looks like it.
        var assembly = Path.Combine(AppFixture.ApplicationDirectory, "TwinkleHMS.dll");
        var stamped = FileVersionInfo.GetVersionInfo(assembly).ProductVersion ?? "";

        var version = stamped.Split('+')[0];
        Assert.Equal($"Version {version}", shown);

        // The fourth part is the publish number, and it is the whole point: it
        // is what identifies which release a clinic is running when they ring
        // up. A three-part version would leave every build since 1.0.0 calling
        // itself 1.0.0.
        Assert.Equal(4, version.Split('.').Length);
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
