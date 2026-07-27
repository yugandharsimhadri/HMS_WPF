namespace Pharma.UiTests;

/// <summary>
/// The queue layout is the user's choice, so both settings have to be equally
/// usable — the actions must work whichever one is picked.
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
}
