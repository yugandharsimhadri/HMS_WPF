using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;
using Xunit;

namespace Pharma.UiTests;

/// <summary>
/// Renders sample consultation receipts to PNG, from the same
/// <see cref="FeeReceiptDocument"/> the application prints — so a sample shown
/// to the clinic is the real document, not a mock-up of one that might drift
/// away from it.
///
/// Not a test of anything; it is skipped unless run by name, the same way
/// <c>ScreenshotCapture</c> is. Output lands in <c>docs/images/samples/</c>.
/// </summary>
public class ReceiptSampleCapture
{
    private static ClinicProfile Clinic() => new()
    {
        Name = "Twinkle Children's Hospital",
        AddressLine = "12-3-45, Beside Sai Temple, Kothapet, Hyderabad 500035",
        Phone = "040 2345 6789",
        FooterText = "Get well soon."
    };

    private static DocumentTheme Theme() => new() { Footer = "Get well soon." };

    private static Patient Anika() => new()
    {
        Name = "Baby Anika", PatientNo = "P00012", Age = 4, Gender = Gender.Female
    };

    private static Doctor Kumar() => new()
    {
        Name = "Dr. A. Kumar",
        Speciality = "Paediatrics",
        RegistrationNo = "REG-00000",
        Qualification = "MBBS, MD (Paediatrics)",
        ConsultationFee = 300m,
        OpdValidDays = 7
    };

    /// <summary>The first visit: charged, and opening a seven-day window.</summary>
    private static Visit NewVisit() => new()
    {
        Id = Guid.NewGuid(),
        VisitNo = "V00031",
        TokenNo = 4,
        ScheduledOn = new DateTime(2026, 9, 2, 10, 15, 0),
        Fee = 300m,
        FeePaid = true,
        FeeReceiptNo = "RCP00041",
        FeePaidOn = new DateTime(2026, 9, 2, 10, 18, 0),
        FeePaymentMode = PaymentMode.Cash,
        FreeFollowUpUntil = new DateTime(2026, 9, 9),
        Patient = Anika(),
        Doctor = Kumar()
    };

    /// <summary>The return four days later — inside the window, so nil.</summary>
    private static Visit RenewVisit(Visit paid) => new()
    {
        Id = Guid.NewGuid(),
        VisitNo = "V00048",
        TokenNo = 7,
        ScheduledOn = new DateTime(2026, 9, 6, 11, 40, 0),
        Fee = 0m,
        FeePaid = false,
        FeePaidOn = new DateTime(2026, 9, 6, 11, 42, 0),
        FeeWaivedAgainstVisitId = paid.Id,
        FeeWaivedAgainstVisit = paid,
        Patient = Anika(),
        Doctor = Kumar()
    };

    /// <summary>
    /// Paginates the document at A4 and renders the first page at 2x, so the
    /// 7pt print text is legible on screen.
    /// </summary>
    private static void Render(FlowDocument doc, string path)
    {
        const double pageWidth = 794;   // A4 at 96dpi
        const double pageHeight = 1123;
        const double scale = 2;

        doc.PageWidth = pageWidth;
        doc.PageHeight = pageHeight;
        doc.ColumnWidth = pageWidth;

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.ComputePageCount();

        using var page = paginator.GetPage(0);

        // The receipt occupies the top of the sheet; showing the whole of an
        // A4 page would be mostly empty space.
        const double showHeight = 470;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pageWidth, showHeight));
            dc.DrawRectangle(new VisualBrush(page.Visual) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                             null, new Rect(0, 0, pageWidth, showHeight));
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(
            (int)(pageWidth * scale), (int)(showHeight * scale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HMS_WPF.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    [StaFact]
    public void Capture_receipt_samples()
    {
        var samples = Path.Combine(Root(), "docs", "images", "samples");

        var paid = NewVisit();
        Render(FeeReceiptDocument.Build(paid, Clinic(), Theme()),
               Path.Combine(samples, "receipt-new.png"));

        Render(FeeReceiptDocument.Build(RenewVisit(paid), Clinic(), Theme()),
               Path.Combine(samples, "receipt-renew.png"));
    }
}
