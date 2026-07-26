using System.Windows;
using System.Windows.Documents;

namespace Pharma.App.Printing;

public partial class PrintPreviewWindow : Window
{
    private readonly Func<FlowDocument> _factory;

    public string JobName { get; }

    public PrintPreviewWindow(Func<FlowDocument> factory, string jobName)
    {
        InitializeComponent();

        _factory = factory;
        JobName = jobName;

        // The preview gets its own copy; Print builds another for the printer.
        var document = factory();
        document.PageWidth = 794;      // A4 at 96 dpi
        document.ColumnWidth = 794;
        Viewer.Document = document;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrintService.Print(_factory, JobName)) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
