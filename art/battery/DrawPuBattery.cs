// Draws the plutonium chemical battery: the vanilla energy cell silhouette (16x32, metal cap, coloured band,
// dark body), with a hazard-yellow cap, a black/yellow band, a small trefoil, and a glass window on the left
// whose olive plutonium fluid (the ampoule's colours) shows the charge level. Light from the top left, outline
// 292234, alpha-3 filler so both cell centres count for the item shape. C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawPuBattery.cs
// Usage: DrawPuBattery.exe <out dir> [vanilla reference pngs for the comparison sheet...]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawPuBattery
{
    static readonly string[] Rows =
    {
        "................",
        "................",
        "................",
        "......aaaa......",
        "....aabbbcaa....",
        "...adccbbbcda...",
        "..addecccbedda..",
        "..adddeeeeddfa..",
        "..afddddddddfa..",
        "..agfddddddfga..",
        "..akYYkkYYkkYa..",
        "..ammmmmmjkkja..",
        "..amwnnnnjkkja..",   // 12: headspace
        "..amwGGGgjkkja..",   // 13-24: fluid rows (replaced per charge level)
        "..amwGGGgjkkja..",
        "..amwGGGgjYkYa..",
        "..amwGGGgjkYka..",
        "..amwGGGgjkYka..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..amwGGGgjkkja..",
        "..ammmmmmjkkja..",
        "...ajjjjjjkka...",
        "....aajjjjaa....",
        "......aaaa......",
        "................",
        "................",
        "................",
    };

    const int FLUID_TOP = 13;
    const int FLUID_ROWS = 12;

    // Bubbles inside the fluid, as (row, column); only drawn below the surface.
    static readonly int[,] Bubbles = { { 15, 6 }, { 18, 7 }, { 20, 5 }, { 22, 7 } };

    static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
    {
        { 'a', "292234" },  // outline, shared with vanilla
        { 'b', "a8aea5" },  // cap metal (vanilla)
        { 'c', "d5d2b8" },  // cap highlight (vanilla)
        { 'e', "44464c" },  // cap shade (vanilla)
        { 'd', "c9a227" },  // hazard-yellow shoulder
        { 'f', "e0c060" },  // shoulder highlight
        { 'g', "6e5414" },  // shoulder shadow
        { 'Y', "d9b23a" },  // band / trefoil yellow
        { 'k', "2f2b3a" },  // body shadow (vanilla)
        { 'j', "373541" },  // body (vanilla)
        { 'm', "44464c" },  // window frame
        { 'w', "88948a" },  // glass highlight
        { 'n', "2e3b3e" },  // empty glass
        { 'G', "7b9f30" },  // plutonium fluid (ampoule)
        { 'h', "5a7423" },  // fluid shadow (ampoule)
        { 'L', "b6d46a" },  // surface glow and bubbles
    };

    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static Bitmap Draw(int percent)
    {
        int filled = (int)Math.Round(FLUID_ROWS * percent / 100.0);
        if (percent > 0 && filled == 0)
        {
            filled = 1;
        }
        int surface = FLUID_TOP + FLUID_ROWS - filled;
        Bitmap bmp = new Bitmap(16, 32, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 32; y++)
        {
            char[] row = Rows[y].ToCharArray();
            if (y >= FLUID_TOP && y < FLUID_TOP + FLUID_ROWS)
            {
                for (int x = 5; x <= 8; x++)
                {
                    if (y < surface)
                    {
                        row[x] = 'n';
                    }
                    else if (y == surface)
                    {
                        row[x] = x == 8 ? 'G' : 'L';
                    }
                    else
                    {
                        row[x] = x == 8 ? 'h' : 'G';
                    }
                }
            }
            for (int i = 0; i < Bubbles.GetLength(0); i++)
            {
                if (Bubbles[i, 0] == y && y > surface)
                {
                    row[Bubbles[i, 1]] = 'L';
                }
            }
            for (int x = 0; x < 16; x++)
            {
                char c = row[x];
                bmp.SetPixel(x, y, c == '.' ? Color.FromArgb(3, 0, 0, 0) : Hex(Palette[c]));
            }
        }
        return bmp;
    }

    public static void Main(string[] args)
    {
        string outDir = args[0];
        Directory.CreateDirectory(outDir);
        int[] levels = { 100, 80, 60, 40, 20, 10, 0 };
        List<Bitmap> tiles = new List<Bitmap>();
        foreach (int level in levels)
        {
            Bitmap bmp = Draw(level);
            bmp.Save(Path.Combine(outDir, "vce_pu_battery" + level + ".png"), ImageFormat.Png);
            tiles.Add(bmp);
        }
        tiles[0].Save(Path.Combine(outDir, "vce_pu_battery.png"), ImageFormat.Png);

        int ours = tiles.Count;
        for (int i = 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]))
            {
                tiles.Add(new Bitmap(args[i]));
            }
        }
        const int S = 8, P = 12;
        int gap = tiles.Count > ours ? P * 3 : 0;
        Bitmap sheet = new Bitmap(tiles.Count * (16 * S + P) + P + gap, 32 * S + 2 * P, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int i = 0; i < tiles.Count; i++)
            {
                int x = P + i * (16 * S + P) + (i >= ours ? gap : 0);
                g.DrawImage(tiles[i], new Rectangle(x, P, 16 * S, 32 * S));
            }
        }
        sheet.Save(Path.Combine(outDir, "pu_battery_compare.png"), ImageFormat.Png);
    }
}
