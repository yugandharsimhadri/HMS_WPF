namespace Pharma.Automation;

/// <summary>
/// One business workflow, driven against an already-running application.
/// DemoRunner knows nothing beyond this: a name to select it by, and a single
/// action to run against a live AppFixture. What happens inside is entirely
/// the workflow's own business.
/// </summary>
public interface IWorkflow
{
    string Name { get; }
    void Execute(AppFixture app);
}
