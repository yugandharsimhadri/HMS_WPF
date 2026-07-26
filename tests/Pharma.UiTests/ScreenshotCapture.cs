using FlaUI.Core.Capturing;

namespace Pharma.UiTests;


public class ScreenshotCapture(AppFixture app) : IClassFixture<AppFixture>
{
    private static readonly string OutputDir =
        Path.Combine(Path.GetTempPath(), "twinkle-shots");

    [Fact]
    public void Capture_screens()
    {
        Directory.CreateDirectory(OutputDir);

        // Capture the queue with people in it — an empty screen shows nothing useful.
        OpdUiTests.BookWalkIn(app, "Baby Anika", "9008007001", "4");
        OpdUiTests.BookWalkIn(app, "Rohan Verma", "9008007001", "7");
        OpdUiTests.BookWalkIn(app, "Sana Iqbal", "9004003002", "2");

        app.ClickTile("OpdWaitingList", "TileDone", "Sana Iqbal");
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", "Sana Iqbal"), "a completed tile");

        app.Navigate("NavOpd", "OPD");
        Thread.Sleep(600);
        Capture("opd");

        app.Navigate("NavProducts", "Medicines");
        Thread.Sleep(400);
        Capture("medicines");

        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", "Para");
        app.Click("SaleFind");
        Thread.Sleep(600);
        Capture("counter");

        app.Navigate("NavSettings", "Settings");
        Thread.Sleep(400);
        Capture("settings");
    }

    private void Capture(string name)
    {
        using var image = FlaUI.Core.Capturing.Capture.Element(app.MainWindow);
        image.ToFile(Path.Combine(OutputDir, $"{name}.png"));
    }
}
