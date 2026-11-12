using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Cover.cs <cover in> <envelope png> <cover out> <crop out>
// Replaces the bottom-middle card (Plutonium Ampoule) of the workshop cover with the cartel envelope.
// Layout measured from the original cover: card interior x 364..660, y 620..822, background #222831; icons are item
// sprites at 4x, centred on (512, 693.5); title Noto Sans SC Bold 24 px #F5F5F5 with its ink top at y 770;
// subtitle 14.5 px #96A0AC with its ink top at y 802; both centred on x 512.
public static class Cover
{
    const int CENTRE_X = 512;

    public static void Main(string[] args)
    {
        Bitmap cover = new Bitmap(args[0]);
        Bitmap envelope = new Bitmap(args[1]);
        using (Graphics g = Graphics.FromImage(cover))
        {
            // Clear the card's contents, inside its border.
            using (SolidBrush bg = new SolidBrush(ColorTranslator.FromHtml("#222831")))
            {
                g.FillRectangle(bg, 366, 622, 293, 199);
            }

            // Envelope: opaque box x 4..28, y 8..26 of the 32x32 sprite, drawn at 4x and centred like the other icons.
            const int SCALE = 4;
            double boxCentreX = (4 + 28 + 1) / 2.0;
            double boxCentreY = (8 + 26 + 1) / 2.0;
            int left = (int)Math.Round(CENTRE_X - boxCentreX * SCALE);
            int top = (int)Math.Round(693.5 - boxCentreY * SCALE);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(envelope, new Rectangle(left, top, envelope.Width * SCALE, envelope.Height * SCALE));
            Console.WriteLine("envelope sprite at " + left + "," + top + " (ink " + (left + 4 * SCALE) + ".." + (left + 29 * SCALE - 1) + ", " + (top + 8 * SCALE) + ".." + (top + 27 * SCALE - 1) + ")");

            DrawCentred(cover, "卡特尔扩展", 24f, ColorTranslator.FromHtml("#F5F5F5"), 770, TextRenderingHint.AntiAliasGridFit);
            DrawCentred(cover, "Cartel Expansion", 14.5f, ColorTranslator.FromHtml("#96A0AC"), 802, TextRenderingHint.AntiAlias);
        }
        cover.Save(args[2], ImageFormat.Png);

        // A 2x crop of the whole bottom row for checking.
        Rectangle row = new Rectangle(40, 612, 944, 220);
        using (Bitmap crop = new Bitmap(row.Width * 2, row.Height * 2))
        using (Graphics g = Graphics.FromImage(crop))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cover, new Rectangle(0, 0, crop.Width, crop.Height), row, GraphicsUnit.Pixel);
            crop.Save(args[3], ImageFormat.Png);
        }
    }

    // Renders the text off-screen, finds its ink box, and copies it so the ink is centred on CENTRE_X with its top at inkTop.
    static void DrawCentred(Bitmap target, string text, float size, Color colour, int inkTop, TextRenderingHint hint)
    {
        Color bg = ColorTranslator.FromHtml("#222831");
        using (Bitmap tmp = new Bitmap(500, 80))
        using (Graphics g = Graphics.FromImage(tmp))
        using (Font f = new Font("Noto Sans SC", size, FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush brush = new SolidBrush(colour))
        {
            g.Clear(bg);
            g.TextRenderingHint = hint;
            g.DrawString(text, f, brush, 20, 20);
            int minx = 999, maxx = -1, miny = 999, maxy = -1;
            for (int y = 0; y < tmp.Height; y++)
                for (int x = 0; x < tmp.Width; x++)
                {
                    Color c = tmp.GetPixel(x, y);
                    if (Math.Abs(c.R - bg.R) + Math.Abs(c.G - bg.G) + Math.Abs(c.B - bg.B) > 24)
                    {
                        minx = Math.Min(minx, x); maxx = Math.Max(maxx, x); miny = Math.Min(miny, y); maxy = Math.Max(maxy, y);
                    }
                }
            int width = maxx - minx + 1;
            int dx = (int)Math.Round(CENTRE_X - width / 2.0) - minx;
            int dy = inkTop - miny;
            for (int y = 0; y < tmp.Height; y++)
                for (int x = 0; x < tmp.Width; x++)
                {
                    Color c = tmp.GetPixel(x, y);
                    if (c.ToArgb() != bg.ToArgb())
                    {
                        target.SetPixel(x + dx, y + dy, c);
                    }
                }
            Console.WriteLine(text + ": ink " + width + "x" + (maxy - miny + 1) + " at x " + (minx + dx) + ".." + (maxx + dx) + ", y " + inkTop + ".." + (maxy + dy));
        }
    }
}
