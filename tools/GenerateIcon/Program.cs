using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = GuessRepoRoot();
        var svgPath = Path.Combine(repoRoot, "images", "KBMixerIconClean.svg");
        var icoPath = Path.Combine(repoRoot, "KBMixer", "KBMixer.ico");

        if (args.Length >= 1)
            svgPath = Path.GetFullPath(args[0]);
        if (args.Length >= 2)
            icoPath = Path.GetFullPath(args[1]);

        if (!File.Exists(svgPath))
        {
            Console.Error.WriteLine($"SVG not found: {svgPath}");
            return 1;
        }

        int[] sizes = [256, 128, 64, 48, 32, 24, 16];
        var entries = new List<(int, int, byte[])>();
        foreach (var s in sizes)
        {
            var png = RenderSvgToPng(svgPath, s);
            int w = s, h = s;
            ReadPngDimensions(png, ref w, ref h);
            entries.Add((w, h, png));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);
        using (var fs = File.Create(icoPath))
            WriteIcoWithPngImages(fs, entries);

        Console.WriteLine($"Wrote {icoPath} ({entries.Count} sizes).");
        return 0;
    }

    /// <summary>net8.0 output is under …/tools/GenerateIcon/bin/Debug/net8.0 — five parents to repo root.</summary>
    private static string GuessRepoRoot()
    {
        var fromOutput = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(fromOutput, "images", "KBMixerIconClean.svg")))
            return fromOutput;
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "images", "KBMixerIconClean.svg")))
            return cwd;
        return fromOutput;
    }

    private static byte[] RenderSvgToPng(string svgPath, int size)
    {
        using var svg = new SKSvg();
        svg.Load(svgPath);
        var picture = svg.Picture;
        if (picture is null)
            throw new InvalidOperationException($"Failed to load SVG: {svgPath}");

        var b = picture.CullRect;
        if (b.Width <= 0 || b.Height <= 0)
            throw new InvalidOperationException("SVG has empty bounds.");

        float scale = Math.Min(size / b.Width, size / b.Height);
        using var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(size / 2f, size / 2f);
            canvas.Scale(scale);
            canvas.Translate(-b.MidX, -b.MidY);
            canvas.DrawPicture(picture);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void WriteIcoWithPngImages(Stream outStream, IReadOnlyList<(int Width, int Height, byte[] Png)> images)
    {
        using var w = new BinaryWriter(outStream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)images.Count);

        int offset = 6 + 16 * images.Count;
        foreach (var (width, height, png) in images)
        {
            w.Write((byte)(width >= 256 ? 0 : width));
            w.Write((byte)(height >= 256 ? 0 : height));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, _, png) in images)
            w.Write(png);
    }

    private static void ReadPngDimensions(ReadOnlySpan<byte> png, ref int width, ref int height)
    {
        if (png.Length < 24 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71)
            return;
        width = BinaryPrimitives.ReadInt32BigEndian(png.Slice(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(png.Slice(20, 4));
    }
}
