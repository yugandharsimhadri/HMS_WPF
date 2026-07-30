using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pharma.App.Printing;

/// <summary>
/// The hospital letterhead that goes at the top of every printed document.
/// </summary>
/// <remarks>
/// <para>
/// One implementation for both ways this application puts ink on paper: the
/// FlowDocument templates (prescription, receipt, invoice) and the QuestPDF
/// report exports. A future template gets the letterhead by calling
/// <see cref="DocumentBuilder.AddClinicHeader"/> like the others, or
/// <see cref="TryGetBytes"/> if it builds a PDF.
/// </para>
/// <para>
/// The image is compiled into the executable as a resource rather than read
/// from disk beside it, so a single-file install cannot lose it and a clinic
/// cannot half-replace it. If it ever fails to load, every caller carries on
/// without a header rather than failing to print — a receipt with no letterhead
/// is a nuisance, a receipt that will not print is a queue at the desk.
/// </para>
/// </remarks>
internal static class DocumentHeaderImage
{
    /// <summary>
    /// Where the letterhead lives inside the assembly. An embedded resource read
    /// through the assembly rather than a <c>pack://</c> URI, because a pack URI
    /// resolves against the WPF <see cref="Application"/> and there is none in a
    /// test host — the letterhead would silently be absent exactly where its
    /// presence is being asserted.
    /// </summary>
    private const string ResourceName = "Pharma.App.Assets.Header.png";

    // Loaded once. Both fields are set together by EnsureLoaded and never
    // reset: a letterhead that could not be read will not start working later
    // in the same session, and retrying it per document would mean a failed
    // decode on every line of a busy day's printing.
    private static bool _loaded;
    private static BitmapImage? _image;
    private static byte[]? _bytes;

    /// <summary>
    /// The letterhead as a block that fills the width of whatever page it is
    /// placed on, or <see langword="null"/> when the image is unavailable.
    /// </summary>
    /// <remarks>
    /// Deliberately given no explicit width or height. The container takes the
    /// page's content width — which changes when <see cref="DocumentBuilder.Send"/>
    /// re-pages the document to the chosen printer's printable area — and
    /// <see cref="Stretch.Uniform"/> then scales the image to it, height
    /// following from the aspect ratio. Nothing is cropped and nothing is
    /// stretched out of shape on any paper size.
    /// </remarks>
    public static Block? TryCreateBlock()
    {
        if (TryGetImage() is not { } source) return null;

        var image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Stretch,

            // The letterhead carries small print — a phone number and an email
            // address — which bilinear scaling turns to mush at print sizes.
            SnapsToDevicePixels = true
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        return new BlockUIContainer(image)
        {
            // Flush to the top and to both edges: the page padding is the only
            // margin the letterhead should have, and the content starts
            // immediately under it.
            Margin = new Thickness(0, 0, 0, 8)
        };
    }

    /// <summary>
    /// The letterhead as PNG bytes, for the PDF exports, or
    /// <see langword="null"/> when the image is unavailable.
    /// </summary>
    public static byte[]? TryGetBytes()
    {
        EnsureLoaded();
        return _bytes;
    }

    private static BitmapImage? TryGetImage()
    {
        EnsureLoaded();
        return _image;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            using var stream =
                typeof(DocumentHeaderImage).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new FileNotFoundException($"{ResourceName} is not in the assembly.");

            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            _bytes = memory.ToArray();

            memory.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();

            // Cache on load: the stream is disposed as soon as this returns,
            // and OnDemand would leave the source reading from a closed stream
            // the first time a document actually rendered.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.EndInit();

            // Frozen so the same instance can be drawn from the print thread
            // as well as the UI one.
            image.Freeze();

            _image = image;

            AppLog.Info(
                $"Print letterhead loaded ({image.PixelWidth}x{image.PixelHeight}, {_bytes.Length / 1024:N0} KB).");
        }
        catch (Exception ex)
        {
            // Requirement, and the right call anyway: no letterhead must never
            // stop a document printing.
            _image = null;
            _bytes = null;

            AppLog.Warn($"Print letterhead unavailable; printing without it ({ex.Message}).");
        }
    }
}
