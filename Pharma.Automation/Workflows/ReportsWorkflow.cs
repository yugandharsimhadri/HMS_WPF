namespace Pharma.Automation;

/// <summary>
/// Walks through a few report tabs — each one hosts its own grid, so simply
/// opening Reports would only ever show whichever tab happened to be selected
/// last time.
/// </summary>
public class ReportsWorkflow : IWorkflow
{
    private static readonly (string Header, string Grid)[] Tabs =
    [
        ("Day book", "ReportsDayBookGrid"),
        ("GST summary", "ReportsGstGrid"),
        ("Stock Register", "ReportsStockGrid"),
    ];

    public string Name => "Reports";

    public void Execute(AppFixture app)
    {
        app.Navigate("NavReports", "Reports");

        foreach (var (header, grid) in Tabs)
        {
            app.SelectTab("ReportsTabs", header);
            AppFixture.WaitUntil(() => app.Find(grid) is not null, $"the {header} report");
        }
    }
}
