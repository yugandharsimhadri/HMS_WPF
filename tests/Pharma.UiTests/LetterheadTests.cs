using System.Windows.Documents;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.UiTests;

/// <summary>
/// The letterhead at the top of every printed document.
/// </summary>
/// <remarks>
/// Every template goes through <c>DocumentBuilder.AddClinicHeader</c>, so these
/// guard the one implementation the whole application shares — including any
/// template added later, which gets the letterhead by calling the same method.
/// </remarks>
public class LetterheadTests
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

    private static Visit Visit() => new()
    {
        VisitNo = "V00007",
        TokenNo = 7,
        ScheduledOn = new DateTime(2026, 7, 25, 10, 30, 0),
        Fee = 350m,
        FeePaid = true,
        FeeReceiptNo = "RCP00004",
        FeePaidOn = new DateTime(2026, 7, 25, 10, 35, 0),
        FeePaymentMode = PaymentMode.Upi,
        Patient = new Patient { Name = "Baby Anika", PatientNo = "P00012", Age = 4, Gender = Gender.Female },
        Doctor = new Doctor { Name = "Dr. A. Kumar", Speciality = "Paediatrics", RegistrationNo = "REG-4471" }
    };

    private static Sale Sale() => new()
    {
        BillNo = "INV00021",
        BillDate = new DateTime(2026, 7, 25, 11, 5, 0),
        CustomerName = "Baby Anika",
        NetAmount = 118m,
        TaxableAmount = 105.36m,
        CgstAmount = 6.32m,
        SgstAmount = 6.32m,
        IsTaxInvoice = true,
        Items =
        [
            new SaleItem
            {
                ProductName = "Paracetamol 250mg", BatchNo = "B12", Quantity = 10, UnitsPerPack = 10,
                Mrp = 118m, GstRate = 12m, TaxableAmount = 105.36m, GstAmount = 12.64m, LineTotal = 118m,
                HsnCode = "3004", ExpiryDate = new DateTime(2028, 3, 31)
            }
        ]
    };

    // ── The letterhead reaches every document ──────────────────────────────

    [StaFact]
    public void A_consultation_receipt_opens_with_the_letterhead()
        => Assert.True(PrintDocumentTests.HasLetterhead(FeeReceiptDocument.Build(Visit(), Shop())));

    [StaFact]
    public void A_prescription_opens_with_the_letterhead()
        => Assert.True(PrintDocumentTests.HasLetterhead(PrescriptionPrinter.Build(Visit(), Shop())));

    [StaFact]
    public void A_pharmacy_invoice_opens_with_the_letterhead()
        => Assert.True(PrintDocumentTests.HasLetterhead(BillPrinter.Build(Sale(), Shop())));

    [StaFact]
    public void A_reprint_opens_with_the_letterhead_too()
    {
        Assert.True(PrintDocumentTests.HasLetterhead(BillPrinter.Build(Sale(), Shop(), isReprint: true)));
        Assert.True(PrintDocumentTests.HasLetterhead(FeeReceiptDocument.Build(Visit(), Shop(), isReprint: true)));
    }

    // ── It is used as supplied ─────────────────────────────────────────────

    [StaFact]
    public void The_letterhead_keeps_the_proportions_of_the_supplied_image()
    {
        var doc = FeeReceiptDocument.Build(Visit(), Shop());
        var image = (System.Windows.Controls.Image)((BlockUIContainer)doc.Blocks.First()).Child;

        // 1900x828 as provided. Asserted so that replacing the file with a
        // differently shaped one is a decision somebody makes on purpose.
        var source = (System.Windows.Media.Imaging.BitmapImage)image.Source;
        Assert.Equal(1900, source.PixelWidth);
        Assert.Equal(828, source.PixelHeight);

        // No fixed size and a uniform stretch: it fills the printable width and
        // takes its height from the aspect ratio, so nothing is cropped and
        // nothing is squashed on any paper size.
        Assert.True(double.IsNaN(image.Width));
        Assert.True(double.IsNaN(image.Height));
        Assert.Equal(System.Windows.Media.Stretch.Uniform, image.Stretch);
    }

    [StaFact]
    public void The_content_starts_immediately_below_the_letterhead()
    {
        var doc = FeeReceiptDocument.Build(Visit(), Shop());

        // Nothing between the image and the rest of the document, and the image
        // is flush to the top of the page's content area.
        var letterhead = (BlockUIContainer)doc.Blocks.First();
        Assert.Equal(0, letterhead.Margin.Top);
        Assert.Equal(0, letterhead.Margin.Left);
        Assert.Equal(0, letterhead.Margin.Right);

        Assert.True(doc.Blocks.Count > 1, "The document has content after the letterhead.");
    }

    // ── What the letterhead does not carry, the page still must ────────────

    [StaFact]
    public void The_licence_numbers_are_still_printed_because_the_letterhead_lacks_them()
    {
        // The image carries the name, phones, email, website and address, so
        // those are not repeated underneath it. GSTIN and the drug licence are
        // not on the image and a tax invoice has to show them.
        var bill = BillPrinter.Build(Sale(), Shop());
        var text = new TextRange(bill.ContentStart, bill.ContentEnd).Text;

        Assert.Contains("36ABCDE1234F1Z5", text);
        Assert.Contains("AP/21B/2024/1234", text);

        // Said once, by the letterhead, not twice.
        Assert.DoesNotContain("12 Main Road, Hyderabad", text);
    }
}
