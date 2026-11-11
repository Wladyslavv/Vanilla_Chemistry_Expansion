// Draws the inventory / counter sprite of the Cartel Network's note: a folded envelope seen from above, warm
// cream paper (brighter than vanilla's grey handnote so it stands out on the counter), the flap folds meeting under
// a black wax seal pressed with the cartel's eye. 32x32 (2x2 cells like the vanilla handnote), outline 292234, light
// from the top left, alpha-1 filler so all four cell centres count. C# 5, System.Drawing only.
// Build: csc -r:System.Drawing.dll DrawCartelEnvelope.cs
// Usage: DrawCartelEnvelope.exe <out dir> [vanilla handnote png for the comparison sheet]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DrawCartelEnvelope
{
    const int S = 32;
    // Envelope body (inclusive), and where the flaps meet.
    const int L = 4, R = 27, T = 8, B = 25;
    const double MX = 15.5, MY = 17.0;

    static readonly Dictionary<char, string> Palette = new Dictionary<char, string>
    {
        { 'a', "292234" },  // outline
        { 'L', "f6ecd2" },  // paper, lit
        { 'P', "eadcb4" },  // paper
        { 'S', "cdb98c" },  // paper, shade
        { 'D', "a38c62" },  // fold crease
        { 'F', "f0e3c0" },  // top flap (a touch lighter: it sits on top)
        { 'K', "1c1716" },  // wax seal
        { 'k', "3a2f2b" },  // wax seal, lit rim
        { 'E', "d9c9a0" },  // the eye pressed into the wax
        { 'h', "1f1a24" },  // shadow under the envelope
    };

    // Distance from (x, y) to the segment a-b.
    static double Seg(double x, double y, double ax, double ay, double bx, double by)
    {
        double vx = bx - ax, vy = by - ay;
        double t = Math.Max(0, Math.Min(1, ((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy)));
        double dx = x - (ax + t * vx), dy = y - (ay + t * vy);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static Bitmap Draw()
    {
        char[,] px = new char[S, S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                px[x, y] = '.';
            }
        }
        // Drop shadow one pixel down and right, so it lifts off the counter.
        for (int y = T + 1; y <= B + 1; y++)
        {
            for (int x = L + 1; x <= R + 1; x++)
            {
                px[x, y] = 'h';
            }
        }
        for (int y = T; y <= B; y++)
        {
            for (int x = L; x <= R; x++)
            {
                char c;
                if (x == L || x == R || y == T || y == B)
                {
                    c = 'a';
                }
                else
                {
                    // Top flap: the triangle from the top corners down to the meeting point.
                    double upper = T + (MY - T) * (1 - Math.Abs(x + 0.5 - MX - 0.5) / (MX - L));
                    bool flap = y <= upper;
                    c = flap ? 'F' : 'P';
                    if (x <= L + 1 || y <= T + 1)
                    {
                        c = 'L';
                    }
                    if (x >= R - 1 || y >= B - 1)
                    {
                        c = 'S';
                    }
                    // Creases: the flap edges (strong) and the lower folds to the bottom corners (faint).
                    if (Seg(x + 0.5, y + 0.5, L + 0.5, T + 0.5, MX, MY) < 0.62 || Seg(x + 0.5, y + 0.5, R + 0.5, T + 0.5, MX + 1, MY) < 0.62)
                    {
                        c = 'D';
                    }
                    else if ((Seg(x + 0.5, y + 0.5, L + 0.5, B + 0.5, MX - 2, MY + 1) < 0.55
                        || Seg(x + 0.5, y + 0.5, R + 0.5, B + 0.5, MX + 3, MY + 1) < 0.55) && c != 'S')
                    {
                        c = 'S';
                    }
                }
                px[x, y] = c;
            }
        }
        // Wax seal over the meeting point, with a pressed eye.
        double sx = 16.0, sy = 17.0;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                double dx = x + 0.5 - sx, dy = y + 0.5 - sy;
                double r = Math.Sqrt(dx * dx + dy * dy);
                if (r <= 4.6)
                {
                    px[x, y] = r > 3.6 && dx + dy < -1.5 ? 'k' : 'K';
                    if (r > 4.0 && (x == 11 || y == 21))
                    {
                        px[x, y] = 'K';
                    }
                }
            }
        }
        // The eye: an almond of three rows with a dark pupil.
        string[] eye = { ".EEE.", "EEKEE", ".EEE." };
        for (int j = 0; j < eye.Length; j++)
        {
            for (int i = 0; i < eye[j].Length; i++)
            {
                if (eye[j][i] != '.')
                {
                    px[14 + i, 16 + j] = eye[j][i];
                }
            }
        }
        // A drip of wax running off the seal, and two ink specks.
        px[17, 21] = 'K';
        px[17, 22] = 'K';
        px[22, 12] = 'K';
        px[9, 22] = 'K';

        Bitmap bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
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
        Bitmap env = Draw();
        env.Save(Path.Combine(outDir, "vce_cartel_envelope.png"), ImageFormat.Png);
        List<Bitmap> tiles = new List<Bitmap> { env };
        if (args.Length > 1 && File.Exists(args[1]))
        {
            tiles.Add(new Bitmap(args[1]));
        }
        const int Z = 8, P = 12;
        // On a counter-coloured strip, like it would sit in the game.
        Bitmap sheet = new Bitmap(tiles.Count * (S * Z + P) + P, S * Z + 2 * P, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 88, 74, 66));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int i = 0; i < tiles.Count; i++)
            {
                g.DrawImage(tiles[i], new Rectangle(P + i * (S * Z + P), P, S * Z, S * Z));
            }
        }
        sheet.Save(Path.Combine(outDir, "cartel_envelope_compare.png"), ImageFormat.Png);
    }
}
