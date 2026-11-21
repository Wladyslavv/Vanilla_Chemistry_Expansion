using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Cover110.cs <cover in> <hplc png> <opened waste png> <cover out> <crop out>
// 1.10.0: the top-middle card (Blended Wine) becomes the HPLC, the bottom-right card (Vitrified Waste) the opened
// nuclear waste barrel. Same layout as Cover.cs (1.8.0): cards 318 px apart horizontally and 224 px vertically, the
// top-right card's interior x 682..978, y 172..374, centred on x 830; icons centred on y 245.5 + 224 per row, drawn
// at 2x; title Noto Sans SC Bold 24 px #F5F5F5 with its ink top at y 322 (+224 per row), subtitle 14.5 px #96A0AC
// at y 354. A sprite's ink box is found from its pixels (alpha > 8, so the alpha-1 cell markers are ignored).
public static class Cover110
{
    const int CARD_STEP_X = 318;
    const int CARD_STEP_Y = 224;
    const int RIGHT_CENTRE_X = 830;
    const double ICON_CENTRE_Y = 245.5;
    const int TITLE_Y = 322;
    const int SUBTITLE_Y = 354;
    const int SCALE = 2;

    public static void Main(string[] args)
    {
        Bitmap cover = new Bitmap(args[0]);
        Bitmap hplc = new Bitmap(args[1]);
        Bitmap waste = new Bitmap(args[2]);
        Card(cover, 1, 0, hplc, "高效液相色谱仪", "HPLC");
        Card(cover, 2, 2, waste, "打开的核废料", "Opened Nuclear Waste");
        cover.Save(args[3], ImageFormat.Png);

        // A 2x crop of both changed cards' columns for checking: the middle and right columns, all rows.
        Rectangle area = new Rectangle(352, 164, 640, 668);
        using (Bitmap crop = new Bitmap(area.Width, area.Height))
        using (Graphics g = Graphics.FromImage(crop))
        {
            g.DrawImage(cover, new Rectangle(0, 0, crop.Width, crop.Height), area, GraphicsUnit.Pixel);
            crop.Save(args[4], ImageFormat.Png);
        }
    }

    // column 0..2 left to right, row 0..2 top to bottom.
    static void Card(Bitmap cover, int column, int row, Bitmap sprite, string title, string subtitle)
    {
        int centreX = RIGHT_CENTRE_X - (2 - column) * CARD_STEP_X;
        int dy = row * CARD_STEP_Y;
        using (Graphics g = Graphics.FromImage(cover))
        {
            using (SolidBrush bg = new SolidBrush(ColorTranslator.FromHtml("#222831")))
            {
                g.FillRectangle(bg, 684 - (2 - column) * CARD_STEP_X, 174 + dy, 293, 199);
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            int minx = 999, maxx = -1, miny = 999, maxy = -1;
            for (int y = 0; y < sprite.Height; y++)
                for (int x = 0; x < sprite.Width; x++)
                {
                    if (sprite.GetPixel(x, y).A > 8)
                    {
                        minx = Math.Min(minx, x); maxx = Math.Max(maxx, x); miny = Math.Min(miny, y); maxy = Math.Max(maxy, y);
                    }
                }
            int inkW = (maxx - minx + 1) * SCALE, inkH = (maxy - miny + 1) * SCALE;
            int left = (int)Math.Round(centreX - inkW / 2.0) - minx * SCALE;
            int top = (int)Math.Round(ICON_CENTRE_Y + dy - inkH / 2.0) - miny * SCALE;
            g.DrawImage(sprite, new Rectangle(left, top, sprite.Width * SCALE, sprite.Height * SCALE));
            Console.WriteLine(title + ": sprite ink " + inkW + "x" + inkH + " centred on (" + centreX + ", " + (ICON_CENTRE_Y + dy) + ")");
        }
        DrawCentred(cover, title, 24f, ColorTranslator.FromHtml("#F5F5F5"), centreX, TITLE_Y + dy, TextRenderingHint.AntiAliasGridFit);
        DrawCentred(cover, subtitle, 14.5f, ColorTranslator.FromHtml("#96A0AC"), centreX, SUBTITLE_Y + dy, TextRenderingHint.AntiAlias);
    }

    // Renders the text off-screen, finds its ink box, and copies it so the ink is centred on centreX with its top at inkTop.
    static void DrawCentred(Bitmap target, string text, float size, Color colour, int centreX, int inkTop, TextRenderingHint hint)
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
            int dx = (int)Math.Round(centreX - width / 2.0) - minx;
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
            Console.WriteLine(text + ": ink " + width + "x" + (maxy - miny + 1) + " at x " + (minx + dx) + ".." + (maxx + dx));
        }
    }
}
