using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Pharma.App.ViewModels;

namespace Pharma.App;

/// <summary>
/// The shell. Beyond hosting pages, its job is making sure no screen can be
/// opened and then lost: Esc closes whatever is over the shell, and clicking
/// the shell while a dialog is open brings that dialog back into view rather
/// than appearing to do nothing at all.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        FitToScreen();

        Activated += (_, _) => SurfaceOpenDialog();
        PreviewMouseDown += (_, _) => SurfaceOpenDialog();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// Sets the size the window returns to when it is un-maximised.
    ///
    /// It opens maximised — the screens this runs on are small, and every pixel
    /// spent on desktop around the edge is a pixel not spent on the queue. But
    /// restoring from maximised has to land somewhere sensible, and on a
    /// 1366x768 clinic PC the designed 1360x820 is taller than the screen, which
    /// would put the footer — Save, Save &amp; complete — below the desktop edge
    /// where it cannot be clicked.
    /// </summary>
    /// <remarks>
    /// This only ever shrinks the window, never grows it, so the same layout
    /// serves every screen size.
    ///
    /// The minimums in XAML are deliberately below 1024x768 for the same reason
    /// this method exists: a 1366x768 screen has roughly 728 usable height once
    /// the taskbar is there, and a 768 minimum would put the footer buttons back
    /// under the desktop edge. They exist only to stop the window being dragged
    /// down to a size where controls overlap.
    /// </remarks>
    private void FitToScreen()
    {
        var work = SystemParameters.WorkArea;

        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);

        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }

    /// <summary>
    /// A modal dialog sitting behind another application leaves this window
    /// unable to respond, with nothing on screen to explain why. The clinic
    /// reads that as the software having hung. Bring the dialog back instead.
    /// </summary>
    private void SurfaceOpenDialog()
    {
        var dialog = OwnedWindows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
        if (dialog is null) return;

        if (dialog.WindowState == WindowState.Minimized) dialog.WindowState = WindowState.Normal;
        dialog.Activate();
    }

    /// <summary>Opens the developer's site from the credit line in the sidebar.</summary>
    private void Vendor_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Web.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not MainViewModel shell || !shell.IsOverlayOpen) return;

        shell.CloseOverlay();
        e.Handled = true;
    }
}
