namespace Pharma.Automation;

/// <summary>
/// Walks the Settings screen's six configuration tabs — General, Clinic,
/// Pharmacy, Doctors, Reports (document branding) and Features — then returns
/// to the Dashboard, the way a demo hands back to whatever runs next.
/// </summary>
public class SettingsWorkflow : IWorkflow
{
    private static readonly (string Header, string Anchor)[] Tabs =
    [
        ("General", "QueueLayout"),
        ("Clinic", "ClinicName"),
        ("Pharmacy", "PharmacyName"),
        ("Doctors", "DoctorsList"),
        ("Reports", "DocumentFooter"),
        ("Features", "DiagnosticsEnabled"),
    ];

    public string Name => "Settings";

    public void Execute(AppFixture app)
    {
        app.Navigate("NavSettings", "Settings");

        foreach (var (header, anchor) in Tabs)
        {
            app.SelectTab("SettingsTabs", header);
            AppFixture.WaitUntil(() => app.Find(anchor) is not null, $"the {header} settings");
        }

        app.Navigate("NavDashboard", "Dashboard");
    }
}
