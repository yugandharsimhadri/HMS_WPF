using System.Windows.Documents;
using System.Windows.Media;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.UiTests;

/// <summary>
/// Builds each printed document and reads the text back. No printer is involved,
/// so these run anywhere — what they guard is that the statutory details actually
/// reach the paper.
/// </summary>
public class PrintDocumentTests
{
    private static ShopProfile Shop() => new()
    {
        Name = "Twinkle Children's Hospital",
        AddressLine = "12 Main Road, Hyderabad",
        Phone = "040-1234567",
        Gstin = "36ABCDE1234F1Z5",
        DrugLicenceNo = "AP/21B/2024/1234",
        PharmacistName = "S. Rao",
        BillFooter = "Get well soon."
    };

    private static string TextOf(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    /// <summary>
    /// True when the document opens with the letterhead image, stretched to the
    /// page width rather than pinned to a size that would crop or distort it.
    /// </summary>
    internal static bool HasLetterhead(FlowDocument doc)
        => doc.Blocks.FirstOrDefault() is BlockUIContainer { Child: System.Windows.Controls.Image image }
           && image.Source is not null
           && image.Stretch == Stretch.Uniform
           && double.IsNaN(image.Width)
           && double.IsNaN(image.Height);

    private static Visit Visit(bool paid = true) => new()
    {
        VisitNo = "V00007",
        TokenNo = 7,
        ScheduledOn = new DateTime(2026, 7, 25, 10, 30, 0),
        Fee = 350m,
        FeePaid = paid,
        FeeReceiptNo = paid ? "RCP00004" : null,
        FeePaidOn = paid ? new DateTime(2026, 7, 25, 10, 35, 0) : null,
        FeePaymentMode = paid ? PaymentMode.Upi : null,
        Diagnosis = "Acute pharyngitis",
        FollowUpOn = new DateTime(2026, 8, 1),
        Patient = new Patient { Name = "Baby Anika", PatientNo = "P00012", Age = 4, Gender = Gender.Female },
        Doctor = new Doctor { Name = "Dr. A. Kumar", Speciality = "Paediatrics", RegistrationNo = "REG-4471" }
    };

    // ── Fee receipt ────────────────────────────────────────────────────────

    [StaFact]
    public void A_fee_receipt_carries_its_number_amount_and_payment_mode()
    {
        var text = TextOf(FeeReceiptDocument.Build(Visit(), Shop()));

        Assert.Contains("CONSULTATION RECEIPT", text);
        Assert.Contains("RCP00004", text);
        Assert.Contains("Baby Anika", text);
        Assert.Contains("Dr. A. Kumar", text);
        Assert.Contains("350.00", text);
        Assert.Contains("Upi", text);
        Assert.Contains("Rupees Three Hundred Fifty only", text);
    }

    [StaFact]
    public void A_reprinted_receipt_is_marked_duplicate()
    {
        Assert.Contains("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Shop(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Shop())));
    }

    [Theory]
    [InlineData(0, "Rupees Zero only")]
    [InlineData(7, "Rupees Seven only")]
    [InlineData(19, "Rupees Nineteen only")]
    [InlineData(350, "Rupees Three Hundred Fifty only")]
    [InlineData(1120, "Rupees One Thousand One Hundred Twenty only")]
    [InlineData(15334, "Rupees Fifteen Thousand Three Hundred Thirty Four only")]
    [InlineData(250000, "Rupees Two Lakh Fifty Thousand only")]
    public void Amounts_are_written_out_in_indian_words(int amount, string expected)
        => Assert.Equal(expected, FeeReceiptDocument.InWords(amount));

    [Fact]
    public void Paise_are_spelled_out_too()
        => Assert.Equal("Rupees Ninety Three and Eighteen Paise only", FeeReceiptDocument.InWords(93.18m));

    // ── Prescription ───────────────────────────────────────────────────────

    [StaFact]
    public void A_prescription_carries_the_doctors_registration_and_every_medicine()
    {
        var visit = Visit();
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "Paracetamol 250mg", Dosage = "5 ml", Frequency = "1-1-1", Days = 3, Quantity = 1
        });
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "ORS Powder", Dosage = "1 sachet", Frequency = "SOS", Days = 2, Quantity = 4,
            Instructions = "Dissolve in 200 ml water"
        });

        var doc = PrescriptionPrinter.Build(visit, Shop());
        var text = TextOf(doc);

        // The hospital is named by the letterhead at the top of the page rather
        // than by a line of text under it — see LetterheadTests below.
        Assert.True(HasLetterhead(doc));

        Assert.Contains("Reg. No: REG-4471", text);
        Assert.Contains("Acute pharyngitis", text);
        Assert.Contains("Paracetamol 250mg", text);
        Assert.Contains("ORS Powder", text);
        Assert.Contains("Dissolve in 200 ml water", text);
        Assert.Contains("Review on 01 Aug 2026", text);
    }

    [StaFact]
    public void A_prescription_with_no_medicines_still_prints()
    {
        var text = TextOf(PrescriptionPrinter.Build(Visit(), Shop()));

        Assert.Contains("No medicines prescribed.", text);
    }

    // ── Pharmacy bill ──────────────────────────────────────────────────────

    private static Sale Sale() => new()
    {
        BillNo = "INV00021",
        BillDate = new DateTime(2026, 7, 25, 11, 5, 0),
        CustomerName = "Baby Anika",
        DoctorName = "Dr. A. Kumar",
        PaymentMode = PaymentMode.Cash,
        IsTaxInvoice = true,
        GrossAmount = 1120m,
        TaxableAmount = 1000m,
        CgstAmount = 60m,
        SgstAmount = 60m,
        RoundOff = 0m,
        NetAmount = 1120m,
        Items =
        [
            new SaleItem
            {
                ProductName = "RELENT PLUS SYRUP 60ML", BatchNo = "D260374",
                ExpiryDate = new DateTime(2028, 4, 30), HsnCode = "30049099",
                Quantity = 10, Mrp = 112m, GstRate = 12m,
                TaxableAmount = 1000m, GstAmount = 120m, LineTotal = 1120m
            }
        ]
    };

    [StaFact]
    public void A_bill_shows_batch_expiry_and_hsn_against_every_line()
    {
        var text = TextOf(BillPrinter.Build(Sale(), Shop()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("INV00021", text);
        Assert.Contains("RELENT PLUS SYRUP 60ML", text);
        Assert.Contains("D260374", text);       // batch — legally required
        Assert.Contains("04/28", text);         // expiry — legally required
        Assert.Contains("30049099", text);      // HSN
    }

    [StaFact]
    public void A_bill_shows_the_licences_and_the_gst_split()
    {
        var text = TextOf(BillPrinter.Build(Sale(), Shop()));

        Assert.Contains("GSTIN: 36ABCDE1234F1Z5", text);
        Assert.Contains("D.L. No: AP/21B/2024/1234", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
        Assert.Contains("SGST", text);
        Assert.Contains("Rupees One Thousand One Hundred Twenty only", text);
        Assert.Contains("S. Rao", text);
    }

    // ── GST registered, or not ─────────────────────────────────────────────

    [StaFact]
    public void An_unregistered_clinic_issues_a_plain_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = false;

        // No tax was charged, so none is shown.
        sale.CgstAmount = sale.SgstAmount = 0m;
        sale.TaxableAmount = sale.NetAmount;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Shop()));

        Assert.Contains("INVOICE", text);
        Assert.DoesNotContain("TAX INVOICE", text);

        // Claiming a GSTIN without being registered would be a false statement.
        Assert.DoesNotContain("GSTIN", text);
        Assert.DoesNotContain("GST SUMMARY", text);
        Assert.DoesNotContain("CGST", text);

        // The drug licence is still required, and the medicine details still show.
        Assert.Contains("D.L. No: AP/21B/2024/1234", text);
        Assert.Contains("D260374", text);
        Assert.Contains("INV00021", text);
    }

    [StaFact]
    public void A_registered_clinic_issues_a_tax_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = true;

        var text = TextOf(BillPrinter.Build(sale, Shop()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN: 36ABCDE1234F1Z5", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
    }

    [StaFact]
    public void A_registered_clinic_selling_zero_rated_goods_still_issues_a_tax_invoice()
    {
        // Being a tax invoice is about registration, not about whether this
        // particular basket happened to carry tax.
        var sale = Sale();
        sale.IsTaxInvoice = true;
        sale.CgstAmount = sale.SgstAmount = 0m;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Shop()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN", text);
    }

    [StaFact]
    public void A_reprinted_bill_is_marked_duplicate()
    {
        Assert.Contains("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Shop(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Shop())));
    }

    [StaFact]
    public void A_bill_prints_even_when_the_shop_details_were_never_filled_in()
    {
        // A clinic that has not visited Settings yet must still be able to bill.
        var text = TextOf(BillPrinter.Build(Sale(), new ShopProfile()));

        Assert.Contains("INV00021", text);
        Assert.Contains("RELENT PLUS SYRUP 60ML", text);
        Assert.DoesNotContain("GSTIN:", text);
    }

    [StaFact]
    public void A_long_bill_keeps_every_line()
    {
        var sale = Sale();
        sale.Items.Clear();

        for (var i = 1; i <= 60; i++)
        {
            sale.Items.Add(new SaleItem
            {
                ProductName = $"MEDICINE {i:D2}", BatchNo = $"B{i:D4}",
                ExpiryDate = new DateTime(2028, 1, 31), HsnCode = "30049099",
                Quantity = 1, Mrp = 10m, GstRate = 5m,
                TaxableAmount = 9.52m, GstAmount = 0.48m, LineTotal = 10m
            });
        }

        var text = TextOf(BillPrinter.Build(sale, Shop()));

        Assert.Contains("MEDICINE 01", text);
        Assert.Contains("MEDICINE 60", text);
    }

    // ── Print-safe palette ─────────────────────────────────────────────────
    //
    // Regression guard for a bug where the preview page picked up a dark
    // background from somewhere while the ink stayed dark too, making a
    // receipt unreadable until the text was selected. Every document must
    // carry its own literal white/black — never null, never a theme colour —
    // so this can never come back silently.

    private static void AssertPrintSafe(FlowDocument doc)
    {
        var background = Assert.IsType<SolidColorBrush>(doc.Background);
        Assert.Equal(Colors.White, background.Color);

        var foreground = Assert.IsType<SolidColorBrush>(doc.Foreground);
        Assert.Equal(Colors.Black, foreground.Color);
    }

    [StaFact]
    public void A_fee_receipt_is_black_ink_on_a_white_page()
        => AssertPrintSafe(FeeReceiptDocument.Build(Visit(), Shop()));

    [StaFact]
    public void A_prescription_is_black_ink_on_a_white_page()
        => AssertPrintSafe(PrescriptionPrinter.Build(Visit(), Shop()));

    [StaFact]
    public void A_bill_is_black_ink_on_a_white_page()
        => AssertPrintSafe(BillPrinter.Build(Sale(), Shop()));
}
