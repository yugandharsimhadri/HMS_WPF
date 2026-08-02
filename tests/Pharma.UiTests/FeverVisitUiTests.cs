using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>
/// One whole visit, exactly as it happens at the desk:
///
///   1. a child comes in with fever and is booked
///   2. the fee is taken and a receipt printed
///   3. the doctor prescribes Paracetamol (stocked), a Cetirizine syrup the shop
///      has run out of, and a Dolo syrup the shop has never carried
///   4. the counter pulls the prescription and reports what it cannot supply
///   5. the operator receives 5 bottles of the Cetirizine syrup
///   6. the counter bills 9 tablets from a strip of 10, and one bottle
///   7. the parent buys the Dolo syrup outside
///
/// This is the test that would have caught the fault reported from the counter:
/// nine tablets being charged and deducted as nine strips.
/// </summary>
public class FeverVisitUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private const string Phone = "9440011223";

    // A strip of ten at ₹30.00 is ₹3.00 a tablet; nine of them is ₹27.00.
    private const decimal ParacetamolMrp = 30.00m;

    // A bottle is one unit — half a bottle is not something a shop hands over.
    private const decimal SyrupMrp = 85.00m;

    [Fact]
    public void A_fever_visit_from_the_door_to_the_bill()
    {
        var run = DateTime.Now.ToString("HHmmssfff");
        var patient = $"Aarav {run}";
        var paracetamol = $"Paracetamol 500mg {run}";
        var cetirizine = $"Cetirizine 5mg Syrup {run}";
        var dolo = $"Dolo Syrup {run}";

        // ── 1. The shop, before the patient arrives ─────────────────────────
        // Paracetamol is on the shelf. The Cetirizine syrup is in the catalogue
        // but the shop has run out — which is the case the counter has to handle.
        CreateMedicine(paracetamol, packSize: "10 TAB", unitsPerPack: 10, unit: "Tablet");
        ReceiveStock(paracetamol, batch: $"PC{run}", packs: 5, mrp: ParacetamolMrp);

        CreateMedicine(cetirizine, packSize: "60ML", unitsPerPack: 1, unit: "Bottle");
        // No stock received on purpose.

        // ── 2. Booked at the desk ──────────────────────────────────────────
        OpdUiTests.BookWalkIn(app, patient, Phone, "4");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", patient), "the tile");

        // ── 3. The fee ─────────────────────────────────────────────────────
        app.TakeFee("OpdWaitingList", patient);
        ClosePreview();

        AppFixture.WaitUntil(() => app.TileAction("OpdWaitingList", "TileConsult", patient) is not null,
                             "the tile after the fee");

        // ── 4. The consultation ────────────────────────────────────────────
        app.ClickTile("OpdWaitingList", "TileConsult", patient);
        app.WaitForConsultation(patient);

        app.SelectTab("ConsultationTabs", "Diagnosis");
        app.Type("RxComplaint", "Fever for two days");
        app.Type("RxDiagnosis", "Viral fever");

        // Paracetamol: one morning, one night, five days — then cut to 9 tablets,
        // because that is what the parent is asked to buy.
        app.SelectTab("ConsultationTabs", "Prescription");
        PickPrescribed(paracetamol);
        AppFixture.WaitUntil(() => app.TextOf("RxMedicineHint").Contains("In our pharmacy"),
                             "the in-stock note");

        app.ComboBox("RxMorning").Select("1");
        app.ComboBox("RxNight").Select("1");
        app.Type("RxDays", "5");
        AppFixture.WaitUntil(() => app.TextBox("RxQuantity").Text == "10", "the course");

        app.Type("RxQuantity", "9");
        app.Click("RxAdd");
        AppFixture.WaitUntil(() => app.Grid("RxGrid").RowCount == 1, "the first line");

        // The syrup the shop has run out of. It is still prescribable — the
        // prescription is a clinical document, not a stock list.
        PickPrescribed(cetirizine);
        AppFixture.WaitUntil(() => app.TextOf("RxMedicineHint").Contains("out of stock"),
                             "the out-of-stock note");

        app.Type("RxDose", "5 ml");
        app.ComboBox("RxNight").Select("1");
        app.Type("RxDays", "5");
        app.Type("RxQuantity", "1");
        app.Click("RxAdd");
        AppFixture.WaitUntil(() => app.Grid("RxGrid").RowCount == 2, "the second line");

        // A syrup the shop has never carried. Typed, never picked.
        app.Type("RxMedicine", dolo);
        AppFixture.WaitUntil(() => app.TextOf("RxMedicineHint").Contains("Not in our pharmacy"),
                             "the not-stocked note");

        app.Type("RxDose", "5 ml");
        app.ComboBox("RxMorning").Select("1");
        app.Type("RxDays", "3");
        app.Type("RxQuantity", "1");
        app.Click("RxAdd");
        AppFixture.WaitUntil(() => app.Grid("RxGrid").RowCount == 3, "the third line");

        app.MainWindow.FindFirstDescendant(cf => cf.ByName("Save & complete"))?.AsButton().Invoke();
        AppFixture.WaitUntil(() => !app.IsConsultationOpen, "the consultation to close");
        AppFixture.WaitUntil(() => app.HasTile("OpdCompletedList", patient), "the completed tile");

        // ── 5. At the counter: what can and cannot be supplied ─────────────
        app.Navigate("NavSale", "Pharmacy counter");
        LoadPrescriptionFor(patient);

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 1, "the stocked medicine");

        // The status is written once the whole prescription has been walked, so
        // wait for it rather than reading it the instant a row appears.
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("Not added"), "what could not be supplied");

        var reported = app.TextOf("SaleStatus");
        Assert.Contains(dolo, reported);                  // never carried
        Assert.Contains("no stock", reported);            // run out

        // ── 6. The operator receives the syrup that came in ────────────────
        ReceiveStock(cetirizine, batch: $"CT{run}", packs: 5, mrp: SyrupMrp);

        // ── 7. Bill it ─────────────────────────────────────────────────────
        app.Navigate("NavSale", "Pharmacy counter");
        LoadPrescriptionFor(patient);

        AppFixture.WaitUntil(() => app.Grid("SaleLinesGrid").RowCount == 2, "both medicines");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains(dolo), "the outside-purchase note");

        // Only the Dolo syrup is left for the parent to buy outside — and the
        // syrup that was out of stock a moment ago is no longer reported.
        var afterRestock = app.TextOf("SaleStatus");
        Assert.Contains(dolo, afterRestock);
        Assert.DoesNotContain("no stock", afterRestock);

        app.Type("SaleCustomerName", patient);

        // 9 tablets at ₹3.00 plus one bottle at ₹85.00. Not 9 strips at ₹30.00.
        AppFixture.WaitUntil(() => app.TextOf("SaleNetTotal").Contains("112"), "the bill total");

        var total = app.TextOf("SaleNetTotal");
        Assert.Contains("112", total);
        Assert.DoesNotContain("355", total);   // what nine strips would have come to

        app.Click("SaleSave");
        AppFixture.WaitUntil(() => app.TextOf("SaleStatus").Contains("INV"), "the bill number");

        // ── 8. What left the shelf ─────────────────────────────────────────
        // 50 tablets less 9, and 5 bottles less 1.
        Assert.Equal("41", StockOnHand(paracetamol));
        Assert.Equal("4", StockOnHand(cetirizine));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void CreateMedicine(string name, string packSize, int unitsPerPack, string unit)
    {
        app.Navigate("NavProducts", "Medicines");
        app.Click("ProductsNew");

        app.Type("ProductName", name);
        app.Type("ProductPackSize", packSize);
        app.Type("ProductUnitsPerPack", unitsPerPack.ToString());
        app.Type("ProductGstRate", "12");
        app.ComboBox("ProductDispensingUnit").Select(unit);

        app.Click("ProductSave");
        AppFixture.WaitUntil(() => app.TextOf("ProductsStatus").Contains("saved"), $"{name} to save");
    }

    private void ReceiveStock(string name, string batch, int packs, decimal mrp)
    {
        app.Navigate("NavInventory", "Inventory");
        app.Type("InventorySearch", name);
        app.Click("InventorySearchButton");
        AppFixture.WaitUntil(() => app.Grid("InventoryProductsGrid").RowCount == 1, $"{name} in inventory");
        app.Grid("InventoryProductsGrid").Rows[0].Select();
        app.Click("InventoryReceive");

        app.Type("StockBatchNo", batch);
        app.Type("StockQuantity", packs.ToString());
        app.Type("StockPurchaseRate", (mrp * 0.7m).ToString("0.00"));
        app.Type("StockMrp", mrp.ToString("0.00"));
        app.Click("StockAdd");

        AppFixture.WaitUntil(() => app.TextOf("InventoryStatus").Contains("added to batch"),
                             $"{name} stock to be added");
    }

    /// <summary>Types enough of a name to find it, then clicks the result.</summary>
    private void PickPrescribed(string name)
    {
        app.Type("RxMedicine", name);

        AppFixture.WaitUntil(
            () => (app.Find("RxMatches")?.FindAllDescendants(cf => cf.ByAutomationId("RxMatch")) ?? []).Length > 0,
            $"{name} in the prescription search");

        app.Find("RxMatches")!.FindAllDescendants(cf => cf.ByAutomationId("RxMatch"))[0].AsButton().Invoke();
    }

    private void LoadPrescriptionFor(string patient)
    {
        var visits = app.ComboBox("SalePrescriptionVisit");

        AppFixture.WaitUntil(() => visits.Items.Any(i => (i.Text ?? "").Contains(patient)),
                             "the patient in today's prescriptions");

        visits.Items.First(i => (i.Text ?? "").Contains(patient)).Select();
        app.Click("SaleLoadPrescription");
    }

    private string StockOnHand(string name)
    {
        app.Navigate("NavProducts", "Medicines");
        app.Type("ProductSearch", name);
        app.Click("ProductsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("ProductsGrid").RowCount == 1, $"{name} in the catalogue");

        return app.CellOf("ProductsGrid", "STOCK");
    }

    private void ClosePreview()
    {
        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.Any(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            "the fee receipt preview");

        var preview = app.MainWindow.ModalWindows.First(
            w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase));

        preview.FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();

        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                w => !w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            "the preview to close");
    }
}
