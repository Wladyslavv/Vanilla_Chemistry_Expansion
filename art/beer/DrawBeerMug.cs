// Draws the draft beer mug and the flat beer mug in perspective (seen slightly from above, like vanilla cans):
// an elliptical rim with the opening visible, a curved thick glass base, and a handle on the right. The draft mug
// has a foam head spilling over the rim; the flat one shows a dull beer surface inside the opening. 32x32 (2x2
// cells), outline 292234, light from the top left, alpha-1 filler so all four cell centres count. Palette kept
// from the 1.4 mugs. C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawBeerMug.cs
// Usage: DrawBeerMug.exe <out dir> [old mug png] [old flat mug png] [vanilla reference png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawBeerMug
{
    const int W = 32, H = 32;

    // Body: columns 5..21, rim ellipse centred on row TOP, base ellipse on row BOT.
    const double CX = 13.0, RX = 8.6, RY = 3.6;
    const int TOP = 10, BOT = 26;
    const double BASE_RY = 2.6;

    static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
    {
        { 'a', "292234" },  // outline
        { 'B', "e8e2cc" },  // foam light
        { 'C', "d5d2b8" },  // foam shade / glass highlight
        { 'D', "a8a698" },  // foam shadow
        { 'e', "8f928c" },  // glass
        { 'i', "5f5a5e" },  // glass shadow
        { 'n', "3d3842" },  // inside of the mug, back wall
        { 'F', "d6a858" },  // beer highlight
        { 'G', "b98444" },  // beer
        { 'h', "946338" },  // beer shadow
        { 'k', "6e4831" },  // beer deep shadow
        { 'J', "e0d09b" },  // bubbles
        { 'S', "8a5f34" },  // flat beer surface, seen from above (darker than the side)
        { 's', "6e4831" },  // flat beer surface, back
        { 'R', "c9a066" },  // reflection on the surface
    };

    static double Half(double dx, double rx, double ry)
    {
        double t = dx / rx;
        return t >= 1 ? -1 : ry * Math.Sqrt(1 - t * t);
    }

    static bool InBody(int x, int y)
    {
        double dx = x - CX;
        if (Math.Abs(dx) > 8.0)
        {
            return false;
        }
        return y >= TOP - Half(dx, RX, RY) && y <= BOT + Half(dx, RX, BASE_RY);
    }

    // Inside the glass ring at the top.
    static bool InOpening(int x, int y)
    {
        double dx = (x - CX) / (RX - 2.0), dy = (y - TOP - 0.3) / (RY - 1.5);
        return dx * dx + dy * dy <= 1.0;
    }

    // The head: bumps above the rim, and inside the rim down to just above the front lip, so the lit glass lip
    // shows as a curve under the foam. Two drips run over the lip.
    static bool InFoam(int x, int y)
    {
        double dxc = x - CX;
        if (Math.Abs(dxc) <= 7.0 && y <= TOP + Half(dxc, RX - 1.2, RY - 1.2) && y >= TOP - RY)
        {
            return true;
        }
        double[,] bumps = { { 7.0, 7.4, 2.6 }, { 10.2, 5.0, 3.0 }, { 14.2, 4.4, 3.2 }, { 18.2, 6.0, 2.8 }, { 20.4, 8.6, 1.9 } };
        for (int i = 0; i < bumps.GetLength(0); i++)
        {
            double dx = x - bumps[i, 0], dy = y - bumps[i, 1];
            if (dx * dx + dy * dy <= bumps[i, 2] * bumps[i, 2] && y <= TOP)
            {
                return true;
            }
        }
        return (x == 8 && y >= TOP + 2 && y <= TOP + 5) || (x == 9 && y >= TOP + 2 && y <= TOP + 4)
            || (x == 17 && y >= TOP + 2 && y <= TOP + 3);
    }

    // The handle: a C shape on the right, columns 21..26, rows 12..24 (hole at 22..23, rows 15..21).
    static char Handle(int x, int y)
    {
        if (y < 12 || y > 24 || x < 22 || x > 26)
        {
            return '\0';
        }
        bool bar = y <= 14 || y >= 22;
        bool post = x >= 24;
        if (!bar && !post)
        {
            return '\0';  // hole
        }
        if (y == 12 || y == 24 || x == 26 || (!bar && x == 24 && false))
        {
            return 'a';
        }
        if (bar && (y == 14 || y == 22) && x <= 23)
        {
            return 'a';  // inner edge of the hole
        }
        if (!bar && x == 24)
        {
            return y == 15 ? 'a' : 'C';
        }
        return y <= 13 || x == 24 ? 'e' : 'i';
    }

    static readonly int[,] Bubbles = { { 10, 16 }, { 15, 14 }, { 12, 20 }, { 16, 19 }, { 9, 23 }, { 14, 24 } };

    static Bitmap Draw(bool draft)
    {
        char[,] px = new char[W, H];
        bool[,] solid = new bool[W, H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                bool body = InBody(x, y);
                bool foam = draft && InFoam(x, y);
                char handle = Handle(x, y);
                solid[x, y] = body || foam || handle != '\0';
                px[x, y] = '.';
                if (handle != '\0' && !body)
                {
                    px[x, y] = handle;
                }
            }
        }
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (!solid[x, y] || px[x, y] != '.')
                {
                    continue;
                }
                double dx = x - CX;
                double topH = Half(dx, RX, RY);
                double botH = Half(dx, RX, BASE_RY);
                bool foam = draft && InFoam(x, y);
                char c;
                if (foam)
                {
                    // Foam: light top left, shade to the bottom right, shadow along its lower edge.
                    bool lowerEdge = y + 1 < H && !InFoam(x, y + 1) && InBody(x, y + 1);
                    c = lowerEdge ? 'D' : (x + y * 0.7 > 26 ? 'C' : 'B');
                    if ((x == 11 && y == 3) || (x == 15 && y == 2) || (x == 7 && y == 6) || (x == 18 && y == 5))
                    {
                        c = 'C';
                    }
                }
                else if (InOpening(x, y))
                {
                    // Flat beer seen through the opening: the far inside wall, then the dull surface.
                    c = !InOpening(x, y - 1) ? 'n' : (!InOpening(x, y - 2) ? 's' : 'S');
                    // A short reflection of the ceiling light, top left of the surface.
                    if (c == 'S' && ((x == 9 || x == 10) && y == TOP + 1 || x == 11 && y == TOP + 1))
                    {
                        c = 'R';
                    }
                }
                else if (y <= TOP + topH)
                {
                    // The glass ring: the front lip is lit (left brighter), the back of the ring darker.
                    c = y >= TOP ? (dx < 2 ? 'C' : 'e') : (dx < -2 ? 'e' : 'i');
                }
                else if (y >= BOT - 2 + botH)
                {
                    // Thick glass base following the bottom curve.
                    c = dx < -3 ? 'C' : (dx < 4 ? 'e' : 'i');
                }
                else if (dx <= -7)
                {
                    c = 'e';  // left wall
                }
                else if (dx >= 7)
                {
                    c = 'i';  // right wall
                }
                else
                {
                    c = dx <= -5 ? 'F' : (dx <= 1 ? 'G' : (dx <= 4 ? 'h' : 'k'));
                    if (dx == -4 && (y % 5 != 0))
                    {
                        c = 'C';  // glass facet catching the light
                    }
                }
                px[x, y] = c;
            }
        }
        if (draft)
        {
            for (int i = 0; i < Bubbles.GetLength(0); i++)
            {
                int bx = Bubbles[i, 0], by = Bubbles[i, 1];
                if (px[bx, by] == 'G' || px[bx, by] == 'h')
                {
                    px[bx, by] = 'J';
                }
            }
        }
        // Outline: solid pixels next to empty space.
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (!solid[x, y] || px[x, y] == 'a')
                {
                    continue;
                }
                bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1
                    || !solid[x - 1, y] || !solid[x + 1, y] || !solid[x, y - 1] || !solid[x, y + 1];
                if (edge)
                {
                    px[x, y] = 'a';
                }
            }
        }
        Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                char c = px[x, y];
                bmp.SetPixel(x, y, c == '.' ? Color.FromArgb(1, 0, 0, 0) : Hex(Palette[c]));
            }
        }
        return bmp;
    }

    static Color Hex(string h)
    {
        return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16),
            Convert.ToInt32(h.Substring(4, 2), 16));
    }

    public static void Main(string[] args)
    {
        string outDir = args[0];
        Directory.CreateDirectory(outDir);
        Bitmap mug = Draw(true);
        Bitmap flat = Draw(false);
        mug.Save(Path.Combine(outDir, "ia_beer_mug.png"), ImageFormat.Png);
        flat.Save(Path.Combine(outDir, "ia_beer_mug_flat.png"), ImageFormat.Png);

        List<Bitmap> tiles = new List<Bitmap> { mug, flat };
        for (int i = 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]))
            {
                tiles.Add(new Bitmap(args[i]));
            }
        }
        const int S = 8, P = 12;
        int x0 = P;
        int width = P;
        foreach (Bitmap t in tiles)
        {
            width += t.Width * S + P;
        }
        Bitmap sheet = new Bitmap(width + P * 2, 32 * S + 2 * P, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 60, 64, 72));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (i == 2)
                {
                    x0 += P * 2;
                }
                g.DrawImage(tiles[i], new Rectangle(x0, P + (32 - tiles[i].Height) * S, tiles[i].Width * S, tiles[i].Height * S));
                x0 += tiles[i].Width * S + P;
            }
        }
        sheet.Save(Path.Combine(outDir, "beer_mug_compare.png"), ImageFormat.Png);
    }
}
