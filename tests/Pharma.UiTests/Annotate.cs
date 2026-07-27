using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FlaUI.Core.AutomationElements;

namespace Pharma.UiTests;

/// <summary>Where a callout sits relative to the control it points at.</summary>
public enum Side { Right, Left, Above, Below }

/// <summary>One control to ring, and what to say about it.</summary>
public record Note(string AutomationId, string Text, Side Side = Side.Right);

/// <summary>
/// Rings controls in red on a captured screenshot and writes what each one is
/// for beside it.
///
/// A guide that says "enter the quantity" under a picture of the whole screen
/// leaves the reader hunting. Drawing the box and the words onto the picture
/// itself means the explanation cannot drift from the screen, because both come
/// out of the same run against the real application.
/// </summary>
public static class Annotate
{
    private static readonly Color Ink = Color.FromArgb(0xC0, 0x1C, 0x1C);
    private static readonly Color Cloud = Color.FromArgb(0xFF, 0xFF, 0xFF);

    private const int MarginWidth = 330;

    /// <summary>
    /// Rings each named control on the window's screenshot and writes its note in
    /// a margin down the right-hand side, joined to the ring by a line.
    ///
    /// The notes go in a margin rather than on top of the picture because a
    /// callout covering the very control it describes is worse than no callout.
    /// </summary>
    public static void Draw(AutomationElement window, string file, params Note[] notes)
    {
        using var shot = FlaUI.Core.Capturing.Capture.Element(window);
        using var screen = new Bitmap(shot.Bitmap);

        using var canvas = new Bitmap(screen.Width + MarginWidth, screen.Height);
        using var g = Graphics.FromImage(canvas);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        g.Clear(Color.FromArgb(0xF7, 0xF8, 0xFA));
        g.DrawImage(screen, 0, 0);

        var origin = window.BoundingRectangle;

        using var pen = new Pen(Ink, 2.5f);
        using var thin = new Pen(Ink, 1.4f) { EndCap = LineCap.Round };
        using var ink = new SolidBrush(Ink);
        using var cloud = new SolidBrush(Cloud);
        using var body = new SolidBrush(Color.FromArgb(0x1B, 0x27, 0x33));
        using var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        using var number = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);

        // Ring everything first, and note where each ring is, so the callouts can
        // be ordered down the page the way the eye reads them.
        var rings = new List<(Note Note, RectangleF Box)>();

        foreach (var note in notes)
        {
            var target = window.FindFirstDescendant(cf => cf.ByAutomationId(note.AutomationId));
            if (target is null) continue;

            var r = target.BoundingRectangle;

            rings.Add((note, new RectangleF(
                (float)(r.Left - origin.Left) - 3,
                (float)(r.Top - origin.Top) - 3,
                (float)r.Width + 6,
                (float)r.Height + 6)));
        }

        rings = [.. rings.OrderBy(x => x.Box.Top).ThenBy(x => x.Box.Left)];

        var y = 14f;
        var index = 0;

        foreach (var (note, box) in rings)
        {
            index++;

            DrawRoundedRect(g, pen, box, 5);

            // The number on the ring itself, so ring and note are unmistakably paired.
            var tag = new RectangleF(box.Right - 9, box.Top - 9, 20, 20);
            g.FillEllipse(ink, tag);
            g.DrawString(index.ToString(), number, cloud, tag.X + (index < 10 ? 6f : 2.5f), tag.Y + 3f);

            var width = MarginWidth - 28f;
            var text = g.MeasureString(note.Text, font, (int)(width - 44));
            var height = Math.Max(40f, text.Height + 20);

            var bubble = new RectangleF(screen.Width + 14, y, width, height);
            y = bubble.Bottom + 10;

            using var shadow = new SolidBrush(Color.FromArgb(28, 0, 0, 0));
            DrawRoundedRect(g, null, Offset(bubble, 1.5f, 1.5f), 8, shadow);
            DrawRoundedRect(g, pen, bubble, 8, cloud);

            var badge = new RectangleF(bubble.X + 11, bubble.Y + 10, 20, 20);
            g.FillEllipse(ink, badge);
            g.DrawString(index.ToString(), number, cloud, badge.X + (index < 10 ? 6f : 2.5f), badge.Y + 3f);

            g.DrawString(note.Text, font, body,
                new RectangleF(bubble.X + 40, bubble.Y + 10, width - 50, height));

            // From the right edge of the ring to the left edge of its note.
            g.DrawLine(thin,
                new PointF(box.Right + 11, box.Top + 1),
                new PointF(bubble.X, bubble.Y + bubble.Height / 2));
        }

        canvas.Save(file, ImageFormat.Png);
    }

    private static RectangleF Offset(RectangleF r, float dx, float dy)
        => new(r.X + dx, r.Y + dy, r.Width, r.Height);

    private static void DrawRoundedRect(Graphics g, Pen? pen, RectangleF r, float radius, Brush? fill = null)
    {
        using var path = new GraphicsPath();

        var d = radius * 2;

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        if (fill is not null) g.FillPath(fill, path);
        if (pen is not null) g.DrawPath(pen, path);
    }
}
