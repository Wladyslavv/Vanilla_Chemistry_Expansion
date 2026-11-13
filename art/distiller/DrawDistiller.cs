// Draws the fractional distiller (48x48, 3x3 cells, like the vanilla chem machines and the electrolyzer) in four
// variants: vce_distiller_<input><output>, 1 where a liquid container is plugged in. Industrial plant style:
//   centre steel column with bolted flange rings, a pressure gauge and a sight glass (boiling liquid while it runs)
//   top    horizontal shell-and-tube condenser with coolant nozzles, fed by the vapour line from the column head
//   left   feed drum (teal band) piped to the column through a red handwheel valve
//   right  product drum (cream band) under the condenser's rundown line and its valve
//   base   steel skid with a hazard-striped edge
// Without a container the pipe ends in an open flange over an empty pad. Palette from the vanilla evaporator /
// chem finisher, shared with art/salt/DrawElectrolysis.cs. The first, glassware design is distiller_compare_v1.png.
// C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawDistiller.cs
// Usage: DrawDistiller.exe <out dir> [reference sprites for the comparison sheet...]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawDistiller
{
    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static readonly Color OUT = Hex("292234"), M_DEEP = Hex("373541"), M_DK = Hex("44464c"), M = Hex("5f6868"),
        M_LT = Hex("88948a"), CREAM = Hex("d5d2b8"), CAVITY = Hex("303a41");
    static readonly Color T_DK = Hex("275654"), T_MD = Hex("2f756f"), T = Hex("45918b"), T_LT = Hex("67aaa5"), GLASS = Hex("8abfbb");
    static readonly Color RED = Hex("c24a3d"), RED_DK = Hex("9b4a3d"), RED_DEEP = Hex("64343b"), RED_LT = Hex("e07a66");
    static readonly Color LIME = Hex("c9c35a"), LIME_MD = Hex("a8bb7f"), LIME_DK = Hex("7b8a4f");

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

    // A 2 px steel pipe, horizontal or vertical, lit from the top left.
    static void PipeH(Canvas c, int x0, int x1, int y) { c.HLine(x0, x1, y, M_LT); c.HLine(x0, x1, y + 1, M_DK); }
    static void PipeV(Canvas c, int x, int y0, int y1) { c.VLine(x, y0, y1, M_LT); c.VLine(x + 1, y0, y1, M_DK); }

    // A red handwheel valve centred on (x, y) of a 2 px pipe: wheel 5 wide, stem down to the pipe.
    static void Valve(Canvas c, int x, int y)
    {
        c.HLine(x - 2, x + 2, y - 3, RED); c.Put(x - 2, y - 3, RED_LT); c.Put(x + 2, y - 3, RED_DK);
        c.Put(x, y - 2, M_DK);
        c.Rect(x - 1, y - 1, x + 1, y + 2, M); c.VLine(x - 1, y - 1, y + 2, M_LT); c.VLine(x + 1, y - 1, y + 2, M_DK);
    }

    // An open pipe end: a flange with a dark bore, pointing down.
    static void OpenFlange(Canvas c, int x, int y)
    {
        c.HLine(x - 1, x + 2, y, M_LT); c.HLine(x - 1, x + 2, y + 1, M_DK); c.HLine(x, x + 1, y + 1, OUT);
    }

    // A 13x13 steel drum with its top-left at (x, y): lid with a bung, two hoops and a coloured band.
    static void Drum(Canvas c, int x, int y, Color band, Color bandLt, Color bandDk)
    {
        const int W = 13, H = 13;
        c.Rect(x, y, x + W - 1, y + H - 1, M);
        // Lid.
        c.HLine(x, x + W - 1, y, M_LT); c.HLine(x + 1, x + W - 2, y + 1, M_DK); c.Put(x + 9, y, OUT); c.Put(x + 3, y + 1, M_LT);
        // Shading: lit left edge, dark right edge.
        c.VLine(x, y + 1, y + H - 2, M_LT); c.VLine(x + 1, y + 2, y + H - 2, CREAM);
        c.VLine(x + W - 1, y + 1, y + H - 1, M_DK); c.VLine(x + W - 2, y + 2, y + H - 2, M);
        // Hoops.
        foreach (int hy in new int[] { y + 3, y + H - 3 })
        {
            c.HLine(x, x + W - 1, hy, M_DK); c.HLine(x + 1, x + W - 2, hy - 1, M_LT);
        }
        // Content band.
        c.Rect(x + 1, y + 5, x + W - 2, y + 7, band);
        c.HLine(x + 1, x + W - 2, y + 5, bandLt); c.HLine(x + 1, x + W - 2, y + 7, bandDk);
        c.Put(x + 1, y + 6, bandLt);
        // Bottom rim.
        c.HLine(x, x + W - 1, y + H - 1, M_DEEP);
        // Rounded corners and a domed lid, so it reads as a drum rather than a crate.
        c.Clear(x, y); c.Clear(x + W - 1, y); c.Clear(x, y + H - 1); c.Clear(x + W - 1, y + H - 1);
        c.Put(x + 1, y, M_LT); c.Put(x + W - 2, y, M_DK);
        c.Put(x + 2, y, CREAM); c.Put(x + 3, y, CREAM);
    }

    static Bitmap Distiller(bool input, bool output)
    {
        Canvas c = new Canvas(48, 48);

        // ---- skid -----------------------------------------------------------------------------------------------------
        c.Rect(1, 39, 46, 43, M_DK);
        c.HLine(1, 46, 39, M_LT); c.Put(1, 39, CREAM); c.Put(2, 39, CREAM);
        // Hazard stripes along the front edge.
        for (int x = 2; x <= 45; x++)
        {
            for (int y = 40; y <= 42; y++)
            {
                c.Put(x, y, ((x + y) / 3) % 2 == 0 ? LIME : M_DEEP);
            }
        }
        c.HLine(1, 46, 43, M_DEEP);
        c.Rect(3, 44, 7, 45, M_DEEP); c.Rect(40, 44, 44, 45, M_DEEP); c.Rect(21, 44, 26, 45, M_DEEP);
        // Drum pads.
        c.HLine(3, 15, 38, M_DEEP); c.HLine(32, 44, 38, M_DEEP);

        // ---- condenser: horizontal shell-and-tube unit ----------------------------------------------------------------
        // Vapour line from the column head.
        PipeH(c, 27, 31, 4);
        // Shell.
        c.Rect(31, 3, 45, 9, M);
        c.HLine(31, 45, 3, M_LT); c.HLine(32, 44, 4, CREAM); c.HLine(31, 45, 8, M_DK); c.HLine(31, 45, 9, M_DEEP);
        // End caps and the tube-sheet flanges.
        c.Rect(30, 2, 31, 10, M_DK); c.VLine(30, 2, 10, M_LT);
        c.Rect(45, 2, 46, 10, M_DK); c.VLine(45, 2, 10, M_LT);
        // Coolant nozzles (teal) on top.
        c.Rect(34, 1, 35, 2, T_MD); c.Put(34, 1, T_LT); c.Put(35, 0, T); c.Put(34, 0, T_LT);
        c.Rect(41, 1, 42, 2, T_MD); c.Put(41, 1, T_LT); c.Put(42, 0, T); c.Put(41, 0, T_LT);
        // Shell bolts.
        c.Put(33, 6, M_DK); c.Put(38, 6, M_DK); c.Put(43, 6, M_DK);

        // Rundown line from the condenser to the product drum, with its valve.
        PipeV(c, 38, 10, 22);
        Valve(c, 38, 16);
        if (output)
        {
            PipeV(c, 38, 23, 25);
            Drum(c, 32, 26, CREAM, Hex("ece8d3"), M_LT);
        }
        else
        {
            OpenFlange(c, 38, 23);
        }

        // ---- column -----------------------------------------------------------------------------------------------------
        // Dome head.
        c.Rect(21, 2, 26, 3, M); c.HLine(21, 26, 2, M_LT); c.Put(21, 2, CREAM);
        c.Rect(20, 4, 27, 5, M); c.HLine(20, 27, 4, M_LT);
        // Shell.
        c.Rect(19, 6, 28, 38, M);
        c.VLine(19, 6, 38, M_LT); c.VLine(20, 6, 38, CREAM); c.VLine(28, 6, 38, M_DK); c.VLine(27, 6, 38, M_DK);
        // Bolted flange rings.
        foreach (int fy in new int[] { 10, 19, 28 })
        {
            c.Rect(18, fy, 29, fy + 1, M_DK); c.HLine(18, 29, fy, M_LT);
            for (int bx = 19; bx <= 28; bx += 3) c.Put(bx, fy, CREAM);
        }
        // Pressure gauge on the top section.
        c.Rect(22, 6, 25, 9, CREAM); c.Put(22, 6, M_LT); c.Put(25, 9, M_LT);
        c.Put(23, 7, OUT); c.Put(24, 8, input ? RED : OUT);
        // Sight glass on the middle section: boiling liquid while running.
        c.Rect(22, 13, 25, 17, OUT); c.Rect(23, 13, 24, 17, CAVITY);
        if (input)
        {
            c.Rect(23, 15, 24, 17, T); c.Put(23, 15, GLASS); c.Put(24, 14, CREAM); c.Put(23, 13, T_LT);
        }
        // Control box on the bottom section: status lights.
        c.Rect(21, 31, 26, 35, M_DEEP); c.Rect(22, 32, 25, 34, CAVITY);
        c.Put(22, 33, input ? RED : RED_DEEP); c.Put(24, 33, output ? LIME_MD : LIME_DK);
        c.Put(25, 32, T_LT);

        // ---- feed: drum, line and valve -----------------------------------------------------------------------------------
        // Line from the column to above the feed drum.
        PipeH(c, 9, 17, 21);
        Valve(c, 13, 21);
        PipeV(c, 9, 21, input ? 25 : 22);
        if (input)
        {
            Drum(c, 3, 26, T_MD, T_LT, T_DK);
        }
        else
        {
            OpenFlange(c, 9, 23);
        }

        return c.Render();
    }

    public static void Main(string[] args)
    {
        string dir = args[0];
        Directory.CreateDirectory(dir);
        List<Bitmap> sheet = new List<Bitmap>();
        string[] names = { "00", "10", "01", "11" };
        foreach (string n in names)
        {
            Bitmap b = Distiller(n[0] == '1', n[1] == '1');
            b.Save(Path.Combine(dir, "vce_distiller_" + n + ".png"), ImageFormat.Png);
            if (n == "00") b.Save(Path.Combine(dir, "vce_distiller.png"), ImageFormat.Png);
            sheet.Add(b);
        }
        for (int i = 1; i < args.Length; i++) sheet.Add(new Bitmap(args[i]));

        // Comparison sheet at 6x: the four variants, then the reference sprites.
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
            outB.Save(Path.Combine(dir, "distiller_compare.png"), ImageFormat.Png);
        }
    }
}
