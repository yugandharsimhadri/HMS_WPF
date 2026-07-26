using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The consultation window is the one screen reached from another window, so it
/// gets its own coverage — it is exactly where an unhandled failure would have
/// gone unnoticed.
/// </summary>
[Collection("ui")]
public class ConsultationUiTests(AppFixture app)
{
    [Fact]
    public void Opening_a_consultation_from_the_queue_does_not_crash()
    {
        app.Navigate("NavOpd", "OPD");

        var name = $"UI Consult {DateTime.Now:HHmmssfff}";
        app.Type("OpdPatientSearch", name);
        app.Click("OpdFind");
        AppFixture.WaitUntil(() => app.Find("OpdNewName") is not null, "the new-patient form");

        app.Type("OpdNewName", name);
        app.Type("OpdNewPhone", "9876500055");
        app.Type("OpdNewAge", "7");
        app.Click("OpdBook");
        AppFixture.WaitUntil(
            () => app.TextOf("OpdStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
            "the booking confirmation");

        // Select the booked visit and open its consultation.
        var queue = app.Grid("OpdQueueGrid");
        queue.Rows[^1].Select();
        app.Click("OpdConsult");

        var consultation = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(consultation);
        Assert.Contains(name, consultation!.FindFirstDescendant(
            cf => cf.ByAutomationId("ConsultationHeader"))?.AsLabel().Text ?? "");

        // The app must still be alive and responsive afterwards.
        consultation.Close();
        AppFixture.WaitUntil(() => app.MainWindow.ModalWindows.Length == 0, "the window to close");

        app.Navigate("NavReports", "Reports");
        Assert.Equal("Reports", app.TextOf("PageTitle"));
    }
}
