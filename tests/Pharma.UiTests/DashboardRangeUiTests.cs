namespace Pharma.UiTests;

/// <summary>
/// The Dashboard's From/To range.
///
/// It opens on today to today, exactly as it always did, so a clinic that never
/// touches the pickers sees no difference. Choosing a wider range has to be
/// said out loud on the tiles, or a week's total reads as a day's.
/// </summary>
public class DashboardRangeUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void OpenDashboard() => app.Navigate("NavDashboard", "Dashboard");

    [Fact]
    public void The_dashboard_opens_on_today_and_the_range_pickers_are_there()
    {
        OpenDashboard();

        Assert.NotNull(app.Find("DashFromDate"));
        Assert.NotNull(app.Find("DashToDate"));
        Assert.NotNull(app.Find("DashToday"));

        // Today to today: still headed with the single day, which is how the
        // dashboard has always opened.
        Assert.DoesNotContain(" to ", app.TextOf("PageSubtitle"));
    }

    /// <summary>
    /// Widening the range must change what the subtitle says. A week's figures
    /// under a heading that reads as one day would be a wrong number that looks
    /// right.
    /// </summary>
    [Fact]
    public void Choosing_a_wider_range_names_the_period_and_Today_returns_to_one_day()
    {
        OpenDashboard();
        var todayTitle = app.TextOf("PageSubtitle");

        app.SetDate("DashFromDate", DateTime.Today.AddDays(-6));

        AppFixture.WaitUntil(
            () => app.TextOf("PageSubtitle").Contains(" to ", StringComparison.Ordinal),
            "the subtitle to name the chosen period");

        Assert.NotEqual(todayTitle, app.TextOf("PageSubtitle"));

        app.Click("DashToday");

        AppFixture.WaitUntil(
            () => app.TextOf("PageSubtitle") == todayTitle,
            "Today to restore the single-day heading");
    }
}
