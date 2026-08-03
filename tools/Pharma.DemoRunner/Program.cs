using Pharma.Automation;

// Phase 2: one continuous HMS session running any number of selected
// workflows in order, instead of one hard-coded workflow per launch.
//
//     dotnet run --project tools/Pharma.DemoRunner
//     dotnet run --project tools/Pharma.DemoRunner -- --workflow PatientRegistration
//     dotnet run --project tools/Pharma.DemoRunner -- --workflow PatientRegistration PatientSearch Consultation
//
// See docs/architecture/Pharma.DemoRunner.md for what this is and is not.

var names = ParseWorkflowNames(args);

Console.WriteLine("Launching HMS...");

AppFixture app;
try
{
    app = new AppFixture();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"HMS did not come up: {ex.Message}");
    return 1;
}

Console.WriteLine("HMS Ready.");

var failures = new List<string>();

using (app)
{
    foreach (var name in names)
    {
        var workflow = WorkflowCatalog.Find(name);
        if (workflow is null)
        {
            Console.Error.WriteLine($"Unknown workflow '{name}' — skipping.");
            failures.Add(name);
            continue;
        }

        Console.WriteLine($"Starting {workflow.Name}...");
        try
        {
            workflow.Execute(app);
            Console.WriteLine($"Completed {workflow.Name}.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{workflow.Name} failed: {ex.Message}");
            failures.Add(workflow.Name);
        }
        finally
        {
            // Whatever the workflow left open, clear it before the next one —
            // the same isolation AppFixture already gives each regression class.
            TryStabilize(app);
        }
    }

    Console.WriteLine("Closing HMS...");
}

if (failures.Count == 0)
{
    Console.WriteLine("Demo completed successfully.");
    return 0;
}

Console.WriteLine($"Demo completed with {failures.Count} failure(s): {string.Join(", ", failures)}.");
return 1;

static void TryStabilize(AppFixture app)
{
    try
    {
        app.DismissModals();
        app.DismissOverlay();
    }
    catch (Exception)
    {
        // HMS itself may be gone; the outer loop's next workflow will fail
        // fast on its own if so.
    }
}

static IReadOnlyList<string> ParseWorkflowNames(string[] args)
{
    var index = Array.IndexOf(args, "--workflow");
    if (index < 0) return ["PatientRegistration"];

    var names = args.Skip(index + 1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
    return names.Length > 0 ? names : ["PatientRegistration"];
}
