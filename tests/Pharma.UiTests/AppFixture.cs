using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Pharma.Data;
using Application = FlaUI.Core.Application;

namespace Pharma.UiTests;

/// <summary>
/// Launches the real application window once for the whole UI suite, pointed at a
/// throwaway database so the tests never touch the live one in ProgramData.
/// </summary>
public class AppFixture : IDisposable
{
    private readonly Application _app;
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }
    public string DatabasePath { get; }

    public AppFixture()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"twinkle-ui-{Guid.NewGuid():N}.db");

        // An aborted run can leave the app alive, which then locks the build output.
        KillStrays();

        var startInfo = new ProcessStartInfo(FindExecutable()) { UseShellExecute = false };
        startInfo.Environment[DbBootstrapper.PathOverrideVariable] = DatabasePath;

        _app = Application.Launch(startInfo);
        Automation = new UIA3Automation();

        MainWindow = _app.GetMainWindow(Automation, TimeSpan.FromSeconds(30))
                     ?? throw new InvalidOperationException("The main window did not appear.");

        // The first page loads asynchronously after the window is shown.
        WaitUntil(() => Label("PageTitle")?.Text is { Length: > 0 }, "first page to load");

        // Recorded once at launch: tests share this app instance and navigate
        // away from the landing page, so it cannot be re-read later.
        InitialPageTitle = TextOf("PageTitle");
    }

    /// <summary>The page the app was showing when it first opened.</summary>
    public string InitialPageTitle { get; }

    private static void KillStrays()
    {
        foreach (var stray in Process.GetProcessesByName("TwinkleHMS"))
        {
            try
            {
                stray.Kill();
                stray.WaitForExit(3000);
            }
            catch (Exception) { /* already gone, or not ours to kill */ }
        }
    }

    private static string FindExecutable()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HMS_WPF.slnx")))
            dir = dir.Parent;

        if (dir is null) throw new InvalidOperationException("Could not locate the solution root.");

        // Match the configuration the tests themselves were built in.
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";

        var exe = Path.Combine(dir.FullName, "src", "Pharma.App", "bin", configuration, "net10.0-windows", "TwinkleHMS.exe");

        if (!File.Exists(exe))
            throw new FileNotFoundException($"TwinkleHMS.exe not found. Build the solution first.\nLooked in: {exe}");

        return exe;
    }

    // ── Element lookup ─────────────────────────────────────────────────────

    public AutomationElement? Find(string automationId)
        => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    public AutomationElement Require(string automationId)
        => Retry.WhileNull(() => Find(automationId), TimeSpan.FromSeconds(10)).Result
           ?? throw new InvalidOperationException($"No element with AutomationId '{automationId}'.");

    public Button Button(string automationId) => Require(automationId).AsButton();
    public TextBox TextBox(string automationId) => Require(automationId).AsTextBox();
    public ComboBox ComboBox(string automationId) => Require(automationId).AsComboBox();
    public Grid Grid(string automationId) => Require(automationId).AsGrid();
    public ListBox ListBox(string automationId) => Require(automationId).AsListBox();
    public CheckBox CheckBox(string automationId) => Require(automationId).AsCheckBox();

    /// <summary>Selects a TabItem by its header text within the named TabControl.</summary>
    public void SelectTab(string tabControlAutomationId, string header)
    {
        var tab = Require(tabControlAutomationId)
                      .FindAllChildren(cf => cf.ByControlType(ControlType.TabItem))
                      .FirstOrDefault(t => t.Name == header)
                  ?? throw new InvalidOperationException($"Tab '{header}' not found in '{tabControlAutomationId}'.");

        tab.AsTabItem().Select();
    }

    public Label? Label(string automationId)
    {
        var element = Find(automationId);
        return element?.ControlType == ControlType.Text ? element.AsLabel() : null;
    }

    public string TextOf(string automationId) => Label(automationId)?.Text ?? "";

    // ── Actions ────────────────────────────────────────────────────────────

    /// <summary>Clicks a left-hand nav button and waits for the page header to change.</summary>
    public void Navigate(string navAutomationId, string expectedTitle)
    {
        Button(navAutomationId).Invoke();
        WaitUntil(() => TextOf("PageTitle") == expectedTitle, $"page '{expectedTitle}'");
    }

    public void Type(string automationId, string value)
    {
        var box = TextBox(automationId);
        box.Focus();
        box.Text = value;
    }

    public void Click(string automationId) => Button(automationId).Invoke();

    public static void WaitUntil(Func<bool> condition, string what, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition()) return;
            }
            catch (Exception)
            {
                // The tree is mid-update; try again.
            }

            Thread.Sleep(120);
        }

        throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for {what}.");
    }

    public void Dispose()
    {
        try
        {
            _app.Close();
            _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2));
        }
        catch (Exception) { /* the app may already be gone */ }

        try { _app.Kill(); } catch (Exception) { }

        Automation.Dispose();
        _app.Dispose();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(DatabasePath + suffix); } catch (IOException) { }
        }
    }
}

/// <summary>
/// UI Automation drives one desktop, so every UI test shares a single app
/// instance and runs sequentially.
/// </summary>
[CollectionDefinition("ui")]
public class UiCollection : ICollectionFixture<AppFixture>;
