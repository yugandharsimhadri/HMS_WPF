using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// Books a walk-in, opens the consultation, records a diagnosis, saves and
/// closes — leaving the shell exactly as the next workflow will find it.
/// </summary>
public class ConsultationWorkflow : IWorkflow
{
    public string Name => "Consultation";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Consult {suffix}";

        OpdVisitSupport.BookWalkIn(app, name, $"9{suffix}", "6");

        app.ClickTile("OpdWaitingList", "TileConsult", name);
        app.WaitForConsultation(name);

        app.SelectTab("ConsultationTabs", "Diagnosis");
        app.Type("RxDiagnosis", "Acute pharyngitis");

        app.Click("ConsultationSave");
        Thread.Sleep(500); // lets the save round-trip before the form is closed
        app.CloseConsultation();
    }
}
