using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The consultation window is the one screen reached from another window, so it
/// gets its own coverage — it is exactly where an unhandled failure would have
/// gone unnoticed.
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

        // Match by title rather than taking whatever modal is first.
        var consultation = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(consultation);

        // The header is filled by an async load after the window appears.
        AppFixture.WaitUntil(
            () => (consultation!.FindFirstDescendant(cf => cf.ByAutomationId("ConsultationHeader"))
                                ?.AsLabel().Text ?? "").Contains(name),
            "the consultation header to load");

        Assert.Contains(name, consultation!.FindFirstDescendant(
            cf => cf.ByAutomationId("ConsultationHeader"))?.AsLabel().Text ?? "");

        consultation.Close();
        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            "the consultation window to close");

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

        var consultation = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(consultation);

        AppFixture.WaitUntil(
            () => (consultation!.FindFirstDescendant(cf => cf.ByAutomationId("ConsultationHeader"))
                                ?.AsLabel().Text ?? "").Contains(name),
            "the consultation header to load");

        // Save and complete closes the window and completes the visit.
        consultation!.FindFirstDescendant(cf => cf.ByName("Save & complete"))?.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            "the consultation window to close");

        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", name), "the tile to move to completed");

        Assert.True(app.HasTile("OpdCompletedList", name));
        Assert.False(app.HasTile("OpdWaitingList", name));
    }
}
