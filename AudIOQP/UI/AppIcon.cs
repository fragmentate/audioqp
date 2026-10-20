using System.Drawing.Drawing2D;

namespace AudIOQP.UI;

internal static class AppIcon
{
    static MemoryStream? _keptAlive;

    public static Icon Create()
    {
        int[] sizes = [16, 32, 48, 256];
        var pngs = sizes.Select(RenderPng).ToArray();
        _keptAlive = new MemoryStream();
        WriteIco(_keptAlive, pngs, sizes);
        _keptAlive.Position = 0;
        return new Icon(_keptAlive);
    }

    public static void WriteIcoFile(string path)
    {
        int[] sizes = [16, 32, 48, 256];
        var pngs = sizes.Select(RenderPng).ToArray();
        using var stream = File.Create(path);
        WriteIco(stream, pngs, sizes);
    }

    static byte[] RenderPng(int size)
    {
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            var s = (float)size;
            using var bg = new SolidBrush(Color.FromArgb(255, 0, 120, 212));
            FillRoundRect(g, bg, new RectangleF(0.5f, 0.5f, s - 1.5f, s - 1.5f), s * 0.22f);
            using var white = new SolidBrush(Color.White);
            PointF[] speaker =
            [
                new(s * 0.20f, s * 0.38f),
                new(s * 0.38f, s * 0.38f),
                new(s * 0.56f, s * 0.22f),
                new(s * 0.56f, s * 0.78f),
                new(s * 0.38f, s * 0.62f),
                new(s * 0.20f, s * 0.62f)
            ];
            g.FillPolygon(white, speaker);
            using var pen = new Pen(Color.White, Math.Max(1.2f, s * 0.055f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(pen, s * 0.50f, s * 0.32f, s * 0.26f, s * 0.36f, -55, 110);
            if (size >= 24)
                g.DrawArc(pen, s * 0.60f, s * 0.24f, s * 0.28f, s * 0.52f, -55, 110);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    static void FillRoundRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    static void WriteIco(Stream stream, byte[][] pngs, int[] sizes)
    {
        WriteU16(stream, 0);
        WriteU16(stream, 1);
        WriteU16(stream, (ushort)pngs.Length);
        var offset = 6 + (16 * pngs.Length);
        for (var i = 0; i < pngs.Length; i++)
        {
            var dimension = sizes[i] >= 256 ? 0 : sizes[i];
            stream.WriteByte((byte)dimension);
            stream.WriteByte((byte)dimension);
            stream.WriteByte(0);
            stream.WriteByte(0);
            WriteU16(stream, 1);
            WriteU16(stream, 32);
            WriteU32(stream, pngs[i].Length);
            WriteU32(stream, offset);
            offset += pngs[i].Length;
        }

        foreach (var png in pngs)
            stream.Write(png, 0, png.Length);
    }

    static void WriteU16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
    }

    static void WriteU32(Stream stream, int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }
}
