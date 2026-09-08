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
        // A full morning, not three names: these screenshots go in front of
        // a prospect, and a queue with three tiles reads as an empty product
        // however good the software behind it is.
        OpdUiTests.BookWalkIn(app, "Baby Anika", "9008007001", "4");
        OpdUiTests.BookWalkIn(app, "Rohan Verma", "9008007001", "7");
        OpdUiTests.BookWalkIn(app, "Sana Iqbal", "9004003002", "2");
        OpdUiTests.BookWalkIn(app, "Aarav Menon", "9004003011", "5");
        OpdUiTests.BookWalkIn(app, "Diya Nair", "9004003022", "3");
        OpdUiTests.BookWalkIn(app, "Kabir Shah", "9004003033", "9");
        OpdUiTests.BookWalkIn(app, "Meher Fatima", "9004003044", "6");
        OpdUiTests.BookWalkIn(app, "Vihaan Rao", "9004003055", "1");
        OpdUiTests.BookWalkIn(app, "Ishita Bose", "9004003066", "8");
        OpdUiTests.BookWalkIn(app, "Arjun Pillai", "9004003077", "11");

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

        // Fees taken across several patients, so the dashboard and the day
        // book have real money in them rather than a single receipt.
        foreach (var who in new[] { "Rohan Verma", "Aarav Menon", "Kabir Shah", "Ishita Bose" })
        {
            app.OpenFeeForm("OpdWaitingList", who);
            AppFixture.WaitUntil(() => app.Find("CollectFeePrint") is not null, "the fee form");
            app.CheckBox("CollectFeePrint").IsChecked = false;   // no preview to dismiss
            app.ConfirmFee();
            AppFixture.WaitUntil(() => app.Find("CollectFeeTake") is null, "the fee form to close");
        }

        // Both columns populated — a completed side with one tile looks like
        // nobody has been seen all day.
        foreach (var who in new[] { "Sana Iqbal", "Diya Nair", "Vihaan Rao" })
        {
            app.ClickTile("OpdWaitingList", "TileDone", who);
            AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", who), "a completed tile for " + who);
        }

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

        app.SelectTab("ConsultationTabs", "Diagnosis");
        Settle();
        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "consultation-diagnosis-annotated.png"),
            new Note("ConsultationHeader", "Token, patient, age and doctor. Check you have the right child."),
            new Note("RxComplaint", "Carried over from booking. Edit it freely."),
            new Note("RxDiagnosis", "Printed in bold on the prescription."),
            new Note("RxTestSearch", "A lab test the doctor wants run. Picked from the catalogue or typed."),
            new Note("RxTestAdd", "Adds it to the list below — for the Diagnostics desk to load later, not billed here."),
            new Note("ConsultationClose", "Leaves. Asks first if anything is unsaved. Esc does the same."));

        // Requested here, loaded later at the Diagnostics desk — see the
        // Diagnostics section further down.
        // Four tests requested, not one — this is also what fills the
        // Diagnostics bill further down, which looked bare with a single line.
        foreach (var t in new[] { "Complete Blood", "Fasting Blood", "Urine Routine", "TSH", "CRP" })
        {
            app.Type("RxTestSearch", t);
            AppFixture.WaitUntil(
                () => (app.Find("RxTestMatches")?.FindAllDescendants(cf => cf.ByAutomationId("RxTestMatch")) ?? []).Length > 0,
                $"search results for '{t}'");
            app.Find("RxTestMatches")!.FindAllDescendants(cf => cf.ByAutomationId("RxTestMatch"))[0].AsButton().Invoke();
            app.Click("RxTestAdd");
        }
        AppFixture.WaitUntil(() => app.Grid("RxTestGrid").RowCount >= 5, "the requested tests");

        app.SelectTab("ConsultationTabs", "Prescription");
        Settle();
        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "consultation-prescription-annotated.png"),
            new Note("RxMedicine", "Type two letters. Pick from the list to link it to your stock, or keep typing for one you do not carry."),
            new Note("RxMedicineHint", "Says whether it is in your pharmacy, out of stock, or not carried at all."),
            new Note("RxMorning", "Morning dose. 0, ¼, ½, 1 or 2 — picked, not typed."),
            new Note("RxDays", "Length of the course."),
            new Note("RxQuantity", "Worked out for you, in individual tablets. Change it if you want."),
            new Note("RxAdd", "Adds the medicine to the prescription below."));

        // Saved, so the requested test is really there to load at the
        // Diagnostics desk later in this walkthrough.
        app.Click("ConsultationSave");
        Settle();

        app.CloseConsultation();

        // ── Medicines ──────────────────────────────────────────────────────
        // A shelf, not a sample. Two products made the catalogue, inventory
        // and reorder screens all look like a fresh install.
        StockMedicine("Calpol Syrup 60ml", "PC2601", 60, 112m, 12m);
        StockMedicine("Amoxyclav Drops 15ml", "AM2604", 40, 96m, 12m);
        StockMedicine("Azithral 200 Suspension", "AZ2607", 35, 138m, 12m);
        StockMedicine("Zincovit Syrup 200ml", "ZN2611", 48, 165m, 12m);
        StockMedicine("Ondem Syrup 30ml", "OD2615", 25, 74m, 12m);
        StockMedicine("Cetirizine Drops 10ml", "CT2618", 55, 58m, 12m);
        StockMedicine("Meftal-P Suspension", "MF2622", 42, 89m, 12m);
        StockMedicine("Sporlac Sachet", "SP2626", 90, 32m, 5m);
        StockMedicine("Nasoclear Nasal Spray", "NC2630", 30, 145m, 12m);
        StockMedicine("Vitamin D3 Drops 15ml", "VD2634", 38, 210m, 5m);

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
        app.Type("SaleCustomerName", "Baby Anika");

        // A real basket — a one-line bill shows none of the arithmetic the
        // totals panel exists for.
        AddSaleLine("Calpol", "10");
        AddSaleLine("Azithral", "1");
        AddSaleLine("Zincovit", "1");
        AddSaleLine("Meftal", "1");
        AddSaleLine("Sporlac", "6");
        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount >= 5, "a basket of bill lines");
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

        // ── Diagnostics ────────────────────────────────────────────────────
        app.Navigate("NavDiagnostics", "Diagnostics");
        Settle();
        Capture("diagnostics");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "diagnostics-annotated.png"),
            new Note("DiagnosticsPatientSearch", "Name or phone, same as everywhere else — or pick a patient below instead."),
            new Note("DiagnosticsRequestedVisit", "Today's OPD visits that requested tests. Same idea as loading a prescription."),
            new Note("DiagnosticsLoadTests", "Picks the patient and pulls in every test requested for that visit."),
            new Note("DiagnosticsAddTest", "Add tests one at a time instead, searchable, for a walk-in with no OPD visit."),
            new Note("DiagnosticsFinalAmount", "What the patient pays."));

        // Pulls in the test requested during Baby Anika's consultation, above.
        var requestedVisits = app.ComboBox("DiagnosticsRequestedVisit");
        AppFixture.WaitUntil(() => requestedVisits.Items.Any(i => (i.Text ?? "").Contains("Anika")),
                             "Baby Anika's requested test");
        requestedVisits.Items.First(i => (i.Text ?? "").Contains("Anika")).Select();
        app.Click("DiagnosticsLoadTests");
        AppFixture.WaitUntil(() => app.Grid("DiagnosticsLinesGrid").RowCount >= 5, "the loaded tests");
        Settle();
        Capture("diagnostics-loaded");

        app.Click("DiagnosticsSave");
        AppFixture.WaitUntil(() => app.TextOf("DiagnosticsStatusMessage").Contains("saved"), "the bill to save");

        // Test Master — the list a "+ Add test" search or "Load diagnostic
        // tests" draws from.
        app.SelectTab("DiagnosticsTabs", "Test Master");
        Settle();
        Capture("diagnostics-test-master");

        app.SelectTab("DiagnosticsTabs", "Billing");

        // ── Dentist ──────────────────────────────────────────────────────
        // A procedure to open a case against, and an anesthesia type for the
        // sitting below — both live on the shared General Master screen.
        app.Navigate("NavGeneralMaster", "General Master");
        app.SelectTab("GeneralMasterTabs", "Procedures");
        app.Click("GeneralMasterDeptDentist");
        app.Click("GeneralMasterNewProcedure");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is not null, "the procedure editor");
        app.Type("ProcedureEditorName", "Root canal");
        app.Type("ProcedureEditorCategory", "Endodontic");
        app.Type("ProcedureEditorPrice", "8500");
        app.Click("ProcedureEditorSave");
        AppFixture.WaitUntil(() => app.Find("ProcedureEditorName") is null, "the editor to close");

        app.SelectTab("GeneralMasterTabs", "Anesthesia Types");
        app.Click("DentistNewAnesthesiaType");
        AppFixture.WaitUntil(() => app.Find("AnesthesiaEditorName") is not null, "the anesthesia editor");
        app.Type("AnesthesiaEditorName", "Local — Lignocaine");
        app.Type("AnesthesiaEditorDefaultCost", "300");
        app.Click("AnesthesiaEditorSave");
        AppFixture.WaitUntil(() => app.Find("AnesthesiaEditorName") is null, "the editor to close");

        app.SelectTab("GeneralMasterTabs", "Dental Replacements");
        app.Click("DentistNewReplacement");
        AppFixture.WaitUntil(() => app.Find("ReplacementEditorName") is not null, "the replacement editor");
        app.Type("ReplacementEditorName", "Ceramic crown");
        app.Type("ReplacementEditorUnitCost", "4000");
        app.Click("ReplacementEditorSave");
        AppFixture.WaitUntil(() => app.Find("ReplacementEditorName") is null, "the editor to close");

        // Cases — register the patient, open a case against Root canal.
        app.Navigate("NavDentist", "Dentist");
        app.Click("DentistNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor");
        app.Type("PatientName", "Meera Iyer");
        app.Type("PatientPhone", "9008007088");
        app.Type("PatientAge", "29");
        app.Click("PatientSave");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(() => app.TextOf("DentistSelectedPatient").Contains("Meera"), "the patient to be selected");

        // Three cases on the patient, not one — the case list is the whole
        // point of the module and a single row does not show it working.
        foreach (var (proc, tooth) in new[]{
            ("Scaling & Polishing", ""), ("Composite Filling", "36"), ("Root Canal Treatment (RCT)", "26") })
        {
            app.ComboBox("DentistNewCaseProcedure").Select(proc);
            if (tooth.Length > 0) app.Type("DentistNewCaseTooth", tooth);
            app.Click("DentistOpenCase");
            AppFixture.WaitUntil(
                () => app.Grid("DentistCasesGrid").Rows.Any(r => r.Cells[0].Value == proc),
                $"the {proc} case to list");
        }

        app.SelectRowByText("DentistCasesGrid", "Root Canal Treatment");
        AppFixture.WaitUntil(() => app.Find("DentistCaseTotalCost") is not null, "the case detail panel");
        Settle();
        Capture("dentist-cases");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "dentist-cases-annotated.png"),
            new Note("DentistNewCaseProcedure", "Pick a procedure — or a package instead, never both."),
            new Note("DentistNewCaseTooth", "FDI or Universal notation. Optional."),
            new Note("DentistOpenCase", "Opens the case at the procedure's own price — snapshotted, so a later price change never moves it."),
            new Note("DentistCasesGrid", "Every case for this patient. Select one to see it below."),
            new Note("DentistCompleteCase", "Marks the highlighted case done. A later payment is still allowed."),
            new Note("DentistSwitchCase", "Not shown here — this is the Cases tab itself. Treatment and Payments each carry a link straight back to it."));

        // Treatment — a course of sittings, which is what the tab is for.
        app.SelectTab("DentistTabs", "Treatment");
        foreach (var work in new[]{
            "Diagnosis, X-ray and pulp testing",
            "Access opening and canal cleaning",
            "Biomechanical preparation",
            "Obturation and temporary filling" })
        {
            app.Type("DentistSittingWorkDone", work);
            app.ComboBox("DentistSittingAnesthesia").Select("Local — Lignocaine");
            app.Click("DentistAddSitting");
            AppFixture.WaitUntil(
                () => app.Grid("DentistSittingsGrid").Rows.Any(r => r.Cells[2].Value.Contains(work[..12])),
                "the sitting to list");
        }

        app.ComboBox("DentistReplacementPicker").Select("Ceramic crown");
        app.Click("DentistAddReplacement");
        AppFixture.WaitUntil(() => app.Grid("DentistReplacementsGrid").RowCount >= 1, "the replacement to list");
        Settle();
        Capture("dentist-treatment");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "dentist-treatment-annotated.png"),
            new Note("DentistSelectedCaseTitle", "Which case this is, and its status — carried across Treatment and Payments so it is never lost switching tabs."),
            new Note("DentistCaseBalance", "What is still owed, next to the total and what has already been paid — the same strip repeats on Payments."),
            new Note("DentistAddSitting", "One visit's worth of work, and any anesthesia used that day."),
            new Note("DentistAddReplacement", "A crown, bridge, denture or implant, priced from its own master."));

        // Payments — several part-payments, which is the story this tab tells.
        app.SelectTab("DentistTabs", "Payments");
        foreach (var amt in new[] { "3000", "2500", "2000" })
        {
            var before = app.Grid("DentistPaymentsGrid").RowCount;
            app.Type("DentistPaymentAmount", amt);
            app.Click("DentistRecordPayment");
            AppFixture.WaitUntil(() => app.Grid("DentistPaymentsGrid").RowCount > before, "the payment to list");
        }
        Settle();
        Capture("dentist-payments");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "dentist-payments-annotated.png"),
            new Note("DentistPaymentAmount", "How much is being paid right now — need not be the full balance."),
            new Note("DentistPaymentMode", "Cash, UPI or card."),
            new Note("DentistRecordPaymentAndPrint", "Records the payment and opens a receipt for it."),
            new Note("DentistPaymentsGrid", "Every payment against this case, most recent included."));

        // ── Pediatrics ───────────────────────────────────────────────────
        // A pharmacy-stocked vaccine brand to give — vaccination draws its
        // price and batch from here, never a manual entry. BCG itself is
        // already on Vaccine Master, seeded from the WHO/UIP schedule.
        StockMedicine("BCG Vaccine (Serum Institute)", "BCG2601", 20, 150m, 0m);

        app.Navigate("NavPediatrics", "Pediatrics");
        app.Click("PediatricsNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor");
        app.Type("PatientName", "Baby Ansh");
        app.Type("PatientPhone", "9008007077");

        // 85 days old — old enough that BCG (due at birth) is overdue unless
        // given, the 42-day doses are overdue, the 70-day doses are overdue,
        // the 98-day doses are due soon, and everything after that reads
        // upcoming — a demo patient that lands on every status at once.
        app.SetDate("PatientDob", DateTime.Today.AddDays(-85));
        app.Click("PatientSave");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(() => app.TextOf("PediatricsSelectedPatient").Contains("Ansh"), "the patient to be selected");

        // A run of visits from birth to now. One measurement draws a chart
        // with a single dot and an empty history — this plots a real curve.
        var visits = new[]{
            (days:-82, w:"3.2", h:"49", hc:"34.5"),
            (days:-68, w:"3.9", h:"52", hc:"36.0"),
            (days:-54, w:"4.5", h:"54", hc:"37.2"),
            (days:-40, w:"5.1", h:"56", hc:"38.4"),
            (days:-26, w:"5.5", h:"57", hc:"39.2"),
            (days:-12, w:"5.8", h:"58", hc:"40.0"),
            (days:0,   w:"6.2", h:"59", hc:"40.6")
        };
        foreach (var v in visits)
        {
            app.Click("PediatricsOpenRecordGrowth");
            AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is not null, "the growth popup to open");
            app.Type("RecordGrowthMeasuredOn", DateTime.Today.AddDays(v.days).ToString("yyyy-MM-dd"));
            app.Type("RecordGrowthWeightKg", v.w);
            app.Type("RecordGrowthHeightCm", v.h);
            app.Type("RecordGrowthHeadCircumferenceCm", v.hc);
            app.Click("RecordGrowthSave");
            AppFixture.WaitUntil(() => app.Find("RecordGrowthWeightKg") is null, "the popup to close");
        }
        AppFixture.WaitUntil(
            () => decimal.TryParse(app.TextOf("PediatricsGrowthChartYTop").Split(' ')[0], out _),
            "the growth chart to render");
        AppFixture.WaitUntil(() => app.Grid("PediatricsGrowthHistoryGrid").RowCount >= 5, "a growth history");
        Settle();
        Capture("pediatrics-growth");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "pediatrics-growth-annotated.png"),
            new Note("PediatricsOpenRecordGrowth", "Logs one measurement — weight, height, head circumference."),
            new Note("PediatricsGrowthMetric", "Switches which of the three the chart below plots."),
            new Note("PediatricsGrowthHistoryGrid", "Every measurement on record for this child, most recent included."));

        app.SelectTab("PediatricsTabs", "Care");
        app.Click("PediatricsOpenRecordVaccination");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is not null, "the vaccination popup to open");
        app.ComboBox("RecordVaccinationVaccinePicker").Select("BCG");
        app.Type("RecordVaccinationProductSearch", "BCG Vaccine");
        AppFixture.WaitUntil(
            () => app.Find("RecordVaccinationProductMatches") is not null
                  && app.ListBox("RecordVaccinationProductMatches").Items.Length >= 1,
            "the brand to be found in pharmacy stock");
        app.ListBox("RecordVaccinationProductMatches").Items[0].Select();
        app.Click("RecordVaccinationSave");
        AppFixture.WaitUntil(() => app.Find("RecordVaccinationVaccinePicker") is null, "the popup to close");
        AppFixture.WaitUntil(() => app.Grid("PediatricsLinesGrid").RowCount >= 1, "the dose to land on the bill");
        Settle();
        Capture("pediatrics-care");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "pediatrics-care-annotated.png"),
            new Note("PediatricsOpenAddProcedure", "Bills a procedure from the shared Procedure Master."),
            new Note("PediatricsOpenRecordVaccination", "Gives a dose — vaccine type from Vaccine Master, brand and price from Pharmacy stock, never typed."),
            new Note("PediatricsLinesGrid", "The running bill — procedures and vaccine doses together."),
            new Note("PediatricsVaccinationHistoryGrid", "Doses already on record, separate from what is still sitting on the bill above."));

        app.Click("PediatricsSaveBill");
        AppFixture.WaitUntil(
            () => app.TextOf("PediatricsBillingStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
            "the bill save confirmation");

        app.SelectTab("PediatricsTabs", "Immunization");
        Settle();
        Capture("pediatrics-immunization");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "pediatrics-immunization-annotated.png"),
            new Note("PediatricsImmunizationOverdueCount", "How many doses are already overdue by this child's age."),
            new Note("PediatricsImmunizationGrid", "Every vaccine on Vaccine Master, checked off against this child's own doses — Record opens the same popup pre-selected."));

        // ── Pathology Lab ────────────────────────────────────────────────
        app.Navigate("NavPathologyLabMaster", "Pathology Lab — Master");
        Settle();
        Capture("pathology-lab-master");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "pathology-lab-master-annotated.png"),
            new Note("LabAnalyteMasterSearch", "By name or category."),
            new Note("LabNewAnalyte", "Name, category, units, result type (numeric or text) and reference range."),
            new Note("LabAnalyteMasterGrid", "A set of common analytes is preloaded — hematology, biochemistry, thyroid, infection screens and more."));

        app.SelectTab("PathologyLabMasterTabs", "Report Master");
        Settle();
        Capture("pathology-lab-report-master");

        app.SelectTab("PathologyLabMasterTabs", "Package Master");
        Settle();
        Capture("pathology-lab-package-master");

        app.Navigate("NavPathologyLab", "Pathology Lab");
        app.Click("LabNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor");
        app.Type("PatientName", "Ravi Kumar");
        app.Type("PatientPhone", "9008007066");
        app.Type("PatientAge", "42");
        app.Click("PatientSave");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(() => app.TextOf("LabSelectedPatient").Contains("Ravi"), "the patient to be selected");

        // Two earlier orders on the patient, so the order list has history
        // behind it rather than a single row.
        foreach (var earlier in new[] { "Liver Function Test (LFT)", "Thyroid Profile (T3 T4 TSH)" })
        {
            app.ComboBox("LabReportPicker").Select(earlier);
            app.Click("LabAddReportLine");
            AppFixture.WaitUntil(() => app.Find("LabNewOrderLines") is not null, "the report added");
            app.Click("LabSaveOrder");
            AppFixture.WaitUntil(
                () => app.TextOf("LabOrdersStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
                "the order to save");
        }

        // The order actually shown: a multi-report panel, so the results
        // grid below fills with analytes instead of five rows.
        foreach (var r in new[] { "Complete Blood Picture (CBC)", "Kidney Function Test (KFT)", "Lipid Profile" })
        {
            app.ComboBox("LabReportPicker").Select(r);
            app.Click("LabAddReportLine");
            AppFixture.WaitUntil(() => app.Find("LabNewOrderLines") is not null, "the report added to the order");
        }
        app.Type("LabReferredBy", "Dr. A. Kumar");
        app.Type("LabSpecimenId", "SP-2601");
        app.Click("LabSaveOrder");
        AppFixture.WaitUntil(() => app.Grid("LabOrdersGrid").RowCount >= 3, "the orders to list");
        app.Grid("LabOrdersGrid").Rows[app.Grid("LabOrdersGrid").RowCount - 1].Select();
        AppFixture.WaitUntil(() => app.Find("LabResultsGrid") is not null, "the order detail panel");
        Settle();
        Capture("pathology-lab-orders");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "pathology-lab-orders-annotated.png"),
            new Note("LabReportPicker", "Add one report at a time — or apply a package below instead, for a bundle billed at its own price."),
            new Note("LabOrdersGrid", "Every order for this patient. Select one to see its results panel."),
            new Note("LabMarkCollected", "Records that the specimen has actually been taken."),
            new Note("LabResultsGrid", "One row per analyte on the report — RESULT is the only editable column, filled in once the sample is processed."));

        app.Click("LabMarkCollected");
        AppFixture.WaitUntil(
            () => app.TextOf("LabOrdersStatus").Contains("collected", StringComparison.OrdinalIgnoreCase),
            "the sample marked collected");
        Settle();
        Capture("pathology-lab-collected");

        // ── Appointments ─────────────────────────────────────────────────
        app.Navigate("NavAppointments", "Appointments");
        app.SelectTab("AppointmentsTabs", "Book appointment");
        app.Click("AppointmentsNewPatient");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient editor");
        app.Type("PatientName", "Farhan Sheikh");
        app.Type("PatientPhone", "9008007055");
        app.Type("PatientAge", "31");
        app.Click("PatientSave");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the editor to close");
        AppFixture.WaitUntil(() => app.TextOf("AppointmentsSelectedPatient").Contains("Farhan"), "the patient to be selected");

        // Booked for today rather than the default tomorrow, so it shows up
        // on the Today's appointments tab captured just below.
        app.Type("AppointmentsBookingDate", DateTime.Today.ToString("yyyy-MM-dd"));
        app.Type("AppointmentsReason", "Follow-up review");
        Settle();
        Capture("appointments-book");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "appointments-book-annotated.png"),
            new Note("AppointmentsDoctor", "Every appointment is booked against one doctor."),
            new Note("AppointmentsContext", "Which module the visit is for — only modules actually switched on are offered."),
            new Note("AppointmentsBookingDate", "yyyy-MM-dd. Defaults to tomorrow."),
            new Note("AppointmentsBookAndPrint", "Books it and opens a printable slip. Book without printing skips the preview."));

        app.Click("AppointmentsBookAndPrint");
        ClosePreview();
        AppFixture.WaitUntil(() => app.TextOf("AppointmentsBookingStatus").Contains("booked", StringComparison.OrdinalIgnoreCase),
                             "the booking confirmation");

        app.SelectTab("AppointmentsTabs", "Today's appointments");
        AppFixture.WaitUntil(() => app.Grid("AppointmentsTodayGrid").RowCount >= 1, "today's appointment to list");
        app.Grid("AppointmentsTodayGrid").Rows[0].Select();
        Settle();
        Capture("appointments-today");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "appointments-today-annotated.png"),
            new Note("AppointmentsSelectedDate", "Any day, not just today — pick one to see its own list."),
            new Note("AppointmentsTodayGrid", "Everyone booked for the chosen day, across every module."),
            new Note("AppointmentsCheckIn", "Converts the appointment into a real OPD visit and hands it a token."),
            new Note("AppointmentsCancelReason", "Required — kept against the appointment, not just a silent removal."),
            new Note("AppointmentsReschedule", "Moves it to a new date and time without cancelling and rebooking."));

        app.Click("AppointmentsCheckIn");
        AppFixture.WaitUntil(
            () => app.TextOf("AppointmentsDailyStatus").Contains("checked in", StringComparison.OrdinalIgnoreCase),
            "the check-in confirmation");

        app.SelectTab("AppointmentsTabs", "Reminders");
        Settle();
        Capture("appointments-reminders");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "appointments-reminders-annotated.png"),
            new Note("AppointmentsReminderLeadDays", "How many days out counts as due — editable, not just the presets."),
            new Note("AppointmentsRemindersGrid", "Follow-ups and next-dose dates due within that window, across every module."));

        // ── General Master ───────────────────────────────────────────────
        // Vaccine Master and the shared Procedure Master both live here,
        // alongside the dentist-specific masters used above.
        app.Navigate("NavGeneralMaster", "General Master");
        app.SelectTab("GeneralMasterTabs", "Vaccine Master");
        Settle();
        Capture("general-master-vaccines");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "general-master-vaccines-annotated.png"),
            new Note("PediatricsNewVaccine", "Name, dose number, recommended age in days, category."),
            new Note("PediatricsVaccineMasterGrid", "The WHO/UIP schedule is preloaded — edit or deactivate rather than delete once a dose has been given."));

        app.SelectTab("GeneralMasterTabs", "Procedures");
        Settle();
        Capture("general-master-procedures");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "general-master-procedures-annotated.png"),
            new Note("GeneralMasterDeptPediatrics", "One procedure list, filtered by which specialty bills it — Pediatrics, Dentist, or General for anything shared."),
            new Note("GeneralMasterNewProcedure", "Name, category, price."),
            new Note("GeneralMasterProcedureGrid", "Filtered to whichever department pill above is selected."));

        app.SelectTab("GeneralMasterTabs", "Dental Replacements");
        Settle();
        Capture("general-master-dental");

        // ── Reports, patients, settings ────────────────────────────────────
        app.Navigate("NavReports", "Reports");
        Settle();
        Capture("reports");

        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", "Baby Ansh");
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the pediatric patient");
        app.Grid("PatientsGrid").Rows[0].Select();
        app.SelectTab("PatientsTabs", "Vaccination history");
        Settle();
        Capture("patients-vaccination-history");

        app.SelectTab("PatientsTabs", "Growth chart");
        Settle();
        Capture("patients-growth-chart");

        app.Type("PatientsSearchBox", "Baby Anika");
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient");
        app.Grid("PatientsGrid").Rows[0].Select();

        // Back onto the default tab — the Ansh detour above left it on
        // Growth chart, and every step below expects Visits & prescriptions.
        app.SelectTab("PatientsTabs", "Visits & prescriptions");
        Settle();
        Capture("patients");

        // The patient form, over the shell.
        app.Click("PatientsEdit");
        AppFixture.WaitUntil(() => app.Find("PatientName") is not null, "the patient form");
        Settle();
        Capture("patient-editor");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "patient-editor-annotated.png"),
            new Note("PatientName", "Required, along with an age or a date of birth."),
            new Note("PatientPhone", "A whole family shares one number, and that is fine."),
            new Note("PatientAllergies", "Shown against every prescription this child is written."),
            new Note("PatientDelete", "Only for somebody already on the register, and it asks first."),
            new Note("PatientSave", "Saves and closes. The register clears for the next patient."));

        app.Click("PatientEditorCancel");
        AppFixture.WaitUntil(() => app.Find("PatientName") is null, "the patient form to close");
        app.Grid("PatientsGrid").Rows[0].Select();

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
        app.SelectTab("SettingsTabs", "General");
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
        app.SelectTab("SettingsTabs", "General");
        app.ComboBox("AppThemeChoice").Select("Light");
        Settle();

        // ── The other queue layout ─────────────────────────────────────────
        app.ComboBox("QueueLayout").Select("Rows");
        app.Click("GeneralSave");
        app.Navigate("NavOpd", "OPD");
        Settle();
        Capture("opd-rows");

        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "General");
        app.ComboBox("QueueLayout").Select("Tiles");
        app.Click("GeneralSave");

        // ── Dashboard, captured last of all ─────────────────────────────────
        // The landing screen, deliberately shot at the end once the day has
        // real OPD, pharmacy and diagnostics activity behind it — an
        // all-zero dashboard is not what anyone using this guide will see.
        app.Navigate("NavDashboard", "Dashboard");
        Settle();
        Capture("dashboard");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetUpShop()
    {
        app.Navigate("NavSettings", "Settings");
        app.SelectTab("SettingsTabs", "Features");
        app.CheckBox("DiagnosticsEnabled").IsChecked = true;
        app.CheckBox("AppointmentsEnabled").IsChecked = true;
        app.CheckBox("PediatricsEnabled").IsChecked = true;
        app.CheckBox("DentistEnabled").IsChecked = true;
        app.CheckBox("PathologyLabEnabled").IsChecked = true;
        Settle();
        Capture("settings-features");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "settings-features-annotated.png"),
            new Note("OpdEnabled", "On by default — the walk-in queue, consultation and prescription. A dentist- or pediatrician-only clinic turns this off."),
            new Note("AppointmentsEnabled", "Advance booking, check-in against today's list, cancel or reschedule, and reminders."),
            new Note("PediatricsEnabled", "Vaccine master, vaccination and growth history, and procedure billing."),
            new Note("DentistEnabled", "Cases tracked across however many sittings and payments they take."),
            new Note("PathologyLabEnabled", "Analyte and report masters with reference ranges, orders, and result entry."),
            new Note("FeaturesSave", "Applies immediately — every nav button appears or disappears without a restart."));

        app.Click("FeaturesSave");
        AppFixture.WaitUntil(() => app.Find("NavDiagnostics") is not null, "the Diagnostics nav button");
        AppFixture.WaitUntil(() => app.Find("NavAppointments") is not null, "the Appointments nav button");
        AppFixture.WaitUntil(() => app.Find("NavPediatrics") is not null, "the Pediatrics nav button");
        AppFixture.WaitUntil(() => app.Find("NavDentist") is not null, "the Dentist nav button");
        AppFixture.WaitUntil(() => app.Find("NavPathologyLab") is not null, "the Pathology Lab nav button");

        app.SelectTab("SettingsTabs", "Security");
        Settle();
        Capture("settings-security");

        Annotate.Draw(app.MainWindow, Path.Combine(OutputDir, "settings-security-annotated.png"),
            new Note("RequireLogin", "Off by default. On asks everyone to sign in and records who did what — the default Admin account can always sign in regardless."),
            new Note("NewUser", "Admin creates the rest — Doctor, Pharmacy and Diagnosis accounts, each seeing only their own part of the sidebar."));

        app.SelectTab("SettingsTabs", "Clinic");
        app.Type("ClinicName", "Twinkle Children's Hospital");
        app.Click("ClinicSave");
        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
                             "the clinic details");

        app.SelectTab("SettingsTabs", "Pharmacy");
        app.Type("PharmacyName", "Twinkle Pharmacy");

        // The sample pharmacy is GST registered, so the guide shows a tax invoice.
        app.CheckBox("PharmacyGstRegistered").IsChecked = true;
        app.Type("PharmacyGstin", "36ABCDE1234F1Z5");
        app.Click("PharmacySave");
        AppFixture.WaitUntil(() => app.TextOf("SettingsStatus").Contains("saved", StringComparison.OrdinalIgnoreCase),
                             "the pharmacy details");
    }


    /// <summary>Adds one line to the counter's running bill, picking the
    /// first stocked match for the search term.</summary>
    private void AddSaleLine(string search, string qty)
    {
        app.Type("SaleSearch", search);
        AppFixture.WaitUntil(() => app.ListBox("SaleMatches").Items.Length >= 1, $"'{search}' on the shelf");
        app.ListBox("SaleMatches").Items[0].Select();
        app.Type("SaleQuantity", qty);
        app.Click("SaleAddLine");
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
