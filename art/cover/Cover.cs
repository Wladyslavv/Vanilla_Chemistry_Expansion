using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Cover.cs <cover in> <tablet png> <fermenter png> <cover out> <crop out>
// 1.8.0: replaces the top-right card (Advanced Chemistry Handbook) of the workshop cover with antibiotics and the
// fermenter. (1.7.1 replaced the bottom-middle card with the cartel envelope; see git history.)
//
// Layout measured from the cover (art/cover, Measure.cs in the commit message): the cards are 296 px wide, 224 px
// apart vertically, background #222831; the top-right card's interior is x 682..978, y 172..374, centred on x 830.
// Icons are centred on y 245.5 (the bottom row's 693.5 minus two rows). Big sprites are drawn at 2x like the
// electrolyzer and the hydroponic machine; a pair sits side by side, bottoms aligned, 10 px apart, centred as a
// group (the "Rock Salt & Electrolyzer" card). Title Noto Sans SC Bold 24 px #F5F5F5, ink top y 322; subtitle
// 14.5 px #96A0AC, ink top y 354; both centred on x 830.
public static class Cover
{
    const int CENTRE_X = 830;
    const double ICON_CENTRE_Y = 245.5;
    const int SCALE = 2;
    const int PAIR_GAP = 10;

    // The tablet's ink inside its 16x16 sprite, and the pile: 3 at the bottom, 2 above, 1 on top.
    const int TAB_INK_X = 1, TAB_INK_Y = 3, TAB_INK_W = 15, TAB_INK_H = 11;
    const int TAB_STEP_X = 12;   // sprite pixels between neighbours in a row (3 px overlap); even, so a half step
                                 // stays on whole sprite pixels and the pile keeps to the 2x pixel grid
    const int TAB_STEP_Y = 7;    // sprite pixels each row sits above the one below

    // The fermenter's ink inside its 48x64 sprite.
    const int FER_INK_X = 3, FER_INK_Y = 0, FER_INK_W = 42, FER_INK_H = 64;

    public static void Main(string[] args)
    {
        Bitmap cover = new Bitmap(args[0]);
        Bitmap tablet = new Bitmap(args[1]);
        Bitmap fermenter = new Bitmap(args[2]);
        using (Graphics g = Graphics.FromImage(cover))
        {
            // Clear the card's contents, inside its border (same box as the 1.7.1 card, moved to this card).
            using (SolidBrush bg = new SolidBrush(ColorTranslator.FromHtml("#222831")))
            {
                g.FillRectangle(bg, 684, 174, 293, 199);
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Sizes on the cover, in cover pixels.
            int pileW = (TAB_STEP_X * 2 + TAB_INK_W) * SCALE;
            int pileH = (TAB_STEP_Y * 2 + TAB_INK_H) * SCALE;
            int ferW = FER_INK_W * SCALE;
            int ferH = FER_INK_H * SCALE;

            int groupLeft = (int)Math.Round(CENTRE_X - (pileW + PAIR_GAP + ferW) / 2.0);
            int bottom = (int)Math.Round(ICON_CENTRE_Y + ferH / 2.0);   // exclusive

            // Fermenter on the right, its ink box at (ferLeft, bottom - ferH).
            int ferLeft = groupLeft + pileW + PAIR_GAP;
            DrawSprite(g, fermenter, ferLeft - FER_INK_X * SCALE, bottom - ferH - FER_INK_Y * SCALE);
            Console.WriteLine("fermenter ink x " + ferLeft + ".." + (ferLeft + ferW - 1) + ", y " + (bottom - ferH) + ".." + (bottom - 1));

            // Tablet pile on the left, bottoms aligned with the fermenter; lower rows first so upper ones sit on top.
            int[][] rows = { new int[] { 0, 1, 2 }, new int[] { 0, 1 }, new int[] { 0 } };
            for (int row = 0; row < rows.Length; row++)
            {
                foreach (int i in rows[row])
                {
                    // Each row is shifted half a step to the right so the tablets rest in the gaps below.
                    int inkLeft = groupLeft + (i * TAB_STEP_X + row * TAB_STEP_X / 2) * SCALE;
                    int inkTop = bottom - (TAB_INK_H + row * TAB_STEP_Y) * SCALE;
                    DrawSprite(g, tablet, inkLeft - TAB_INK_X * SCALE, inkTop - TAB_INK_Y * SCALE);
                }
            }
            Console.WriteLine("tablet pile ink x " + groupLeft + ".." + (groupLeft + pileW - 1) + ", y " + (bottom - pileH) + ".." + (bottom - 1));

            DrawCentred(cover, "抗生素 / 发酵罐", 24f, ColorTranslator.FromHtml("#F5F5F5"), 322, TextRenderingHint.AntiAliasGridFit);
            DrawCentred(cover, "Antibiotics & Fermenter", 14.5f, ColorTranslator.FromHtml("#96A0AC"), 354, TextRenderingHint.AntiAlias);
        }
        Bitmap original = new Bitmap(args[0]);
        Legend.Relayout(original, cover);
        cover.Save(args[3], ImageFormat.Png);

        // A 2x crop of the whole top row for checking.
        Rectangle row1 = new Rectangle(40, 164, 944, 220);
        using (Bitmap crop = new Bitmap(row1.Width * 2, row1.Height * 2))
        using (Graphics g = Graphics.FromImage(crop))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cover, new Rectangle(0, 0, crop.Width, crop.Height), row1, GraphicsUnit.Pixel);
            crop.Save(args[4], ImageFormat.Png);
        }
    }

    static void DrawSprite(Graphics g, Bitmap sprite, int left, int top)
    {
        g.DrawImage(sprite, new Rectangle(left, top, sprite.Width * SCALE, sprite.Height * SCALE));
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
