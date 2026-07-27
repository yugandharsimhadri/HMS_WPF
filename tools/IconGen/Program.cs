using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Draws the application icon and writes a multi-size .ico.
//
// Kept as a tool rather than a committed binary nobody can change: the icon is
// vector, so it can be adjusted and regenerated instead of redrawn by hand.
//
//     dotnet run --project tools/IconGen -- src/Pharma.App/twinkle.ico

var target = args.Length > 0
    ? args[0]
    : Path.Combine("src", "Pharma.App", "twinkle.ico");

// Sizes Windows actually asks for: the taskbar, the desktop, Alt-Tab, and the
// 256 that File Explorer uses for large thumbnails.
int[] sizes = [16, 24, 32, 48, 64, 128, 256];

var pngs = sizes.Select(Render).ToList();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
WriteIcon(target, sizes, pngs);

Console.WriteLine($"Wrote {target} ({new FileInfo(target).Length / 1024} KB, {sizes.Length} sizes)");
return 0;

// ── The drawing ────────────────────────────────────────────────────────────

static byte[] Render(int size)
{
    var visual = new DrawingVisual();

    using (var dc = visual.RenderOpen())
    {
        const double box = 256;

        // The clinic's teal, deepening towards the bottom so the tile has weight.
        var plate = new LinearGradientBrush(
            Color.FromRgb(0x15, 0x8F, 0x86),
            Color.FromRgb(0x0B, 0x5A, 0x54),
            new Point(0, 0), new Point(1, 1));

        var radius = box * 0.22;
        dc.DrawRoundedRectangle(plate, null, new Rect(0, 0, box, box), radius, radius);

        // A cross reads as medicine at sixteen pixels, where anything finer —
        // a caduceus, a stethoscope — turns to mud.
        var arm = box * 0.155;
        var reach = box * 0.30;
        var centre = box / 2;
        var round = arm * 0.28;

        var white = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

        dc.DrawRoundedRectangle(white, null,
            new Rect(centre - arm / 2, centre - reach, arm, reach * 2), round, round);

        dc.DrawRoundedRectangle(white, null,
            new Rect(centre - reach, centre - arm / 2, reach * 2, arm), round, round);

        // A heartbeat across the cross, for a children's hospital rather than a
        // pharmacy chain. Dropped below 32px, where it only muddies the cross.
        if (size >= 32)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x0B, 0x5A, 0x54)), box * 0.038)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            var figure = new PathFigure { StartPoint = new Point(centre - reach * 0.66, centre) };

            figure.Segments.Add(new PolyLineSegment(
            [
                new Point(centre - reach * 0.30, centre),
                new Point(centre - reach * 0.14, centre - reach * 0.34),
                new Point(centre + reach * 0.06, centre + reach * 0.30),
                new Point(centre + reach * 0.24, centre),
                new Point(centre + reach * 0.66, centre)
            ], true));

            dc.DrawGeometry(null, pen, new PathGeometry([figure]));
        }
    }

    var bitmap = new RenderTargetBitmap(size, size, 96 * size / 256.0 * (256.0 / size) * 96 / 96,
                                        96, PixelFormats.Pbgra32);

    // Render the 256-unit drawing scaled into this bitmap.
    var scaled = new DrawingVisual();

    using (var dc = scaled.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
        dc.DrawDrawing(visual.Drawing);
        dc.Pop();
    }

    bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(scaled);

    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));

    using var stream = new MemoryStream();
    encoder.Save(stream);

    return stream.ToArray();
}

// ── The container ──────────────────────────────────────────────────────────

static void WriteIcon(string path, int[] sizes, List<byte[]> pngs)
{
    using var file = File.Create(path);
    using var w = new BinaryWriter(file);

    w.Write((ushort)0);              // reserved
    w.Write((ushort)1);              // type: icon
    w.Write((ushort)sizes.Length);

    // Each entry points at its image, which all follow the directory.
    var offset = 6 + sizes.Length * 16;

    for (var i = 0; i < sizes.Length; i++)
    {
        // 256 is written as 0, which is how the format says "two hundred and fifty six".
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)0);            // palette
        w.Write((byte)0);            // reserved
        w.Write((ushort)1);          // colour planes
        w.Write((ushort)32);         // bits per pixel
        w.Write(pngs[i].Length);
        w.Write(offset);

        offset += pngs[i].Length;
    }

    foreach (var png in pngs) w.Write(png);
}
