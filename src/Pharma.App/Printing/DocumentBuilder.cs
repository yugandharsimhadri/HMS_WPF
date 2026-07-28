using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pharma.App.Printing;

/// <summary>Small helpers so the two print templates stay readable.</summary>
internal static class DocumentBuilder
{
    // ── Print-safe palette ───────────────────────────────────────────────
    //
    // A printed page and its on-screen preview must read the same way on
    // every PC regardless of the app's light/dark theme, the Windows theme,
    // or any future WPF default — paper is white and ink is dark, always.
    // These four brushes are the only colours a printable document may use;
    // nothing here is a DynamicResource, and nothing here is looked up from
    // Theme.xaml, so switching the app's theme cannot repaint a receipt.
    //
    // Every FlowDocument built below also sets Background/Foreground on the
    // document itself (see NewDocument), not just on each Run — so even a
    // paragraph added later that forgets to set a brush still renders black
    // on white instead of silently inheriting whatever the page turns out
    // to default to.
    public static readonly Brush PrintPageBackground = Frozen(0xFF, 0xFF, 0xFF);
    public static readonly Brush PrintForeground = Frozen(0x00, 0x00, 0x00);
    public static readonly Brush PrintSecondaryForeground = Frozen(0x33, 0x33, 0x33);
    public static readonly Brush PrintBorderBrush = Frozen(0x44, 0x44, 0x44);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    // Old names kept as aliases so every call site below reads the same as
    // it always has; the values are the print-safe ones above.
    public static readonly Brush Muted = PrintSecondaryForeground;
    public static readonly Brush Line = PrintBorderBrush;

    /// <summary>
    /// The clinic identity block every printed document opens with. Kept in one
    /// place so a bill, a receipt and a prescription can never disagree about the
    /// shop's name, GSTIN or licence number.
    /// </summary>
    public static void AddClinicHeader(
        FlowDocument doc, Pharma.Data.ShopProfile shop, string? documentTitle, bool showGstin = true)
    {
        doc.Blocks.Add(Text(shop.Name, 18, FontWeights.Bold, align: TextAlignment.Center));

        var contact = new List<string>();
        if (!string.IsNullOrWhiteSpace(shop.AddressLine)) contact.Add(shop.AddressLine);
        if (!string.IsNullOrWhiteSpace(shop.Phone)) contact.Add($"Ph {shop.Phone}");
        if (contact.Count > 0)
            doc.Blocks.Add(Text(string.Join("  ·  ", contact), 10, brush: Muted, align: TextAlignment.Center));

        var licences = new List<string>();

        // A GSTIN belongs only on a document that is actually a tax invoice.
        if (showGstin && !string.IsNullOrWhiteSpace(shop.Gstin)) licences.Add($"GSTIN: {shop.Gstin}");

        // The drug licence is required whether or not the shop charges GST.
        if (!string.IsNullOrWhiteSpace(shop.DrugLicenceNo)) licences.Add($"D.L. No: {shop.DrugLicenceNo}");
        if (licences.Count > 0)
            doc.Blocks.Add(Text(string.Join("  ·  ", licences), 10, brush: Muted, align: TextAlignment.Center));

        if (!string.IsNullOrWhiteSpace(documentTitle))
            doc.Blocks.Add(Text(documentTitle, 11, FontWeights.SemiBold, align: TextAlignment.Center, topMargin: 8));

        doc.Blocks.Add(Rule());
    }

    public static FlowDocument NewDocument() => new()
    {
        PageWidth = 794,           // A4 at 96 dpi
        PagePadding = new Thickness(40),
        ColumnWidth = double.MaxValue,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 11,
        // Pinned so the page is always paper-white with black ink, whatever
        // the app's theme is doing — see the print-safe palette above.
        Background = PrintPageBackground,
        Foreground = PrintForeground
    };

    public static Paragraph Text(string text, double size = 11, FontWeight? weight = null,
                                 Brush? brush = null, TextAlignment align = TextAlignment.Left,
                                 double topMargin = 0, double bottomMargin = 0)
        => new(new Run(text))
        {
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = brush ?? PrintForeground,
            TextAlignment = align,
            Margin = new Thickness(0, topMargin, 0, bottomMargin)
        };

    public static Block Rule() => new BlockUIContainer(new Border
    {
        BorderBrush = Line,
        BorderThickness = new Thickness(0, 1, 0, 0),
        Margin = new Thickness(0, 6, 0, 6)
    });

    public static Table NewTable(params double[] widths)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };
        foreach (var w in widths) table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });
        return table;
    }

    public static TableRow Row(bool header, params string[] cells)
    {
        var row = new TableRow();

        foreach (var (value, index) in cells.Select((v, i) => (v, i)))
        {
            var paragraph = new Paragraph(new Run(value))
            {
                Margin = new Thickness(4, 3, 4, 3),
                FontSize = header ? 9.5 : 10.5,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = header ? PrintSecondaryForeground : PrintForeground,
                // Everything after the first two columns is numeric.
                TextAlignment = index >= 2 ? TextAlignment.Right : TextAlignment.Left
            };

            row.Cells.Add(new TableCell(paragraph)
            {
                BorderBrush = Line,
                BorderThickness = new Thickness(0, 0, 0, header ? 1 : 0.5)
            });
        }

        return row;
    }

    /// <summary>Shows the print dialog and sends the document to the chosen printer.</summary>
    public static void Send(FlowDocument document, string jobName)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;

        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, jobName);
    }
}
