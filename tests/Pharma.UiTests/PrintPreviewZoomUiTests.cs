using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace Pharma.UiTests;

/// <summary>
/// The preview must let somebody see the whole sheet.
///
/// An A4 page is 794 device-independent pixels wide, and
/// <c>FlowDocumentPageViewer</c> neither scrolls horizontally nor brings a
/// toolbar of its own — so a page opened at 100% in a narrower window sat
/// clipped with no way to reach the missing edge, and no control to click.
/// These tests pin the three things that fix it: it opens fitted, the buttons
/// change the zoom, and Fit returns.
/// </summary>
public class PrintPreviewZoomUiTests(AppFixture app) : IClassFixture<AppFixture>
{
    private Window Preview()
    {
        var preview = Retry.WhileNull(
            () => app.MainWindow.ModalWindows.FirstOrDefault(
                w => w.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15)).Result;

        Assert.NotNull(preview);
        return preview!;
    }

    private static AutomationElement Control(Window w, string id)
        => w.FindFirstDescendant(cf => cf.ByAutomationId(id))
           ?? throw new InvalidOperationException($"the preview has no '{id}'");

    private static int ZoomOf(Window w)
    {
        var text = Control(w, "PreviewZoomLabel").Name ?? "";
        return int.Parse(text.TrimEnd('%'));
    }

    /// <summary>
    /// The width the sheet is actually drawn at.
    ///
    /// Asserting on the percentage label alone is circular — the window writes
    /// that label itself, so it reads back whatever was set whether or not the
    /// page moved. An earlier version of these tests did exactly that and passed
    /// while zooming in did nothing at all.
    /// </summary>
    private static double RenderedPageWidth(Window w)
        => Control(w, "PreviewViewer").BoundingRectangle.Width;

    private static void Click(Window w, string id) => Control(w, id).AsButton().Invoke();

    private void ClosePreview(Window w)
    {
        Click(w, "PreviewClose");
        AppFixture.WaitUntil(
            () => app.MainWindow.ModalWindows.All(
                x => !x.Title.StartsWith("Print preview", StringComparison.OrdinalIgnoreCase)),
            "the preview to close");
    }

    /// <summary>Raises a fee receipt preview — the shortest path to one.</summary>
    private Window OpenAPreview()
    {
        var name = $"Zoom {DateTime.Now:HHmmssfff}";
        OpdUiTests.BookWalkIn(app, name, "9007006021", "6");
        AppFixture.WaitUntil(() => app.HasTile("OpdWaitingList", name), "the tile to appear");
        app.TakeFee("OpdWaitingList", name);
        return Preview();
    }

    [Fact]
    public void The_preview_opens_with_the_whole_page_fitted()
    {
        var preview = OpenAPreview();

        // Fitted, not 100% — the page is wider than the window it opens in, so
        // anything at or above 100% is the clipped state this guards against.
        var zoom = ZoomOf(preview);
        Assert.InRange(zoom, 20, 99);

        ClosePreview(preview);
    }

    /// <summary>
    /// The one that matters. FlowDocumentPageViewer cannot render a page bigger
    /// than the space it is given, so zooming in past "fit" used to leave the
    /// sheet exactly the same size — measured at 502x710 both at 64% and at 144%.
    /// This asserts on drawn pixels so that regression cannot pass again.
    /// </summary>
    [Fact]
    public void Zooming_in_actually_enlarges_the_page()
    {
        var preview = OpenAPreview();
        var fittedWidth = RenderedPageWidth(preview);

        for (var i = 0; i < 6; i++) Click(preview, "PreviewZoomIn");

        var enlarged = RenderedPageWidth(preview);
        Assert.True(enlarged > fittedWidth * 1.5,
            $"zooming in should draw a visibly bigger page: was {fittedWidth:0}px, now {enlarged:0}px");

        ClosePreview(preview);
    }

    [Fact]
    public void Zoom_out_shrinks_the_page_and_fit_returns()
    {
        var preview = OpenAPreview();
        var fitted = ZoomOf(preview);
        var fittedWidth = RenderedPageWidth(preview);

        for (var i = 0; i < 4; i++) Click(preview, "PreviewZoomOut");
        Assert.True(RenderedPageWidth(preview) < fittedWidth,
            "zooming out should draw a smaller page");

        Click(preview, "PreviewZoomFit");
        Assert.Equal(fitted, ZoomOf(preview));
        Assert.Equal(fittedWidth, RenderedPageWidth(preview), 1.0);

        ClosePreview(preview);
    }

    /// <summary>Zooming in has to be worth doing — which means being able to
    /// reach the parts of the page that no longer fit.</summary>
    [Fact]
    public void The_page_can_be_panned_once_it_is_larger_than_the_window()
    {
        var preview = OpenAPreview();
        for (var i = 0; i < 6; i++) Click(preview, "PreviewZoomIn");

        var scrollable = Control(preview, "PreviewViewer")
            .FindAllChildren().Length >= 0 && preview.Patterns.Scroll.IsSupported
            || preview.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ScrollBar)).Length > 0;

        Assert.True(scrollable, "a page larger than the window must be scrollable");

        ClosePreview(preview);
    }
}
