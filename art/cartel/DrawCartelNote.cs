// Draws the Cartel Network's "we see" note: an aged parchment folded once across the middle (314 x 402, the size of
// the game's paper notebook, shown at 2x) with the cartel mark in black brush ink, an open eye whose lower lid runs
// with ink, and the words under it in brush-like handwriting that drips too. The words are part of the image, one
// image per language (the page picks it through the localization key cartel_note_background):
//   vce_cartel_note_zh.png  "我们看见" written stroke by stroke with a pixel brush (HandBrush, below), no font;
//   vce_cartel_note_en.png  "We see" in Script MT Bold, a heavy calligraphic script that survives pixelation.
// Deterministic (fixed seed). C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawCartelNote.cs
// Usage: DrawCartelNote.exe <out dir>
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawCartelNote
{
    const int W = 314, H = 402;

    // Eye: centre, half width, half height of the almond.
    const double EX = 157, EY = 150, EW = 92, EH = 40;

    // The fold, halfway down; the words sit in the lower half.
    const int FOLD = 201;
    const int WORDS_TOP = 262, WORDS_H = 70;

    static readonly Random Rng = new Random(20261110);
    static double[,] noise;

    static Color C(int r, int g, int b)
    {
        return Color.FromArgb(255, r, g, b);
    }

    // Smooth value noise on a coarse grid, for stains and ragged ink edges.
    static double[,] MakeNoise(int cell)
    {
        int gw = W / cell + 2, gh = H / cell + 2;
        double[,] grid = new double[gw, gh];
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                grid[x, y] = Rng.NextDouble();
            }
        }
        double[,] n = new double[W, H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                double fx = (double)x / cell, fy = (double)y / cell;
                int ix = (int)fx, iy = (int)fy;
                double tx = fx - ix, ty = fy - iy;
                tx = tx * tx * (3 - 2 * tx);
                ty = ty * ty * (3 - 2 * ty);
                double a = grid[ix, iy] * (1 - tx) + grid[ix + 1, iy] * tx;
                double b = grid[ix, iy + 1] * (1 - tx) + grid[ix + 1, iy + 1] * tx;
                n[x, y] = a * (1 - ty) + b * ty;
            }
        }
        return n;
    }

    static double Half(double dx)
    {
        double t = Math.Abs(dx) / EW;
        return t >= 1 ? -1 : EH * Math.Pow(1 - t * t, 0.85);
    }

    // How much ink covers (x, y): 0 = paper, 1 = solid ink.
    static double Ink(int x, int y, double[,] fine)
    {
        double rough = (fine[x, y] - 0.5) * 3.2;  // ragged brush edge, +-1.6 px
        double dx = x - EX, dy = y - EY;
        double h = Half(dx);
        double ink = 0;
        double t = Math.Abs(dx) / EW;
        if (h > 0)
        {
            // Upper lid: a heavy stroke, thickest above the iris, tapering to the corners.
            double upper = EY - h;
            double thickU = 3 + 9 * (1 - t * t);
            if (y >= upper - thickU + rough && y <= upper + 2 + rough * 0.5)
            {
                ink = 1;
            }
            // Lower lid: a thinner stroke.
            double lower = EY + h;
            double thickL = 2 + 4 * (1 - t * t);
            if (y <= lower + thickL + rough && y >= lower - 2 + rough * 0.5)
            {
                ink = 1;
            }
            // Iris, clipped to the opening; a paper highlight top left.
            double r = Math.Sqrt(dx * dx + (dy + 2) * (dy + 2));
            if (r <= 31 + rough && y > upper && y < lower)
            {
                ink = 1;
                double hx = dx + 10, hy = dy + 12;
                if (hx * hx + hy * hy <= 30)
                {
                    ink = 0;
                }
                // A thin paper ring between iris and pupil, broken like a dry brush.
                if (r >= 15 && r <= 17.2 && fine[x, y] > 0.42)
                {
                    ink = 0;
                }
            }
        }
        // Corners: the stroke flicks out past the almond on both sides.
        if (Math.Abs(dy + (dx > 0 ? 3 : 1)) <= 2.2 + rough * 0.4 && Math.Abs(dx) >= EW - 2 && Math.Abs(dx) <= EW + (dx > 0 ? 16 : 9))
        {
            ink = 1;
        }
        // Lash flicks over the upper lid.
        double[] lashes = { -60, -32, 0, 32, 60 };
        foreach (double lx in lashes)
        {
            double baseY = EY - Half(lx) - 1 - 9 * (1 - Math.Pow(lx / EW, 2));
            double ang = lx / EW * 0.9;
            for (double s = 0; s <= 22; s += 0.5)
            {
                double px = EX + lx + Math.Sin(ang) * s, py = baseY - Math.Cos(ang) * s;
                double w = 3.0 * (1 - s / 24);
                if ((x - px) * (x - px) + (y - py) * (y - py) <= w * w)
                {
                    ink = 1;
                }
            }
        }
        // Ink running down from the lower lid, each drip ending in a drop.
        double[,] drips = { { -38, 26 }, { -6, 48 }, { 22, 34 }, { 47, 18 } };
        for (int i = 0; i < drips.GetLength(0); i++)
        {
            double cx = EX + drips[i, 0];
            double top = EY + Half(drips[i, 0]) + 2;
            double len = drips[i, 1];
            double width = 2.2 - 0.6 * (y - top) / Math.Max(1, len);
            if (y >= top && y <= top + len && Math.Abs(x - cx - (fine[x, y] - 0.5) * 0.8) <= width)
            {
                ink = 1;
            }
            double bx = cx, by = top + len + 2;
            if ((x - bx) * (x - bx) + (y - by) * (y - by) * 0.8 <= 12)
            {
                ink = 1;
            }
        }
        return ink;
    }

    public static void Main(string[] args)
    {
        string outDir = args[0];
        Directory.CreateDirectory(outDir);
        noise = MakeNoise(24);
        double[,] fine = MakeNoise(3);
        double[,] grain = MakeNoise(1);

        // Parchment palette, light to dark (quantized so it reads as pixel art next to the game's paper).
        Color[] paper = { C(244, 233, 196), C(236, 222, 178), C(226, 208, 160), C(212, 190, 140), C(193, 168, 118), C(168, 140, 94) };
        Color edge = C(120, 96, 62);
        Color[] inkTones = { C(28, 23, 22), C(40, 33, 30), C(54, 45, 40) };

        Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Ragged outline: the sheet is inset a little, more where the noise is low.
                double inset = 2 + noise[x, y] * 3;
                if (Math.Abs(y - FOLD - 0.5) <= 2)
                {
                    inset += 3 - Math.Abs(y - FOLD - 0.5);  // the edges pinch in where the sheet was folded
                }
                double dEdge = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y));
                if (dEdge < inset - 1.5)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    continue;
                }
                // Darkening toward the edges, stains, creases, grain.
                double vignette = Math.Max(0, 1 - dEdge / 46.0);
                double shade = 0.9 * vignette * vignette + 0.55 * Math.Max(0, noise[x, y] - 0.62) + 0.18 * (grain[x, y] - 0.5);
                // Folded once across the middle: the paper dips into the crease from above, a dark line, a lit ridge
                // just below it, then a shadow fading out over the lower half.
                int fy = y - FOLD;
                if (fy >= -9 && fy < 0)
                {
                    shade += 0.07 * (fy + 10);
                }
                else if (fy == 0)
                {
                    shade += 1.2;
                }
                else if (fy == 1)
                {
                    shade += 0.55;
                }
                else if (fy == 2)
                {
                    shade -= 0.6;
                }
                else if (fy >= 3 && fy <= 14)
                {
                    shade += 0.5 * (1 - (fy - 3) / 12.0);
                }
                int level = (int)Math.Round(shade * 4.2);
                level = Math.Max(0, Math.Min(paper.Length - 1, level));
                Color c = paper[level];
                if (dEdge < inset + 0.5)
                {
                    c = edge;
                }
                double ink = Ink(x, y, fine);
                if (ink > 0)
                {
                    int tone = grain[x, y] > 0.7 ? 1 : (grain[x, y] < 0.15 ? 2 : 0);
                    c = y == FOLD + 2 ? inkTones[2] : inkTones[tone];  // the fold's ridge shows through the ink
                }
                bmp.SetPixel(x, y, c);
            }
        }
        // Spatter around the mark.
        for (int i = 0; i < 70; i++)
        {
            double a = Rng.NextDouble() * Math.PI * 2;
            double d = 60 + Rng.NextDouble() * 70;
            int sx = (int)(EX + Math.Cos(a) * d * 1.3), sy = (int)(EY + Math.Sin(a) * d * 0.75);
            if (sx < 12 || sy < 12 || sx >= W - 12 || sy >= H - 12)
            {
                continue;
            }
            bmp.SetPixel(sx, sy, inkTones[0]);
            if (Rng.NextDouble() < 0.3)
            {
                bmp.SetPixel(sx + 1, sy, inkTones[1]);
            }
        }
        Bitmap zh = WithWords(bmp, HandBrush.Words(fine), grain, inkTones, 7);
        Bitmap en = WithWords(bmp, FontMask("We see", "Script MT Bold", 52f, FontStyle.Regular, fine), grain, inkTones, 11);
        zh.Save(Path.Combine(outDir, "vce_cartel_note_zh.png"), ImageFormat.Png);
        en.Save(Path.Combine(outDir, "vce_cartel_note_en.png"), ImageFormat.Png);

        // Preview: both, at the in-game 2x.
        Bitmap preview = new Bitmap(W * 4 + 24, H * 2, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(preview))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(zh, new Rectangle(0, 0, W * 2, H * 2));
            g.DrawImage(en, new Rectangle(W * 2 + 24, 0, W * 2, H * 2));
        }
        preview.Save(Path.Combine(outDir, "cartel_note_preview.png"), ImageFormat.Png);
    }

    // Words set in a font, centred under the mark: rendered 4x with anti-aliasing, then each page pixel is ink
    // where enough of it is covered (the threshold wobbles with the noise, for a ragged brush edge).
    static bool[,] FontMask(string words, string fontName, float size, FontStyle style, double[,] fine)
    {
        const int SS = 4;
        Bitmap big = new Bitmap(W * SS, WORDS_H * SS, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(big))
        using (Font font = new Font(fontName, size * SS, style, GraphicsUnit.Pixel))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.Clear(Color.FromArgb(0, 0, 0, 0));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(words, font, Brushes.Black, new RectangleF(0, 0, W * SS, WORDS_H * SS), sf);
        }
        bool[,] ink = new bool[W, WORDS_H];
        for (int y = 0; y < WORDS_H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                double cover = 0;
                for (int j = 0; j < SS; j++)
                {
                    for (int i = 0; i < SS; i++)
                    {
                        cover += big.GetPixel(x * SS + i, y * SS + j).A / 255.0;
                    }
                }
                cover /= SS * SS;
                ink[x, y] = cover > 0.36 + (fine[x, WORDS_TOP + y] - 0.5) * 0.25;
            }
        }
        return ink;
    }

    // The words' ink mask (W x WORDS_H, from WORDS_TOP) onto the page. A few stroke ends run down as drips with a drop
    // at the end, and a couple of blots land nearby.
    static Bitmap WithWords(Bitmap page, bool[,] ink, double[,] grain, Color[] inkTones, int seed)
    {
        Bitmap result = (Bitmap)page.Clone();
        Random rng = new Random(seed);
        // Drips: stroke ends (ink with two rows of paper under it), at least 14 px apart.
        System.Collections.Generic.List<int> dripXs = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<Point> ends = new System.Collections.Generic.List<Point>();
        for (int y = 1; y < WORDS_H - 2; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (ink[x, y] && ink[x, y - 1] && !ink[x, y + 1] && !ink[x, y + 2])
                {
                    ends.Add(new Point(x, y));
                }
            }
        }
        for (int n = 0; n < 600 && dripXs.Count < 8 && ends.Count > 0; n++)
        {
            Point p = ends[rng.Next(ends.Count)];
            if (dripXs.Exists(dx => Math.Abs(dx - p.X) < 10))
            {
                continue;
            }
            dripXs.Add(p.X);
            int len = 8 + rng.Next(18);
            for (int k = 1; k <= len; k++)
            {
                int yy = WORDS_TOP + p.Y + k;
                // Two pixels wide where it leaves the stroke, thinning to one.
                Ink(result, p.X, yy, inkTones, grain);
                if (k <= len / 2)
                {
                    Ink(result, p.X + 1, yy, inkTones, grain);
                }
            }
            // The drop at the end.
            int by = WORDS_TOP + p.Y + len + 1;
            for (int j = 0; j <= 2; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (!(j == 2 && i != 0) && !(j == 0 && i == -1))
                    {
                        Ink(result, p.X + i, by + j, inkTones, grain);
                    }
                }
            }
        }
        for (int y = 0; y < WORDS_H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (ink[x, y])
                {
                    Ink(result, x, WORDS_TOP + y, inkTones, grain);
                }
            }
        }
        // Two blots where the brush touched down.
        for (int b = 0; b < 2; b++)
        {
            int cx = 70 + rng.Next(W - 140), cy = WORDS_TOP + WORDS_H - 6 + rng.Next(10);
            for (int j = -2; j <= 2; j++)
            {
                for (int i = -2; i <= 2; i++)
                {
                    if (i * i + j * j <= 3 + rng.Next(2))
                    {
                        Ink(result, cx + i, cy + j, inkTones, grain);
                    }
                }
            }
        }
        return result;
    }

    static void Ink(Bitmap bmp, int x, int y, Color[] inkTones, double[,] grain)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || bmp.GetPixel(x, y).A == 0)
        {
            return;
        }
        int tone = grain[x, y] > 0.75 ? 1 : 0;
        bmp.SetPixel(x, y, inkTones[tone]);
    }
}

// "我们看见" written stroke by stroke with a pixel brush instead of a font. Each character sits in a 40 x 40 box; a
// stroke is a list of (x, y, radius) points, so the brush presses in at the start and the turns, and lifts off at
// the end of 撇 and 提. The words lean right, each character a little bigger or lower than the last, a few strokes
// joined by hairlines as in running script, and the brush edge wobbles with the paper noise.
public static class HandBrush
{
    const int BOX = 40, GAP = 4;
    // The characters are drawn 15% larger than their 40 px boxes, with a brush 70% as wide as written.
    const double SIZE = 1.15, BRUSH = 0.7;

    static readonly double[][][] Chars =
    {
        // 我
        new double[][]
        {
            new double[] { 17, 3, 2.6, 12, 7, 2.0, 7, 9, 0.9 },
            new double[] { 3, 15, 2.4, 18, 13.5, 2.0, 35, 12, 2.7 },
            new double[] { 13, 7, 2.8, 13.5, 22, 2.4, 13, 34, 2.4, 9, 31, 0.9 },
            new double[] { 3, 27, 2.4, 12, 23, 1.8, 21, 19, 0.8 },
            new double[] { 20, 3, 2.7, 21.5, 14, 2.4, 25, 24, 2.4, 30, 31, 2.4, 36, 36, 2.7, 37.5, 29, 0.9 },
            new double[] { 32, 19, 2.6, 27, 25, 2.0, 21, 31, 0.9 },
            new double[] { 29, 4, 1.5, 32, 8, 2.5 },
        },
        // 们
        new double[][]
        {
            new double[] { 13, 2, 2.6, 9, 10, 2.2, 3, 19, 0.9 },
            new double[] { 9, 13, 2.6, 9.5, 25, 2.4, 9, 38, 1.7 },
            new double[] { 17, 3, 1.5, 19.5, 7.5, 2.5 },
            new double[] { 19.5, 7.5, 0.6, 17, 10.5, 0.6 },  // hairline into the next stroke
            new double[] { 16.5, 11, 2.6, 17, 24, 2.4, 16.5, 38, 2.0 },
            new double[] { 21, 8, 2.4, 28, 7.5, 2.0, 35, 7, 2.9, 35.5, 20, 2.4, 35, 33, 2.4, 33, 36, 2.0, 29, 33, 0.8 },
        },
        // 看
        new double[][]
        {
            new double[] { 31, 2, 2.6, 21, 5, 2.0, 11, 7, 0.9 },
            new double[] { 11, 10.5, 2.2, 30, 9.5, 2.5 },
            new double[] { 30, 9.5, 0.6, 7, 15, 0.6 },  // hairline
            new double[] { 6, 15.5, 2.4, 22, 14.5, 2.0, 35, 14, 2.7 },
            new double[] { 22, 6, 2.6, 18, 17, 2.4, 12, 26, 2.0, 4, 34, 0.8 },
            new double[] { 14, 21, 2.6, 14.5, 38, 2.4 },
            new double[] { 14, 21, 2.2, 32, 20, 2.7, 31.5, 30, 2.4, 31.5, 38, 2.4 },
            new double[] { 15.5, 26, 1.8, 30, 25.5, 2.0 },
            new double[] { 15.5, 31, 1.8, 30, 30.5, 2.0 },
            new double[] { 15, 37.5, 2.0, 31, 37, 2.3 },
        },
        // 见
        new double[][]
        {
            new double[] { 9, 4, 2.6, 9.5, 14, 2.4, 9, 24, 2.2 },
            new double[] { 9, 4, 2.2, 29, 3.5, 2.7, 28.5, 14, 2.4, 28, 23, 2.2 },
            new double[] { 19, 10, 2.6, 18.5, 20, 2.4, 14, 29, 1.8, 5, 37, 0.8 },
            new double[] { 24, 10, 2.6, 24, 22, 2.4, 24.5, 32, 2.4, 28, 36, 2.2, 36, 36.5, 2.5, 38, 30, 0.8 },
        },
    };

    // Per character: scale, x nudge, y nudge.
    static readonly double[,] Placement = { { 1.02, 0, 1 }, { 0.96, 1, 4 }, { 1.06, 0, -1 }, { 1.0, 2, 3 } };

    public static bool[,] Words(double[,] fine)
    {
        int w = 314, h = 70, top = 262;
        bool[,] ink = new bool[w, h];
        Random rng = new Random(4242);
        double total = (Chars.Length * BOX + (Chars.Length - 1) * GAP) * SIZE;
        double x0 = (w - total) / 2.0;
        for (int c = 0; c < Chars.Length; c++)
        {
            double scale = Placement[c, 0] * SIZE;
            double ox = x0 + c * (BOX + GAP) * SIZE + Placement[c, 1];
            double oy = 8 + Placement[c, 2];
            foreach (double[] stroke in Chars[c])
            {
                int n = stroke.Length / 3;
                double[] px = new double[n], py = new double[n], pr = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double sx = stroke[i * 3] + (rng.NextDouble() - 0.5) * 1.2;
                    double sy = stroke[i * 3 + 1] + (rng.NextDouble() - 0.5) * 1.2;
                    // Lean right: the top of each character sits further right than the bottom.
                    px[i] = ox + (sx + (BOX / 2.0 - sy) * 0.13) * scale;
                    py[i] = oy + sy * scale;
                    pr[i] = Math.Max(0.6, stroke[i * 3 + 2] * scale * BRUSH);
                }
                for (int i = 0; i + 1 < n; i++)
                {
                    double len = Math.Sqrt((px[i + 1] - px[i]) * (px[i + 1] - px[i]) + (py[i + 1] - py[i]) * (py[i + 1] - py[i]));
                    int steps = Math.Max(2, (int)(len * 4));
                    for (int s = 0; s <= steps; s++)
                    {
                        double t = (double)s / steps;
                        double cx = px[i] + (px[i + 1] - px[i]) * t;
                        double cy = py[i] + (py[i + 1] - py[i]) * t;
                        double r = pr[i] + (pr[i + 1] - pr[i]) * t;
                        Stamp(ink, cx, cy, r, fine, top, w, h);
                    }
                }
            }
        }
        return ink;
    }

    // One dab of the brush: a disc whose edge follows the paper noise. A thin, fast stroke skips a pixel now and then
    // (dry brush).
    static void Stamp(bool[,] ink, double cx, double cy, double r, double[,] fine, int top, int w, int h)
    {
        int x0 = (int)Math.Floor(cx - r - 1), x1 = (int)Math.Ceiling(cx + r + 1);
        int y0 = (int)Math.Floor(cy - r - 1), y1 = (int)Math.Ceiling(cy + r + 1);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h)
                {
                    continue;
                }
                double n = fine[x, top + y];
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double reach = r + (n - 0.5) * 0.9;
                if (r < 1.2 && n > 0.78)
                {
                    continue;
                }
                if (dx * dx + dy * dy <= reach * reach)
                {
                    ink[x, y] = true;
                }
            }
        }
    }
}
