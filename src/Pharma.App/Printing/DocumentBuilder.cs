using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pharma.Data;

namespace Pharma.App.Printing;

/// <summary>Small helpers so the print templates stay readable.</summary>
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
    public static readonly Brush PrintBorderBrush = Frozen(0x22, 0x20, 0x18);

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
    /// The clinic identity block a prescription and a fee receipt open with —
    /// boxed, matching the reference print those two are laid out from. Never
    /// shows a drug licence number: that is a pharmacy credential, and a
    /// doctor's own document never carried one on paper either. Shows GSTIN
    /// only when the clinic itself, not the pharmacy, is registered.
    /// </summary>
    public static void AddClinicHeader(
        FlowDocument doc, ClinicProfile clinic, DocumentTheme theme, string? documentTitle)
    {
        var (line1, line2) = ContactLines(clinic.AddressLine, clinic.AddressLine2, clinic.Phone);
        doc.Blocks.Add(IdentityBlock(
            clinic.Name, line1, line2,
            clinic.GstRegistered && !string.IsNullOrWhiteSpace(clinic.Gstin) ? $"GSTIN: {clinic.Gstin}" : null,
            documentTitle, theme, boxed: true, nameFontSize: ClinicNameFontSize));
    }

    /// <summary>
    /// The clinic's own name prints two points larger than every other
    /// identity name on a document — a specific ask, not derived from
    /// anything else on the page.
    /// </summary>
    private const double ClinicNameFontSize = IdentityNameFontSize + 2;

    private const double IdentityNameFontSize = 13;

    /// <summary>
    /// The pharmacy identity block a bill opens with — unboxed, matching its
    /// own reference print. Carries the GSTIN, which a retail chemist's tax
    /// invoice is required to show here; the drug licence number prints as
    /// its own labelled field in the identity grid below instead, the same
    /// way the bill number, the date and the patient do — see
    /// <see cref="BillPrinter"/>.
    /// </summary>
    public static void AddPharmacyHeader(
        FlowDocument doc, PharmacyProfile pharmacy, DocumentTheme theme, string? documentTitle, bool showGstin)
    {
        var licence = showGstin && !string.IsNullOrWhiteSpace(pharmacy.Gstin) ? $"GSTIN: {pharmacy.Gstin}" : null;

        var (line1, line2) = ContactLines(pharmacy.AddressLine, pharmacy.AddressLine2, pharmacy.Phone);
        doc.Blocks.Add(IdentityBlock(
            pharmacy.Name, line1, line2, licence,
            documentTitle, theme, boxed: false, nameFontSize: IdentityNameFontSize));
    }

    /// <summary>
    /// Address line 1, address line 2 and the phone number, folded down to
    /// at most two printed lines: line 1 is address line 1 alone; line 2 is
    /// address line 2 and the phone together, so a clinic with only one
    /// address line and a phone still gets a tidy two-line header instead of
    /// a lone "Ph …" line of its own. Whichever piece is missing is simply
    /// left out — an address without a line 2, or without a phone, still
    /// prints cleanly.
    /// </summary>
    private static (string? Line1, string? Line2) ContactLines(string? address1, string? address2, string? phone)
    {
        var line1 = string.IsNullOrWhiteSpace(address1) ? null : address1;

        var line2Parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(address2)) line2Parts.Add(address2);
        if (!string.IsNullOrWhiteSpace(phone)) line2Parts.Add($"Ph {phone}");
        var line2 = line2Parts.Count > 0 ? string.Join("   |   ", line2Parts) : null;

        return line1 is null ? (line2, null) : (line1, line2);
    }

    /// <summary>
    /// How much of the header's width the logo column gets when there is a
    /// logo — 28 of 100 shares, the middle of the "25 to 30%" asked for,
    /// with the name, contact line and licences in the 72 that are left.
    /// </summary>
    private const int LogoColumnShare = 28;

    /// <summary>
    /// Builds the identity block itself. Two shapes, chosen by whether a logo
    /// is configured:
    /// <list type="bullet">
    /// <item>**With a logo** — a column <see cref="LogoColumnShare"/>% wide on
    /// the left holding it, the name and the rest of the text in the column
    /// beside it, both filling the header's full height. The text is centred
    /// within that column — plain centre alignment, the same as centring a
    /// paragraph in a word processor, not shifted or reflowed to account for
    /// the logo beside it.</item>
    /// <item>**Without one** — the name and text alone, centred across the
    /// full width, exactly as before a logo was ever uploaded.</item>
    /// </list>
    /// Boxed for the clinic's own documents, plain for the pharmacy's, per
    /// each one's own reference print.
    /// </summary>
    /// <remarks>
    /// The box itself is a <see cref="Section"/>, not a <see cref="Table"/>
    /// cell — <see cref="Block"/> (which <see cref="Section"/> is one of) has
    /// carried its own <see cref="Block.BorderBrush"/>/
    /// <see cref="Block.BorderThickness"/>/<see cref="Block.Padding"/> since
    /// .NET 4.5. The two-column split, when there is a logo, is a
    /// <see cref="Table"/> nested <em>inside</em> that section with **no
    /// border on either cell** — the border a first attempt at this put on
    /// two adjacent cells, at different thicknesses on the edge they share,
    /// tripped a known WPF bug in <see cref="Table"/>'s row-height
    /// measurement and printed a blank box a third of a page tall above the
    /// header. Nesting a borderless table inside an already-bordered section
    /// gets the same two-column layout without giving that bug anything to
    /// trip on. Every <see cref="Paragraph"/> below also carries an explicit
    /// <see cref="TextElement.Foreground"/> rather than inheriting one — a
    /// second, unrelated bug (documents picking up the app's own accent
    /// colour) came from two paragraphs here relying on inheritance instead.
    /// </remarks>
    private static Block IdentityBlock(
        string name, string? contactLine1, string? contactLine2, string? licenceLine, string? documentTitle,
        DocumentTheme theme, bool boxed, double nameFontSize)
    {
        var section = new Section
        {
            Margin = new Thickness(0, 0, 0, 4),
            Padding = boxed ? new Thickness(6, 4, 6, 4) : new Thickness(0),
            Foreground = PrintForeground
        };

        if (boxed)
        {
            section.BorderBrush = PrintBorderBrush;
            section.BorderThickness = new Thickness(1);
        }

        var text = IdentityText(name, contactLine1, contactLine2, licenceLine, documentTitle, theme, boxed, nameFontSize);

        if (LogoElement(theme) is not { } logo)
        {
            foreach (var block in text) section.Blocks.Add(block);
            return section;
        }

        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(LogoColumnShare, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100 - LogoColumnShare, GridUnitType.Star) });

        var logoCell = new TableCell { BorderThickness = new Thickness(0), Padding = new Thickness(4) };
        logoCell.Blocks.Add(new BlockUIContainer(logo));

        var textCell = new TableCell { BorderThickness = new Thickness(0) };
        foreach (var block in text) textCell.Blocks.Add(block);

        var row = new TableRow();
        row.Cells.Add(logoCell);
        row.Cells.Add(textCell);

        var group = new TableRowGroup();
        group.Rows.Add(row);
        table.RowGroups.Add(group);

        section.Blocks.Add(table);
        return section;
    }

    /// <summary>The name, up to two contact lines, licences and document
    /// title, as freshly built blocks — homed under whichever parent the
    /// caller has for them, since a <see cref="Block"/> can only belong to
    /// one. Always centred, plainly — the way centring a paragraph works in
    /// a word processor, regardless of what else shares the header.</summary>
    private static List<Block> IdentityText(
        string name, string? contactLine1, string? contactLine2, string? licenceLine, string? documentTitle,
        DocumentTheme theme, bool boxed, double nameFontSize)
    {
        var titleParagraph = new Paragraph(new Run(name))
        {
            FontSize = nameFontSize + theme.TitleFontSizeDelta,
            FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
            Foreground = PrintForeground
        };

        // Left unset — not defaulted to "Segoe UI" here — so it inherits
        // whatever the rest of the document is printing in (the general
        // print font, itself set on the FlowDocument by ApplyPrintSettings)
        // whenever the clinic has not asked for the name to stand out in a
        // face of its own.
        if (!string.IsNullOrWhiteSpace(theme.TitleFontFamily))
            titleParagraph.FontFamily = new FontFamily(theme.TitleFontFamily);

        var blocks = new List<Block> { titleParagraph };

        if (contactLine1 is not null)
            blocks.Add(new Paragraph(new Run(contactLine1))
            {
                FontSize = 8, TextAlignment = TextAlignment.Center,
                Foreground = PrintSecondaryForeground, Margin = new Thickness(0, 1, 0, 0)
            });

        if (contactLine2 is not null)
            blocks.Add(new Paragraph(new Run(contactLine2))
            {
                FontSize = 8, TextAlignment = TextAlignment.Center, Foreground = PrintSecondaryForeground
            });

        if (licenceLine is not null)
            blocks.Add(new Paragraph(new Run(licenceLine))
            {
                FontSize = 8, TextAlignment = TextAlignment.Center, Foreground = PrintSecondaryForeground
            });

        if (!string.IsNullOrWhiteSpace(documentTitle))
            blocks.Add(new Paragraph(new Run(documentTitle))
            {
                FontSize = 8.6, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
                TextDecorations = boxed ? TextDecorations.Underline : null,
                Margin = new Thickness(0, 4, 0, 0), Foreground = PrintForeground
            });

        return blocks;
    }

    /// <summary>
    /// The uploaded logo, filling its column left-to-right and top-to-bottom
    /// — or <see langword="null"/> when none is configured. There is
    /// deliberately no fallback to a compiled default image here: a shrunken
    /// copy of a banner designed to be read at full width is not a logo, it
    /// is an illegible postage stamp. Until a logo is uploaded under Reports,
    /// the header prints as text only, centred across the full width —
    /// exactly what already happens today whenever the compiled banner fails
    /// to load.
    /// </summary>
    /// <remarks>
    /// No fixed <see cref="FrameworkElement.Width"/>/<see cref="FrameworkElement.Height"/>
    /// here — the column's width is already fixed by the <see cref="Table"/>'s
    /// star sizing, and the row's height is already fixed by the text column
    /// beside it, so the image only needs <see cref="FrameworkElement.MaxWidth"/>/
    /// <see cref="FrameworkElement.MaxHeight"/> as a backstop against a very
    /// large or very oddly-shaped source image inflating the row past what
    /// the text column would otherwise need — the same kind of unbounded
    /// growth that produced the giant blank header box <see cref="IdentityBlock"/>
    /// documents. <see cref="Stretch.Uniform"/> then fits the actual image
    /// inside that box without distorting it, letterboxing whichever side
    /// doesn't match the box's own proportions.
    /// </remarks>
    private static UIElement? LogoElement(DocumentTheme theme)
    {
        if (DecodeLogo(theme) is not { } source) return null;

        var image = new Image
        {
            Source = source,
            MaxWidth = LogoColumnMaxWidth,
            MaxHeight = LogoColumnMaxHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    /// <summary>
    /// The logo column's own ceiling, in the same device-independent units
    /// as everything else on the page (96 per inch) — derived from the page
    /// geometry itself rather than picked by eye, and the two numbers
    /// <c>SettingsViewModel</c>'s upload bounds are sized from.
    ///
    /// <see cref="NewDocument"/> is an A4 page, 794 DIU wide, with 30 DIU of
    /// padding on each side — 734 DIU of usable width. <see cref="LogoColumnShare"/>
    /// (28%) of that is 205 DIU, minus the logo cell's own 4-DIU padding on
    /// each side, leaving <b>197 DIU</b> — rounded down for a little slack.
    /// </summary>
    public const double LogoColumnMaxWidth = 190;

    /// <summary>
    /// The logo column's height ceiling — matched to the text column beside
    /// it rather than forced taller. At their fullest (name, contact line,
    /// licence line and a document title all present) those four paragraphs
    /// run to roughly <b>50-55 DIU</b>; 50 keeps the image from being the
    /// thing that stretches the header, in every case except the rare
    /// four-line one, where it comes within a few DIU of the row anyway.
    /// </summary>
    public const double LogoColumnMaxHeight = 50;

    private static BitmapImage? DecodeLogo(DocumentTheme theme)
    {
        if (!theme.HasLogo) return null;

        try
        {
            var bytes = Convert.FromBase64String(theme.LogoBase64!);
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            // No logo must never stop a document printing.
            AppLog.Warn($"Print logo could not be decoded; printing without it ({ex.Message}).");
            return null;
        }
    }

    /// <summary>
    /// A4, and sized for a five-line visit to sit comfortably in the top half
    /// of the sheet — the design target the redesign works to. Type this
    /// small is what a boxed header and a dense identity grid need to earn
    /// their keep; a looser page just moves the same content further down.
    /// </summary>
    public static FlowDocument NewDocument() => new()
    {
        PageWidth = 794,           // A4 at 96 dpi
        PagePadding = new Thickness(30),
        ColumnWidth = double.MaxValue,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 9,
        // Pinned so the page is always paper-white with black ink, whatever
        // the app's theme is doing — see the print-safe palette above.
        Background = PrintPageBackground,
        Foreground = PrintForeground
    };

    /// <summary>
    /// Applies the clinic's chosen print font (Settings → Reports → Document
    /// branding) to a finished document. Walked on afterwards rather than
    /// threaded through every <see cref="Text"/>/<see cref="Row(bool, string[])"/>/
    /// <see cref="IdentityRow(ValueTuple{string, string}[])"/> call across
    /// four printer files — those already carry their own deliberate size
    /// differences (the prescription and the fee receipt print a couple of
    /// points larger than the pharmacy and diagnostic bills), and this adds
    /// the clinic's own adjustment on top of whichever size each element
    /// already has, rather than replacing it.
    /// </summary>
    public static void ApplyPrintSettings(FlowDocument doc, DocumentTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(theme.PrintFontFamily))
            doc.FontFamily = new FontFamily(theme.PrintFontFamily);

        if (theme.PrintFontSizeDelta == 0) return;

        foreach (var block in doc.Blocks) AdjustFontSize(block, theme.PrintFontSizeDelta);
    }

    /// <summary>Every size on the page is set explicitly somewhere below
    /// this point (never inherited from the document default), so reaching
    /// every <see cref="Paragraph"/> — through <see cref="Section"/>,
    /// <see cref="Table"/>/<see cref="TableCell"/> and <see cref="List"/> —
    /// and adjusting each one directly is what actually changes the page,
    /// rather than setting the delta once at the top and relying on inheritance.</summary>
    private static void AdjustFontSize(Block block, double delta)
    {
        block.FontSize = Math.Max(4, block.FontSize + delta);

        switch (block)
        {
            case Section section:
                foreach (var b in section.Blocks) AdjustFontSize(b, delta);
                break;
            case Table table:
                foreach (var row in table.RowGroups.SelectMany(g => g.Rows))
                    foreach (var cell in row.Cells)
                        foreach (var b in cell.Blocks) AdjustFontSize(b, delta);
                break;
            case List list:
                foreach (var b in list.ListItems.SelectMany(i => i.Blocks))
                    AdjustFontSize(b, delta);
                break;
        }
    }

    public static Paragraph Text(string text, double size = 9, FontWeight? weight = null,
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
        Margin = new Thickness(0, 4, 0, 4)
    });

    public static Table NewTable(params double[] widths)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var w in widths) table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });
        return table;
    }

    public static TableRow Row(bool header, params string[] cells) => Row(header, 0, cells);

    /// <summary>Same row, sized <paramref name="sizeDelta"/> points off the
    /// usual 7.6/8.4 — used by OPD's fee receipt and the prescription slip,
    /// which print a couple of points larger than the pharmacy bill and the
    /// diagnostic bill without those two changing to match.</summary>
    public static TableRow Row(bool header, double sizeDelta, params string[] cells)
    {
        var row = new TableRow();

        foreach (var (value, index) in cells.Select((v, i) => (v, i)))
        {
            var paragraph = new Paragraph(new Run(value))
            {
                Margin = new Thickness(3, 2, 3, 2),
                FontSize = (header ? 7.6 : 8.4) + sizeDelta,
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

    /// <summary>
    /// A row of the identity grid — three label/value pairs, label above value
    /// in each cell. The receipt reference uses three of these rows to carry
    /// nine facts in three lines of height instead of the six a flat
    /// label-then-value layout would take.
    /// </summary>
    public static TableRow IdentityRow(params (string Label, string Value)[] cells) => IdentityRow(0, cells);

    /// <summary>Same identity row, sized <paramref name="sizeDelta"/> points
    /// off the usual 6.6/7.8 — see <see cref="Row(bool, double, string[])"/>.</summary>
    public static TableRow IdentityRow(double sizeDelta, params (string Label, string Value)[] cells)
    {
        var row = new TableRow();

        foreach (var (label, value) in cells)
        {
            var cell = new TableCell
            {
                BorderBrush = Line,
                BorderThickness = new Thickness(0)
            };
            cell.Blocks.Add(new Paragraph(new Run(label))
            {
                FontSize = 6.6 + sizeDelta, FontWeight = FontWeights.Bold,
                Foreground = PrintSecondaryForeground, Margin = new Thickness(2, 1, 2, 0)
            });
            cell.Blocks.Add(new Paragraph(new Run(value))
            {
                FontSize = 7.8 + sizeDelta, FontWeight = FontWeights.Normal, Margin = new Thickness(2, 0, 2, 2),
                Foreground = PrintForeground
            });
            row.Cells.Add(cell);
        }

        return row;
    }

    /// <summary>
    /// A single "Label: value" line, bold label and ordinary-weight value —
    /// the same convention as every label/value pair on the page, for the
    /// stray lines (pharmacist, HSN, complaint, diagnosis) that sit outside
    /// any grid.
    /// </summary>
    public static Paragraph LabelValueText(string label, string value, double size = 8, Brush? brush = null,
                                            TextAlignment align = TextAlignment.Left,
                                            double topMargin = 0, double bottomMargin = 0)
    {
        var paragraph = new Paragraph
        {
            FontSize = size, Foreground = brush ?? PrintForeground, TextAlignment = align,
            Margin = new Thickness(0, topMargin, 0, bottomMargin)
        };

        paragraph.Inlines.Add(new Run($"{label}: ") { FontWeight = FontWeights.Bold });
        paragraph.Inlines.Add(new Run(value) { FontWeight = FontWeights.Normal });

        return paragraph;
    }

    /// <summary>
    /// A row of a totals block — the label in bold, the value in ordinary
    /// weight and right-aligned, the empty middle cell keeping the same
    /// three-column shape the totals table is built with.
    /// </summary>
    public static TableRow TotalRow(string label, string value)
    {
        var row = new TableRow();

        row.Cells.Add(TotalCell(label, FontWeights.Bold, TextAlignment.Left));
        row.Cells.Add(TotalCell("", FontWeights.Normal, TextAlignment.Left));
        row.Cells.Add(TotalCell(value, FontWeights.Normal, TextAlignment.Right));

        return row;
    }

    private static TableCell TotalCell(string text, FontWeight weight, TextAlignment align) => new(new Paragraph(new Run(text))
    {
        Margin = new Thickness(3, 2, 3, 2),
        FontSize = 8.4,
        FontWeight = weight,
        Foreground = PrintForeground,
        TextAlignment = align
    })
    {
        BorderBrush = Line,
        BorderThickness = new Thickness(0)
    };

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
