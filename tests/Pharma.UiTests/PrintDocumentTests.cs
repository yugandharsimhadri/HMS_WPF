using System.Windows.Documents;
using System.Windows.Media;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.UiTests;

/// <summary>
/// Builds each printed document and reads the text back. No printer is involved,
/// so these run anywhere — what they guard is that the statutory details actually
/// reach the paper, and that a document only ever carries the identity it is
/// meant to speak as.
/// </summary>
public class PrintDocumentTests
{
    private static ClinicProfile Clinic() => new()
    {
        Name = "Twinkle Children's Hospital",
        AddressLine = "12 Main Road, Hyderabad",
        Phone = "040-1234567"
    };

    private static PharmacyProfile Pharmacy() => new()
    {
        Name = "Twinkle Pharmacy",
        AddressLine = "12 Main Road, Hyderabad",
        Phone = "040-1234567",
        Gstin = "36ABCDE1234F1Z5",
        DrugLicenceNo = "AP/21B/2024/1234",
        PharmacistName = "S. Rao"
    };

    private static DocumentTheme Theme() => new() { Footer = "Get well soon." };

    private static string TextOf(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

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
        var text = TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("CASH RECEIPT", text);
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
        Assert.Contains("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme())));
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

        var doc = PrescriptionPrinter.Build(visit, Clinic(), Theme());
        var text = TextOf(doc);

        Assert.Contains("Twinkle Children's Hospital", text);
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
        var text = TextOf(PrescriptionPrinter.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("No medicines prescribed.", text);
    }

    // ── The clinic/pharmacy split: neither identity carries the other's credentials ──

    [StaFact]
    public void A_prescription_never_carries_the_drug_licence_number()
    {
        // A drug licence is a pharmacy credential. A GST-registered clinic
        // would previously have had the pharmacy's GSTIN and licence printed
        // on a doctor's own document by accident, because both used to share
        // one identity. Neither should ever appear here now.
        var clinic = Clinic();
        clinic.GstRegistered = true;
        clinic.Gstin = "36ABCDE1234F1Z5";

        var text = TextOf(PrescriptionPrinter.Build(Visit(), clinic, Theme()));

        Assert.DoesNotContain("D.L. No", text);
    }

    [StaFact]
    public void A_prescription_shows_the_clinics_own_gstin_only_when_the_clinic_is_registered()
    {
        var registered = Clinic();
        registered.GstRegistered = true;
        registered.Gstin = "36CLINIC1234F1Z5";
        Assert.Contains("36CLINIC1234F1Z5", TextOf(PrescriptionPrinter.Build(Visit(), registered, Theme())));

        var unregistered = Clinic();
        unregistered.GstRegistered = false;
        unregistered.Gstin = "36CLINIC1234F1Z5";
        Assert.DoesNotContain("36CLINIC1234F1Z5", TextOf(PrescriptionPrinter.Build(Visit(), unregistered, Theme())));
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
        var text = TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

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
        var text = TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

        Assert.Contains("36ABCDE1234F1Z5", text);
        Assert.Contains("AP/21B/2024/1234", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
        Assert.Contains("SGST", text);
        Assert.Contains("Rupees One Thousand One Hundred Twenty only", text);
        Assert.Contains("S. Rao", text);
    }

    // ── GST registered, or not ─────────────────────────────────────────────

    [StaFact]
    public void An_unregistered_pharmacy_issues_a_plain_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = false;

        // No tax was charged, so none is shown.
        sale.CgstAmount = sale.SgstAmount = 0m;
        sale.TaxableAmount = sale.NetAmount;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

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
    public void A_registered_pharmacy_issues_a_tax_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = true;

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN: 36ABCDE1234F1Z5", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
    }

    [StaFact]
    public void A_registered_pharmacy_selling_zero_rated_goods_still_issues_a_tax_invoice()
    {
        // Being a tax invoice is about registration, not about whether this
        // particular basket happened to carry tax.
        var sale = Sale();
        sale.IsTaxInvoice = true;
        sale.CgstAmount = sale.SgstAmount = 0m;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN", text);
    }

    [StaFact]
    public void A_reprinted_bill_is_marked_duplicate()
    {
        Assert.Contains("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme())));
    }

    [StaFact]
    public void A_bill_prints_even_when_the_pharmacy_details_were_never_filled_in()
    {
        // A clinic that has not visited Settings yet must still be able to bill.
        var text = TextOf(BillPrinter.Build(Sale(), new PharmacyProfile { Gstin = "" }, new DocumentTheme()));

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

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

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
        => AssertPrintSafe(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void A_prescription_is_black_ink_on_a_white_page()
        => AssertPrintSafe(PrescriptionPrinter.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void A_bill_is_black_ink_on_a_white_page()
        => AssertPrintSafe(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

    // ── Every paragraph carries its own ink ─────────────────────────────────
    //
    // A stronger guard than AssertPrintSafe above, added after a real bug:
    // the identity header's clinic name and document title printed in the
    // app's teal accent colour and the contact line printed blue and
    // underlined, on a real clinic's machine, because two Paragraphs in
    // DocumentBuilder.IdentityBlock left Foreground unset and inherited
    // whatever the hosting window's resources supplied instead of the
    // literal black AssertPrintSafe checks at the document level. That
    // check passed the whole time — doc.Foreground was correctly black, the
    // paragraph just did not inherit it the way it was assumed to. This
    // walks every actual Paragraph a built document contains and fails if
    // any one of them was left to inherit rather than told what colour it is.

    private static IEnumerable<Paragraph> AllParagraphs(FlowDocument doc)
        => doc.Blocks.SelectMany(AllParagraphsIn);

    private static IEnumerable<Paragraph> AllParagraphsIn(Block block)
    {
        switch (block)
        {
            case Paragraph p:
                yield return p;
                break;

            case Section s:
                foreach (var inner in s.Blocks.SelectMany(AllParagraphsIn))
                    yield return inner;
                break;

            case Table t:
                foreach (var inner in t.RowGroups
                             .SelectMany(g => g.Rows)
                             .SelectMany(r => r.Cells)
                             .SelectMany(c => c.Blocks)
                             .SelectMany(AllParagraphsIn))
                    yield return inner;
                break;
        }
    }

    // The two literal inks DocumentBuilder prints with (PrintForeground and
    // PrintSecondaryForeground). Duplicated here rather than referenced,
    // because DocumentBuilder is internal to Pharma.App and this is the
    // check that must not pass by accident: asserting only "some
    // SolidColorBrush" would have let the real bug through, since the
    // theme's accent green and a Hyperlink's default blue are each a
    // SolidColorBrush too — checking the exact colour is the point.
    private static readonly Color BlackInk = Color.FromRgb(0x00, 0x00, 0x00);
    private static readonly Color MutedInk = Color.FromRgb(0x33, 0x33, 0x33);

    private static void AssertEveryParagraphHasAnExplicitBrush(FlowDocument doc)
    {
        var paragraphs = AllParagraphs(doc).ToList();
        Assert.NotEmpty(paragraphs);

        foreach (var paragraph in paragraphs)
        {
            var brush = Assert.IsType<SolidColorBrush>(paragraph.Foreground);
            Assert.True(brush.Color == BlackInk || brush.Color == MutedInk,
                $"A paragraph printed in {brush.Color}, not the black or muted grey every " +
                $"document is meant to be. Text: \"{new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim()}\"");
        }
    }

    [StaFact]
    public void Every_paragraph_on_the_receipt_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void Every_paragraph_on_the_prescription_has_its_own_brush()
    {
        var visit = Visit();
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "Paracetamol 250mg", Dosage = "5 ml", Frequency = "1-1-1", Days = 3, Quantity = 1
        });

        AssertEveryParagraphHasAnExplicitBrush(PrescriptionPrinter.Build(visit, Clinic(), Theme()));
    }

    [StaFact]
    public void Every_paragraph_on_the_bill_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(BillPrinter.Build(Sale(), Pharmacy(), Theme()));
}
