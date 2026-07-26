using FlaUI.Core.Capturing;

namespace Pharma.UiTests;

[Collection("ui")]
public class ScreenshotCapture(AppFixture app)
{
    private static readonly string OutputDir =
        Path.Combine(Path.GetTempPath(), "twinkle-shots");

    [Fact]
    public void Capture_screens()
    {
        Directory.CreateDirectory(OutputDir);

        app.Navigate("NavOpd", "OPD");
        Thread.Sleep(400);
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
