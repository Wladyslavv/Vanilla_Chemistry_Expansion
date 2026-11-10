// Draws the rock salt ore: a cluster of cubic halite crystals (white and pale pink) on a grey rock, in the
// frame of the vanilla metal ore (Items/items_material "ore1"): 48x32, 3x2 cells, the shared 292234 outline,
// light from the top left, and an alpha-1 filler so every 16 px cell centre counts for the item shape.
// C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawSaltOre.cs
// Usage: DrawSaltOre.exe <out dir> [vanilla ore1.png for the comparison sheet]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class DrawSaltOre
{
    const int W = 48, H = 32;
    static readonly Color OUTLINE = Hex("292234");
    // Rock: the vanilla ore greys.
    static readonly Color ROCK_HI = Hex("88948a"), ROCK = Hex("5f6868"), ROCK_DK = Hex("44464c"), ROCK_DEEP = Hex("373541");

    class Skin
    {
        public Color Top, Front, Side, Edge, Crease;
        public Skin(string top, string front, string side, string edge, string crease)
        {
            Top = Hex(top); Front = Hex(front); Side = Hex(side); Edge = Hex(edge); Crease = Hex(crease);
        }
    }

    static readonly Skin White = new Skin("f4ede8", "d6cbc8", "a8979f", "fffaf4", "7d6a78");
    static readonly Skin Pink = new Skin("f2d8d1", "d9b3ae", "a9878d", "fbebe5", "7a6070");

    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    static Color?[,] px = new Color?[W, H];

    static void Put(int x, int y, Color c)
    {
        if (x >= 1 && x < W - 1 && y >= 1 && y < H - 1)
        {
            px[x, y] = c;
        }
    }

    static Color Mix(Color a, Color b)
    {
        return Color.FromArgb(255, (a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2);
    }

    static bool InCube(int px0, int py0, int x, int y, int s, int d)
    {
        if (px0 >= x && px0 < x + s && py0 >= y && py0 < y + s)
        {
            return true;
        }
        for (int i = 1; i <= d; i++)
        {
            if (py0 == y - i && px0 >= x + i && px0 <= x + s - 1 + i)
            {
                return true;
            }
            if (px0 == x + s - 1 + i && py0 >= y - i + 1 && py0 <= y + s - 1 - i)
            {
                return true;
            }
        }
        return false;
    }

    // A cube in oblique view: an s x s front face at (x, y), a top face and a right face d pixels deep.
    // Each crystal gets its own dark outline first, like the separate chunks of the vanilla ore.
    static void Cube(int x, int y, int s, int d, Skin k)
    {
        for (int cy = y - d - 1; cy <= y + s; cy++)
        {
            for (int cx = x - 1; cx <= x + s + d; cx++)
            {
                if (InCube(cx, cy, x, y, s, d))
                {
                    continue;
                }
                if (InCube(cx + 1, cy, x, y, s, d) || InCube(cx - 1, cy, x, y, s, d)
                    || InCube(cx, cy + 1, x, y, s, d) || InCube(cx, cy - 1, x, y, s, d))
                {
                    Put(cx, cy, OUTLINE);
                }
            }
        }
        for (int i = 1; i <= d; i++)
        {
            for (int cx = x + i; cx <= x + s - 1 + i; cx++)
            {
                Put(cx, y - i, k.Top);
            }
            for (int cy = y - i + 1; cy <= y + s - 1 - i; cy++)
            {
                Put(x + s - 1 + i, cy, k.Side);
            }
        }
        Color frontShade = Mix(k.Front, k.Side);
        for (int cy = y; cy < y + s; cy++)
        {
            for (int cx = x; cx < x + s; cx++)
            {
                // The lower third of the front face falls into shade.
                Put(cx, cy, cy >= y + s - Math.Max(2, s / 3) ? frontShade : k.Front);
            }
        }
        // Lit top-front edge and left edge, dark crease between the front and the right face.
        for (int cx = x; cx < x + s; cx++)
        {
            Put(cx, y, k.Edge);
        }
        for (int cy = y + 1; cy < y + s - 1; cy++)
        {
            Put(x, cy, k.Top);
        }
        for (int cy = y; cy < y + s; cy++)
        {
            Put(x + s - 1, cy, k.Crease);
        }
        // A small internal glint and a cleavage line on the bigger crystals.
        if (s >= 9)
        {
            Put(x + 2, y + 2, k.Edge);
            Put(x + 3, y + 2, k.Edge);
            Put(x + 2, y + 3, k.Edge);
            for (int cx = x + s / 2; cx < x + s - 2; cx++)
            {
                Put(cx, y + s - 3, k.Side);
            }
        }
    }

    static void Rock()
    {
        // A low, wide grey rock: lit top rim, darker body, deepest along the bottom.
        double cx = 24, cy = 24.5, rx = 22, ry = 6.2;
        for (int y = 15; y < H - 1; y++)
        {
            for (int x = 1; x < W - 1; x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                double r = dx * dx + dy * dy;
                if (r > 1.0)
                {
                    continue;
                }
                bool rim = dy < 0 && r > 0.55;
                Color c = rim ? ROCK_HI : (y <= 25 ? ROCK : (y <= 28 ? ROCK_DK : ROCK_DEEP));
                Put(x, y, c);
            }
        }
        // A few speckles of salt in the rock.
        int[,] specks = { { 8, 25 }, { 15, 28 }, { 29, 29 }, { 38, 26 }, { 41, 28 }, { 5, 27 } };
        for (int i = 0; i < specks.GetLength(0); i++)
        {
            Put(specks[i, 0], specks[i, 1], White.Front);
        }
    }

    public static void Main(string[] args)
    {
        Rock();
        // Back to front, so nearer crystals cover farther ones.
        Cube(17, 7, 12, 5, White);
        Cube(6, 13, 10, 4, Pink);
        Cube(30, 12, 10, 4, White);
        Cube(21, 18, 9, 4, Pink);
        Cube(10, 20, 8, 3, White);
        Cube(34, 21, 7, 3, Pink);

        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                b.SetPixel(x, y, Color.FromArgb(1, 0, 0, 0));
            }
        }
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (px[x, y].HasValue)
                {
                    b.SetPixel(x, y, px[x, y].Value);
                    continue;
                }
                // Silhouette outline: an empty pixel next to a filled one.
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    edge = nx >= 0 && nx < W && ny >= 0 && ny < H && px[nx, ny].HasValue;
                }
                if (edge)
                {
                    b.SetPixel(x, y, OUTLINE);
                }
            }
        }
        string outDir = args[0];
        b.Save(Path.Combine(outDir, "vce_salt_ore.png"), ImageFormat.Png);

        List<Bitmap> tiles = new List<Bitmap>();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            tiles.Add(new Bitmap(args[1]));
        }
        tiles.Add(b);
        const int S = 8, P = 16;
        Bitmap sheet = new Bitmap(tiles.Count * (W * S + P) + P, H * S + 2 * P, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < tiles.Count; i++)
            {
                g.DrawImage(tiles[i], new Rectangle(P + i * (W * S + P), P, W * S, H * S));
            }
        }
        sheet.Save(Path.Combine(outDir, "salt_ore_compare.png"), ImageFormat.Png);
    }
}
