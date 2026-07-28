using System.Windows;

namespace Pharma.App;

/// <summary>
/// Every question and warning the application puts up, drawn by us instead of
/// by Windows.
///
/// MessageBox is a Win32 dialog. It takes its colours from the operating system
/// and knows nothing of the palette, so in the dark theme it opened as a light
/// grey box in the middle of a near-black screen — and in either theme it was
/// the one part of the application wearing somebody else's clothes.
///
/// The signature is MessageBox.Show's on purpose: call sites read the same and
/// still switch on MessageBoxResult, so swapping this in changed one word per
/// call and nothing else.
/// </summary>
public static class Dialog
{
    public static MessageBoxResult Show(string message, string title,
                                        MessageBoxButton buttons = MessageBoxButton.OK,
                                        MessageBoxImage icon = MessageBoxImage.None)
    {
        var app = Application.Current;

        // No application object means a unit test or a designer, where the
        // themed window has no resources to draw itself from.
        if (app is null) return MessageBox.Show(message, title, buttons, icon);

        return app.Dispatcher.CheckAccess()
            ? Ask(message, title, buttons, icon)
            : app.Dispatcher.Invoke(() => Ask(message, title, buttons, icon));
    }

    private static MessageBoxResult Ask(string message, string title,
                                        MessageBoxButton buttons, MessageBoxImage icon)
    {
        var window = new Views.MessageWindow(message, title, buttons, icon);

        // Owned, so it stays over the screen that asked and cannot be lost
        // behind the shell. There is no owner during startup — a database that
        // will not open is reported before any window exists — so fall back to
        // the middle of the screen rather than refusing to appear.
        var owner = Application.Current.Windows.OfType<Window>()
                       .FirstOrDefault(w => w.IsActive && w.IsLoaded)
                    ?? Application.Current.MainWindow;

        if (owner is { IsLoaded: true } && !ReferenceEquals(owner, window))
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        window.ShowDialog();

        return window.Answer;
    }
}
