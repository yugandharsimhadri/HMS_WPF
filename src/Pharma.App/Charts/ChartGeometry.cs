namespace Pharma.App.Charts;

/// <summary>
/// Turns a list of numbers into WPF Path mini-language strings — no charting
/// library, because a trend line and a donut are the only two shapes the
/// dashboard needs, and <see cref="System.Windows.Media.Geometry"/> already
/// has a built-in string converter: a plain <c>string</c> property binds
/// straight onto a <c>Path.Data</c> with no value converter of its own.
/// </summary>
public static class ChartGeometry
{
    /// <summary>
    /// The stroked line and the filled area beneath it for a simple trend,
    /// scaled to fit exactly inside (width, height) with the origin at the
    /// top-left, y growing downward — the same coordinate space a
    /// <c>Canvas</c> of that size already uses, so the strings drop straight
    /// into <c>Path.Data</c> with no further transform.
    /// </summary>
    public static (string Line, string Area) Trend(
        IReadOnlyList<double> values, double width, double height, double? min = null, double? max = null)
    {
        if (values.Count == 0) return ("", "");

        var lo = min ?? values.Min();
        var hi = max ?? values.Max();

        // A flat (or single-point) series still has to draw something other
        // than a divide-by-zero — a flat line across the middle reads fine.
        if (hi - lo < 0.0001) { lo -= 1; hi += 1; }

        var stepX = values.Count > 1 ? width / (values.Count - 1) : 0;
        var points = values
            .Select((v, i) => (X: i * stepX, Y: height - (v - lo) / (hi - lo) * height))
            .ToList();

        var line = "M" + string.Join(" L", points.Select(p => $"{p.X:0.##},{p.Y:0.##}"));
        var area = line + $" L{points[^1].X:0.##},{height:0.##} L{points[0].X:0.##},{height:0.##} Z";

        return (line, area);
    }

    /// <summary>Where the trend line's last point lands, in the same
    /// (width, height) box — for a highlighted dot marking "today". Pass the
    /// same <paramref name="min"/>/<paramref name="max"/> used for <see cref="Trend"/>
    /// so the dot lands exactly on the line it belongs to.</summary>
    public static (double X, double Y) TrendEndPoint(
        IReadOnlyList<double> values, double width, double height, double? min = null, double? max = null)
    {
        if (values.Count == 0) return (0, 0);

        var lo = min ?? values.Min();
        var hi = max ?? values.Max();
        if (hi - lo < 0.0001) { lo -= 1; hi += 1; }

        var x = values.Count > 1 ? width : 0;
        var y = height - (values[^1] - lo) / (hi - lo) * height;
        return (x, y);
    }

    /// <summary>
    /// One donut wedge's path data, as an SVG-style arc — WPF's Path
    /// mini-language accepts the same "A rx,ry angle isLarge isSweep x,y" arc
    /// command SVG does. <paramref name="startDeg"/>/<paramref name="endDeg"/>
    /// are clockwise from 3 o'clock, matching how the caller walks a running
    /// total around the circle.
    /// </summary>
    public static string DonutSegment(double cx, double cy, double rOuter, double rInner, double startDeg, double endDeg)
    {
        static (double X, double Y) OnCircle(double cx, double cy, double r, double deg)
        {
            var rad = deg * Math.PI / 180;
            return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }

        var large = endDeg - startDeg > 180 ? 1 : 0;
        var (x0o, y0o) = OnCircle(cx, cy, rOuter, startDeg);
        var (x1o, y1o) = OnCircle(cx, cy, rOuter, endDeg);
        var (x1i, y1i) = OnCircle(cx, cy, rInner, endDeg);
        var (x0i, y0i) = OnCircle(cx, cy, rInner, startDeg);

        return $"M{x0o:0.##},{y0o:0.##} " +
               $"A{rOuter:0.##},{rOuter:0.##} 0 {large} 1 {x1o:0.##},{y1o:0.##} " +
               $"L{x1i:0.##},{y1i:0.##} " +
               $"A{rInner:0.##},{rInner:0.##} 0 {large} 0 {x0i:0.##},{y0i:0.##} Z";
    }
}
