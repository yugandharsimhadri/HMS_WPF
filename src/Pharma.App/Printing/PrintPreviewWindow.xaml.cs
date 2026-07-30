using System.Windows;
using System.Windows.Documents;

namespace Pharma.App.Printing;

/// <summary>
/// Shows a document as it will print.
/// </summary>
/// <remarks>
/// The page height is stated as well as the width. FlowDocumentPageViewer
/// paginates to whatever space it is given, so with the height left unset the
/// preview broke pages at the size of this window rather than at the size of a
/// sheet of paper — a receipt that prints on one page was previewed as two, with
/// its footer and its DUPLICATE mark stranded on the second. Fixing both
/// dimensions means the preview breaks pages exactly where the printer will.
/// </remarks>
public partial class PrintPreviewWindow : Window
{
    private readonly Func<FlowDocument> _factory;

    public string JobName { get; }

    public PrintPreviewWindow(Func<FlowDocument> factory, string jobName)
    {
        InitializeComponent();

        _factory = factory;
        JobName = jobName;

        // Naming the window makes it obvious which document is on screen when
        // several have been opened during a busy counter session.
        Title = $"Print preview — {jobName}";

        // The preview gets its own copy; Print builds another for the printer.
        var document = factory();
        document.PageWidth = 794;       // A4 at 96 dpi
        document.PageHeight = 1123;     // and its height, so a page is a page
        document.ColumnWidth = 794;
        Viewer.Document = document;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrintService.Print(_factory, JobName)) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
