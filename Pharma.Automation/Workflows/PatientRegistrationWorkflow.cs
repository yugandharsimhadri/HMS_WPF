namespace Pharma.Automation;

/// <summary>
/// Registers a new patient from the Patients screen. Extracted from what was
/// DiagnosticsUiTests' private NewPatient helper: same screen, same fields,
/// same waits, so a run through here is indistinguishable from what a test
/// already exercised.
///
/// The static <see cref="Register"/> method is also called directly by
/// DiagnosticsUiTests (Pharma.UiTests) as test setup — its signature is load
/// bearing for that regression path and must not change.
/// </summary>
public class PatientRegistrationWorkflow : IWorkflow
{
    public string Name => "PatientRegistration";

    public void Execute(AppFixture app)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        Register(app, $"Demo Patient {suffix}", $"9{suffix}");
    }

    public static string Register(AppFixture app, string name, string phone, string age = "30")
    {
        app.Navigate("NavPatients", "Patients");
        app.Click("PatientsNew");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");

        app.Type("PatientName", name);
        app.Type("PatientPhone", phone);
        app.Type("PatientAge", age);
        app.Click("PatientSave");

        AppFixture.WaitUntil(() => app.TextOf("PatientsStatus").Contains("saved"), $"{name} to save");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the form to close");

        return name;
    }
}
