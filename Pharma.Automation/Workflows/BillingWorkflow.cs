using Pharma.Automation.Support;

namespace Pharma.Automation;

/// <summary>
/// Books a walk-in and takes the consultation fee — reusing AppFixture.TakeFee,
/// which already opens the fee form and confirms it. The receipt preview it
/// leaves open is cleared generically by DismissModals rather than a workflow
/// of its own knowing the preview's automation id.
/// </summary>
public class BillingWorkflow : IWorkflow
{
    public string Name => "Billing";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = $"Demo Billing {suffix}";

        OpdVisitSupport.BookWalkIn(app, name, $"9{suffix}", "10");

        app.TakeFee("OpdWaitingList", name);
        app.DismissModals();
    }
}
