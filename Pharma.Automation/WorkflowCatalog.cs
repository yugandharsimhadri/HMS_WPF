namespace Pharma.Automation;

/// <summary>
/// Every workflow a demo session can select, by name. Consumers (DemoRunner)
/// resolve a workflow by string and call IWorkflow.Execute — nothing here
/// exposes what a workflow actually does.
/// </summary>
public static class WorkflowCatalog
{
    public static readonly IReadOnlyList<IWorkflow> All =
    [
        new PatientRegistrationWorkflow(),
        new PatientSearchWorkflow(),
        new ConsultationWorkflow(),
        new PrescriptionWorkflow(),
        new BillingWorkflow(),
        new MedicineSaleWorkflow(),
        new PurchaseWorkflow(),
        new InventoryWorkflow(),
        new ReportsWorkflow(),
    ];

    public static IWorkflow? Find(string name)
        => All.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
}
