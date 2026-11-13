// Draws the three antibiotic items:
//   vce_strain          16x16 (1x1)  枝顶孢霉菌菌种: a petri dish seen at a slight angle, cream colonies on amber agar
//   vce_strain_frozen   64x64 (4x4)  冷冻菌种: a cryo dewar, frosted, with a window onto the racked vials
//   vce_antibiotic      16x16 (1x1)  抗生素片: two scored tablets, like the vanilla pills but flat discs
// Palette from the vanilla pills and the mod's other items; the culture keeps the fermenter's ochre family so the
// three read as one chain. C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawStrain.cs
// Usage: DrawStrain.exe <out dir> [reference sprites for the comparison sheet...]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawStrain
{
    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static readonly Color OUT = Hex("292234"), M_DEEP = Hex("373541"), M_DK = Hex("44464c"), M = Hex("5f6868"),
        M_LT = Hex("88948a"), TOP = Hex("a8aea5"), CREAM = Hex("d5d2b8"), CAVITY = Hex("303a41");
    static readonly Color T_DK = Hex("275654"), T_MD = Hex("2f756f"), T_LT = Hex("67aaa5"), GLASS = Hex("8abfbb");
    static readonly Color RED = Hex("c24a3d"), RED_DEEP = Hex("64343b");
    // Culture: agar amber, colonies cream-white.
    static readonly Color AGAR_LT = Hex("d8c89a"), AGAR = Hex("b89a52"), AGAR_DK = Hex("8a7138");
    static readonly Color COL_LT = Hex("f2efe0"), COL = Hex("d5d2b8"), COL_DK = Hex("a8a48c");
    // Frost.
    static readonly Color ICE_LT = Hex("e8f2f5"), ICE = Hex("b8d4dd"), ICE_DK = Hex("7fa8b5");

    class Canvas
    {
        public int W, H;
        public Color?[,] Px;
        public Canvas(int w, int h) { W = w; H = h; Px = new Color?[w, h]; }
        public void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[x, y] = c; }
        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Put(x, y, c);
        }
        public void HLine(int x0, int x1, int y, Color c) { Rect(x0, y, x1, y, c); }
        public void VLine(int x, int y0, int y1, Color c) { Rect(x, y0, x, y1, c); }
        public void Clear(int x, int y) { if (x >= 0 && y >= 0 && x < W && y < H) Px[x, y] = null; }

        // Outline every empty pixel that touches a filled one, then fill the rest with an alpha-1 filler so each
        // 16 px cell centre counts for the item shape.
        public Bitmap Render()
        {
            Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (Px[x, y].HasValue) { b.SetPixel(x, y, Px[x, y].Value); continue; }
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        edge = nx >= 0 && ny >= 0 && nx < W && ny < H && Px[nx, ny].HasValue;
                    }
                    b.SetPixel(x, y, edge ? OUT : Color.FromArgb(1, 0, 0, 0));
                }
            }
            return b;
        }
    }

    // Half-width of an ellipse of half-axes a, b at vertical offset dy from its centre.
    static int EllipseHalf(double a, double b, double dy)
    {
        double t = dy / b;
        if (t <= -1.0 || t >= 1.0) return -1;
        return (int)Math.Round(a * Math.Sqrt(1.0 - t * t));
    }

    static void Ellipse(Canvas c, double cx, double cy, double a, double b, Color fill)
    {
        for (int y = (int)Math.Floor(cy - b); y <= (int)Math.Ceiling(cy + b); y++)
        {
            int half = EllipseHalf(a, b, y - cy);
            if (half < 0) continue;
            c.HLine((int)Math.Round(cx - half), (int)Math.Round(cx + half), y, fill);
        }
    }

    // ---- 1x1 strain: a petri dish ---------------------------------------------------------------------------------
    static Bitmap Strain()
    {
        Canvas c = new Canvas(16, 16);
        // Dish wall below, so the dish has visible depth.
        Ellipse(c, 7.5, 9.5, 6.6, 3.8, M_DK);
        // Glass rim all round.
        Ellipse(c, 7.5, 8.0, 6.6, 3.8, T_MD);
        Ellipse(c, 7.5, 7.7, 6.2, 3.4, GLASS);
        // Agar surface, inset so the rim stays visible.
        Ellipse(c, 7.5, 8.0, 5.0, 2.6, AGAR);
        Ellipse(c, 7.5, 7.8, 4.4, 2.1, AGAR_LT);
        // Shaded far side of the agar.
        for (int x = 4; x <= 11; x++) c.Put(x, 9, AGAR_DK);
        // Colonies: fuzzy cream blobs.
        int[,] colonies = { { 6, 7, 1 }, { 9, 7, 1 }, { 7, 9, 0 }, { 10, 9, 0 }, { 5, 9, 0 } };
        for (int i = 0; i < colonies.GetLength(0); i++)
        {
            int x = colonies[i, 0], y = colonies[i, 1];
            if (colonies[i, 2] == 1)
            {
                c.Rect(x - 1, y, x + 1, y, COL); c.Put(x, y - 1, COL); c.Put(x, y + 1, COL_DK); c.Put(x, y, COL_LT);
            }
            else
            {
                c.Put(x, y, COL); c.Put(x, y + 1, COL_DK);
            }
        }
        // Light catching the near and far edges of the glass.
        c.Put(3, 6, T_LT); c.Put(4, 5, T_LT); c.Put(11, 11, T_DK); c.Put(10, 11, T_DK);
        return c.Render();
    }

    // ---- 3x3 frozen strain: a cryo dewar --------------------------------------------------------------------------
    static Bitmap Frozen()
    {
        Canvas c = new Canvas(48, 48);
        const int X0 = 9, X1 = 38;

        // Carry handle over the lid.
        c.Rect(19, 1, 28, 2, M_LT); c.HLine(19, 28, 1, TOP);
        c.Rect(17, 3, 18, 6, M_DK); c.Rect(29, 3, 30, 6, M_DK);
        c.VLine(17, 3, 6, M_LT); c.VLine(29, 3, 6, M_LT);

        // Lid: top face is the lit step, then the locking ring.
        Ellipse(c, 23.5, 8.0, 9.5, 2.4, TOP);
        Ellipse(c, 23.5, 7.6, 7.0, 1.6, CREAM);
        c.Rect(14, 9, 33, 12, M); c.HLine(14, 33, 9, M_LT);
        c.VLine(14, 9, 12, M_LT); c.VLine(15, 9, 12, CREAM); c.VLine(32, 9, 12, M_DK); c.VLine(33, 9, 12, M_DEEP);
        for (int x = 17; x <= 31; x += 4) c.Put(x, 11, M_DEEP);   // latches
        // Neck ring.
        c.Rect(12, 13, 35, 14, M_DK); c.HLine(12, 35, 13, TOP);

        // Tank body, shaded like a tube lit from the top left.
        for (int y = 15; y <= 41; y++)
        {
            for (int x = X0; x <= X1; x++)
            {
                double t = (x - X0) / (double)(X1 - X0);
                Color col = t < 0.04 ? M : t < 0.12 ? M_LT : t < 0.24 ? CREAM
                    : t < 0.46 ? M_LT : t < 0.72 ? M : t < 0.88 ? M_DK : M_DEEP;
                c.Put(x, y, col);
            }
        }
        // Vacuum-jacket seams.
        for (int x = X0; x <= X1; x++)
        {
            double t = (x - 23.5) / 15.0;
            int dip = (int)Math.Round(2.0 * Math.Sqrt(Math.Max(0.0, 1.0 - t * t)));
            c.Put(x, 17 + dip, M_DEEP);
            c.Put(x, 38 + dip, M_DEEP);
        }

        // Window onto the racked vials.
        c.Rect(13, 21, 34, 35, OUT);
        c.Rect(14, 22, 33, 34, CAVITY);
        for (int i = 0; i < 4; i++)
        {
            int vx = 16 + i * 5;
            c.Rect(vx, 25, vx + 2, 33, GLASS);
            c.VLine(vx, 25, 33, ICE_LT);
            c.VLine(vx + 2, 25, 33, T_DK);
            c.Rect(vx, 27, vx + 2, 32, COL);        // the frozen culture inside
            c.Put(vx + 1, 27, COL_LT);
            c.Rect(vx, 23, vx + 2, 24, M_LT);        // cap
            c.Put(vx + 2, 24, M_DK);
        }
        // Frost creeping over the glass.
        int[,] frost = { { 15, 23 }, { 15, 33 }, { 32, 24 }, { 32, 32 }, { 23, 34 }, { 28, 22 } };
        for (int i = 0; i < frost.GetLength(0); i++) c.Put(frost[i, 0], frost[i, 1], ICE);

        // Frost patches on the steel, heavier toward the bottom.
        int[,] ice = { { 11, 37 }, { 12, 38 }, { 11, 39 }, { 35, 37 }, { 36, 38 }, { 35, 40 },
                       { 13, 19 }, { 34, 19 }, { 19, 39 }, { 29, 40 }, { 24, 38 } };
        for (int i = 0; i < ice.GetLength(0); i++)
        {
            c.Put(ice[i, 0], ice[i, 1], i % 3 == 0 ? ICE_LT : ICE);
            c.Put(ice[i, 0], ice[i, 1] + 1, ICE_DK);
        }

        // Side handles.
        foreach (int hx in new int[] { 7, 40 })
        {
            c.Rect(hx, 24, hx + 1, 31, M_DK);
            c.VLine(hx, 24, 31, M_LT);
            c.Put(hx + (hx < 23 ? 1 : 0), 23, M_LT);
        }

        // Foot ring.
        c.Rect(11, 42, 36, 44, M_DK); c.HLine(11, 36, 42, M_LT); c.HLine(12, 35, 45, M_DEEP);
        c.Clear(11, 44); c.Clear(36, 44);

        // Cold vapour settling over the shoulder.
        int[,] vapour = { { 11, 11 }, { 12, 10 }, { 35, 11 }, { 36, 10 } };
        for (int i = 0; i < vapour.GetLength(0); i++) c.Put(vapour[i, 0], vapour[i, 1], i % 2 == 0 ? ICE : ICE_DK);

        return c.Render();
    }

    // ---- 1x1 antibiotic tablet -------------------------------------------------------------------------------------
    static Bitmap Tablet()
    {
        Canvas c = new Canvas(16, 16);
        // One round scored tablet, face on: rim, body, lit upper face. Two overlapping discs turn to mush at 16 px,
        // so a single clear tablet reads better next to the vanilla capsules.
        Ellipse(c, 7.5, 8.0, 6.2, 4.6, COL_DK);
        Ellipse(c, 7.5, 7.8, 5.5, 3.9, COL);
        Ellipse(c, 7.5, 7.2, 4.5, 2.8, COL_LT);
        // Score groove down the middle.
        c.VLine(7, 4, 11, COL_DK); c.Put(7, 5, COL); c.Put(7, 10, COL);
        // Bevel shading at the bottom and a warm glint at the top left.
        c.HLine(4, 11, 11, COL_DK);
        c.Put(4, 6, AGAR_LT); c.Put(5, 5, AGAR_LT);
        return c.Render();
    }

    public static void Main(string[] args)
    {
        string dir = args[0];
        Directory.CreateDirectory(dir);
        List<Bitmap> sheet = new List<Bitmap>();
        Bitmap strain = Strain(), frozen = Frozen(), tablet = Tablet();
        strain.Save(Path.Combine(dir, "vce_strain.png"), ImageFormat.Png);
        frozen.Save(Path.Combine(dir, "vce_strain_frozen.png"), ImageFormat.Png);
        tablet.Save(Path.Combine(dir, "vce_antibiotic.png"), ImageFormat.Png);
        sheet.Add(frozen); sheet.Add(strain); sheet.Add(tablet);
        for (int i = 1; i < args.Length; i++) sheet.Add(new Bitmap(args[i]));

        const int S = 6, P = 10;
        int w = P, h = 0;
        foreach (Bitmap b in sheet) { w += b.Width * S + P; h = Math.Max(h, b.Height * S); }
        using (Bitmap outB = new Bitmap(w, h + 2 * P))
        using (Graphics g = Graphics.FromImage(outB))
        {
            g.Clear(ColorTranslator.FromHtml("#3A3E47"));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            int x = P;
            foreach (Bitmap b in sheet)
            {
                g.DrawImage(b, new Rectangle(x, P + h - b.Height * S, b.Width * S, b.Height * S));
                x += b.Width * S + P;
            }
            outB.Save(Path.Combine(dir, "strain_compare.png"), ImageFormat.Png);
        }
    }
}
