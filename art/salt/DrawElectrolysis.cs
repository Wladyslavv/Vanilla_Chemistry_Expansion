// Draws the brine electrolyzer (48x48, 3x3 cells, like the vanilla chem machines in Items/items_tool2) and the gas
// canister (16x32, 1x2 cells) in three states: empty, hydrogen and chlorine. Palette taken from the vanilla
// evaporator / chem finisher: grey metal, 292234 outline, teal glass and liquid, cream highlights.
// C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawElectrolysis.cs
// Usage: DrawElectrolysis.exe <out dir> [vanilla sprite dir with evaporator.png and chem_finisher.png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class DrawElectrolysis
{
    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static readonly Color OUT = Hex("292234"), M_DEEP = Hex("373541"), M_DK = Hex("44464c"), M = Hex("5f6868"),
        M_LT = Hex("88948a"), CREAM = Hex("d5d2b8"), CAVITY = Hex("303a41");
    static readonly Color T_DK = Hex("275654"), T_MD = Hex("2f756f"), T = Hex("45918b"), T_LT = Hex("67aaa5"), GLASS = Hex("8abfbb");
    static readonly Color RED = Hex("c24a3d"), RED_DK = Hex("9b4a3d"), RED_DEEP = Hex("64343b");
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

    // ---- electrolyzer -------------------------------------------------------------------------------------------

    // A small canister standing on the housing, hosed to the outlet above it.
    static void MiniCanister(Canvas c, int x, Color band, Color bandDk, int hoseToX)
    {
        // Hose from the valve up and over to the outlet.
        c.VLine(x + 2, 4, 12, M_DEEP); c.HLine(Math.Min(x + 2, hoseToX), Math.Max(x + 2, hoseToX), 4, M_DEEP);
        c.Put(x + 2, 13, M_DK);
        // Shoulder band and steel body (5 px wide).
        c.Rect(x, 14, x + 4, 16, band); c.Put(x + 4, 14, bandDk); c.Put(x + 4, 15, bandDk); c.Put(x + 4, 16, bandDk);
        c.Rect(x, 17, x + 4, 27, M);
        c.VLine(x, 17, 27, M_LT); c.VLine(x + 1, 17, 27, CREAM); c.VLine(x + 4, 17, 27, M_DK);
        c.Rect(x + 1, 21, x + 3, 23, CREAM); c.HLine(x + 1, x + 2, 22, M_DK);
        c.HLine(x, x + 4, 28, M_DEEP);
    }

    static Bitmap Electrolyzer(bool container, bool canisterA, bool canisterB)
    {
        Canvas c = new Canvas(48, 48);
        // Product B (chlorine, lime outlet) on the left, product A (hydrogen, red outlet) on the right.
        if (canisterB) MiniCanister(c, 2, LIME_MD, LIME_DK, 13);
        if (canisterA) MiniCanister(c, 41, RED, RED_DEEP, 34);

        // Gas outlets on the lid: chlorine (lime cap) on the anode side, hydrogen (red cap) on the cathode side.
        c.Rect(13, 3, 15, 7, M_DK); c.VLine(13, 3, 7, M_LT);
        c.Rect(12, 1, 16, 2, LIME_MD); c.HLine(12, 16, 1, LIME); c.Put(16, 2, LIME_DK);
        c.Rect(32, 3, 34, 7, M_DK); c.VLine(32, 3, 7, M_LT);
        c.Rect(31, 1, 35, 2, RED_DK); c.HLine(31, 35, 1, RED); c.Put(35, 2, RED_DEEP);

        // Lid.
        c.Rect(8, 8, 39, 10, M); c.HLine(8, 39, 8, M_LT); c.HLine(8, 39, 10, M_DK);
        c.Put(8, 8, CREAM); c.Put(9, 8, CREAM);
        // Terminals: + (red) and - (dark).
        c.Rect(19, 6, 20, 7, RED); c.Put(19, 6, Hex("e07a66"));
        c.Rect(27, 6, 28, 7, M_DEEP); c.Put(27, 6, M_DK);

        // Glass tank.
        c.Rect(9, 11, 38, 28, CAVITY);
        c.VLine(9, 11, 28, OUT); c.VLine(38, 11, 28, OUT);
        // Head space above the brine, with a glass glint.
        c.Rect(10, 11, 37, 13, Hex("3d4f55"));
        c.VLine(11, 12, 26, GLASS); c.Put(12, 12, GLASS);
        // Brine, only while a container is plugged into the brine slot; otherwise an empty glass tank.
        if (container)
        {
            c.Rect(10, 14, 37, 27, T); c.HLine(10, 37, 14, GLASS); c.Rect(10, 24, 37, 27, T_MD); c.HLine(10, 37, 27, T_DK);
            c.VLine(11, 15, 26, T_LT);
        }
        else
        {
            c.HLine(10, 37, 27, M_DEEP);
        }
        // Membrane between the two halves.
        for (int y = 11; y <= 27; y++) c.Put(23, y, y % 2 == 0 ? M_LT : CREAM);
        c.VLine(24, 11, 27, M_DK);
        // Electrodes hanging from the lid.
        c.Rect(15, 11, 16, 25, M_DK); c.VLine(15, 11, 25, M_LT);
        c.Rect(31, 11, 32, 25, M_DK); c.VLine(31, 11, 25, M_LT);
        // Bubbles rising off the electrodes.
        if (container)
        {
            int[,] bubbles = { { 18, 22 }, { 17, 18 }, { 19, 16 }, { 13, 20 }, { 29, 23 }, { 34, 19 }, { 30, 17 }, { 33, 15 }, { 35, 22 } };
            for (int i = 0; i < bubbles.GetLength(0); i++) c.Put(bubbles[i, 0], bubbles[i, 1], i % 3 == 0 ? CREAM : GLASS);
        }

        // Housing.
        c.Rect(4, 29, 43, 43, M); c.HLine(4, 43, 29, M_LT); c.Rect(4, 37, 43, 43, M_DK); c.HLine(4, 43, 43, M_DEEP);
        c.Put(4, 29, CREAM); c.Put(5, 29, CREAM); c.Put(6, 29, CREAM);
        c.VLine(4, 30, 42, M_LT);
        // Readout window.
        c.Rect(8, 32, 20, 38, OUT); c.Rect(9, 33, 19, 37, CAVITY);
        int[,] digits = { { 10, 34 }, { 11, 34 }, { 12, 34 }, { 12, 35 }, { 11, 36 }, { 14, 34 }, { 14, 35 }, { 14, 36 }, { 15, 36 }, { 16, 36 }, { 17, 34 }, { 17, 35 }, { 18, 36 } };
        for (int i = 0; i < digits.GetLength(0); i++) c.Put(digits[i, 0], digits[i, 1], GLASS);
        // Status lights.
        c.Put(9, 40, LIME_MD); c.Put(11, 40, LIME_MD); c.Put(13, 40, RED);
        // Vents.
        for (int y = 32; y <= 40; y += 2) c.HLine(24, 40, y, M_DEEP);
        // Teal trim like the chem finisher.
        c.HLine(5, 42, 30, T_MD); c.HLine(5, 42, 31, T_DK);
        // Feet.
        c.Rect(7, 44, 12, 45, M_DEEP); c.Rect(35, 44, 40, 45, M_DEEP);

        return c.Render();
    }

    // ---- gas canister ----------------------------------------------------------------------------------------------

    static Bitmap Canister(Color band, Color bandDk, Color bandLt, bool labelled)
    {
        Canvas c = new Canvas(16, 32);
        // Valve and hand wheel.
        c.Rect(5, 1, 10, 2, M_DK); c.HLine(5, 10, 1, M_LT);
        c.Rect(7, 3, 8, 5, M_DEEP); c.Put(7, 3, M_DK);
        // Shoulder (gas colour band), rounded.
        c.Rect(5, 6, 10, 6, band);
        c.Rect(4, 7, 11, 11, band);
        c.VLine(4, 7, 11, bandLt); c.VLine(5, 7, 11, bandLt); c.VLine(10, 6, 11, bandDk); c.VLine(11, 7, 11, bandDk);
        c.Put(5, 6, bandLt);
        // Steel body with a cylinder shading: highlight, mid, shadow.
        c.Rect(4, 12, 11, 28, M);
        c.VLine(4, 12, 28, M_LT); c.VLine(5, 12, 28, CREAM);
        c.VLine(10, 12, 28, M_DK); c.VLine(11, 12, 28, M_DEEP);
        c.HLine(4, 11, 12, M_DEEP);
        // Label.
        if (labelled)
        {
            c.Rect(6, 16, 9, 22, CREAM);
            c.HLine(6, 8, 18, M_DK); c.HLine(6, 9, 20, M_DK);
        }
        // Foot ring.
        c.HLine(4, 11, 29, M_DEEP); c.HLine(5, 10, 30, M_DK);
        return c.Render();
    }

    // Hydrogen + chlorine, half and half. Gases sorted by density for readability: hydrogen (2) on top in red,
    // chlorine (71) at the bottom in yellow-green; the steel shading stays at the edges.
    static Bitmap MixedCanister()
    {
        Canvas c = new Canvas(16, 32);
        // Valve and hand wheel, as on the other canisters.
        c.Rect(5, 1, 10, 2, M_DK); c.HLine(5, 10, 1, M_LT);
        c.Rect(7, 3, 8, 5, M_DEEP); c.Put(7, 3, M_DK);
        Color redLt = Hex("e07a66");
        // Shoulder: hydrogen colour.
        c.Rect(5, 6, 10, 6, RED);
        c.Rect(4, 7, 11, 11, RED);
        c.VLine(4, 7, 11, redLt); c.VLine(5, 7, 11, redLt); c.VLine(10, 6, 11, RED_DEEP); c.VLine(11, 7, 11, RED_DEEP);
        c.Put(5, 6, redLt);
        // Body: upper half hydrogen, lower half chlorine, with the cylinder shading kept.
        for (int y = 12; y <= 28; y++)
        {
            bool top = y <= 19;
            Color mid = top ? RED_DK : LIME_MD, lt = top ? redLt : LIME, dk = top ? RED_DEEP : LIME_DK;
            c.HLine(4, 11, y, mid);
            c.Put(4, y, lt); c.Put(5, y, lt);
            c.Put(10, y, dk); c.Put(11, y, dk);
        }
        // A thin boundary line where the two gases meet, and the seam under the shoulder.
        c.HLine(4, 11, 20, M_DEEP);
        c.HLine(4, 11, 12, M_DEEP);
        // Label straddling the boundary.
        c.Rect(6, 16, 9, 23, CREAM);
        c.HLine(6, 8, 18, M_DK); c.HLine(6, 9, 21, M_DK);
        // Foot ring.
        c.HLine(4, 11, 29, M_DEEP); c.HLine(5, 10, 30, M_DK);
        return c.Render();
    }

    // ---- window connector -----------------------------------------------------------------------------------------

    // The fork drawn between the brine slot and the two product slots in the machine window: one line in from the
    // left, splitting into a red line up to product A (hydrogen) and a blue line down to product B (chlorine).
    // Transparent background; it is a UI picture, not an item.
    static Bitmap Lines()
    {
        const int W = 24, H = 48, MID = 23, TOP = 9, BOTTOM = 37, SPLIT = 9;
        Color red = RED, redLt = Hex("e07a66"), blue = Hex("3f6fb5"), blueLt = Hex("7aa3dc");
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        Color?[,] px = new Color?[W, H];
        Action<int, int, Color> put = delegate(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[x, y] = c; };
        // Shared stem from the brine slot, half red / half blue.
        for (int x = 0; x <= SPLIT; x++) { put(x, MID - 1, redLt); put(x, MID, red); put(x, MID + 1, blue); put(x, MID + 2, blueLt); }
        // Red branch: up, then right to product A.
        for (int y = TOP; y <= MID; y++) { put(SPLIT, y, redLt); put(SPLIT + 1, y, red); }
        for (int x = SPLIT; x < W; x++) { put(x, TOP, redLt); put(x, TOP + 1, red); }
        // Blue branch: down, then right to product B.
        for (int y = MID + 1; y <= BOTTOM + 1; y++) { put(SPLIT, y, blueLt); put(SPLIT + 1, y, blue); }
        for (int x = SPLIT; x < W; x++) { put(x, BOTTOM, blueLt); put(x, BOTTOM + 1, blue); }
        // Arrow heads.
        for (int i = 1; i <= 2; i++) { put(W - 1 - i, TOP - i, red); put(W - 1 - i, TOP + 1 + i, red); put(W - 1 - i, BOTTOM - i, blue); put(W - 1 - i, BOTTOM + 1 + i, blue); }
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (px[x, y].HasValue) { b.SetPixel(x, y, px[x, y].Value); continue; }
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    edge = nx >= 0 && ny >= 0 && nx < W && ny < H && px[nx, ny].HasValue;
                }
                b.SetPixel(x, y, edge ? OUT : Color.FromArgb(0, 0, 0, 0));
            }
        }
        return b;
    }

    public static void Main(string[] args)
    {
        string outDir = args[0];
        Dictionary<string, Bitmap> o = new Dictionary<string, Bitmap>();
        o["vce_electrolyzer_lines"] = Lines();
        // vce_electrolyzer_<container><product A><product B>; the plain name is the full machine, for previews.
        for (int k = 0; k < 8; k++)
        {
            bool cont = (k & 4) != 0, a = (k & 2) != 0, bb = (k & 1) != 0;
            o["vce_electrolyzer_" + (cont ? "1" : "0") + (a ? "1" : "0") + (bb ? "1" : "0")] = Electrolyzer(cont, a, bb);
        }
        o["vce_electrolyzer"] = Electrolyzer(true, true, true);
        o["vce_gas_canister"] = Canister(M_LT, M, CREAM, false);
        o["vce_gas_canister_h2"] = Canister(RED, RED_DEEP, Hex("e07a66"), true);
        o["vce_gas_canister_cl2"] = Canister(LIME_MD, LIME_DK, LIME, true);
        o["vce_gas_canister_mix"] = MixedCanister();
        // Hydrogen chloride is colourless: a near-white band, lighter than the empty canister's grey.
        o["vce_gas_canister_hcl"] = Canister(Hex("e9e6dc"), Hex("a9a69c"), Hex("fbfaf4"), true);
        foreach (KeyValuePair<string, Bitmap> kv in o)
        {
            kv.Value.Save(Path.Combine(outDir, kv.Key + ".png"), ImageFormat.Png);
        }

        // Comparison: vanilla evaporator and chem finisher, the electrolyzer, then the three canisters.
        List<Bitmap> tiles = new List<Bitmap>();
        if (args.Length > 1)
        {
            foreach (string n in new string[] { "evaporator", "chem_finisher" })
            {
                string p = Path.Combine(args[1], n + ".png");
                if (File.Exists(p)) tiles.Add(new Bitmap(p));
            }
        }
        foreach (string k in new string[] { "000", "100", "011", "111" })
        {
            tiles.Add(o["vce_electrolyzer_" + k]);
        }
        tiles.Add(o["vce_electrolyzer_lines"]);
        tiles.Add(o["vce_gas_canister"]);
        tiles.Add(o["vce_gas_canister_h2"]);
        tiles.Add(o["vce_gas_canister_cl2"]);
        tiles.Add(o["vce_gas_canister_mix"]);
        tiles.Add(o["vce_gas_canister_hcl"]);
        const int S = 6, P = 14;
        int width = P, height = 48 * S + 2 * P;
        foreach (Bitmap t in tiles) width += t.Width * S + P;
        Bitmap sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            int x = P;
            foreach (Bitmap t in tiles)
            {
                g.DrawImage(t, new Rectangle(x, P + (48 - t.Height) * S, t.Width * S, t.Height * S));
                x += t.Width * S + P;
            }
        }
        sheet.Save(Path.Combine(outDir, "electrolysis_compare.png"), ImageFormat.Png);
    }
}
