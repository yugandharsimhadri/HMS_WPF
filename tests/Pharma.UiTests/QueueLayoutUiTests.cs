namespace Pharma.UiTests;

/// <summary>
/// How the OPD queue is shown: tiles or rows, and which sitting.
///
/// The layout is the user's choice, so both settings have to be equally usable.
/// The sitting is a filter, and a filter that quietly loses a patient is worse
/// than no filter at all — so it has to say when it is holding people back.
/// </summary>
public class QueueLayoutUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private void ChooseLayout(string layout)
    {
        app.Navigate("NavSettings", "Settings");
        app.ComboBox("QueueLayout").Select(layout);
        app.Click("ShopSave");

        AppFixture.WaitUntil(
            () => app.TextOf("SettingsStatus").Contains(layout, StringComparison.OrdinalIgnoreCase),
            $"the {layout} setting to save");
    }

    [Fact]
    public void The_chosen_layout_survives_leaving_the_screen()
    {
        ChooseLayout("Rows");

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");

        AppFixture.WaitUntil(
            () => app.ComboBox("QueueLayout").SelectedItems.FirstOrDefault()?.Text == "Rows",
            "the setting to be read back");

        Assert.Equal("Rows", app.ComboBox("QueueLayout").SelectedItems[0].Text);

        ChooseLayout("Tiles");
        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");

        AppFixture.WaitUntil(
            () => app.ComboBox("QueueLayout").SelectedItems.FirstOrDefault()?.Text == "Tiles",
            "the setting to be read back");

        Assert.Equal("Tiles", app.ComboBox("QueueLayout").SelectedItems[0].Text);
    }

    [Fact]
    public void A_visit_can_be_completed_in_either_layout()
    {
        foreach (var layout in new[] { "Rows", "Tiles" })
        {
            ChooseLayout(layout);

            var name = $"Layout {layout} {DateTime.Now:HHmmssfff}";
            OpdUiTests.BookWalkIn(app, name, "9005004003", "5");

            AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), $"the {layout} entry to appear");

            app.ClickTile("OpdWaitingList", "TileDone", name);

            AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", name),
                                 $"the {layout} entry to move to completed");

            Assert.True(app.HasTile("OpdCompletedList", name), $"Completing failed in {layout} layout.");
            Assert.False(app.HasTile("OpdWaitingList", name));
        }
    }

    /// <summary>Sets the consulting hours and waits for the save to land.</summary>
    private void SetHours(string morningFrom, string morningTo, string eveningFrom, string eveningTo)
    {
        app.Navigate("NavSettings", "Settings");
        app.Type("MorningFrom", morningFrom);
        app.Type("MorningTo", morningTo);
        app.Type("EveningFrom", eveningFrom);
        app.Type("EveningTo", eveningTo);
        app.Click("ShopSave");

        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("Saved"), "the hours to save");
    }

    /// <summary>
    /// Doctors sit mornings and evenings with the afternoon off, so "who is left
    /// this evening" is the question actually asked at the desk.
    /// </summary>
    [Fact]
    public void A_sitting_shows_only_the_visits_booked_in_its_hours()
    {
        SetHours("10:00", "13:00", "16:00", "20:00");

        // Booked at eleven, so it is a morning patient whatever time the test runs.
        var name = $"Morning {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9003002001", "5", at: "11:00");

        app.Navigate("NavOpd", "OPD");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the patient on the full day");

        // The counts in the heading are what is read from here on, not the
        // tiles. Switching the session rebuilds the whole list, and the rebuilt
        // buttons are not reliably in the automation tree afterwards — a quirk
        // of retemplating an ItemsControl, and nothing to do with sessions. The
        // heading is driven by the same two collections, so it says the same
        // thing without the flakiness.
        app.ComboBox("OpdSession").Select("Morning");
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains("Morning sitting, 10:00 to 13:00"),
                             "the heading to name the morning sitting and its hours");

        // Booked at eleven, so the morning keeps them and hides nobody.
        var morning = app.TextOf("PageSubtitle");
        Assert.StartsWith("1 waiting", morning);
        Assert.DoesNotContain("outside these hours", morning);

        app.ComboBox("OpdSession").Select("Evening");
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").Contains("Evening sitting, 16:00 to 20:00"),
                             "the heading to name the evening sitting");

        // The one thing that stops this being a way to lose a patient: the
        // evening holds them back and says so, rather than simply not showing
        // them.
        var evening = app.TextOf("PageSubtitle");
        Assert.StartsWith("0 waiting", evening);
        Assert.Contains("1 more today outside these hours", evening);

        app.ComboBox("OpdSession").Select("Full day");
        AppFixture.WaitUntil(() => app.TextOf("PageSubtitle").StartsWith("1 waiting"),
                             "the full day to show everyone");
        Assert.DoesNotContain("sitting", app.TextOf("PageSubtitle"));
    }

    [Fact]
    public void The_consulting_hours_survive_leaving_the_screen()
    {
        SetHours("09:30", "12:30", "17:00", "21:00");

        app.Navigate("NavOpd", "OPD");
        app.Navigate("NavSettings", "Settings");

        AppFixture.WaitUntil(() => app.TextBox("MorningFrom").Text == "09:30", "the hours to be read back");

        Assert.Equal("12:30", app.TextBox("MorningTo").Text);
        Assert.Equal("17:00", app.TextBox("EveningFrom").Text);
        Assert.Equal("21:00", app.TextBox("EveningTo").Text);

        // Put them back, so the other tests in this class see the usual hours.
        SetHours("10:00", "13:00", "16:00", "20:00");
    }
}
