using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// Produces the screenshots used by docs/USER_GUIDE.md, driving the real
/// application so the guide can never drift from what the app actually looks
/// like. Re-run this one test to refresh every image.
/// </summary>
public class ScreenshotCapture(AppFixture app) : IClassFixture<AppFixture>
{
    private static string OutputDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HMS_WPF.slnx")))
                dir = dir.Parent;

            var images = Path.Combine(dir?.FullName ?? Path.GetTempPath(), "docs", "images");
            Directory.CreateDirectory(images);
            return images;
        }
    }

    [Fact]
    public void Capture_screens_for_the_user_guide()
    {
        SetUpShop();

        // ── OPD ────────────────────────────────────────────────────────────
        OpdUiTests.BookWalkIn(app, "Baby Anika", "9008007001", "4");
        OpdUiTests.BookWalkIn(app, "Rohan Verma", "9008007001", "7");
        OpdUiTests.BookWalkIn(app, "Sana Iqbal", "9004003002", "2");

        app.ClickTile("OpdWaitingList", "TileFee", "Baby Anika");
        ClosePreview();

        app.ClickTile("OpdWaitingList", "TileDone", "Sana Iqbal");
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", "Sana Iqbal"), "a completed tile");

        app.Navigate("NavOpd", "OPD");
        Settle();
        Capture("opd-tiles");

        // The booking panel, open.
        app.Click("OpdNewVisit");
        app.Type("OpdPatientSearch", "9008007001");
        app.Click("OpdFind");
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 2, "the family list");
        Settle();
        Capture("opd-booking");
        app.Click("OpdCloseBooking");

        // ── Consultation ───────────────────────────────────────────────────
        app.ClickTile("OpdWaitingList", "TileConsult", "Baby Anika");

        var consultation = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Consultation", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        AppFixture.WaitUntil(
            () => (consultation!.FindFirstDescendant(cf => cf.ByAutomationId("ConsultationHeader"))
                                ?.AsLabel().Text ?? "").Contains("Baby Anika"),
            "the consultation to load");

        Settle();
        CaptureWindow(consultation!, "consultation");
        consultation!.Close();

        // ── Medicines ──────────────────────────────────────────────────────
        StockMedicine("Calpol Syrup 60ml", "PC2601", 60, 112m, 12m);
        StockMedicine("Amoxyclav Drops 15ml", "AM2604", 40, 96m, 12m);
        Settle();
        Capture("medicines");

        // ── Pharmacy counter ───────────────────────────────────────────────
        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", "Calpol");
        app.Click("SaleFind");
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();
        AppFixture.WaitUntil(() => app.ComboBox("SaleBatch").SelectedItems.Length == 1, "a batch");

        app.Type("SaleCustomerName", "Baby Anika");
        app.Type("SaleQuantity", "10");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");
        Settle();
        Capture("counter");

        app.Click("SaleSave");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("INV"), "the bill to save");

        // ── Reports, patients, settings ────────────────────────────────────
        app.Navigate("NavReports", "Reports");
        Settle();
        Capture("reports");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", "Baby Anika");
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient");
        app.Grid("PatientsGrid").Rows[0].Select();
        Settle();
        Capture("patients");

        // Print preview, from the patient's own record.
        app.Grid("PatientHistoryGrid").Rows[0].Select();
        app.Click("PatientPrintReceipt");

        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Settle();
        CaptureWindow(preview!, "print-preview");
        ClosePreview();

        app.Navigate("NavSettings", "Settings");
        Settle();
        Capture("settings");

        // ── The other queue layout ─────────────────────────────────────────
        app.ComboBox("QueueLayout").Select("Rows");
        app.Click("ShopSave");
        app.Navigate("NavOpd", "OPD");
        Settle();
        Capture("opd-rows");

        app.Navigate("NavSettings", "Settings");
        app.ComboBox("QueueLayout").Select("Tiles");
        app.Click("ShopSave");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetUpShop()
    {
        app.Navigate("NavSettings", "Settings");
        app.Type("ShopName", "Twinkle Children's Hospital");
        app.Type("ShopGstin", "36ABCDE1234F1Z5");
        app.Click("ShopSave");
        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("Saved"), "the shop details");
    }


    private void StockMedicine(string name, string batch, int quantity, decimal mrp, decimal gst)
    {
        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");
        app.Type("ProductName", name);
        app.Type("ProductGstRate", gst.ToString("0.##"));
        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), $"{name} to save");

        app.Type("StockBatchNo", batch);
        app.Type("StockQuantity", quantity.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.72m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("added to batch"), "stock");
    }

    private void ClosePreview()
    {
        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        preview?.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            "the preview to close");
    }

    /// <summary>Screenshots taken mid-transition come out ghosted.</summary>
    private static void Settle() => Thread.Sleep(1200);

    private void Capture(string name) => CaptureWindow(app.MainWindow, name);

    private static void CaptureWindow(AutomationElement element, string name)
    {
        using var image = FlaUI.Core.Capturing.Capture.Element(element);
        image.ToFile(Path.Combine(OutputDir, $"{name}.png"));
    }
}
