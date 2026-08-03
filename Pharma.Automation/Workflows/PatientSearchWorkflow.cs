namespace Pharma.Automation;

/// <summary>
/// Registers a fresh patient, then finds them again from the Patients screen —
/// composed from <see cref="PatientRegistrationWorkflow"/> rather than
/// repeating its steps.
/// </summary>
public class PatientSearchWorkflow : IWorkflow
{
    public string Name => "PatientSearch";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var name = PatientRegistrationWorkflow.Register(app, $"Demo Search {suffix}", $"9{suffix}");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");

        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");
    }
}
