using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// Books a walk-in, opens the consultation, prescribes a medicine the pharmacy
/// does not stock (so no product setup is needed first), saves and closes.
/// </summary>
public class PrescriptionWorkflow : IWorkflow
{
    public string Name => "Prescription";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Rx {suffix}";

        OpdVisitSupport.BookWalkIn(app, name, $"9{suffix}", "8");

        app.ClickTile("OpdWaitingList", "TileConsult", name);
        app.WaitForConsultation(name);
        app.SelectTab("ConsultationTabs", "Prescription");

        app.Type("RxMedicine", "Demo Prescribed Ointment");
        app.ComboBox("RxMorning").Select("1");
        app.ComboBox("RxNight").Select("1");
        app.Type("RxDays", "3");

        AppFixture.WaitUntil(() => app.TextBox("RxQuantity").Text == "6", "the course to be worked out");

        app.Click("RxAdd");
        AppFixture.WaitUntil(() => app.Grid("RxGrid").RowCount == 1, "the prescription line to be added");

        app.Click("ConsultationSave");
        Thread.Sleep(500);
        app.CloseConsultation();
    }
}
