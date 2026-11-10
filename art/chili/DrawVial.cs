// Draws the incapacitant vial: a small amber reagent bottle with a black screw cap and a white label carrying a
// chili-magenta band. Same outline and filler conventions as the Crackine vial (16x16, alpha-3 filler so every
// cell centre counts for the item shape). C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawVial.cs
// Usage: DrawVial.exe <out dir> [crackine vial png for the comparison sheet]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class DrawVial
{
    static readonly string[] Rows =
    {
        "................",
        ".....bbbbbb.....",
        ".....bkKKKb.....",
        ".....bkKKKb.....",
        ".....bbbbbb.....",
        "......bnnb......",
        "....bbAHAAbb....",
        "...bAHAAAAADb...",
        "...bAHWWWWWDb...",
        "...bAHRRRRRDb...",
        "...bAHWWWWWDb...",
        "...bAHWggWWDb...",
        "...bAHWWWWWDb...",
        "...bAHAAAAADb...",
        "....bDDDDDDb....",
        "................",
    };

    static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
    {
        { 'b', "292234" },  // outline, shared with the other mod sprites
        { 'K', "45444c" },  // cap
        { 'k', "73727d" },  // cap highlight
        { 'n', "5a2e12" },  // neck
        { 'A', "8a4a1c" },  // amber glass
        { 'H', "c07a3a" },  // glass highlight
        { 'D', "5a2e12" },  // glass shadow
        { 'W', "e8e4d6" },  // label
        { 'R', "9c2463" },  // label band, the chili's magenta
        { 'g', "8e8a80" },  // label text
    };

    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    public static void Main(string[] args)
    {
        string outDir = args[0];
        Bitmap vial = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                char c = Rows[y][x];
                vial.SetPixel(x, y, c == '.' ? Color.FromArgb(3, 0, 0, 0) : Hex(Palette[c]));
            }
        }
        vial.Save(Path.Combine(outDir, "vce_incapacitant_vial.png"), ImageFormat.Png);

        List<Bitmap> tiles = new List<Bitmap> { vial };
        if (args.Length > 1 && File.Exists(args[1]))
        {
            tiles.Add(new Bitmap(args[1]));
        }
        const int S = 10, P = 12;
        Bitmap sheet = new Bitmap(tiles.Count * (16 * S + P) + P, 16 * S + 2 * P, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < tiles.Count; i++)
            {
                g.DrawImage(tiles[i], new Rectangle(P + i * (16 * S + P), P, 16 * S, 16 * S));
            }
        }
        sheet.Save(Path.Combine(outDir, "vial_compare.png"), ImageFormat.Png);
    }
}
