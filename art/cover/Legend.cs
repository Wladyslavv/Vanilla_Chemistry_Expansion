using System;
using System.Drawing;
using System.Drawing.Text;

// The "New Liquids" legend, compiled together with Cover.cs.
// Before 1.8.0 it listed 19 liquids in 5 columns, though the mod already had 24; 1.8.0 lists all 26 in 7 columns.
// The 19 old dots and labels are copied pixel for pixel from the old cover to their new places. The 7 new ones reuse
// an old dot's shape with their own colour (the dots are hard pixel circles: page, ring #C8CDD4, fill, nothing in
// between) and labels in the old labels' font, found by comparing renders with the cover pixel by pixel:
// Microsoft YaHei Regular 17 px, AntiAliasGridFit, #F5F5F5. In the heading only "19" is redrawn, as "26", in
// Microsoft YaHei UI Bold 20.5 px, AntiAlias, #E8D6A0, centred on the old digits and on their baseline.
public static class Legend
{
    static readonly Color PAGE = ColorTranslator.FromHtml("#161A20");
    static readonly Color RING = ColorTranslator.FromHtml("#C8CDD4");
    static readonly Color LABEL = ColorTranslator.FromHtml("#F5F5F5");
    static readonly Color HEADING = ColorTranslator.FromHtml("#E8D6A0");

    const int DOT = 19;
    const int ROW_TOP = 891;          // first row's dot top
    const int ROW_PITCH = 30;
    const int OLD_LEFT = 67, OLD_PITCH = 182, OLD_COLUMNS = 5, OLD_COUNT = 19;
    const int NEW_LEFT = 67, NEW_PITCH = 129, NEW_COLUMNS = 7;
    const int LABEL_INK_DX = 26;      // label ink left minus dot left, as on the old cover
    const int LABEL_INK_DY = 1;       // label ink top minus dot top

    // Added after the old legend in the order the features came in: Xinke, Crackine, Hollow's Lantern, brine
    // electrolysis, antibiotics. Colours are each liquid's own, from its AddLiquid call in src/.
    static readonly string[] NEW_NAMES = { "生物碱", "盐酸快克粉", "辣椒素", "辣酯", "食盐", "头孢菌素 C", "生物废液" };
    static readonly string[] NEW_COLOURS = { "#e8e4d0", "#f0e2a8", "#d8452e", "#b8336f", "#f1ece6", "#efe4c0", "#7d7a52" };

    public static void Relayout(Bitmap original, Bitmap cover)
    {
        // Clear the entries, not the heading.
        using (Graphics g = Graphics.FromImage(cover))
        using (SolidBrush page = new SolidBrush(PAGE))
        {
            g.FillRectangle(page, 40, ROW_TOP - 6, 960, 4 * ROW_PITCH + 6);
        }

        for (int i = 0; i < OLD_COUNT; i++)
        {
            int ox = OLD_LEFT + (i % OLD_COLUMNS) * OLD_PITCH, oy = ROW_TOP + (i / OLD_COLUMNS) * ROW_PITCH;
            int nx = NEW_LEFT + (i % NEW_COLUMNS) * NEW_PITCH, ny = ROW_TOP + (i / NEW_COLUMNS) * ROW_PITCH;
            // Dot and label together: from the dot to just before the next column's dot, one row tall.
            CopyInk(original, cover, new Rectangle(ox - 1, oy - 4, OLD_PITCH - 6, ROW_PITCH - 1), nx - ox, ny - oy);
        }

        Rectangle template = new Rectangle(OLD_LEFT, ROW_TOP, DOT, DOT);
        Color fill = original.GetPixel(OLD_LEFT + DOT / 2, ROW_TOP + DOT / 2);
        for (int k = 0; k < NEW_NAMES.Length; k++)
        {
            int i = OLD_COUNT + k;
            int nx = NEW_LEFT + (i % NEW_COLUMNS) * NEW_PITCH, ny = ROW_TOP + (i / NEW_COLUMNS) * ROW_PITCH;
            Color colour = ColorTranslator.FromHtml(NEW_COLOURS[k]);
            for (int y = 0; y < DOT; y++)
                for (int x = 0; x < DOT; x++)
                {
                    Color c = original.GetPixel(template.X + x, template.Y + y);
                    if (c.ToArgb() == fill.ToArgb()) cover.SetPixel(nx + x, ny + y, colour);
                    else if (c.ToArgb() == RING.ToArgb()) cover.SetPixel(nx + x, ny + y, RING);
                }
            DrawText(cover, NEW_NAMES[k], "Microsoft YaHei", 17f, FontStyle.Regular, TextRenderingHint.AntiAliasGridFit, LABEL,
                nx + LABEL_INK_DX, ny + LABEL_INK_DY, false);
            Console.WriteLine("new entry " + i + " (" + NEW_COLOURS[k] + ") dot at " + nx + "," + ny);
        }

        // Heading: "19" (ink x 467..489, y 855..871) becomes "26".
        using (Graphics g = Graphics.FromImage(cover))
        using (SolidBrush page = new SolidBrush(PAGE))
        {
            g.FillRectangle(page, 465, 853, 27, 21);
        }
        DrawText(cover, "26", "Microsoft YaHei UI", 20.5f, FontStyle.Bold, TextRenderingHint.AntiAlias, HEADING, 478, 871, true);
    }

    // Copies the pixels of a region that are not page background, shifted by (dx, dy).
    static void CopyInk(Bitmap from, Bitmap to, Rectangle region, int dx, int dy)
    {
        for (int y = region.Top; y < region.Bottom; y++)
            for (int x = region.Left; x < region.Right; x++)
            {
                Color c = from.GetPixel(x, y);
                if (c.ToArgb() != PAGE.ToArgb()) to.SetPixel(x + dx, y + dy, c);
            }
    }

    // Renders text on the page colour and copies its ink. Labels: ink left at x, CJK ink top at y (measured on a
    // reference glyph, so a label ending in a Latin letter keeps the same baseline). Digits: ink centred on x,
    // ink bottom at y.
    static void DrawText(Bitmap target, string text, string family, float size, FontStyle style, TextRenderingHint hint,
        Color colour, int x, int y, bool centreAndBottom)
    {
        using (Bitmap tmp = new Bitmap(300, 70))
        using (Bitmap refBmp = new Bitmap(300, 70))
        using (Font f = new Font(family, size, style, GraphicsUnit.Pixel))
        using (SolidBrush brush = new SolidBrush(colour))
        {
            Render(tmp, text, f, brush, hint);
            Render(refBmp, "梦", f, brush, hint);
            Rectangle ink = InkBox(tmp), refInk = InkBox(refBmp);
            int dx, dy;
            if (centreAndBottom)
            {
                dx = (int)Math.Round(x - ink.Width / 2.0) - ink.X;
                dy = y - (ink.Bottom - 1);
            }
            else
            {
                dx = x - ink.X;
                dy = y - refInk.Y;
            }
            for (int yy = 0; yy < tmp.Height; yy++)
                for (int xx = 0; xx < tmp.Width; xx++)
                {
                    Color c = tmp.GetPixel(xx, yy);
                    if (c.ToArgb() != PAGE.ToArgb()) target.SetPixel(xx + dx, yy + dy, c);
                }
            Console.WriteLine("text (" + text.Length + " chars): ink " + ink.Width + "x" + ink.Height + " at " + (ink.X + dx) + "," + (ink.Y + dy));
        }
    }

    static void Render(Bitmap b, string text, Font f, Brush brush, TextRenderingHint hint)
    {
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(PAGE);
            g.TextRenderingHint = hint;
            g.DrawString(text, f, brush, 10, 10);
        }
    }

    static Rectangle InkBox(Bitmap b)
    {
        int x0 = 9999, x1 = -1, y0 = 9999, y1 = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                Color c = b.GetPixel(x, y);
                if (Math.Abs(c.R - PAGE.R) + Math.Abs(c.G - PAGE.G) + Math.Abs(c.B - PAGE.B) > 40)
                {
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                }
            }
        return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }
}
