using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// The consultation is reached from a tile and shown over the shell, so it gets
/// its own coverage — it is exactly where an unhandled failure would have gone
/// unnoticed.
/// </summary>
public class ConsultationUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    [Fact]
    public void Opening_a_consultation_from_a_tile_does_not_crash()
    {
        var name = $"UI Consult {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9876500055", "7");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");
        app.ClickTile("OpdWaitingList", "TileConsult", name);

        // The header is filled by an async load after the layer appears.
        app.WaitForConsultation(name);
        Assert.Contains(name, app.TextOf("ConsultationHeader"));

        app.CloseConsultation();

        // The app must still be alive and responsive afterwards.
        app.Navigate("NavReports", "Reports");
        Assert.Equal("Reports", app.TextOf("PageTitle"));
    }

    [Fact]
    public void Completing_a_consultation_moves_the_tile_to_completed()
    {
        var name = $"UI Complete {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9876500066", "5");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");
        app.ClickTile("OpdWaitingList", "TileConsult", name);

        app.WaitForConsultation(name);

        // Save and complete closes the layer and completes the visit.
        app.MainWindow.FindFirstDescendant(cf => cf.ByName("Save & complete"))?.AsButton().Invoke();

        AppFixture.WaitUntil(() => !app.IsConsultationOpen, "the consultation to close");
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", name), "the tile to move to completed");

        Assert.True(app.HasTile("OpdCompletedList", name));
        Assert.False(app.HasTile("OpdWaitingList", name));
    }

    [Fact]
    public void Leaving_with_unsaved_notes_asks_before_discarding_them()
    {
        var name = $"UI Unsaved {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9876500088", "9");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");
        app.ClickTile("OpdWaitingList", "TileConsult", name);
        app.WaitForConsultation(name);

        app.SelectTab("ConsultationTabs", "Diagnosis");
        app.Type("RxDiagnosis", "Viral fever, typed but not saved");

        app.Click("ConsultationClose");

        // A half-entered prescription is not recoverable, so it is worth a question.
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 1, "the unsaved-changes question");

        var question = app.MainWindow.ModalWindows[0];
        question.FindFirstDescendant(cf => cf.ByName("Yes"))?.AsButton().Invoke();

        AppFixture.WaitUntil(() => !app.IsConsultationOpen, "the consultation to close");
    }
}
