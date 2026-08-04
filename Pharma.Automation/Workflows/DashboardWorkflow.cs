namespace Pharma.Automation;

/// <summary>
/// Opens the Dashboard and holds there briefly — the opening shot of a demo
/// recording, not a workflow that changes any data.
/// </summary>
public class DashboardWorkflow : IWorkflow
{
    public string Name => "Dashboard";

    public void Execute(AppFixture app)
    {
        app.Navigate("NavDashboard", "Dashboard");

        // Gives a recording a few seconds to hold on the opening screen
        // before the next workflow starts navigating away from it.
        Thread.Sleep(3000);
    }
}
