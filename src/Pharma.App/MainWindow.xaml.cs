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
    /// Clinic PCs are often 1366x768. A window taller than the screen puts its
    /// footer — Save, Save &amp; complete — below the desktop where it cannot be
    /// clicked, so shrink to what the screen actually has and re-centre.
    /// </summary>
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
