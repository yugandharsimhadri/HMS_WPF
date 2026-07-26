using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace Pharma.App;

/// <summary>
/// Runs an action that touches the database, a file or a printer, and turns any
/// failure into something a receptionist can act on.
///
/// App.xaml.cs already catches anything that escapes, so the application cannot
/// be closed by an exception either way. This exists so the common failures read
/// as plain sentences instead of a stack trace.
/// </summary>
public static class Safely
{
    public static async Task RunAsync(Func<Task> action, string what, Action<string>? report = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var message = $"{what} could not be completed. {Explain(ex)}";

            AppLog.Error($"{what} failed.", ex);
            report?.Invoke(message);

            MessageBox.Show(
                $"{message}\n\nNothing was changed. Details were written to:\n{AppLog.CurrentFile}",
                App.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Turns the usual failures into something worth reading.</summary>
    public static string Explain(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException =>
            "Someone else changed this record at the same time. Refresh and try again.",

        DbUpdateException =>
            "The change could not be saved — it may already exist, or a required field is missing.",

        UnauthorizedAccessException =>
            "Windows refused access to that file or folder.",

        FileNotFoundException or DirectoryNotFoundException =>
            "That file could not be found.",

        IOException =>
            "The file is in use by another program. Close it and try again.",

        InvalidOperationException => ex.Message,

        _ => ex.Message
    };
}
