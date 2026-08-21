using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace Pharma.App.Printing;

/// <summary>
/// Shows a document as it will print.
/// </summary>
/// <remarks>
/// <para>
/// The page height is stated as well as the width. FlowDocumentPageViewer
/// paginates to whatever space it is given, so with the height left unset the
/// preview broke pages at the size of this window rather than at the size of a
/// sheet of paper — a receipt that prints on one page was previewed as two, with
/// its footer and its DUPLICATE mark stranded on the second. Fixing both
/// dimensions means the preview breaks pages exactly where the printer will.
/// </para>
/// <para>
/// Zoom is this window's own, built from a ScaleTransform inside a ScrollViewer,
/// and deliberately not <c>FlowDocumentPageViewer.Zoom</c>. That property resizes
/// the page only within the room the viewer already has, and the control does not
/// scroll — so it can fit a page or shrink one, but it can never render a page
/// larger than the window. Measured: at 64% the sheet drew 502x710, and at 144%
/// it drew 502x710 again. Zooming in did nothing at all, which is what made zoom
/// look broken. The transform scales what is rendered and the ScrollViewer
/// supplies the panning that makes zooming in worth doing.
/// </para>
/// </remarks>
public partial class PrintPreviewWindow : Window
{
    /// <summary>A4 at 96 dpi — the page the document is paginated to.</summary>
    private const double PageWidth = 794;
    private const double PageHeight = 1123;

    private readonly Func<FlowDocument> _factory;

    /// <summary>
    /// True while the zoom is the window's choice rather than the user's, which
    /// is what lets a resize keep the page fitted. Any manual zoom hands control
    /// over for good — re-fitting under someone who has deliberately zoomed in
    /// would look like the window fighting them.
    /// </summary>
    private bool _autoFit = true;

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
        document.PageWidth = PageWidth;
        document.PageHeight = PageHeight;
        document.ColumnWidth = PageWidth;
        Viewer.Document = document;

        Loaded += (_, _) => FitToWindow();
    }

    // ── Zoom ────────────────────────────────────────────────────────────────
    //
    // Driven by the ScaleTransform, not FlowDocumentPageViewer.Zoom — see the
    // note in the XAML for why that property cannot zoom in past "fit".

    private const double MinZoom = 25;
    private const double MaxZoom = 400;

    /// <summary>Current zoom as a percentage.</summary>
    private double Zoom => Scale.ScaleX * 100;

    /// <summary>The zoom at which a whole page fits the well.</summary>
    private double FitZoom()
    {
        // The viewer draws its own page-navigation strip under the sheet, so
        // measure what it actually occupies rather than assuming it is the page.
        //
        // ActualWidth is the size layout gave the viewer, which is *before* the
        // ScaleTransform is applied — it stays at the sheet's 818x1174 whatever the
        // zoom, and only the rendered rectangle moves. Scaling it here as well
        // double-counted the zoom, so fitting while already zoomed out measured
        // the page as several times its real size and answered with the 25%
        // floor: pressing Fit at 36% shrank the sheet instead of fitting it.
        var pageW = Viewer.ActualWidth > 0 ? Viewer.ActualWidth : PageWidth + Viewer.Padding.Left + Viewer.Padding.Right;
        var pageH = Viewer.ActualHeight > 0 ? Viewer.ActualHeight : PageHeight + Viewer.Padding.Top + Viewer.Padding.Bottom;

        var available = new Size(Well.ViewportWidth, Well.ViewportHeight);
        if (available.Width <= 0 || available.Height <= 0 || pageW <= 0 || pageH <= 0) return Zoom;

        // A little breathing room, so the sheet reads as paper on a desk rather
        // than as something jammed against the window edge.
        var zoom = Math.Min(available.Width / pageW, available.Height / pageH) * 100 - 3;

        return Math.Clamp(Math.Round(zoom), MinZoom, MaxZoom);
    }

    private void SetZoom(double zoom, bool auto = false)
    {
        var pct = Math.Clamp(Math.Round(zoom), MinZoom, MaxZoom);
        Scale.ScaleX = Scale.ScaleY = pct / 100;
        _autoFit = auto;
        ZoomLabel.Text = $"{pct:0}%";
    }

    private void FitToWindow() => SetZoom(FitZoom(), auto: true);

    private void Well_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_autoFit) FitToWindow();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(Zoom * 1.15);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(Zoom / 1.15);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => FitToWindow();

    /// <summary>
    /// Ctrl+wheel zooms, which is what anyone who has used a PDF reader will try
    /// first. Handled rather than left to the viewer so it works at any zoom and
    /// does not also page the document.
    /// </summary>
    private void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        SetZoom(Zoom * (e.Delta > 0 ? 1.15 : 1/1.15));
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                // OemPlus/OemMinus are the main keyboard; Add/Subtract the numpad.
                case Key.OemPlus or Key.Add:
                    SetZoom(Zoom * 1.15); e.Handled = true; return;
                case Key.OemMinus or Key.Subtract:
                    SetZoom(Zoom / 1.15); e.Handled = true; return;
                case Key.D0 or Key.NumPad0:
                    FitToWindow(); e.Handled = true; return;
            }
        }

        base.OnPreviewKeyDown(e);
    }

    // ── Commands ────────────────────────────────────────────────────────────

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrintService.Print(_factory, JobName)) Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
