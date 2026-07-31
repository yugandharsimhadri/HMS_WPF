using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// Anything printed once must be printable again later, from the patient's record
/// rather than only on the day it happened.
/// </summary>
public class ReprintUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private bool NoPreviewOpen()
        => app.MainWindow.ModalWindows.All(
            w => !w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase));

    private Window WaitForPreview()
    {
        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(preview);
        return preview!;
    }

    private void ClosePreview()
    {
        WaitForPreview().FindFirstDescendant(cf => cf.ByAutomationId("PreviewClose"))?.AsButton().Invoke();
        AppFixture.WaitUntil(NoPreviewOpen, "the preview to close");
    }

    /// <summary>Books a patient and takes the fee, closing the receipt preview it raises.</summary>
    private string BookAndPay(string suffix)
    {
        var name = $"Reprint {suffix}";
        OpdUiTests.BookWalkIn(app, name, "9007006005", "5");

        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");
        app.TakeFee("OpdWaitingList", name);
        ClosePreview();

        return name;
    }

    private void OpenPatient(string name)
    {
        app.Navigate("NavPatients", "Patients");
        app.Type("PatientsSearchBox", name);
        app.Click("PatientsSearchButton");
        AppFixture.WaitUntil(() => app.Grid("PatientsGrid").RowCount == 1, "the patient to be found");
        app.Grid("PatientsGrid").Rows[0].Select();
    }

    [Fact]
    public void Taking_the_fee_issues_a_numbered_receipt()
    {
        var name = BookAndPay(DateTime.Now.ToString("HHmmssfff"));

        Assert.Contains("RCP", app.TextOf("OpdStatus"));
        Assert.Contains(name, app.TextOf("OpdStatus"));
    }

    /// <summary>
    /// Pressing Fee used to take the money there and then, at whatever payment
    /// mode a combo at the top of the screen was left on, and go straight to a
    /// print preview. A receipt is numbered and dated as it is written, so a fee
    /// taken wrongly is a fee reversed on paper. It has to ask first.
    /// </summary>
    [Fact]
    public void The_fee_form_shows_what_is_about_to_be_taken()
    {
        var name = $"Shown {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9007006007", "6");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");

        app.OpenFeeForm("OpdWaitingList", name);

        // The child, and the amount, before anything is written.
        Assert.Contains(name, app.TextOf("CollectFeeHeader"));
        Assert.NotEqual("", app.TextBox("CollectFeeAmount").Text);

        app.Click("CollectFeeCancel");
        AppFixture.WaitUntil(() => app.Find("CollectFeeTake") is null, "the form to close");

        // Backing out burns no receipt number: the tile still owes the fee.
        app.Navigate("NavOpd", "OPD");
        OpenPatient(name);
        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");
        Assert.Equal("", app.CellOf("PatientHistoryGrid", "RECEIPT"));
    }

    /// <summary>
    /// A follow-up seen at half fee, or a family concession. The receipt has to
    /// say what actually changed hands, not what was quoted at booking.
    /// </summary>
    [Fact]
    public void An_edited_amount_is_what_the_receipt_says()
    {
        var name = $"Concession {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9007006008", "3");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");

        app.OpenFeeForm("OpdWaitingList", name);
        app.Type("CollectFeeAmount", "150");

        // It says so, rather than letting a mistyped digit pass as a decision.
        AppFixture.WaitUntil(() => app.TextOf("CollectFeeNote").Contains("150"), "the changed-fee note");

        app.ConfirmFee();
        ClosePreview();

        Assert.Contains("150", app.TextOf("OpdStatus"));

        OpenPatient(name);
        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");
        Assert.Equal("150.00", app.CellOf("PatientHistoryGrid", "FEE"));
    }

    [Fact]
    public void A_receipt_can_be_printed_again_from_the_patients_record()
    {
        var name = BookAndPay(DateTime.Now.ToString("HHmmssfff"));

        OpenPatient(name);
        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");

        app.Grid("PatientHistoryGrid").Rows[0].Select();
        app.Click("PatientPrintReceipt");

        var preview = WaitForPreview();

        // The window names the job, so a duplicate is obvious before it prints.
        Assert.Contains("duplicate", preview.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RCP", preview.Title);

        ClosePreview();
    }

    [Fact]
    public void Printing_a_receipt_for_an_unpaid_visit_says_so_instead_of_printing()
    {
        var name = $"Unpaid {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9007006006", "8");

        OpenPatient(name);
        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");
        app.Grid("PatientHistoryGrid").Rows[0].Select();

        app.Click("PatientPrintReceipt");

        AppFixture.WaitUntil(
            () => app.TextOf("PatientsStatus").Contains("No consultation fee", StringComparison.OrdinalIgnoreCase),
            "the explanation");

        Assert.True(NoPreviewOpen(), "A preview opened when nothing should have printed.");
    }

    [Fact]
    public void A_visit_with_no_prescription_says_so_rather_than_printing_a_blank_page()
    {
        var name = BookAndPay(DateTime.Now.ToString("HHmmssfff"));

        OpenPatient(name);
        AppFixture.WaitUntil(() => app.Grid("PatientHistoryGrid").RowCount == 1, "the visit history");
        app.Grid("PatientHistoryGrid").Rows[0].Select();

        app.Click("PatientPrintRx");

        AppFixture.WaitUntil(
            () => app.TextOf("PatientsStatus").Contains("no prescription", StringComparison.OrdinalIgnoreCase),
            "the explanation");

        Assert.True(NoPreviewOpen(), "A preview opened when nothing should have printed.");
    }

    [Fact]
    public void Searching_for_a_bill_that_does_not_exist_says_so()
    {
        app.Navigate("NavReports", "Reports");

        app.Type("BillSearchBox", "NOSUCHBILL");
        app.Click("FindBillButton");

        AppFixture.WaitUntil(
            () => app.TextOf("ReportsStatus").Contains("No bill", StringComparison.OrdinalIgnoreCase),
            "the search result");

        Assert.Contains("No bill", app.TextOf("ReportsStatus"));
    }
}
