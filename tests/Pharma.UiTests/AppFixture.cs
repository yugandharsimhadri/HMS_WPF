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
    public string LogDirectory { get; private set; } = "";

    public AppFixture()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"twinkle-ui-{Guid.NewGuid():N}.db");

        // An aborted run can leave the app alive, which then locks the build output.
        KillStrays();

        var startInfo = new ProcessStartInfo(FindExecutable()) { UseShellExecute = false };
        startInfo.Environment[DbBootstrapper.PathOverrideVariable] = DatabasePath;

        // Keep test runs out of the clinic's real log folder.
        LogDirectory = Path.Combine(Path.GetTempPath(), $"twinkle-ui-logs-{Guid.NewGuid():N}");
        startInfo.Environment[AppLog.DirectoryOverrideVariable] = LogDirectory;

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

    /// <summary>
    /// Where the application under test was built to. Tests that check what the
    /// build stamped on the assembly need to read the assembly itself, not a
    /// number typed into the test.
    /// </summary>
    public static string ApplicationDirectory => Path.GetDirectoryName(FindExecutable())!;

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
        // Nothing can be clicked behind a modal, so clear any the previous test
        // left behind before trying.
        DismissModals();
        DismissOverlay();

        Button(navAutomationId).Invoke();
        WaitUntil(() => TextOf("PageTitle") == expectedTitle, $"page '{expectedTitle}'");
    }

    /// <summary>
    /// Closes anything sitting over the shell — the consultation, the medicine
    /// editor — the way Esc does.
    ///
    /// An overlay disables the navigation behind it, so a test that leaves one
    /// open does not fail itself; it fails whichever test runs next, somewhere
    /// else entirely. That is an afternoon nobody gets back, so it is cleared
    /// here rather than trusted to every test remembering.
    /// </summary>
    public void DismissOverlay()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (Find("Overlay") is null) return;

            MainWindow.Focus();
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESC);

            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Selects the grid row containing the given text. Tests share one app and one
    /// database, so addressing a row by position picks up whatever another test
    /// happened to add.
    /// </summary>
    public void SelectRowByText(string gridAutomationId, string text)
    {
        AppFixture.WaitUntil(() => FindRow(gridAutomationId, text) is not null, $"a row containing '{text}'");

        var row = FindRow(gridAutomationId, text)
                  ?? throw new InvalidOperationException($"No row in {gridAutomationId} contains '{text}'.");

        row.Select();
    }

    /// <summary>
    /// A cell of a row, addressed by its column heading rather than its position.
    ///
    /// Reading cells by index broke every time a column was added or taken away
    /// — which is a change to how a screen looks, not to what it means, and
    /// should not be able to fail a test about stock counts. The failure was
    /// also silent-ish: the index still existed, so the assertion compared a
    /// stock figure against a GST rate.
    /// </summary>
    public string CellOf(string gridAutomationId, string header, int row = 0)
    {
        var grid = Grid(gridAutomationId);
        var columns = grid.Header.Columns.Select(c => c.Text ?? "").ToArray();
        var index = Array.FindIndex(columns, c => string.Equals(c, header, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            throw new InvalidOperationException(
                $"'{gridAutomationId}' has no column '{header}'. It has: {string.Join(", ", columns)}.");

        return TextOfCell(grid.Rows[row].Cells[index]);
    }

    /// <summary>
    /// What a cell is actually showing.
    ///
    /// A text column reports its own value. A template column has none, and WPF
    /// hands back its internal placeholder instead — "Item: Pharma.Core.Product,
    /// Column Display Index: 7" — so fall through to reading the text it is
    /// displaying. Both ways of finding a row need this, and only one of them
    /// used to have it.
    /// </summary>
    private static string TextOfCell(FlaUI.Core.AutomationElements.GridCell cell)
    {
        var value = cell.Value ?? "";
        if (value.Length > 0 && !value.StartsWith("Item:", StringComparison.Ordinal)) return value;

        return cell.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                   .Select(t => t.Name ?? "")
                   .FirstOrDefault(t => t.Length > 0) ?? "";
    }

    private FlaUI.Core.AutomationElements.GridRow? FindRow(string gridAutomationId, string text)
        => Grid(gridAutomationId).Rows
            .FirstOrDefault(r => r.Cells.Any(c => TextOfCell(c).Contains(text, StringComparison.OrdinalIgnoreCase)));

    /// <summary>How many tiles are in a queue column.</summary>
    public int TileCount(string listAutomationId)
        => Require(listAutomationId).FindAllDescendants(cf => cf.ByAutomationId("TileConsult")).Length;

    /// <summary>
    /// Finds a tile action by what it does and who it belongs to. Tiles have no
    /// index a test can rely on — the queue reorders as people are seen — so every
    /// tile button carries the patient's name as its automation name.
    /// </summary>
    public AutomationElement? TileAction(string listAutomationId, string action, string patientName)
        => Find(listAutomationId)?
            .FindAllDescendants(cf => cf.ByAutomationId(action))
            .FirstOrDefault(e => string.Equals(e.Name, patientName, StringComparison.Ordinal));

    public bool HasTile(string listAutomationId, string patientName)
        => TileAction(listAutomationId, listAutomationId == "OpdWaitingList" ? "TileConsult" : "TileRx",
                      patientName) is not null;

    public void ClickTile(string listAutomationId, string action, string patientName)
    {
        WaitUntil(() => TileAction(listAutomationId, action, patientName) is not null,
                  $"the {action} button on {patientName}'s tile");

        TileAction(listAutomationId, action, patientName)!.AsButton().Invoke();
    }

    /// <summary>
    /// Takes the consultation fee at the amount already on the form.
    ///
    /// Pressing Fee no longer takes the money — it opens a form over the shell
    /// showing the amount and the payment mode, and the press after that is a
    /// plain yes or no. Three test classes need the whole sequence, so it lives
    /// here rather than being spelled out in each of them.
    ///
    /// Leaves the receipt preview open; the caller closes it.
    /// </summary>
    public void TakeFee(string listAutomationId, string patientName)
    {
        OpenFeeForm(listAutomationId, patientName);
        ConfirmFee();
    }

    /// <summary>Presses Fee on a tile and waits for the form to open.</summary>
    public void OpenFeeForm(string listAutomationId, string patientName)
    {
        ClickTile(listAutomationId, "TileFee", patientName);
        WaitUntil(() => Find("CollectFeeTake") is not null, $"the fee form for {patientName}");
    }

    /// <summary>Presses Take fee and says yes to the confirmation behind it.</summary>
    public void ConfirmFee()
    {
        Click("CollectFeeTake");

        WaitUntil(() => MainWindow.ModalWindows.Length == 1, "the fee confirmation");
        MainWindow.ModalWindows[0].FindFirstDescendant(cf => cf.ByName("Yes"))?.AsButton().Invoke();

        WaitUntil(() => Find("CollectFeeTake") is null, "the fee form to close");
    }

    public void Type(string automationId, string value)
    {
        var box = TextBox(automationId);
        box.Focus();
        box.Text = value;
    }

    public void Click(string automationId) => Button(automationId).Invoke();

    // ── The consultation layer ─────────────────────────────────────────────
    // It is part of the main window rather than a window of its own, so that it
    // cannot be lost behind another application.

    public bool IsConsultationOpen => Find("ConsultationHeader") is not null;

    public void WaitForConsultation(string patient)
        => WaitUntil(() => TextOf("ConsultationHeader").Contains(patient),
                     $"the consultation for {patient}");

    /// <summary>Closes it, answering the unsaved-changes question if it appears.</summary>
    public void CloseConsultation()
    {
        if (!IsConsultationOpen) return;

        Click("ConsultationClose");
        ConfirmDiscard();

        WaitUntil(() => !IsConsultationOpen, "the consultation to close");
    }

    /// <summary>Says yes to "close and lose them?" if the app asks.</summary>
    private void ConfirmDiscard()
    {
        Thread.Sleep(250);

        var dialog = MainWindow.ModalWindows.FirstOrDefault();
        var yes = dialog?.FindFirstDescendant(cf => cf.ByName("Yes"))?.AsButton();

        yes?.Invoke();
    }

    /// <summary>
    /// Closes anything modal left open. The suite shares one app, so a test that
    /// fails while a preview or a message box is up would otherwise poison every
    /// test after it.
    /// </summary>
    public void DismissModals()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var modal = MainWindow.ModalWindows.FirstOrDefault();
            if (modal is null) return;

            try
            {
                var close = modal.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton();

                if (close is not null) close.Invoke();
                else modal.AsWindow().Close();
            }
            catch (Exception)
            {
                // Already gone, or closing raced with the app.
            }

            Thread.Sleep(250);
        }
    }

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

        try { Directory.Delete(LogDirectory, recursive: true); } catch (Exception) { }
    }
}

/// <summary>
/// Each test class gets its own application and its own database.
///
/// A single instance shared by every class was cheaper to launch but made the
/// suite unrepeatable: one test failing with a modal open left it on screen for
/// the next class, and data created by one class changed what another class saw.
/// Classes still run one at a time — UI Automation drives one desktop.
/// </summary>
public abstract class UiTestBase(AppFixture app) : IClassFixture<AppFixture>
{
    protected readonly AppFixture App = app;
}
