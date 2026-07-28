using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Pharma.App.Printing;

/// <summary>
/// Printing that a clinic PC can survive: no printer, a cancelled dialog or a
/// driver that throws must never lose the bill that was just saved.
///
/// Callers pass a factory rather than a document because a FlowDocument can only
/// have one parent — previewing one and then printing the same instance throws.
/// Building a second copy for the printer avoids that entirely.
/// </summary>
public static class PrintService
{
    public static void Preview(Func<FlowDocument> factory, string jobName, Window? owner = null)
    {
        try
        {
            var window = new PrintPreviewWindow(factory, jobName)
            {
                Owner = owner ?? Application.Current?.MainWindow
            };

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            Fail($"{jobName} could not be previewed.", ex);
        }
    }

    /// <summary>Shows the printer dialog and prints. False means nothing was printed.</summary>
    public static bool Print(Func<FlowDocument> factory, string jobName)
    {
        try
        {
            if (!AnyPrinterInstalled())
            {
                AppLog.Warn($"{jobName}: no printer is installed.");
                Dialog.Show(
                    "No printer is set up on this PC.\n\n" +
                    "Add a printer in Windows Settings, or use Preview to save the document as a PDF.",
                    "Print", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                AppLog.Info($"{jobName}: printing cancelled.");
                return false;
            }

            var document = factory();
            LayOutForPrinter(document, dialog);

            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, jobName);
            AppLog.Info($"{jobName}: sent to {dialog.PrintQueue?.FullName ?? "the default printer"}.");
            return true;
        }
        catch (Exception ex)
        {
            Fail($"{jobName} could not be printed.", ex);
            return false;
        }
    }

    /// <summary>Fits the document to the paper the chosen printer reports.</summary>
    public static void LayOutForPrinter(FlowDocument document, PrintDialog dialog)
    {
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(36);

        // Anything less than the page width lets WPF break the document into
        // newspaper columns, which mangles a bill.
        document.ColumnWidth = dialog.PrintableAreaWidth;
    }

    private static bool AnyPrinterInstalled()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues().Any();
        }
        catch (Exception ex)
        {
            // A broken spooler should not stop the user trying anyway.
            AppLog.Warn($"Could not list printers: {ex.Message}");
            return true;
        }
    }

    private static void Fail(string message, Exception ex)
    {
        AppLog.Error(message, ex);
        Dialog.Show(
            $"{message}\n\n{ex.Message}\n\nThe record itself is saved and can be printed again.",
            "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
