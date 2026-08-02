using System.Runtime.InteropServices;

namespace Pharma.UiTests;

public class _VisualCheckDiagnosticsScroll(AppFixture app) : IClassFixture<AppFixture>
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [StaFact]
    public void Capture_diagnostics_bill_panel_at_common_sizes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hms_diag_scroll_check");
        Directory.CreateDirectory(dir);

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        if (app.CheckBox("DiagnosticsEnabled").IsChecked != true)
        {
            app.CheckBox("DiagnosticsEnabled").IsChecked = true;
            app.Click("FeaturesSave");
            AppFixture.WaitUntil(() => app.Find("NavDiagnostics") is not null, "the nav button");
        }

        app.Navigate("NavDiagnostics", "Diagnostics");

        var hwnd = app.MainWindow.Properties.NativeWindowHandle.Value;

        // 1360x768 — the smallest size this app is meant to run at.
        SetWindowPos(hwnd, IntPtr.Zero, 40, 40, 1360, 768, 0x0040);
        Thread.Sleep(400);
        using (var image = FlaUI.Core.Capturing.Capture.Element(app.MainWindow))
            image.ToFile(Path.Combine(dir, "1360x768.png"));

        // 1920x1080 maximized-ish — the common desktop case.
        SetWindowPos(hwnd, IntPtr.Zero, 20, 20, 1900, 1000, 0x0040);
        Thread.Sleep(400);
        using (var image = FlaUI.Core.Capturing.Capture.Element(app.MainWindow))
            image.ToFile(Path.Combine(dir, "1900x1000.png"));
    }
}
