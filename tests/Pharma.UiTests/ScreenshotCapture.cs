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

        // The fee form, before it is taken.
        app.OpenFeeForm("OpdWaitingList", "Baby Anika");
        Settle();
        Capture("collect-fee");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "collect-fee-annotated.png"),
            new Note("CollectFeeSummary", "Age, sex, booked time and the doctor. Check you have the right child."),
            new Note("CollectFeeAmount", "What is actually being taken. Change it for a concession or a follow-up."),
            new Note("CollectFeeMode", "Cash, UPI or card. Recorded against the receipt."),
            new Note("CollectFeePrint", "Off if you do not want paper. The receipt is still numbered and reprintable."),
            new Note("CollectFeeTake", "Asks once more, naming the amount and the child, then writes the receipt."));

        app.ConfirmFee();
        ClosePreview();

        app.ClickTile("OpdWaitingList", "TileDone", "Sana Iqbal");
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", "Sana Iqbal"), "a completed tile");

        app.Navigate("NavOpd", "OPD");
        Settle();
        Capture("opd-tiles");

        // The booking form, over the shell.
        app.Click("OpdNewVisit");
        AppFixture.WaitUntil(() => app.Find("OpdPatientSearch") is not null, "the booking form");
        app.Type("OpdPatientSearch", "9008007001");
        app.Click("OpdFind");
        AppFixture.WaitUntil(() => app.ListBox("OpdMatches").Items.Length >= 2, "the family list");
        Settle();
        Capture("opd-booking");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "opd-booking-annotated.png"),
            new Note("OpdPatientSearch", "Name or phone. A whole family shares one number, so search by it."),
            new Note("OpdFind", "Searches. If nobody matches, the new-patient form opens instead."),
            new Note("OpdMatches", "Everyone on that number. Pick the child who is actually here — this is where siblings get mixed up."),
            new Note("OpdBook", "Books the visit and allocates the next token for the day."),
            new Note("OpdClearBooking", "Empties the form. Nothing is booked until Book visit is pressed."));

        app.Click("OpdCloseBooking");

        // ── Consultation ───────────────────────────────────────────────────
        app.ClickTile("OpdWaitingList", "TileConsult", "Baby Anika");
        app.WaitForConsultation("Baby Anika");

        Settle();
        Capture("consultation");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "consultation-annotated.png"),
            new Note("ConsultationHeader", "Token, patient, age and doctor. Check you have the right child."),
            new Note("RxComplaint", "Carried over from booking. Edit it freely."),
            new Note("RxDiagnosis", "Printed in bold on the prescription."),
            new Note("RxMedicine", "Type two letters. Pick from the list to link it to your stock, or keep typing for one you do not carry."),
            new Note("RxMedicineHint", "Says whether it is in your pharmacy, out of stock, or not carried at all."),
            new Note("RxMorning", "Morning dose. 0, ¼, ½, 1 or 2 — picked, not typed."),
            new Note("RxDays", "Length of the course."),
            new Note("RxQuantity", "Worked out for you, in individual tablets. Change it if you want."),
            new Note("RxAdd", "Adds the medicine to the prescription below."),
            new Note("ConsultationClose", "Leaves. Asks first if anything is unsaved. Esc does the same."));

        app.CloseConsultation();

        // ── Medicines ──────────────────────────────────────────────────────
        StockMedicine("Calpol Syrup 60ml", "PC2601", 60, 112m, 12m);
        StockMedicine("Amoxyclav Drops 15ml", "AM2604", 40, 96m, 12m);

        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsSearchButton");
        Settle();
        Capture("medicines");

        // The editor, over the shell. Three columns, so the whole medicine is on
        // one screen at the resolutions this runs on.
        app.SelectRowByText("ProductsGrid", "Calpol");
        app.Click("ProductsEdit");
        AppFixture.WaitUntil(() => app.Find("ProductName") is not null, "the medicine editor");
        Settle();
        Capture("medicine-editor");

        app.Click("MedicineEditorCancel");
        AppFixture.WaitUntil(() => app.Find("ProductName") is null, "the editor to close");

        // ── Inventory ──────────────────────────────────────────────────────
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", "Calpol");
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount >= 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        Settle();
        Capture("inventory");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "inventory-annotated.png"),
            new Note("InventorySearch", "Find the medicine you are receiving. Brand, drug, maker or rack."),
            new Note("InventoryProductsGrid", "Click it. The heading then names it and says how much is on hand."),
            new Note("InventoryBatches", "What is on the shelf for it right now, batch by batch."),
            new Note("InventoryReceive", "Opens the delivery form for that medicine. Double-clicking the row does the same."),
            new Note("InventoryCorrect", "For when the shelf and the system disagree. It keeps a record of why."));

        // ── Receiving stock ────────────────────────────────────────────────
        // A form of its own now, over the shell, so nothing from the last
        // delivery is left in it.
        app.Click("InventoryReceive");
        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is not null, "the receiving form");
        Settle();
        Capture("receive-stock");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "receive-stock-annotated.png"),
            new Note("StockBatchNo", "Printed on the pack. Required — it has to appear on the bill by law."),
            new Note("StockExpiry", "The pack is good until the END of that month."),
            new Note("StockQuantity", "How many PACKS arrived — strips, boxes or bottles. Not tablets."),
            new Note("StockFreeQuantity", "Scheme quantity: the +1 in 10+1. Goes on the shelf, costs nothing."),
            new Note("StockMrp", "The price printed on the pack. The counter prices everything from this."),
            new Note("StockAdd", "Adds to the shelf. It never replaces what is already there."));

        app.Click("ReceiveStockCancel");
        AppFixture.WaitUntil(() => app.Find("StockBatchNo") is null, "the receiving form to close");

        // ── Correcting a count ─────────────────────────────────────────────
        app.Click("InventoryCorrect");
        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is not null, "the correction form");
        Settle();
        Capture("correct-stock");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "correct-stock-annotated.png"),
            new Note("CorrectBatch", "Which batch is wrong. A count is only ever wrong for one of them."),
            new Note("CorrectQuantity", "What is ACTUALLY on the shelf, in tablets. Not the difference."),
            new Note("CorrectReason", "Why they disagree. This is kept, and it is what an inspection asks for."),
            new Note("CorrectStock", "Writes the correction and a record of it. Nothing here is silent."));

        app.Click("CorrectStockCancel");
        AppFixture.WaitUntil(() => app.Find("CorrectQuantity") is null, "the correction form to close");

        // ── Importing a supplier bill ──────────────────────────────────────
        app.Click("InventoryImport");

        var import = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Import", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        // Profile B is the dash-date, month-name-expiry supplier.
        import!.FindFirstDescendant(cf => cf.ByAutomationId("ImportProfile"))!
               .AsComboBox().Select(1);

        import.FindFirstDescendant(cf => cf.ByAutomationId("ImportFilePath"))!
              .AsTextBox().Text = FixtureFile("Profile_B.csv");

        import.FindFirstDescendant(cf => cf.ByAutomationId("ImportSupplier"))!
              .AsTextBox().Text = "SW Distributors";

        import.FindFirstDescendant(cf => cf.ByAutomationId("ImportPreview"))!.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => (import.FindFirstDescendant(cf => cf.ByAutomationId("ImportSummary"))
                         ?.AsLabel().Text ?? "").Contains("line"),
            "the bill to be read");

        Settle();
        CaptureWindow(import, "import");
        import.FindFirstDescendant(cf => cf.ByAutomationId("ImportClose"))!.AsButton().Invoke();

        // ── Pharmacy counter ───────────────────────────────────────────────
        app.Navigate("NavSale", "Pharmacy counter");
        app.Type("SaleSearch", "Calpol");
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, "the medicine");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Type("SaleCustomerName", "Baby Anika");
        app.Type("SaleQuantity", "10");
        app.Click("SaleAddLine");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the bill line");
        Settle();
        Capture("counter");

        // The same screen with every control ringed and explained, so the guide
        // does not have to describe where things are in prose.
        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "counter-annotated.png"),
            new Note("SaleSearch",
                     "Type any part of the brand, drug, maker or rack. It filters as you type — no button, any case."),
            new Note("SaleMatches",
                     "What is on the shelf. Price per unit and how many left. Click the one you want."),
            new Note("SaleQuantity", "How many. Nine tablets is 9 — never the number of strips."),
            new Note("SaleQuantityUnit",
                     "What that number counts. Tablets, or strips of 10. Remembered per medicine."),
            new Note("SaleQuickStock",
                     "The medicine is in the shop but the screen says none? Put it on the shelf without leaving the bill."),
            new Note("SaleLinesGrid",
                     "The bill. Only QTY can be changed; the price comes from the batch and cannot be edited."),
            new Note("SaleNetTotal", "What the customer pays. GST is already inside the MRP."),
            new Note("SaveAndPrint", "Saves the bill, deducts the stock, and opens the print preview."));

        // ── Adding stock from the counter ──────────────────────────────────
        // Adding the line cleared the search along with the selection, so pick the
        // medicine again — which is what the operator does too, since quick stock
        // is for whichever medicine they have just failed to find in stock.
        app.Type("SaleSearch", "Calpol");
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, "the medicine again");
        app.ListBox("SaleMatches").Items[0].Select();

        app.Click("SaleQuickStock");

        var quickStock = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.Contains("Add stock", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        quickStock!.FindFirstDescendant(cf => cf.ByAutomationId("QuickStockPacks"))!.AsTextBox().Text = "5";
        Settle();
        CaptureWindow(quickStock, "quick-stock");
        quickStock.FindFirstDescendant(cf => cf.ByAutomationId("QuickStockCancel"))!.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.Contains("Add stock", StringComparison.OrdinalIgnoreCase)),
            "the dialog to close");

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

        // ── The dark theme ─────────────────────────────────────────────────
        app.ComboBox("AppThemeChoice").Select("Dark");
        Settle();
        Capture("settings-dark");

        app.Navigate("NavSale", "Pharmacy counter");
        Settle();
        Capture("counter-dark");

        // Reports too: the tabs across the top of it are the one place the
        // operating system's own chrome used to show through the palette.
        app.Navigate("NavReports", "Reports");
        Settle();
        Capture("reports-dark");

        app.Navigate("NavSettings", "Settings");
        app.ComboBox("AppThemeChoice").Select("Light");
        Settle();

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

        // The sample clinic is GST registered, so the guide shows a tax invoice.
        app.CheckBox("ShopGstRegistered").IsChecked = true;
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

        // Both guide samples are liquids, so the screenshots read sensibly.
        app.ComboBox("ProductDispensingUnit").Select("Bottle");

        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), $"{name} to save");

        // Stock lives on its own screen now.
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, "the medicine in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", batch);
        app.Type("StockQuantity", quantity.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.72m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");
        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"), "stock");
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

    /// <summary>The supplier files kept as test fixtures double as guide samples.</summary>
    private static string FixtureFile(string name)
        => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                        "Pharma.Tests", "bin", "Debug", "net10.0", "Fixtures", name);

    /// <summary>Screenshots taken mid-transition come out ghosted.</summary>
    private static void Settle() => Thread.Sleep(1200);

    private void Capture(string name) => CaptureWindow(app.MainWindow, name);

    private static void CaptureWindow(AutomationElement element, string name)
    {
        using var image = FlaUI.Core.Capturing.Capture.Element(element);
        image.ToFile(Path.Combine(OutputDir, $"{name}.png"));
    }
}
