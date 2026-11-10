// Draws the cyberpunk chili sprites (magenta-purple): seed packet, produce in three sizes, and the two
// hydroponic overlays. C# 5 (the .NET Framework csc that ships with Windows), System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawChili.cs
// Usage: DrawChili.exe <vanilla plant sprite dir> <xinke seed png> <out dir>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class DrawChili
{
    static readonly Color OUTLINE = Hex("292234");
    static readonly Color EDGE = Hex("2c2236");
    // Chili skin: deep plum to hot magenta, with a pale pink glint.
    static readonly Color PEP_DK = Hex("5e1a45"), PEP = Hex("9c2463"), PEP_LT = Hex("cf3f7f"), PEP_HI = Hex("f59bc6");
    // Unripe chili: near-black violet.
    static readonly Color RAW_DK = Hex("2f2142"), RAW = Hex("4a3163"), RAW_LT = Hex("6d4f86");
    // Calyx and stem: the vanilla leaf greens.
    static readonly Color ST_DK = Hex("41603f"), ST = Hex("5c6f46"), ST_LT = Hex("848552");
    // Foliage: dark purple leaves, like an ornamental black pepper.
    static readonly Color LF_DK = Hex("33263f"), LF = Hex("4b3559"), LF_LT = Hex("664a74"), LF_HI = Hex("8a6a93");
    static readonly Color BR_DK = Hex("3b2c3d"), BR = Hex("5a4152");
    static readonly Color PETAL = Hex("ddd0e8"), PETAL_DK = Hex("b39cc6"), POLLEN = Hex("e05a96");

    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    // ---- a pixel canvas with a silhouette outline pass -------------------------------------------------------

    class Canvas
    {
        public int W, H;
        public Dictionary<long, Color> Px = new Dictionary<long, Color>();
        public HashSet<long> Ring = new HashSet<long>();
        public int MinX = 0, MaxX = 9999, MinY = 0, MaxY = 9999;

        public Canvas(int w, int h) { W = w; H = h; MaxX = w - 1; MaxY = h - 1; }
        public static long K(int x, int y) { return ((long)y << 16) | (uint)x; }

        public void Put(int x, int y, Color c)
        {
            if (x < MinX || x > MaxX || y < MinY || y > MaxY) return;
            Px[K(x, y)] = c;
        }

        public bool Has(int x, int y) { return Px.ContainsKey(K(x, y)); }

        public Bitmap Render(Color filler, int outlineMaxY)
        {
            Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) b.SetPixel(x, y, filler);
            HashSet<long> outer = new HashSet<long>();
            List<long> keys = new List<long>(Px.Keys);
            foreach (long k in keys)
            {
                int x = (int)(k & 0xffff), y = (int)(k >> 16);
                int[,] n = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + n[i, 0], ny = y + n[i, 1];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H || ny > outlineMaxY || Has(nx, ny)) continue;
                    if (Ring.Contains(k)) Px[k] = OUTLINE;
                    else outer.Add(K(nx, ny));
                }
            }
            foreach (long k in outer) b.SetPixel((int)(k & 0xffff), (int)(k >> 16), OUTLINE);
            foreach (KeyValuePair<long, Color> kv in Px) b.SetPixel((int)(kv.Key & 0xffff), (int)(kv.Key >> 16), kv.Value);
            return b;
        }
    }

    // ---- chili: a tapered body along a quadratic curve, lit from the upper left ------------------------------

    class Skin { public Color Dk, Mid, Lt, Hi; public Skin(Color d, Color m, Color l, Color h) { Dk = d; Mid = m; Lt = l; Hi = h; } }

    static void Chili(Canvas c, double x0, double y0, double x1, double y1, double x2, double y2, double half, Skin s,
        bool calyx, double stemDx, double stemDy, int stemLen)
    {
        const int N = 240;
        double[] cx = new double[N + 1], cy = new double[N + 1];
        for (int i = 0; i <= N; i++)
        {
            double t = (double)i / N, u = 1 - t;
            cx[i] = u * u * x0 + 2 * u * t * x1 + t * t * x2;
            cy[i] = u * u * y0 + 2 * u * t * y1 + t * t * y2;
        }
        double lx = -0.8, ly = -0.6;
        int minX = (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2)) - half - 2), maxX = (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2)) + half + 2);
        int minY = (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2)) - half - 2), maxY = (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2)) + half + 2);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                int best = 0; double bd = 1e9;
                for (int i = 0; i <= N; i++)
                {
                    double d = (px - cx[i]) * (px - cx[i]) + (py - cy[i]) * (py - cy[i]);
                    if (d < bd) { bd = d; best = i; }
                }
                double t = (double)best / N;
                double prof = t < 0.14 ? 0.72 + 0.28 * t / 0.14 : Math.Pow(1 - (t - 0.14) / 0.86, 0.8);
                double w = half * prof;
                double dist = Math.Sqrt(bd);
                if (dist > w + 0.15 || (t > 0.985)) continue;
                double vx = (px - cx[best]) / Math.Max(w, 0.6), vy = (py - cy[best]) / Math.Max(w, 0.6);
                double lit = vx * lx + vy * ly;
                double r = Math.Sqrt(vx * vx + vy * vy);
                Color col;
                if (lit > 0.45 && r > 0.3 && r < 0.85 && t > 0.08 && t < 0.62) col = s.Hi;
                else if (lit > 0.12) col = s.Lt;
                else if (lit > -0.4) col = s.Mid;
                else col = s.Dk;
                c.Put(x, y, col);
            }
        }
        if (!calyx) return;
        // calyx: a green cap over the shoulder, then a curved stem
        double tx = cx[8] - cx[0], ty = cy[8] - cy[0];
        double tl = Math.Sqrt(tx * tx + ty * ty); tx /= tl; ty /= tl;
        double nx = -ty, ny = tx;
        for (int y = (int)(y0 - half - 2); y <= (int)(y0 + half + 2); y++)
        {
            for (int x = (int)(x0 - half - 2); x <= (int)(x0 + half + 2); x++)
            {
                double dx = x + 0.5 - x0, dy = y + 0.5 - y0;
                double along = dx * tx + dy * ty, across = dx * nx + dy * ny;
                // a cap over the shoulder whose lower edge is cut into a few pointed sepals (triangular wave)
                double u = across / half;
                double tw = Math.Abs(((u * 1.5 + 10.5) % 1.0) * 2 - 1);
                double edge = half * (0.4 + 0.6 * (1 - tw)) * (1 - 0.3 * Math.Abs(u)) + 0.3;
                // the cap covers the whole head: every skin pixel above the sepal edge turns green
                bool skin = c.Has(x, y);
                if (along < edge && (skin || (along > -1.4 && Math.Abs(across) < half * 1.02)))
                {
                    double lit = across * (nx * lx + ny * ly) / Math.Max(half, 1);
                    Color g = lit > 0.15 ? ST_LT : (lit > -0.25 ? ST : ST_DK);
                    if (along > edge - 1.0 && along > 0.5) g = ST_DK;  // shadowed rim where the sepals meet the skin
                    c.Put(x, y, g);
                }
            }
        }
        // stem: two pixels thick for the lower part on bigger chilis, so it reads as a sturdy green stalk
        double sx = x0 - tx * 1.0, sy = y0 - ty * 1.0;
        for (int i = 0; i < stemLen; i++)
        {
            double k = (double)i / Math.Max(1, stemLen - 1);
            double ex = sx - tx * i + stemDx * k * k * stemLen, ey = sy - ty * i + stemDy * k * k * stemLen;
            int px0 = (int)Math.Floor(ex), py0 = (int)Math.Floor(ey);
            if (half >= 3 && i < stemLen * 0.6) c.Put(px0 + 1, py0, ST_DK);
            c.Put(px0, py0, i < stemLen - 1 ? ST : ST_LT);
        }
    }

    static Skin Ripe { get { return new Skin(PEP_DK, PEP, PEP_LT, PEP_HI); } }
    static Skin Raw { get { return new Skin(RAW_DK, RAW, RAW_LT, RAW_LT); } }

    // ---- produce ------------------------------------------------------------------------------------------------

    static Bitmap Produce(int w, int h, double[] p, double half, double sdx, double sdy, int stem)
    {
        Canvas c = new Canvas(w, h);
        c.MinX = 1; c.MaxX = w - 2; c.MinY = 1; c.MaxY = h - 2;
        Chili(c, p[0], p[1], p[2], p[3], p[4], p[5], half, Ripe, true, sdx, sdy, stem);
        // The item shape comes from the pixel at the centre of each 16 px cell: an invisible alpha-3 filler makes
        // every cell count, so the diagonal large chili takes its full 2x2 (like the Xinke leaves).
        return c.Render(Color.FromArgb(3, 0, 0, 0), 9999);
    }

    // ---- seed packet: the Xinke packet frame with magenta trim and a chili emblem -------------------------------

    static Bitmap Seed(string xinkeSeed)
    {
        Bitmap src = new Bitmap(xinkeSeed);
        Bitmap b = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        Color paper = Hex("d5d2b8");
        Dictionary<int, Color> swap = new Dictionary<int, Color>();
        swap[Hex("5a2c34").ToArgb()] = Hex("5e1a45");  // trim, dark
        swap[Hex("7e3c3e").ToArgb()] = Hex("9c2463");  // trim, light
        swap[Hex("9e584c").ToArgb()] = Hex("b9558c");  // side bands
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Color p = src.GetPixel(x, y);
                Color o;
                bool frame = x <= 2 || x >= 13 || y <= 3 || y >= 11;
                if (p.A < 8) o = p;
                else if (swap.TryGetValue(p.ToArgb(), out o)) { }
                else if (!frame && p.ToArgb() != OUTLINE.ToArgb()) o = paper;  // clear the Xinke emblem
                else o = p;
                b.SetPixel(x, y, o);
            }
        }
        // emblem: a small chili falling to the lower left
        string[] art =
        {
            "..........", // y3 (frame row, unused)
            "......gs..", // y4
            ".....ggg..", // y5
            "....lmd...", // y6
            "...lmd....", // y7
            "..lmd.....", // y8
            "..md......", // y9
            ".d........", // y10
        };
        Dictionary<char, Color> pal = new Dictionary<char, Color>();
        pal['g'] = ST_DK; pal['s'] = ST; pal['l'] = PEP_LT; pal['m'] = PEP; pal['d'] = PEP_DK;
        for (int r = 1; r < art.Length; r++)
            for (int i = 0; i < art[r].Length; i++)
                if (art[r][i] != '.') b.SetPixel(3 + i, 3 + r, pal[art[r][i]]);
        src.Dispose();
        return b;
    }

    // ---- hydroponic overlays (48x64, plant base at (24,45), drawn under the lamp) --------------------------------

    static void Leaf(Canvas c, double cx, double cy, double length, double width, double ang)
    {
        double a = length / 2.0, b = width / 2.0, ux = Math.Cos(ang), uy = Math.Sin(ang);
        for (int y = (int)(cy - a - 1); y <= (int)(cy + a + 1); y++)
        {
            for (int x = (int)(cx - a - 1); x <= (int)(cx + a + 1); x++)
            {
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double along = dx * ux + dy * uy, across = -dx * uy + dy * ux;
                double v = Math.Pow(along / (a + 0.55), 2) + Math.Pow(across / (b + 0.55), 2);
                double inner = Math.Pow(along / a, 2) + Math.Pow(across / b, 2);
                if (v > 1.0) continue;
                if (inner > 1.0) { c.Put(x, y, EDGE); c.Ring.Add(Canvas.K(x, y)); continue; }
                bool lower = across * (ux >= 0 ? 1 : -1) > 0;
                Color col;
                if (inner > 0.7 && lower) col = LF_DK;
                else if (Math.Abs(across) < 0.45 && Math.Abs(along) < a * 0.8) col = LF_LT;
                else if (lower) col = LF_DK;
                else col = LF;
                if (along > a * 0.35 && !lower && Math.Abs(across) >= 0.45 && Math.Abs(across) < 1.1) col = LF_HI;
                c.Put(x, y, col);
                c.Ring.Remove(Canvas.K(x, y));
            }
        }
    }

    static List<int[]> Line(Canvas c, int x0, int y0, int x1, int y1, Color col)
    {
        int n = Math.Max(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)), 1);
        List<int[]> pts = new List<int[]>();
        for (int i = 0; i <= n; i++)
        {
            int x = (int)Math.Round(x0 + (x1 - x0) * (double)i / n), y = (int)Math.Round(y0 + (y1 - y0) * (double)i / n);
            c.Put(x, y, col);
            pts.Add(new int[] { x, y });
        }
        return pts;
    }

    static Bitmap Plant(string stage, Bitmap coffee)
    {
        Canvas c = new Canvas(48, 64);
        c.MinX = 4; c.MaxX = 44; c.MinY = 12;
        Random rng = new Random(stage == "p" ? 7 : 7);
        // a bushy pepper plant: short trunk, branches fanning low and wide
        int[,] branches = { { 13, 27 }, { 18, 23 }, { 24, 21 }, { 30, 23 }, { 35, 27 }, { 10, 34 }, { 38, 34 } };
        for (int y = 38; y < 46; y++) { c.Put(23, y, BR); c.Put(24, y, BR_DK); }
        List<List<int[]>> stems = new List<List<int[]>>();
        for (int i = 0; i < branches.GetLength(0); i++)
        {
            int ex = branches[i, 0], ey = branches[i, 1];
            int sx = 23 + (ex > 24 ? 1 : 0), sy = Math.Abs(ex - 24) < 12 ? 39 : 41;
            stems.Add(Line(c, sx, sy, ex, ey, BR));
        }
        stems.Sort(delegate(List<int[]> p, List<int[]> q) { return Math.Abs(q[q.Count - 1][0] - 24).CompareTo(Math.Abs(p[p.Count - 1][0] - 24)); });
        List<double[]> hang = new List<double[]>();
        foreach (List<int[]> stem in stems)
        {
            int[] s0 = stem[0], s1 = stem[stem.Count - 1];
            double baseAng = Math.Atan2(s1[1] - s0[1], s1[0] - s0[0]);
            int n = stem.Count, k = 0;
            for (int i = n - 3; i > 5; i -= 5, k++)
            {
                int side = k % 2 == 0 ? 1 : -1;
                double ang = baseAng + side * (Math.PI / 180.0) * (38 + rng.Next(-5, 6));
                double ln = 9 + (i < n * 0.6 ? 1 : 0) + rng.Next(0, 2);
                int x = stem[i][0], y = stem[i][1];
                Leaf(c, x + Math.Cos(ang) * ln * 0.45, y + Math.Sin(ang) * ln * 0.48, ln, 4.8, ang);
                if (rng.NextDouble() < 0.8)
                    hang.Add(new double[] { x + Math.Cos(ang) * ln * 0.35, y + Math.Sin(ang) * ln * 0.35 + 2, side });
            }
            Leaf(c, s1[0] + Math.Cos(baseAng) * 3.5, s1[1] + Math.Sin(baseAng) * 3.5, 8, 4.0, baseAng);
        }
        foreach (List<int[]> stem in stems)
            for (int i = 0; i < stem.Count * 3 / 4; i++) c.Put(stem[i][0], stem[i][1], BR);

        if (stage == "p")
        {
            // star-shaped lilac flowers, plus a few small unripe chilis
            for (int i = 0; i < hang.Count; i++)
            {
                int x = (int)Math.Round(hang[i][0]), y = (int)Math.Round(hang[i][1]);
                if (i % 3 == 1)
                {
                    Chili(c, x, y, x + hang[i][2] * 0.5, y + 3, x - hang[i][2] * 0.5, y + 5.5, 1.4, Raw, false, 0, 0, 0);
                    c.Put(x, y - 1, ST);
                    continue;
                }
                c.Put(x - 1, y - 1, EDGE); c.Put(x + 1, y - 1, EDGE); c.Put(x - 1, y + 1, EDGE); c.Put(x + 1, y + 1, EDGE);
                c.Put(x, y - 1, PETAL); c.Put(x - 1, y, PETAL); c.Put(x + 1, y, PETAL_DK); c.Put(x, y + 1, PETAL_DK);
                c.Put(x, y, POLLEN);
            }
        }
        else
        {
            // ripe chilis hanging under the leaves, tips curling outward
            foreach (double[] h in hang)
            {
                double x = Math.Round(h[0]) + 0.5, y = Math.Round(h[1]) + 0.5, side = h[2];
                double len = 6 + rng.Next(0, 3);
                Chili(c, x, y, x + side * 0.3, y + len * 0.6, x + side * 1.6, y + len, 1.6, Ripe, false, 0, 0, 0);
                // green cap: a stalk pixel above and a 3-wide calyx over the shoulder
                int gx = (int)x, gy = (int)y;
                c.Put(gx, gy - 2, ST_DK);
                c.Put(gx, gy - 1, ST);
                c.Put(gx - 1, gy, ST_LT); c.Put(gx, gy, ST); c.Put(gx + 1, gy, ST_DK);
            }
        }
        Bitmap b = c.Render(Color.FromArgb(3, 0, 0, 0), 46);
        // water ripples around the trunk, from the vanilla coffee overlay
        HashSet<int> water = new HashSet<int>();
        foreach (string w in new string[] { "275654", "45918b", "67aaa5", "273456" }) water.Add(Hex(w).ToArgb());
        for (int y = 42; y < 47; y++)
            for (int x = 10; x < 40; x++)
            {
                Color p = coffee.GetPixel(x, y);
                Color here = p.A > 3 ? Color.FromArgb(255, p.R, p.G, p.B) : p;
                if (p.A > 3 && water.Contains(here.ToArgb()) && !c.Has(x + 1, y)) b.SetPixel(x + 1, y, p);
            }
        return b;
    }

    public static void Main(string[] args)
    {
        string vanilla = args[0], xinkeSeed = args[1], outDir = args[2];
        Directory.CreateDirectory(outDir);
        Bitmap coffee = new Bitmap(Path.Combine(vanilla, "coffee_bean_p.png"));
        Dictionary<string, Bitmap> o = new Dictionary<string, Bitmap>();
        o["vce_chili_seed"] = Seed(xinkeSeed);
        o["vce_chili_s"] = Produce(16, 16, new double[] { 10.5, 4.5, 8.5, 10, 4.5, 13.5 }, 2.3, 1.0, -0.6, 3);
        o["vce_chili"] = Produce(16, 32, new double[] { 8.5, 8.5, 9.5, 21, 5.0, 29 }, 3.4, 0.9, -0.5, 5);
        o["vce_chili_l"] = Produce(32, 32, new double[] { 21.5, 7.5, 21, 20, 8, 27 }, 5.2, 0.8, -0.5, 6);
        o["vce_chili_p"] = Plant("p", coffee);
        o["vce_chili_p2"] = Plant("p2", coffee);
        foreach (KeyValuePair<string, Bitmap> kv in o)
        {
            kv.Value.Save(Path.Combine(outDir, kv.Key + ".png"), ImageFormat.Png);
            Console.WriteLine("saved " + kv.Key);
        }
    }
}
