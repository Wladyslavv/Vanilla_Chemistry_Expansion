// Draws the fermenter (48x64, 3x4 cells, same footprint as the vanilla hydroponic machine), in two states:
// vce_fermenter (empty) and vce_fermenter_b (with broth).
//
// Perspective, copied from the vanilla hydroponic machine: the viewer is slightly above the machine, so every
// horizontal surface shows as a flattened ellipse and is lit a step brighter (A8AEA5) than the front faces.
//   - the base's top surface is visible, like the hydroponic's water trough;
//   - the rings around the tank (top flange, jacket bands, bottom flange) dip about 2 px in the middle, because
//     we see the front half of each ring;
//   - the dome and the motor on top are drawn looking down on them.
// Palette from the vanilla hydroponic and the mod's other machines; broth uses the lime/ochre family so it reads
// as a culture rather than water.
// C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawFermenter.cs
// Usage: DrawFermenter.exe <out dir> [reference sprites for the comparison sheet...]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawFermenter
{
    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    // Grey metal. TOP is the hydroponic's lit horizontal surface; the others shade the vertical faces.
    static readonly Color OUT = Hex("292234"), M_DEEP = Hex("373541"), M_DK = Hex("44464c"), M = Hex("5f6868"),
        M_LT = Hex("88948a"), TOP = Hex("a8aea5"), CREAM = Hex("d5d2b8"), CAVITY = Hex("303a41");
    static readonly Color T_DK = Hex("275654"), T_MD = Hex("2f756f"), T = Hex("45918b"), T_LT = Hex("67aaa5"), GLASS = Hex("8abfbb");
    static readonly Color RED = Hex("c24a3d"), RED_DEEP = Hex("64343b");
    static readonly Color WARM = Hex("e0d09b");
    // Culture broth.
    static readonly Color B_LT = Hex("c9c35a"), B_MD = Hex("a8bb7f"), B_DK = Hex("7b8a4f");

    // Tank body. Narrower than the base, so the base's top surface shows all round it like the hydroponic's trough.
    const int X0 = 11, X1 = 36;
    const double CX = (X0 + X1) / 2.0, A = (X1 - X0) / 2.0;

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

    // How far the front half of a horizontal ring dips at horizontal position x (0 at the sides, depth in the middle).
    static int Dip(int x, double depth)
    {
        double t = (x - CX) / A;
        if (t < -1.0) t = -1.0; else if (t > 1.0) t = 1.0;
        return (int)Math.Round(depth * Math.Sqrt(1.0 - t * t));
    }

    // One row of the cylinder's front face, shaded like a tube lit from the top left.
    static void CylRow(Canvas c, int y, int x0, int x1)
    {
        for (int x = x0; x <= x1; x++)
        {
            double t = (x - x0) / (double)(x1 - x0);
            Color col = t < 0.04 ? M : t < 0.12 ? M_LT : t < 0.24 ? CREAM
                : t < 0.46 ? M_LT : t < 0.72 ? M : t < 0.88 ? M_DK : M_DEEP;
            c.Put(x, y, col);
        }
    }

    // A ring around the tank: the front half of a horizontal band, dipping in the middle.
    static void Ring(Canvas c, int y, int thickness, Color top, Color face, int widen)
    {
        for (int x = X0 - widen; x <= X1 + widen; x++)
        {
            int dy = y + Dip(x, 2.0);
            c.Put(x, dy, top);
            for (int k = 1; k < thickness; k++) c.Put(x, dy + k, face);
        }
    }

    static Bitmap Fermenter(bool broth)
    {
        Canvas c = new Canvas(48, 64);

        // ---- agitator motor on top (seen slightly from above: its top face is the lit step) -------------------------
        c.Rect(19, 1, 28, 2, TOP); c.HLine(19, 28, 0, M_LT);
        c.Rect(19, 3, 28, 7, M); c.VLine(19, 3, 7, M_LT); c.VLine(20, 3, 7, CREAM);
        c.VLine(27, 3, 7, M_DK); c.VLine(28, 3, 7, M_DEEP);
        for (int y = 4; y <= 6; y++) { c.HLine(22, 26, y, M_DK); }   // cooling fins
        // Gearbox under the motor.
        c.Rect(18, 8, 29, 9, M_LT); c.HLine(18, 29, 8, TOP);
        c.Rect(18, 10, 29, 11, M_DK);

        // ---- domed head ---------------------------------------------------------------------------------------------
        // Looking down on the dome: a bright crown that widens as it comes toward us.
        int[,] dome = { { 15, 32, 12 }, { 13, 34, 13 }, { 12, 35, 14 }, { 11, 36, 15 } };
        for (int i = 0; i < dome.GetLength(0); i++)
        {
            int dx0 = dome[i, 0], dx1 = dome[i, 1], dy = dome[i, 2];
            for (int x = dx0; x <= dx1; x++)
            {
                double t = (x - dx0) / (double)(dx1 - dx0);
                c.Put(x, dy, i == 0 ? (t < 0.45 ? TOP : M_LT) : t < 0.12 ? CREAM : t < 0.5 ? TOP : t < 0.8 ? M_LT : M);
            }
        }
        // Ports on the dome: inoculation port (red cap) left, air inlet (teal) right.
        c.Rect(14, 10, 16, 11, M_DK); c.HLine(14, 16, 10, M_LT); c.Put(15, 9, RED); c.Put(14, 9, RED_DEEP);
        c.Rect(31, 10, 33, 11, M_DK); c.HLine(31, 33, 10, M_LT); c.Put(32, 9, T_LT); c.Put(31, 9, T_MD);

        // ---- top flange ring ------------------------------------------------------------------------------------------
        Ring(c, 16, 3, TOP, M_DK, 1);
        for (int x = X0 + 1; x <= X1; x += 4) c.Put(x, 16 + Dip(x, 2.0), CREAM);   // bolts

        // ---- cylinder body --------------------------------------------------------------------------------------------
        for (int y = 19; y <= 48; y++) CylRow(c, y, X0, X1);

        // Jacket bands: the front half of each ring dips in the middle, and is darker than the body so it reads.
        Ring(c, 24, 1, M_LT, M_DK, 0);
        Ring(c, 25, 2, M_DEEP, M_DK, 0);
        Ring(c, 42, 1, M_LT, M_DK, 0);
        Ring(c, 43, 2, M_DEEP, M_DK, 0);

        // Sight glass: a tall window with the broth level inside.
        c.Rect(19, 28, 28, 41, OUT);
        c.Rect(20, 29, 27, 40, CAVITY);
        if (broth)
        {
            c.Rect(20, 32, 27, 40, B_MD);
            c.HLine(20, 27, 32, B_LT);
            c.Rect(20, 38, 27, 40, B_DK);
            c.VLine(20, 33, 39, B_LT);
            int[,] bubbles = { { 24, 35 }, { 22, 38 }, { 26, 36 }, { 23, 30 }, { 25, 31 } };
            for (int i = 0; i < bubbles.GetLength(0); i++) c.Put(bubbles[i, 0], bubbles[i, 1], i < 3 ? B_LT : WARM);
        }
        c.VLine(20, 29, 31, GLASS);   // glass glint above the broth line

        // Agitator shaft and two impellers, seen through the glass.
        c.VLine(23, 29, 40, broth ? B_DK : M_DK); c.VLine(24, 29, 40, broth ? B_DK : M_DEEP);
        c.HLine(21, 26, 34, broth ? B_DK : M_DK);
        c.HLine(21, 26, 39, broth ? B_DK : M_DK);

        // Air line down the right side into the sparger.
        c.Rect(33, 20, 34, 47, M_DK); c.VLine(33, 20, 47, M_LT);
        c.Put(33, 19, T_MD); c.Put(34, 19, T_DK);

        // ---- base: a rounded box whose top surface we can see, like the hydroponic's --------------------------------
        // The tank goes down into a collar on the base, so the two read as one machine.
        int[,] top = { { 8, 39, 49 }, { 6, 41, 50 }, { 5, 42, 51 }, { 4, 43, 52 }, { 4, 43, 53 }, { 4, 43, 54 } };
        for (int i = 0; i < top.GetLength(0); i++)
        {
            for (int x = top[i, 0]; x <= top[i, 1]; x++)
            {
                if (x >= X0 - 1 && x <= X1 + 1 && i < 2) continue;   // the tank still occupies the middle up here
                c.Put(x, top[i, 2], TOP);
            }
        }
        // Coolant channel sunk into the top surface, to each side of the tank: the hydroponic's trough read.
        foreach (int[] span in new int[][] { new int[] { 5, 9 }, new int[] { 38, 42 } })
        {
            c.Rect(span[0], 52, span[1], 53, T_MD);
            c.HLine(span[0], span[1], 52, T_DK);
            c.Put(span[0] + 1, 53, T_LT);
        }
        c.HLine(4, 43, 55, M_LT);
        // Collar where the tank enters the base.
        Ring(c, 47, 2, TOP, M_DK, 2);
        // Front face.
        c.Rect(4, 56, 43, 61, M);
        c.HLine(4, 43, 61, M_DK); c.HLine(5, 42, 62, M_DEEP);
        c.VLine(4, 56, 61, M_LT); c.VLine(5, 56, 61, M_LT);
        c.VLine(42, 56, 61, M_DK); c.VLine(43, 56, 61, M_DEEP);
        // Readout window on the left, like the hydroponic's.
        c.Rect(7, 57, 19, 60, OUT); c.Rect(8, 58, 18, 59, CAVITY);
        int[,] digits = { { 9, 59 }, { 10, 59 }, { 11, 58 }, { 13, 59 }, { 14, 58 }, { 15, 59 }, { 17, 58 }, { 17, 59 } };
        for (int i = 0; i < digits.GetLength(0); i++) c.Put(digits[i, 0], digits[i, 1], broth ? B_LT : T_MD);
        // Status lights and buttons on the right.
        c.Put(23, 59, broth ? B_MD : M_DEEP); c.Put(25, 59, broth ? RED : RED_DEEP);
        c.Rect(29, 57, 31, 60, M_DK); c.HLine(29, 31, 57, M_LT);
        c.Rect(34, 57, 36, 60, M_DK); c.HLine(34, 36, 57, M_LT);
        c.Rect(38, 57, 39, 60, T_DK); c.HLine(38, 39, 57, T_MD);
        // Rounded corners.
        c.Clear(4, 61); c.Clear(43, 61); c.Clear(5, 62); c.Clear(42, 62);

        return c.Render();
    }

    public static void Main(string[] args)
    {
        string dir = args[0];
        Directory.CreateDirectory(dir);
        List<Bitmap> sheet = new List<Bitmap>();
        Bitmap empty = Fermenter(false), full = Fermenter(true);
        empty.Save(Path.Combine(dir, "vce_fermenter.png"), ImageFormat.Png);
        full.Save(Path.Combine(dir, "vce_fermenter_b.png"), ImageFormat.Png);
        sheet.Add(empty); sheet.Add(full);
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
            outB.Save(Path.Combine(dir, "fermenter_compare.png"), ImageFormat.Png);
        }
    }
}
