using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pharma.App.Printing;

/// <summary>Small helpers so the two print templates stay readable.</summary>
internal static class DocumentBuilder
{
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x61, 0x70, 0x7E));
    public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(0xC8, 0xD0, 0xD8));

    public static FlowDocument NewDocument() => new()
    {
        PageWidth = 794,           // A4 at 96 dpi
        PagePadding = new Thickness(40),
        ColumnWidth = double.MaxValue,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 11
    };

    public static Paragraph Text(string text, double size = 11, FontWeight? weight = null,
                                 Brush? brush = null, TextAlignment align = TextAlignment.Left,
                                 double topMargin = 0, double bottomMargin = 0)
        => new(new Run(text))
        {
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = brush ?? Brushes.Black,
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
                Foreground = header ? Muted : Brushes.Black,
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
