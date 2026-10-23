using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Draws all arena art in code (tiles, backgrounds, props, bugs, effects) and imports it.
    /// Everything is 32 pixels per unit so it matches the knight (64px tall = 2 units).
    /// </summary>
    public static class ArenaArt
    {
        public const float PPU = 32f;

        // ------------------------------------------------------------------ canvas

        public class Px
        {
            public readonly int W, H;
            public readonly Color32[] C;

            public Px(int w, int h)
            {
                W = w;
                H = h;
                C = new Color32[w * h];
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
            public Color32 Get(int x, int y) => In(x, y) ? C[y * W + x] : default;
            public bool Solid(int x, int y) => In(x, y) && C[y * W + x].a > 0;

            public void Set(int x, int y, Color32 c)
            {
                if (In(x, y)) C[y * W + x] = c;
            }

            public void Blend(int x, int y, Color32 c)
            {
                if (!In(x, y)) return;
                var d = C[y * W + x];
                float a = c.a / 255f;
                float da = d.a / 255f;
                float oa = a + da * (1f - a);
                if (oa <= 0f) return;
                C[y * W + x] = new Color32(
                    (byte)((c.r * a + d.r * da * (1f - a)) / oa),
                    (byte)((c.g * a + d.g * da * (1f - a)) / oa),
                    (byte)((c.b * a + d.b * da * (1f - a)) / oa),
                    (byte)(oa * 255f));
            }

            public void Rect(int x, int y, int w, int h, Color32 c)
            {
                for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
            }

            /// <summary>Filled ellipse; shade gets normalized coords (-1..1, y up).</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, Func<float, float, Color32> shade, bool blend = false)
            {
                for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny > 1f) continue;
                    var c = shade(nx, ny);
                    if (c.a == 0) continue;
                    if (blend) Blend(x, y, c);
                    else Set(x, y, c);
                }
            }

            public void Line(float x0, float y0, float x1, float y1, Color32 c, int thickness = 1)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                    int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int a = 0; a < thickness; a++)
                    for (int b = 0; b < thickness; b++)
                        Set(x + a - thickness / 2, y + b - thickness / 2, c);
                }
            }

            /// <summary>Adds a 1px dark outline around every opaque pixel.</summary>
            public void Outline(Color32 ink, byte minAlpha = 140)
            {
                var copy = (Color32[])C.Clone();
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a > 0) continue;
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx >= 0 && ny >= 0 && nx < W && ny < H && copy[ny * W + nx].a >= minAlpha) edge = true;
                    }
                    if (edge) C[y * W + x] = ink;
                }
            }

            public void Paste(Px src, int ox, int oy)
            {
                for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    var c = src.C[y * src.W + x];
                    if (c.a > 0) Blend(ox + x, oy + y, c);
                }
            }
        }

        // ------------------------------------------------------------------ palette

        public static Color32 Hex(string hex, byte a = 255)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            var c32 = (Color32)c;
            c32.a = a;
            return c32;
        }

        static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);

        static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        static readonly Color32 Ink = Hex("#0C0A12");
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        // Ground stone
        static readonly Color32 Stone = Hex("#4A5868");
        static readonly Color32 StoneLight = Hex("#62748A");
        static readonly Color32 StoneLighter = Hex("#8196AD");
        static readonly Color32 StoneDark = Hex("#343F4C");
        static readonly Color32 Mortar = Hex("#1E242E");
        static readonly Color32 Moss = Hex("#3F6934");
        static readonly Color32 MossLight = Hex("#62964A");
        static readonly Color32 MossBright = Hex("#8CC063");
        static readonly Color32 MossDark = Hex("#284626");

        // Back wall
        static readonly Color32 Wall = Hex("#212D38");
        static readonly Color32 WallLight = Hex("#2C3B49");
        static readonly Color32 WallMortar = Hex("#121920");
        static readonly Color32 WallMoss = Hex("#243C2C");

        // ------------------------------------------------------------------ deterministic noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        // =====================================================================
        // Tiles
        // =====================================================================

        static Px StoneBricks(int seed, Color32 baseColor, Color32 light, Color32 dark, Color32 mortar, int brickW = 16, int brickH = 8, float noise = 0.12f)
        {
            var p = new Px(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = y / brickH;
                int ly = y % brickH;
                int offset = (row % 2) * (brickW / 2);
                int xo = (x + offset) % 32;
                int lx = xo % brickW;
                int brick = xo / brickW;
                bool mortarPx = ly == 0 || lx == 0 || x == 0;
                if (mortarPx)
                {
                    p.Set(x, y, mortar);
                    continue;
                }
                float v = Hash(brick + (x < (brickW - offset) % 32 ? 0 : 7), row, seed);
                var c = Mix(baseColor, v > 0.5f ? light : dark, Mathf.Abs(v - 0.5f) * 0.7f);
                if (ly == brickH - 1) c = Mix(c, light, 0.55f);
                else if (ly == 1) c = Mix(c, dark, 0.5f);
                if (lx == 1) c = Mix(c, light, 0.25f);
                if (lx == brickW - 1) c = Mix(c, dark, 0.35f);
                float n = Hash(x, y, seed + 99);
                if (n < noise) c = Shade(c, 0.88f);
                else if (n > 1f - noise * 0.5f) c = Shade(c, 1.1f);
                p.Set(x, y, c);
            }
            return p;
        }

        static Px GroundFill(int seed)
        {
            var p = StoneBricks(seed, Stone, StoneLight, StoneDark, Mortar);
            // a crack now and then
            if (seed % 2 == 1)
            {
                int cx = 6 + seed * 5 % 20, cy = 4 + seed * 3 % 20;
                for (int i = 0; i < 6; i++) p.Set(cx + i / 2, cy - i, Mortar);
            }
            return p;
        }

        static Px GroundTop(int seed)
        {
            var p = GroundFill(seed);
            for (int x = 0; x < 32; x++)
            {
                int depth = 3 + Mathf.FloorToInt(Hash(x / 2, 0, seed + 11) * 4f);
                for (int d = 0; d < depth; d++)
                {
                    int y = 31 - d;
                    Color32 c = d == 0 ? MossBright : d == 1 ? MossLight : d == depth - 1 ? MossDark : Moss;
                    if (Hash(x, y, seed + 5) < 0.12f) c = Shade(c, 0.85f);
                    p.Set(x, y, c);
                }
                // stone lip under the moss catches the light
                p.Set(x, 31 - depth, Mix(p.Get(x, 31 - depth), StoneLighter, 0.5f));
                // the odd drip of moss
                if (Hash(x, 3, seed + 21) < 0.12f)
                    for (int d = depth; d < depth + 3; d++) p.Set(x, 31 - d, MossDark);
            }
            return p;
        }

        static Px GroundUnder(int seed)
        {
            var p = GroundFill(seed);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, Shade(p.Get(x, y), 0.55f + y * 0.1f));
            return p;
        }

        static Px Platform(int part)
        {
            var p = new Px(32, 32);
            const int top = 31, bottom = 22;
            for (int y = bottom; y <= top; y++)
            for (int x = 0; x < 32; x++)
            {
                bool capL = part == 0 && x < 2 && (y == top || y == bottom);
                bool capR = part == 2 && x > 29 && (y == top || y == bottom);
                if (capL || capR) continue;
                Color32 c = y == top ? StoneLighter : y == top - 1 ? StoneLight : y == bottom ? Mortar : y == bottom + 1 ? StoneDark : Stone;
                if ((x == 0 && part != 0) || x == 16 || (x == 31 && part != 2)) c = Mortar;
                if (part == 0 && x == 0) c = StoneDark;
                if (part == 2 && x == 31) c = StoneDark;
                if (Hash(x, y, 77 + part) < 0.1f && y < top - 1 && y > bottom + 1) c = Shade(c, 0.88f);
                p.Set(x, y, c);
            }
            // iron bracket under each end of the ledge
            if (part != 1)
            {
                int bx = part == 0 ? 5 : 21;
                var iron = Hex("#3A3F48");
                var ironLight = Hex("#6B7380");
                p.Rect(bx, bottom - 2, 6, 2, iron);
                p.Rect(bx, bottom - 1, 6, 1, ironLight);
                for (int i = 0; i < 6; i++) p.Set(part == 0 ? bx + i : bx + 5 - i, bottom - 3 - i, iron);
                p.Rect(part == 0 ? bx : bx + 5, bottom - 9, 1, 7, iron);
            }
            // moss on top
            for (int x = 0; x < 32; x++)
                if (Hash(x, 1, 31 + part) < 0.35f) p.Set(x, top, Hash(x, 2, part) < 0.5f ? MossLight : Moss);
            p.Outline(Ink);
            return p;
        }

        static Px BackWall(int seed)
        {
            var p = StoneBricks(seed + 40, Wall, WallLight, Shade(Wall, 0.8f), WallMortar, 16, 16, 0.08f);
            // moss streaks and a missing brick or two
            for (int x = 0; x < 32; x++)
            {
                if (Hash(x, 0, seed) < 0.18f)
                {
                    int len = 3 + (int)(Hash(x, 1, seed) * 10f);
                    for (int y = 31; y > 31 - len; y--) p.Set(x, y, WallMoss);
                }
            }
            if (seed == 2) p.Rect(17, 1, 14, 14, WallMortar);
            return p;
        }

        static Px BackWallTop(int seed)
        {
            var p = BackWall(seed);
            for (int x = 0; x < 32; x++)
            {
                int cut = (int)(Hash(x / 4, 9, seed) * 10f) + (int)(Hash(x, 8, seed) * 3f);
                for (int y = 31; y > 31 - cut; y--) p.Set(x, y, Clear);
                if (cut < 31) p.Set(x, 31 - cut, WallLight);
            }
            return p;
        }

        static Px Pillar(bool top)
        {
            var p = new Px(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 6; x < 26; x++)
            {
                Color32 c = x == 6 ? WallMortar : x == 7 ? WallLight : x == 25 ? WallMortar : x == 24 ? Shade(Wall, 0.75f) : Wall;
                if (x == 12 || x == 18) c = Shade(Wall, 0.8f);
                if (y % 16 == 0) c = WallMortar;
                p.Set(x, y, Shade(c, 1.05f));
            }
            if (top)
            {
                for (int x = 0; x < 32; x++)
                {
                    int cut = 8 + (int)(Hash(x / 3, 4, 3) * 9f);
                    for (int y = 31; y > 31 - cut; y--) p.Set(x, y, Clear);
                }
            }
            return p;
        }

        // =====================================================================
        // Backgrounds
        // =====================================================================

        static Px FarBackground()
        {
            const int w = 480, h = 270;
            var p = new Px(w, h);
            var skyTop = Hex("#070D16");
            var skyLow = Hex("#1D3A47");
            for (int y = 0; y < h; y++)
            {
                float t = Mathf.Pow(y / (float)h, 0.8f);
                int band = Mathf.FloorToInt(t * 14f);
                var c = Mix(skyLow, skyTop, band / 14f);
                for (int x = 0; x < w; x++) p.Set(x, y, c);
            }
            // stars
            for (int i = 0; i < 160; i++)
            {
                int x = (int)(Hash(i, 0, 5) * w), y = 120 + (int)(Hash(i, 1, 5) * 150);
                byte a = (byte)(90 + Hash(i, 2, 5) * 165);
                p.Blend(x, y, new Color32(210, 235, 240, a));
            }
            // moon with halo
            int mx = 330, my = 200;
            for (int r = 60; r > 20; r -= 8) p.Ellipse(mx, my, r, r, (nx, ny) => new Color32(120, 190, 200, 12), true);
            p.Ellipse(mx, my, 20, 20, (nx, ny) =>
            {
                var c = Hex("#D7EEE6");
                if (-nx * 0.5f + ny * 0.6f < -0.35f) c = Hex("#A9CFC7");
                return c;
            });
            p.Ellipse(mx - 6, my + 4, 4, 3, (a, b) => Hex("#B9DAD2"));
            p.Ellipse(mx + 7, my - 6, 3, 3, (a, b) => Hex("#B9DAD2"));

            // distant castle silhouettes
            var far = Hex("#13283A");
            var farLit = Hex("#F2B45A");
            int baseY = 70;
            for (int x = 0; x < w; x++)
            {
                int hgt = baseY + (int)(Mathf.PerlinNoise(x * 0.02f, 3.3f) * 30f);
                for (int y = 0; y < hgt; y++) p.Set(x, y, far);
            }
            for (int i = 0; i < 9; i++)
            {
                int tx = 20 + i * 52 + (int)(Hash(i, 0, 9) * 20f);
                int tw = 10 + (int)(Hash(i, 1, 9) * 10f);
                int th = 110 + (int)(Hash(i, 2, 9) * 70f);
                p.Rect(tx, 0, tw, th, far);
                for (int k = 0; k < tw / 2 + 4; k++) p.Rect(tx - 2 + k, th + k * 2, Mathf.Max(1, tw + 4 - k * 2), 2, far);
                for (int k = 0; k < 3; k++)
                    if (Hash(i, k + 3, 9) < 0.6f) p.Rect(tx + 3 + (int)(Hash(i, k, 4) * (tw - 6)), th - 20 - k * 22, 2, 3, farLit);
            }
            // mist band
            for (int y = 20; y < 95; y++)
            for (int x = 0; x < w; x++)
            {
                float m = Mathf.PerlinNoise(x * 0.015f, y * 0.06f) * (1f - Mathf.Abs(y - 55) / 40f);
                if (m > 0.25f) p.Blend(x, y, new Color32(70, 130, 140, (byte)(m * 70)));
            }
            return p;
        }

        static Px MidRuins()
        {
            const int w = 480, h = 200;
            var p = new Px(w, h);
            var c = Hex("#0B1922");
            var edge = Hex("#15303C");
            // ground mass
            for (int x = 0; x < w; x++)
            {
                int hgt = 26 + (int)(Mathf.PerlinNoise(x * 0.03f, 1.7f) * 18f);
                for (int y = 0; y < hgt; y++) p.Set(x, y, c);
            }
            // arches and broken columns
            for (int i = 0; i < 6; i++)
            {
                int ax = 10 + i * 80;
                int colH = 120 + (int)(Hash(i, 0, 12) * 60f);
                p.Rect(ax, 0, 14, colH, c);
                p.Rect(ax + 56, 0, 14, colH - 10, c);
                // arch between the columns
                for (int x = ax; x < ax + 70; x++)
                {
                    float nx = (x - (ax + 35)) / 35f;
                    int archY = colH - 30 + (int)(Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx)) * 28f);
                    if (Hash(i, x, 3) < 0.85f || Mathf.Abs(nx) > 0.6f) p.Rect(x, archY, 1, 12, c);
                }
                // broken top
                for (int x = ax; x < ax + 14; x++) p.Rect(x, colH, 1, (int)(Hash(x, i, 8) * 8f), c);
                // vines
                for (int v = 0; v < 4; v++)
                {
                    int vx = ax + (int)(Hash(i, v, 13) * 70f);
                    int len = 15 + (int)(Hash(v, i, 14) * 35f);
                    int top = colH - 25;
                    for (int y = top; y > top - len; y--) p.Set(vx + (int)(Mathf.Sin(y * 0.3f) * 1.2f), y, c);
                }
            }
            // faint rim light
            for (int y = 1; y < h; y++)
            for (int x = 1; x < w; x++)
                if (p.Solid(x, y) && !p.Solid(x, y + 1)) p.Set(x, y, edge);
            return p;
        }

        // =====================================================================
        // Props
        // =====================================================================

        static Px TorchBody()
        {
            var p = new Px(12, 24);
            var iron = Hex("#3A3F48");
            var ironLight = Hex("#6B7380");
            var wood = Hex("#5A3A22");
            p.Rect(4, 2, 4, 14, wood);
            p.Rect(5, 2, 1, 14, Hex("#7A5232"));
            p.Rect(2, 14, 8, 2, iron);
            p.Rect(2, 16, 1, 3, iron);
            p.Rect(9, 16, 1, 3, iron);
            p.Rect(2, 15, 8, 1, ironLight);
            p.Rect(3, 4, 6, 2, iron);
            p.Rect(5, 0, 2, 2, iron);
            p.Outline(Ink);
            return p;
        }

        static Px[] TorchFlames()
        {
            var frames = new Px[4];
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(12, 16);
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 12; x++)
                {
                    float sway = Mathf.Sin((y * 0.5f) + f * 1.6f) * (y / 16f) * 1.6f;
                    float dx = (x + 0.5f - 6f - sway) / (3.6f * (1f - y / 18f));
                    float dy = y / 14f;
                    float d = dx * dx + dy * dy * 0.6f;
                    if (d > 1f || y > 13 - (f % 2)) continue;
                    Color32 c = d < 0.25f && y < 8 ? Hex("#FFF4C2") : d < 0.55f ? Hex("#FFD36B") : d < 0.8f ? Hex("#FF9A3C") : Hex("#E0531F");
                    p.Set(x, y, c);
                }
                frames[f] = p;
            }
            return frames;
        }

        static Px ChainLink()
        {
            var p = new Px(8, 16);
            var iron = Hex("#4A505B");
            var light = Hex("#7D8796");
            p.Ellipse(4f, 4f, 3f, 4f, (nx, ny) => nx * nx + ny * ny > 0.4f ? (ny > 0 ? light : iron) : Clear);
            p.Rect(3, 9, 2, 7, iron);
            p.Rect(3, 9, 1, 7, light);
            p.Outline(Ink);
            return p;
        }

        static Px Banner()
        {
            var p = new Px(20, 44);
            var cloth = Hex("#6E1F2B");
            var clothLight = Hex("#93303E");
            var gold = Hex("#D9A441");
            p.Rect(0, 41, 20, 3, Hex("#4A3A2A"));
            for (int y = 0; y < 41; y++)
            for (int x = 1; x < 19; x++)
            {
                int tatter = (int)(Hash(x, 0, 66) * 6f);
                if (y < tatter + (x % 6 == 0 ? 3 : 0)) continue;
                var c = x < 4 ? clothLight : cloth;
                if (x == 2 || x == 17) c = gold;
                p.Set(x, y, c);
            }
            // emblem: a simple bug crest
            p.Ellipse(10f, 26f, 4f, 5f, (nx, ny) => gold);
            p.Rect(9, 20, 2, 12, cloth);
            p.Line(6, 31, 4, 34, gold);
            p.Line(14, 31, 16, 34, gold);
            p.Outline(Ink);
            return p;
        }

        static Px Window()
        {
            var p = new Px(24, 40);
            var frame = Hex("#2A3946");
            var glass = Hex("#5FA7B8");
            var glassLight = Hex("#A7E1E6");
            for (int y = 0; y < 40; y++)
            for (int x = 0; x < 24; x++)
            {
                float nx = (x + 0.5f - 12f) / 12f;
                float archTop = 28f + Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx)) * 12f;
                if (y > archTop) continue;
                bool border = x < 3 || x > 20 || y < 3 || y > archTop - 3;
                p.Set(x, y, border ? frame : Mix(glass, glassLight, y / 40f));
            }
            for (int y = 3; y < 37; y++) { p.Set(11, y, frame); p.Set(12, y, frame); }
            for (int x = 3; x < 21; x++) { p.Set(x, 16, frame); p.Set(x, 26, frame); }
            p.Outline(Ink);
            return p;
        }

        static Px[] GrassTufts()
        {
            var list = new Px[3];
            for (int v = 0; v < 3; v++)
            {
                var p = new Px(16, 10);
                for (int b = 0; b < 7; b++)
                {
                    int x = 1 + (int)(Hash(b, v, 50) * 14f);
                    int hgt = 3 + (int)(Hash(v, b, 51) * 7f);
                    int lean = Hash(b, v, 52) < 0.5f ? -1 : 1;
                    for (int y = 0; y < hgt; y++)
                        p.Set(x + (y > hgt / 2 ? lean : 0), y, y == hgt - 1 ? MossBright : y > hgt / 2 ? MossLight : Moss);
                }
                list[v] = p;
            }
            return list;
        }

        static Px Bones()
        {
            var p = new Px(18, 10);
            var bone = Hex("#D9CFB8");
            var boneDark = Hex("#A79E86");
            p.Ellipse(6f, 5f, 4.5f, 4f, (nx, ny) => ny < -0.3f ? boneDark : bone);
            p.Rect(4, 3, 2, 2, Ink);
            p.Rect(7, 3, 2, 2, Ink);
            p.Line(10, 1, 17, 3, bone, 2);
            p.Rect(16, 2, 2, 3, bone);
            p.Outline(Ink);
            return p;
        }

        static Px DoorFrame()
        {
            var p = new Px(72, 104);
            for (int y = 0; y < 104; y++)
            for (int x = 0; x < 72; x++)
            {
                float nx = (x + 0.5f - 36f) / 36f;
                float outerTop = 68f + Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx)) * 36f;
                if (y > outerTop) continue;
                float ix = (x + 0.5f - 36f) / 24f;
                float innerTop = 72f + Mathf.Sqrt(Mathf.Max(0f, 1f - ix * ix)) * 22f;
                bool opening = Mathf.Abs(ix) < 1f && y < innerTop && y >= 0;
                if (opening)
                {
                    p.Set(x, y, Hex("#05080C"));
                    continue;
                }
                int row = y / 8, lx = (x + (row % 2) * 6) % 12;
                Color32 c = (y % 8 == 0 || lx == 0) ? Mortar : Mix(Stone, StoneLight, Hash(x / 12, row, 5) * 0.6f);
                if (y % 8 == 7) c = Mix(c, StoneLighter, 0.4f);
                p.Set(x, y, c);
            }
            // keystone
            p.Rect(31, 94, 10, 10, StoneLight);
            p.Rect(33, 96, 6, 6, Hex("#3FD0C0"));
            p.Rect(34, 99, 2, 2, Hex("#C9FFF6"));
            p.Outline(Ink);
            return p;
        }

        static Px Portcullis()
        {
            var p = new Px(48, 84);
            var iron = Hex("#3B414B");
            var light = Hex("#717B89");
            for (int b = 0; b < 6; b++)
            {
                int x = 3 + b * 8;
                p.Rect(x, 6, 3, 78, iron);
                p.Rect(x, 6, 1, 78, light);
                p.Rect(x, 0, 3, 6, iron);
                p.Set(x + 1, 0, Clear);
            }
            foreach (int y in new[] { 24, 52, 76 })
            {
                p.Rect(0, y, 48, 3, iron);
                p.Rect(0, y + 2, 48, 1, light);
            }
            p.Outline(Ink);
            return p;
        }

        static Px[] PortalFrames()
        {
            var frames = new Px[6];
            for (int f = 0; f < 6; f++)
            {
                var p = new Px(48, 84);
                for (int y = 0; y < 84; y++)
                for (int x = 0; x < 48; x++)
                {
                    float nx = (x + 0.5f - 24f) / 24f;
                    float top = 62f + Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx)) * 22f;
                    if (y > top) continue;
                    float swirl = Mathf.Sin((x * 0.35f) + (y * 0.22f) - f * (Mathf.PI * 2f / 6f));
                    float centre = 1f - Mathf.Abs(nx);
                    float v = 0.35f + 0.35f * swirl + 0.3f * centre;
                    int band = Mathf.Clamp(Mathf.FloorToInt(v * 4f), 0, 3);
                    Color32 c = band == 3 ? Hex("#D8FFF6") : band == 2 ? Hex("#6FF0D8") : band == 1 ? Hex("#2BA9A0") : Hex("#14585E");
                    p.Set(x, y, c);
                }
                for (int s = 0; s < 8; s++)
                {
                    int sx = (int)(Hash(s, f, 70) * 44f) + 2, sy = (int)(Hash(f, s, 71) * 70f) + 4;
                    p.Set(sx, sy, Hex("#FFFFFF"));
                }
                frames[f] = p;
            }
            return frames;
        }

        static Px ArrowDown()
        {
            var p = new Px(13, 10);
            var c = Hex("#F2C14E");
            for (int y = 0; y < 7; y++) p.Rect(6 - y, y + 3, y * 2 + 1, 1, c);
            p.Rect(5, 0, 3, 4, Clear);
            for (int y = 0; y < 7; y++) p.Set(6, y + 3, Hex("#FFE39A"));
            p.Outline(Ink);
            return p;
        }

        // =====================================================================
        // Bugs (all face right)
        // =====================================================================

        /// <summary>Side-view beetle. scale 1 = 40x28 frame. legPhase 0..3, pose 0 walk / 1 tell / 2 lunge / 3 charge.</summary>
        static void DrawBeetle(Px p, float s, int legPhase, int pose, Color32 shell, Color32 shellLight, Color32 shellDark, Color32 headCol, Color32 eye, bool horn, bool cracks)
        {
            var leg = Hex("#22150E");
            float lift = pose == 1 ? 2f * s : 0f;           // rears up when about to attack
            float lean = pose == 2 || pose == 3 ? 2f * s : 0f; // leans forward when lunging

            // legs (behind the shell)
            for (int i = 0; i < 3; i++)
            {
                float hx = (11 + i * 8) * s + lean;
                float hy = 8 * s + lift * 0.5f;
                int phase = (legPhase + i) % 4;
                float footOffset = (phase == 0 ? -2f : phase == 1 ? 0f : phase == 2 ? 2f : 0f) * s;
                float footLift = phase == 3 ? 1.5f * s : 0f;
                float kx = hx + 3f * s, ky = 4.5f * s + footLift;
                p.Line(hx, hy, kx, ky, leg, Mathf.Max(1, Mathf.RoundToInt(s)));
                p.Line(kx, ky, hx + footOffset + 1f * s, footLift, leg, Mathf.Max(1, Mathf.RoundToInt(s)));
            }

            // shell dome
            float cx = 19f * s + lean, cy = 12f * s + lift, rx = 14f * s, ry = 9f * s;
            p.Ellipse(cx, cy, rx, ry, (nx, ny) =>
            {
                if (ny < -0.45f) return Clear;
                float light = -nx * 0.45f + ny * 0.8f;
                var c = light > 0.62f ? shellLight : light < -0.25f ? shellDark : shell;
                if (ny < -0.25f) c = shellDark;
                return c;
            });
            // elytra seam + spots
            for (float x = cx - rx * 0.8f; x < cx + rx * 0.8f; x++) p.Set((int)x, (int)(cy - ry * 0.25f), shellDark);
            for (int i = 0; i < 3; i++)
                p.Ellipse(cx - rx * 0.4f + i * rx * 0.35f, cy + ry * 0.25f, 1.2f * s, 1f * s, (a, b) => shellDark);
            if (cracks)
            {
                var glow = Hex("#FF8C3A");
                p.Line(cx - rx * 0.5f, cy + ry * 0.5f, cx - rx * 0.2f, cy - ry * 0.1f, glow, Mathf.RoundToInt(s * 0.8f));
                p.Line(cx + rx * 0.1f, cy + ry * 0.7f, cx + rx * 0.35f, cy + ry * 0.1f, glow, Mathf.RoundToInt(s * 0.8f));
                p.Line(cx + rx * 0.35f, cy + ry * 0.1f, cx + rx * 0.55f, cy + ry * 0.3f, glow, Mathf.RoundToInt(s * 0.8f));
            }

            // head
            float hx2 = 34f * s + lean * 1.5f, hy2 = 9f * s + lift * 1.3f;
            p.Ellipse(hx2, hy2, 5f * s, 4.5f * s, (nx, ny) => ny > 0.4f && nx < 0.2f ? Shade(headCol, 1.3f) : headCol);
            p.Rect(Mathf.RoundToInt(hx2 + 1f * s), Mathf.RoundToInt(hy2 + 1f * s), Mathf.Max(2, Mathf.RoundToInt(2f * s)), Mathf.Max(2, Mathf.RoundToInt(2f * s)), eye);

            // mandibles (open when attacking)
            var mand = Hex("#E8D9C0");
            float open = pose == 1 || pose == 2 ? 2.5f * s : 0.8f * s;
            p.Line(hx2 + 4f * s, hy2 - 1f * s, hx2 + 7f * s, hy2 - 1f * s - open, mand, Mathf.Max(1, Mathf.RoundToInt(s * 0.8f)));
            p.Line(hx2 + 4f * s, hy2 - 2.5f * s, hx2 + 7f * s, hy2 - 3f * s + open * 0.3f, mand, Mathf.Max(1, Mathf.RoundToInt(s * 0.8f)));

            if (horn)
            {
                p.Line(hx2 + 1f * s, hy2 + 4f * s, hx2 + 5f * s, hy2 + 11f * s + lift, mand, Mathf.Max(2, Mathf.RoundToInt(s * 1.2f)));
                p.Line(hx2 + 5f * s, hy2 + 11f * s + lift, hx2 + 3f * s, hy2 + 14f * s + lift, mand, Mathf.Max(1, Mathf.RoundToInt(s)));
            }
            p.Outline(Ink);
        }

        static Px[] BeetleFrames(float s, int w, int h, Color32 shell, Color32 light, Color32 dark, Color32 head, Color32 eye, bool horn, bool cracks)
        {
            // walk 0-3, tell 4, lunge 5
            var frames = new List<Px>();
            for (int i = 0; i < 4; i++) { var p = new Px(w, h); DrawBeetle(p, s, i, 0, shell, light, dark, head, eye, horn, cracks); frames.Add(p); }
            { var p = new Px(w, h); DrawBeetle(p, s, 0, 1, shell, light, dark, head, eye, horn, cracks); frames.Add(p); }
            { var p = new Px(w, h); DrawBeetle(p, s, 2, 2, shell, light, dark, head, eye, horn, cracks); frames.Add(p); }
            return frames.ToArray();
        }

        static Px[] WaspFrames()
        {
            var frames = new Px[3];
            var yellow = Hex("#F2C14E");
            var black = Hex("#1D1A16");
            var thorax = Hex("#6B4A24");
            var wing = Hex("#CFEEFD", 170);
            var wingEdge = Hex("#8FC8E0", 220);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(36, 30);
                // legs
                p.Line(17, 11, 15, 5, black);
                p.Line(20, 11, 21, 4, black);
                p.Line(23, 12, 25, 6, black);
                // abdomen with stripes
                p.Ellipse(10f, 13f, 8f, 6f, (nx, ny) =>
                {
                    int band = Mathf.FloorToInt((nx + 1f) * 3.5f);
                    var c = band % 2 == 0 ? yellow : black;
                    if (ny > 0.5f && c.r > 100) c = Hex("#FFE38F");
                    return c;
                });
                // stinger
                p.Line(2, 10, 0, 8, black);
                p.Set(1, 9, black);
                // thorax + head
                p.Ellipse(19.5f, 15f, 5f, 4.5f, (nx, ny) => ny > 0.4f ? Shade(thorax, 1.3f) : thorax);
                p.Ellipse(26f, 16f, 4f, 4f, (nx, ny) => black);
                p.Rect(27, 16, 3, 3, Hex("#E04040"));
                p.Set(28, 18, Hex("#FFB0B0"));
                p.Line(27, 20, 31, 25, black);
                p.Outline(Ink);
                // wings over the top (semi transparent)
                float wy = f == 0 ? 24f : f == 1 ? 21f : 18f;
                float wry = f == 1 ? 2.5f : 3.5f;
                p.Ellipse(16f, wy, 9f, wry, (nx, ny) => nx * nx + ny * ny > 0.7f ? wingEdge : wing, true);
                if (f != 2) p.Ellipse(20f, wy - 1f, 7f, wry * 0.8f, (nx, ny) => nx * nx + ny * ny > 0.7f ? wingEdge : wing, true);
                frames[f] = p;
            }
            return frames;
        }

        static Px[] TickFrames()
        {
            // idle 0-1 (sac pulse), walk 2-3, charge 4 (swollen), spit 5
            var frames = new List<Px>();
            for (int f = 0; f < 6; f++)
            {
                var p = new Px(36, 28);
                float swell = f == 1 ? 1f : f == 4 ? 2.2f : f == 5 ? -1.5f : 0f;
                int legPhase = f == 2 ? 1 : f == 3 ? 3 : 0;
                var leg = Hex("#1F2A14");
                for (int i = 0; i < 3; i++)
                {
                    float hx = 12 + i * 6;
                    float off = ((legPhase + i) % 2 == 0 ? -1.5f : 1.5f);
                    p.Line(hx, 6, hx + 2, 3, leg);
                    p.Line(hx + 2, 3, hx + off + 1, 0, leg);
                }
                // sac
                p.Ellipse(13f, 13f, 11f + swell, 9.5f + swell, (nx, ny) =>
                {
                    float light = -nx * 0.4f + ny * 0.8f;
                    var c = light > 0.55f ? Hex("#C8FF7A") : light < -0.3f ? Hex("#3E8A2A") : Hex("#7FDC4A");
                    return c;
                });
                for (int sp = 0; sp < 5; sp++)
                    p.Ellipse(6f + sp * 3.5f, 11f + (sp % 2) * 5f, 1.4f, 1.2f, (a, b) => Hex("#2F6A20"));
                // head
                float headUp = f == 5 ? 3f : 0f;
                p.Ellipse(27f, 8f + headUp, 5f, 4f, (nx, ny) => ny > 0.3f ? Hex("#5A7A38") : Hex("#3D5A2A"));
                p.Rect(28, 9 + (int)headUp, 2, 2, Hex("#FFE066"));
                if (f == 5) p.Rect(31, 6 + (int)headUp, 3, 2, Hex("#C8FF7A"));
                p.Outline(Ink);
                frames.Add(p);
            }
            return frames.ToArray();
        }

        static Px[] AcidFrames()
        {
            var frames = new Px[2];
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(12, 12);
                p.Ellipse(6f, 6f, 4.5f - f * 0.5f, 4f + f * 0.5f, (nx, ny) => -nx * 0.5f + ny * 0.6f > 0.4f ? Hex("#E8FFB0") : Hex("#8CEB3E"));
                p.Set(4, 8, Hex("#FFFFFF"));
                p.Outline(Hex("#1F4A12"));
                frames[f] = p;
            }
            return frames;
        }

        // =====================================================================
        // Bosses: Hornet Queen (floor 20) and Reaper Mantis (floor 30)
        // =====================================================================

        static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>Tapered limb: a run of circles from a (radius r0) to b (radius r1).</summary>
        static void Capsule(Px p, Vector2 a, Vector2 b, float r0, float r1, Color32 c)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var q = Vector2.Lerp(a, b, t);
                float r = Mathf.Max(0.55f, Mathf.Lerp(r0, r1, t));
                p.Ellipse(q.x, q.y, r, r, (nx, ny) => c);
            }
        }

        /// <summary>
        /// Hornet Queen (96x80, faces right). wing 0 up / 1 mid / 2 down.
        /// pose 0 fly, 1 tell (abdomen curled forward, stinger aimed), 2 stuck (wings drooped), 3 dive (tucked).
        /// </summary>
        static Px DrawHornet(int wing, int pose)
        {
            var p = new Px(96, 80);
            var gold = Hex("#F2B33D");
            var goldLight = Hex("#FFD978");
            var black = Hex("#1D1A16");
            var thorax = Hex("#5C3415");
            var thoraxLight = Hex("#8A5426");
            var wingFill = Hex("#D8F2FF", 150);
            var wingEdge = Hex("#9CCBE0", 215);
            var crown = Hex("#FFC83A");
            var crownLight = Hex("#FFF0A8");

            // wings first (behind the body)
            Vector2 w1, w2;
            float r1y, r2y;
            if (pose == 3) { w1 = new Vector2(34f, 50f); w2 = new Vector2(38f, 47f); r1y = 5f; r2y = 4f; }
            else if (pose == 2) { w1 = new Vector2(40f, 38f); w2 = new Vector2(44f, 36f); r1y = 5f; r2y = 4f; }
            else if (wing == 0) { w1 = new Vector2(44f, 65f); w2 = new Vector2(50f, 60f); r1y = 8f; r2y = 6f; }
            else if (wing == 1) { w1 = new Vector2(42f, 56f); w2 = new Vector2(48f, 54f); r1y = 6f; r2y = 5f; }
            else { w1 = new Vector2(44f, 47f); w2 = new Vector2(50f, 46f); r1y = 8f; r2y = 6f; }
            p.Ellipse(w1.x, w1.y, 25f, r1y, (nx, ny) => nx * nx + ny * ny > 0.72f ? wingEdge : wingFill, true);
            p.Ellipse(w2.x, w2.y, 19f, r2y, (nx, ny) => nx * nx + ny * ny > 0.72f ? wingEdge : wingFill, true);
            p.Line(w1.x + 18f, w1.y, w1.x - 16f, w1.y + (wing == 2 ? -2f : 2f), wingEdge);

            // legs
            bool tucked = pose == 3;
            for (int i = 0; i < 3; i++)
            {
                float hx = 50f + i * 5f, hy = 34f;
                var knee = tucked ? new Vector2(hx - 6f, hy - 3f) : new Vector2(hx - 2f + i, hy - 8f);
                var foot = tucked ? new Vector2(hx - 10f, hy - 1f) : new Vector2(hx - 5f + i * 2f, hy - 16f);
                p.Line(hx, hy, knee.x, knee.y, black, 2);
                p.Line(knee.x, knee.y, foot.x, foot.y, black, 2);
            }

            // abdomen + stinger
            if (pose == 1)
            {
                p.Ellipse(40f, 24f, 15f, 12f, (nx, ny) =>
                {
                    int band = Mathf.FloorToInt((ny + 1f) * 3.5f + nx * 0.8f);
                    var c = band % 2 == 0 ? gold : black;
                    if (nx < -0.2f && ny > 0.3f && c.r > 100) c = goldLight;
                    return c;
                });
                p.Line(50f, 14f, 60f, 8f, black, 2);
                p.Line(58f, 9f, 63f, 6f, Hex("#FFE680"), 2);
            }
            else
            {
                p.Ellipse(28f, 38f, 21f, 13f, (nx, ny) =>
                {
                    int band = Mathf.FloorToInt((nx + 1f) * 4f);
                    var c = band % 2 == 0 ? gold : black;
                    if (ny > 0.45f && c.r > 100) c = goldLight;
                    return c;
                });
                p.Line(9f, 36f, 2f, 33f, black, 2);
                p.Line(3f, 33f, 0f, 32f, Hex("#FFE680"));
            }

            // waist, thorax (fuzzy), head
            p.Ellipse(46f, 39f, 4f, 3f, (nx, ny) => black);
            p.Ellipse(56f, 42f, 12f, 11f, (nx, ny) =>
            {
                int x = Mathf.RoundToInt(56f + nx * 12f), y = Mathf.RoundToInt(42f + ny * 11f);
                var c = ny > 0.35f ? thoraxLight : thorax;
                if (Hash(x, y, 610) < 0.18f) c = Shade(c, 0.75f);
                return c;
            });
            p.Ellipse(72f, 44f, 9f, 9f, (nx, ny) => ny > 0.55f && nx < 0f ? Hex("#3A332C") : black);
            p.Ellipse(75f, 47f, 4.5f, 5.5f, (nx, ny) => nx < 0.1f && ny > 0.3f ? Hex("#FF9A9A") : Hex("#D83030"));
            p.Line(80f, 38f, 85f, 35f, Hex("#E8D9C0"), 2);
            p.Line(80f, 36f, 84f, 31f, Hex("#E8D9C0"));
            p.Line(70f, 52f, 77f, 61f, black);
            p.Line(77f, 61f, 86f, 65f, black);

            // crown
            p.Rect(64, 52, 14, 3, crown);
            foreach (var (x, h) in new[] { (64, 4), (68, 6), (72, 7), (76, 4) })
            {
                p.Rect(x, 55, 2, h, crown);
                p.Set(x, 55 + h - 1, crownLight);
            }
            p.Rect(71, 57, 2, 2, Hex("#E0304A"));
            p.Rect(64, 54, 14, 1, crownLight);

            p.Outline(Ink);
            return p;
        }

        static Px[] HornetQueenFrames()
        {
            // 0-2 fly, 3 tell (wings up), 4 stuck, 5 dive, 6 tell (wings down)
            return new[]
            {
                DrawHornet(0, 0), DrawHornet(1, 0), DrawHornet(2, 0),
                DrawHornet(0, 1), DrawHornet(1, 2), DrawHornet(1, 3), DrawHornet(2, 1),
            };
        }

        static Px StingerSprite()
        {
            var p = new Px(18, 6);
            var shaft = Hex("#3A2A14");
            p.Rect(0, 1, 4, 4, shaft);
            p.Rect(4, 2, 8, 2, shaft);
            p.Rect(12, 2, 4, 2, Hex("#FFD54A"));
            p.Set(16, 2, Hex("#FFF2B0"));
            p.Set(16, 3, Hex("#FFF2B0"));
            p.Set(17, 3, Color.white);
            p.Rect(4, 3, 8, 1, Hex("#6B5030"));
            p.Outline(Ink);
            return p;
        }

        static Px[] PoisonSacFrames()
        {
            var frames = new Px[2];
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(14, 14);
                p.Ellipse(7f, 7f, 5f - f * 0.5f, 4.5f + f * 0.5f, (nx, ny) =>
                {
                    float l = -nx * 0.5f + ny * 0.6f;
                    return l > 0.45f ? Hex("#E5B8FF") : l < -0.35f ? Hex("#6A2BB0") : Hex("#A64DFF");
                });
                p.Rect(5, 8, 2, 1, Hex("#B8FF6A"));
                p.Set(8, 4, Hex("#B8FF6A"));
                p.Outline(Hex("#2A0E44"));
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>Reaper Mantis pose (104x104, faces right, feet at y = 0).</summary>
        struct MantisPose
        {
            public int legPhase;    // 0..3 walk cycle
            public float crouch;    // body drops this many pixels (legs bend)
            public float lean;      // head and shoulders shift forward
            public float arm, blade; // directions in degrees: upper arm, and the scythe blade
            public int wings;       // 0 folded, 1 open (flap up), 2 open (flap down), 3 half open
            public bool dangle;     // legs hanging (flying)
            public bool buried;     // scythes driven into the ground
            public float antenna;   // antenna sway
        }

        static Px DrawMantis(MantisPose m)
        {
            var p = new Px(104, 104);
            var body = Hex("#3E8F57");
            var bodyLight = Hex("#6CC27A");
            var bodyDark = Hex("#23573A");
            var legNear = Hex("#2F7347");
            var legFar = Hex("#1E4A30");
            var blade = Hex("#E6E0C4");
            var bladeFar = Hex("#A8A48C");
            var edge = Hex("#FFFFFF");
            var spike = Hex("#F2EEDC");
            var wingFold = Hex("#7CC08A");
            var wingVein = Hex("#4E8F5E");
            var wingOpen = Hex("#B6F0C0", 105);
            var wingOpenEdge = Hex("#86CF98", 210);
            float c = m.crouch + 8f; // the body hangs low between bent legs

            float c2 = c * 0.6f; // the abdomen sinks a little less than the chest when crouching

            // open wings fan out behind the body
            if (m.wings != 0)
            {
                float tilt = m.wings == 1 ? 28f : m.wings == 2 ? -6f : 12f;
                var wc = new Vector2(26f, (m.wings == 1 ? 58f : m.wings == 2 ? 48f : 50f) - c);
                RotEllipse(p, wc.x, wc.y, 25f, m.wings == 3 ? 6f : 9f, tilt, (u, v) =>
                {
                    float r = u * u + v * v;
                    if (r > 0.8f) return wingOpenEdge;
                    return Mathf.Repeat(u * 6f + v * 2f, 1f) < 0.12f ? wingOpenEdge : wingOpen;
                }, true);
                RotEllipse(p, wc.x + 6f, wc.y - 3f, 22f, 4.5f, tilt - 6f, (u, v) => v > 0.3f ? Hex("#9ED9A6") : wingFold);
            }

            float[] steps = { -3f, 0f, 3f, 0f };
            void Leg(Vector2 hip, Vector2 knee, Vector2 foot, int i, Color32 col)
            {
                if (m.dangle)
                {
                    bool back = knee.x < hip.x;
                    knee = hip + new Vector2(back ? -8f : 6f, -7f);
                    foot = hip + new Vector2(back ? -12f : 4f, -17f);
                }
                else
                {
                    int ph = (m.legPhase + i) % 4;
                    foot += new Vector2(steps[ph], ph == 3 ? 2f : 0f);
                }
                p.Line(hip.x, hip.y, knee.x, knee.y, col, 3);
                p.Line(knee.x, knee.y, foot.x, foot.y, col, 2);
                if (!m.dangle) p.Line(foot.x, foot.y, foot.x + (knee.x < hip.x ? -3f : 3f), foot.y, col); // little foot
            }

            // far legs (bent-back hind leg, forward middle leg)
            Leg(new Vector2(37f, 32f - c), new Vector2(23f, 37f - c * 0.5f), new Vector2(15f, 0f), 1, legFar);
            Leg(new Vector2(43f, 33f - c), new Vector2(62f, 35f - c * 0.5f), new Vector2(72f, 0f), 3, legFar);

            var neckBase = new Vector2(41f, 36f - c);
            var neckTop = new Vector2(50f + m.lean, 63f - c);
            var shoulder = Vector2.Lerp(neckBase, neckTop, 0.78f);

            void Arm(Vector2 s, float armDeg, float bladeDeg, Color32 armCol, Color32 bladeCol, bool near)
            {
                var elbow = s + Dir(armDeg) * 20f;
                var tip = elbow + Dir(bladeDeg) * 24f;
                if (m.buried) tip = new Vector2(tip.x, 1f);
                Capsule(p, s, elbow, 3.4f, 2.8f, armCol);
                // spines along the inside of the upper arm
                var n = new Vector2(-Dir(armDeg).y, Dir(armDeg).x);
                if (Vector2.Dot(n, Dir(bladeDeg)) < 0f) n = -n;
                for (float t = 0.25f; t < 0.95f; t += 0.17f)
                {
                    var q = Vector2.Lerp(s, elbow, t) + n * 3.6f;
                    p.Set(Mathf.RoundToInt(q.x), Mathf.RoundToInt(q.y), near ? spike : bladeFar);
                }
                Capsule(p, elbow, tip, 3f, 0.7f, bladeCol);
                if (near)
                {
                    var outward = -n;
                    p.Line(elbow.x + outward.x * 2f, elbow.y + outward.y * 2f, tip.x, tip.y, edge);
                    p.Ellipse(elbow.x, elbow.y, 2f, 2f, (u, v) => bodyDark); // knuckle
                }
                if (m.buried)
                {
                    for (int d = -4; d <= 4; d++) p.Set(Mathf.RoundToInt(tip.x) + d, 0, Hex("#6B5A44"));
                    p.Set(Mathf.RoundToInt(tip.x) - 3, 1, Hex("#8A7660"));
                    p.Set(Mathf.RoundToInt(tip.x) + 2, 1, Hex("#8A7660"));
                    p.Set(Mathf.RoundToInt(tip.x) + 3, 2, Hex("#8A7660"));
                }
            }

            // far arm, behind the body
            Arm(shoulder + new Vector2(-3f, 1f), m.arm - 7f, m.blade + 9f, legFar, bladeFar, false);

            // long abdomen: tapered, tilted up toward the tail, with segment bands
            RotEllipse(p, 21f, 40f - c2, 19f, 7.5f, -14f, (u, v) =>
            {
                if (u < -0.2f && Mathf.Abs(v) > 1f - (-0.2f - u) * 0.75f) return Clear; // tapers to the tail
                var col = v > 0.4f ? bodyLight : v < -0.35f ? bodyDark : body;
                if (Mathf.Repeat(u * 19f, 4.5f) < 1f && v < 0.6f) col = bodyDark;
                return col;
            });
            // folded wings lie along the top of the abdomen
            if (m.wings == 0)
            {
                RotEllipse(p, 23f, 45f - c2, 20f, 3.6f, -14f, (u, v) => v > 0.25f ? Hex("#9ED9A6") : wingFold);
                RotEllipse(p, 23f, 45f - c2, 18f, 0.6f, -14f, (u, v) => wingVein);
            }

            // thorax where the legs join
            p.Ellipse(39f, 34f - c, 6f, 5f, (u, v) => v > 0.3f ? bodyLight : body);

            // near legs
            Leg(new Vector2(36f, 32f - c), new Vector2(19f, 40f - c * 0.5f), new Vector2(8f, 0f), 0, legNear);
            Leg(new Vector2(42f, 33f - c), new Vector2(57f, 38f - c * 0.5f), new Vector2(64f, 0f), 2, legNear);

            // neck (prothorax) with a lit front edge
            Capsule(p, neckBase, neckTop, 4f, 3.2f, body);
            p.Line(neckBase.x + 3.2f, neckBase.y + 2f, neckTop.x + 2.6f, neckTop.y - 1f, bodyLight);

            // head: wide triangle with big crimson eyes, mouthparts and long antennae
            var head = neckTop + new Vector2(4f, 4f);
            p.Line(head.x - 1f, head.y + 6f, head.x - 7f + m.antenna, head.y + 20f, Hex("#1E3A28"));
            p.Line(head.x - 7f + m.antenna, head.y + 20f, head.x - 15f + m.antenna * 1.5f, head.y + 30f, Hex("#1E3A28"));
            p.Line(head.x + 2f, head.y + 6f, head.x - 1f + m.antenna, head.y + 22f, Hex("#1E3A28"));
            p.Line(head.x - 1f + m.antenna, head.y + 22f, head.x - 6f + m.antenna * 1.5f, head.y + 33f, Hex("#1E3A28"));
            p.Ellipse(head.x, head.y, 9f, 7.5f, (u, v) =>
            {
                if (v < -0.1f && Mathf.Abs(u) > 1f + v * 1.1f) return Clear; // narrows to the jaw
                return v > 0.35f ? bodyLight : body;
            });
            p.Ellipse(head.x - 4.5f, head.y + 3.5f, 3f, 3.4f, (u, v) => Hex("#9E2433")); // far eye
            p.Ellipse(head.x + 3.5f, head.y + 3f, 4.3f, 4.7f, (u, v) => u < -0.1f && v > 0.25f ? Hex("#FFC0C8") : v < -0.5f ? Hex("#A8202E") : Hex("#E33B4E"));
            p.Line(head.x + 1f, head.y - 6f, head.x + 2f, head.y - 9f, bodyDark);
            p.Line(head.x + 3f, head.y - 5f, head.x + 5f, head.y - 8f, bodyDark);

            // near arm, in front
            Arm(shoulder, m.arm, m.blade, body, blade, true);

            p.Outline(Ink);
            return p;
        }

        /// <summary>Filled ellipse rotated by degrees; shade gets coords along (u) and across (v) the ellipse, -1..1.</summary>
        static void RotEllipse(Px p, float cx, float cy, float rx, float ry, float degrees, Func<float, float, Color32> shade, bool blend = false)
        {
            float a = degrees * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            int r = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float u = (dx * ca + dy * sa) / rx, v = (-dx * sa + dy * ca) / ry;
                if (u * u + v * v > 1f) continue;
                var col = shade(u, v);
                if (col.a == 0) continue;
                if (blend) p.Blend(x, y, col);
                else p.Set(x, y, col);
            }
        }

        static Px[] MantisFrames()
        {
            // 0-3 walk, 4 raise (tell), 5 slash down, 6 slash up, 7 crouch, 8-9 fly, 10 slam (stuck),
            // 11 guard (parry), 12 dodge, 13 idle breath
            var frames = new List<Px>();
            for (int i = 0; i < 4; i++)
                frames.Add(DrawMantis(new MantisPose { legPhase = i, arm = -55f, blade = 102f, crouch = i % 2 == 1 ? 1f : 0f, antenna = i % 2 }));
            frames.Add(DrawMantis(new MantisPose { arm = 72f, blade = 150f, lean = -3f, antenna = -2f }));
            frames.Add(DrawMantis(new MantisPose { arm = -12f, blade = -42f, lean = 5f, crouch = 2f, antenna = 3f }));
            frames.Add(DrawMantis(new MantisPose { arm = 22f, blade = 48f, lean = 4f, antenna = 3f }));
            frames.Add(DrawMantis(new MantisPose { arm = -30f, blade = 125f, crouch = 9f, lean = 2f, wings = 3 }));
            frames.Add(DrawMantis(new MantisPose { arm = 62f, blade = 145f, wings = 1, dangle = true, crouch = -2f, antenna = -3f }));
            frames.Add(DrawMantis(new MantisPose { arm = 62f, blade = 145f, wings = 2, dangle = true, crouch = -2f, antenna = -3f }));
            frames.Add(DrawMantis(new MantisPose { arm = -72f, blade = -100f, crouch = 13f, lean = 9f, wings = 3, buried = true, antenna = 4f }));
            frames.Add(DrawMantis(new MantisPose { arm = 82f, blade = -25f, lean = 1f, crouch = 2f }));
            frames.Add(DrawMantis(new MantisPose { arm = 40f, blade = 120f, lean = -6f, wings = 3, antenna = -4f }));
            frames.Add(DrawMantis(new MantisPose { arm = -51f, blade = 106f, crouch = -1f, antenna = -1.5f }));
            return frames.ToArray();
        }

        /// <summary>Thin, sharp scythe crescent for the mantis's slashes (pivot at the arc center).</summary>
        static Px ScytheSlash()
        {
            var p = new Px(96, 96);
            const float cx = 30f, cy = 48f, outer = 44f;
            for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < -82f || ang > 82f) continue;
                float along = (ang + 82f) / 164f;
                float thick = 1.5f + 6.5f * Mathf.Sin(Mathf.PI * along);
                if (r > outer || r < outer - thick) continue;
                float k = (outer - r) / thick;
                byte a = (byte)(255 * Mathf.Clamp01(1.3f - Mathf.Abs(along - 0.5f) * 2f));
                p.Set(x, y, k < 0.3f ? new Color32(255, 255, 255, a) : k < 0.65f ? new Color32(214, 255, 196, a) : new Color32(110, 220, 130, (byte)(a * 0.7f)));
            }
            return p;
        }

        // =====================================================================
        // Fire (Flame Slash skill)
        // =====================================================================

        static Color32 FireColor(float heat)
        {
            if (heat > 0.78f) return Hex("#FFF6D5");
            if (heat > 0.58f) return Hex("#FFD34E");
            if (heat > 0.38f) return Hex("#FF8C2A");
            if (heat > 0.18f) return Hex("#E0401C");
            if (heat > 0.06f) return Hex("#8A1E10", 210);
            return Clear;
        }

        /// <summary>Burning crescent (bulges toward +x). Different seeds give flickering frames.</summary>
        static Px FireSlash(int seed = 0)
        {
            var p = new Px(84, 84);
            for (int y = 0; y < 84; y++)
            for (int x = 0; x < 84; x++)
            {
                float dx = x + 0.5f - 26f, dy = y + 0.5f - 42f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < -82f || ang > 82f) continue;
                float taper = 1f - Mathf.Abs(ang) / 82f;
                float tongue = (Hash((int)(ang / 6f), seed, 320) * 7f + Mathf.Sin(ang * 0.4f + seed * 1.9f) * 2f) * taper;
                float outer = 38f + tongue;
                float thick = 4f + 13f * taper;
                if (r > outer || r < outer - thick - 2f) continue;
                float heat = (1f - Mathf.Abs(r - (outer - thick * 0.45f)) / (thick * 0.7f)) * (0.55f + 0.6f * taper);
                heat += (Hash(x, y, 321 + seed) - 0.5f) * 0.15f;
                p.Set(x, y, FireColor(heat));
            }
            // embers flaking off the back edge
            for (int s = 0; s < 10; s++)
            {
                float ang = (Hash(s, seed, 322) - 0.5f) * 150f * Mathf.Deg2Rad;
                float rr = 18f + Hash(seed, s, 323) * 12f;
                p.Set(Mathf.RoundToInt(26f + Mathf.Cos(ang) * rr), Mathf.RoundToInt(42f + Mathf.Sin(ang) * rr), Hash(s, s, 324 + seed) < 0.5f ? Hex("#FFD34E") : Hex("#FF8C2A"));
            }
            return p;
        }

        static Px FlameSlashIcon()
        {
            var p = new Px(24, 24);
            // flames licking up the blade
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float bx = (x + y) * 0.5f;               // distance along the blade diagonal
                float off = (x - y) * 0.7071f;            // distance from the blade
                if (bx < 9f || bx > 22f) continue;
                float width = 3.5f + Mathf.Sin(bx * 1.3f) * 1.2f;
                float heat = (1f - Mathf.Abs(off - 1.5f) / width) * Mathf.Clamp01((bx - 8f) / 6f);
                heat += (Hash(x, y, 330) - 0.5f) * 0.25f;
                if (heat > 0.06f) p.Set(x, y, FireColor(heat * 0.95f));
            }
            // blade (bottom-left to top-right)
            var steel = Hex("#E8EEF6");
            var steelDark = Hex("#9AA6B6");
            for (int i = 6; i < 22; i++)
            {
                p.Set(i, i, steel);
                p.Set(i + 1, i, steelDark);
            }
            // guard and grip
            var gold = Hex("#F2C14E");
            p.Line(3, 8, 8, 3, gold);
            p.Line(4, 8, 8, 4, gold);
            p.Line(1, 1, 5, 5, Hex("#6B4A24"), 2);
            p.Set(1, 1, gold);
            p.Outline(Ink);
            return p;
        }

        // =====================================================================
        // Other skills
        // =====================================================================

        /// <summary>
        /// Big overhead arc: starts behind the head, sweeps over the top and down in front (facing +x).
        /// Pivot is the arc's center (around the knight's shoulders).
        /// </summary>
        static Px OverheadArc()
        {
            var p = new Px(128, 128);
            const float cx = 48f, cy = 44f, outer = 60f;
            const float start = 165f, end = -55f; // degrees, counter-clockwise from +x
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < end || ang > start) continue;
                float along = (start - ang) / (start - end); // 0 behind the head .. 1 at the front tip
                float thick = 3f + 18f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(along * 0.85f + 0.08f));
                if (r > outer || r < outer - thick) continue;
                float k = (outer - r) / thick;
                byte a = (byte)(255 * Mathf.Clamp01(along * 1.8f));
                Color32 c = k < 0.3f ? new Color32(255, 255, 255, a)
                    : k < 0.6f ? new Color32(190, 236, 255, a)
                    : new Color32(90, 170, 240, (byte)(a * 0.75f));
                // speed lines inside the arc
                if (k > 0.6f && Hash((int)(ang * 0.5f), 0, 501) < 0.35f) c = new Color32(255, 255, 255, (byte)(a * 0.6f));
                p.Set(x, y, c);
            }
            return p;
        }

        static Px[] TornadoFrames()
        {
            var frames = new Px[4];
            var light = Hex("#E8F4F8", 235);
            var mid = Hex("#A8D4E2", 220);
            var dark = Hex("#6C8FA6", 210);
            var edge = Hex("#3C5468", 230);
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(60, 92);
                for (int y = 0; y < 92; y++)
                {
                    float t = y / 91f;
                    float half = 3f + Mathf.Pow(t, 1.25f) * 25f;
                    float cx = 30f + Mathf.Sin(y * 0.11f + f * 1.57f) * 4f * t;
                    for (int x = 0; x < 60; x++)
                    {
                        float u = (x + 0.5f - cx) / half; // -1..1 across the funnel
                        if (Mathf.Abs(u) > 1f) continue;
                        // diagonal bands that scroll every frame = spinning
                        float band = Mathf.Repeat(u * 2.2f + y * 0.09f - f * 0.5f, 3f);
                        Color32 c = band < 1f ? light : band < 2f ? mid : dark;
                        if (Mathf.Abs(u) > 0.86f) c = edge;
                        if (Hash(x, y, 520 + f) < 0.05f) c = light;
                        p.Set(x, y, c);
                    }
                }
                // debris whirling around it
                for (int d = 0; d < 9; d++)
                {
                    float a = Hash(d, 0, 530) * Mathf.PI * 2f + f * 1.57f;
                    int dy = 8 + (int)(Hash(d, 1, 531) * 76f);
                    float rad = 6f + dy * 0.28f;
                    int dx = Mathf.RoundToInt(30f + Mathf.Cos(a) * rad);
                    p.Rect(dx, dy, 2, 2, Hash(d, 2, 532) < 0.5f ? Hex("#7E705F") : Hex("#5C6672"));
                }
                // electric crackle
                for (int z = 0; z < 3; z++)
                {
                    int zy = 20 + (int)(Hash(z, f, 540) * 60f);
                    int zx = 30 + (int)((Hash(f, z, 541) - 0.5f) * 20f);
                    p.Line(zx, zy, zx + 3, zy + 3, Hex("#FFFFFF"));
                    p.Line(zx + 3, zy + 3, zx + 1, zy + 6, Hex("#9FE8FF"));
                }
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>A small horizontal lightning arc (pivot at the left end), stretched between two points.</summary>
        static Px[] ZapFrames()
        {
            var frames = new Px[3];
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(64, 16);
                float y = 8f;
                for (int x = 0; x < 64; x += 6)
                {
                    float ny = x == 0 || x >= 60 ? 8f : 8f + (Hash(x, f, 550) - 0.5f) * 10f;
                    p.Line(x, y, Mathf.Min(63, x + 6), ny, new Color32(90, 170, 255, 150), 4);
                    p.Line(x, y, Mathf.Min(63, x + 6), ny, new Color32(255, 255, 255, 255), 1);
                    y = ny;
                }
                frames[f] = p;
            }
            return frames;
        }

        static Px[] OrbFrames()
        {
            var frames = new Px[2];
            for (int f = 0; f < 2; f++)
            {
                var p = new Px(20, 20);
                float r = 7.5f + f * 0.7f;
                p.Ellipse(10f, 10f, r, r, (nx, ny) =>
                {
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (d < 0.35f) return Hex("#FFFFFF");
                    if (-nx * 0.5f + ny * 0.6f > 0.45f) return Hex("#F3D2FF");
                    if (d < 0.7f) return Hex("#C77DFF");
                    return Hex("#7B2CBF");
                });
                p.Outline(Hex("#2A0E44"));
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>Jagged electric ring for Thunderclap's shockwave.</summary>
        static Px ShockRing()
        {
            var p = new Px(128, 128);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = x + 0.5f - 64f, dy = y + 0.5f - 64f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float wobble = Mathf.Sin(ang * 9f) * 2.5f + (Hash((int)((ang + Mathf.PI) * 14f), 0, 560) - 0.5f) * 5f;
                float target = 56f + wobble;
                float d = Mathf.Abs(r - target);
                if (d < 1.2f) p.Set(x, y, new Color32(255, 255, 255, 255));
                else if (d < 3f) p.Set(x, y, new Color32(150, 225, 255, 230));
                else if (d < 6f && r < target) p.Set(x, y, new Color32(70, 140, 255, (byte)(140 - d * 18f)));
            }
            // sparks jumping inward off the ring
            for (int s = 0; s < 16; s++)
            {
                float a = s / 16f * Mathf.PI * 2f + Hash(s, 1, 561) * 0.3f;
                float r0 = 55f, r1 = 42f + Hash(s, 2, 562) * 8f;
                float mid = (r0 + r1) * 0.5f;
                var p0 = new Vector2(64f + Mathf.Cos(a) * r0, 64f + Mathf.Sin(a) * r0);
                var p1 = new Vector2(64f + Mathf.Cos(a + 0.06f) * mid, 64f + Mathf.Sin(a + 0.06f) * mid);
                var p2 = new Vector2(64f + Mathf.Cos(a - 0.03f) * r1, 64f + Mathf.Sin(a - 0.03f) * r1);
                p.Line(p0.x, p0.y, p1.x, p1.y, new Color32(200, 240, 255, 255));
                p.Line(p1.x, p1.y, p2.x, p2.y, new Color32(200, 240, 255, 255));
            }
            return p;
        }

        static Px[] BoltFrames()
        {
            var frames = new Px[3];
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(32, 160);
                var pts = new List<Vector2>();
                float x = 16f;
                for (int y = 0; y <= 160; y += 10)
                {
                    pts.Add(new Vector2(x, y));
                    x = Mathf.Clamp(16f + (Hash(y, f, 400) - 0.5f) * 18f, 4f, 28f);
                }
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    p.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, new Color32(60, 120, 255, 120), 7);
                }
                for (int i = 0; i < pts.Count - 1; i++)
                    p.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, new Color32(140, 220, 255, 230), 4);
                for (int i = 0; i < pts.Count - 1; i++)
                    p.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, new Color32(255, 255, 255, 255), 2);
                // a forked branch
                int b = 3 + f * 3;
                if (b < pts.Count - 1)
                {
                    var s = pts[b];
                    p.Line(s.x, s.y, s.x + (f % 2 == 0 ? 9 : -9), s.y + 14, new Color32(170, 230, 255, 220), 2);
                }
                frames[f] = p;
            }
            return frames;
        }

        static Px IconOverhead()
        {
            var p = new Px(24, 24);
            // the arc
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float dx = x + 0.5f - 9f, dy = y + 0.5f - 8f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < -50f || ang > 160f) continue;
                float along = (160f - ang) / 210f;
                float thick = 1f + 3.5f * Mathf.Sin(Mathf.PI * along);
                if (r <= 14f && r > 14f - thick) p.Set(x, y, r > 14f - thick * 0.45f ? Hex("#FFFFFF") : Hex("#8FD8FF"));
            }
            // sword pointing up-right at the end of the swing
            p.Line(6, 6, 15, 15, Hex("#E8EEF6"));
            p.Line(7, 6, 15, 14, Hex("#9AA6B6"));
            p.Line(3, 7, 7, 3, Hex("#F2C14E"));
            p.Line(2, 2, 4, 4, Hex("#6B4A24"), 2);
            p.Outline(Ink);
            return p;
        }

        static Px IconTornado()
        {
            var p = new Px(24, 24);
            for (int y = 2; y < 23; y++)
            {
                float t = (y - 2) / 20f;
                float half = 1.5f + t * 8.5f;
                float cx = 12f + Mathf.Sin(y * 0.5f) * 1.5f * t;
                for (int x = 0; x < 24; x++)
                {
                    float u = (x + 0.5f - cx) / half;
                    if (Mathf.Abs(u) > 1f) continue;
                    float band = Mathf.Repeat(u * 1.6f + y * 0.35f, 2f);
                    p.Set(x, y, band < 1f ? Hex("#E8F4F8") : Hex("#8FB6CC"));
                }
            }
            p.Line(15, 12, 18, 15, Hex("#FFE66B"));
            p.Line(18, 15, 16, 17, Hex("#FFE66B"));
            p.Outline(Ink);
            return p;
        }

        static Px IconOrbs()
        {
            var p = new Px(24, 24);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + Mathf.PI / 2f;
                float ox = 12f + Mathf.Cos(a) * 7f, oy = 12f + Mathf.Sin(a) * 7f;
                p.Ellipse(ox, oy, 2.8f, 2.8f, (nx, ny) => nx * nx + ny * ny < 0.25f ? Hex("#FFFFFF") : ny > 0.2f ? Hex("#E0A8FF") : Hex("#9D4EDD"));
            }
            p.Ellipse(12f, 12f, 1.6f, 1.6f, (nx, ny) => Hex("#C77DFF"));
            p.Outline(Hex("#1E0A33"));
            return p;
        }

        static Px IconThunderclap()
        {
            var p = new Px(24, 24);
            // ground ring
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float dx = (x + 0.5f - 12f) / 11f, dy = (y + 0.5f - 4f) / 3.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 0.75f && d < 1f) p.Set(x, y, Hex("#9FE8FF"));
            }
            // big bolt slamming down
            var bolt = Hex("#FFFFFF");
            var glow = Hex("#7FD3FF");
            int[][] pts = { new[] { 14, 23 }, new[] { 10, 16 }, new[] { 14, 14 }, new[] { 9, 5 } };
            for (int i = 0; i < pts.Length - 1; i++)
            {
                p.Line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], glow, 3);
                p.Line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], bolt, 1);
            }
            p.Outline(Ink);
            return p;
        }

        static Px IconThunder()
        {
            var p = new Px(24, 24);
            var bolt = Hex("#FFE66B");
            var boltLight = Hex("#FFFBE0");
            int[][] shape =
            {
                new[] { 14, 22, 5 }, new[] { 13, 20, 5 }, new[] { 12, 18, 5 }, new[] { 11, 16, 5 }, new[] { 10, 14, 5 },
                new[] { 9, 12, 9 }, new[] { 12, 10, 5 }, new[] { 11, 8, 5 }, new[] { 10, 6, 4 }, new[] { 9, 4, 3 }, new[] { 8, 2, 2 },
            };
            foreach (var s in shape)
            {
                p.Rect(s[0], s[1], s[2], 2, bolt);
                p.Set(s[0], s[1] + 1, boltLight);
            }
            p.Outline(Ink);
            return p;
        }

        static Px IconRiposte()
        {
            var p = new Px(24, 24);
            var shield = Hex("#7890B4");
            var rim = Hex("#D9E2F0");
            p.Ellipse(11f, 12f, 7.5f, 9f, (nx, ny) => nx * nx + ny * ny > 0.72f ? rim : ny > 0.2f && nx < 0f ? Hex("#9FB3D1") : shield);
            p.Rect(10, 6, 2, 12, Hex("#F2C14E"));
            p.Rect(7, 12, 8, 2, Hex("#F2C14E"));
            // spark of the parry
            var spark = Hex("#FFE39A");
            p.Line(18, 18, 23, 23, spark);
            p.Line(18, 22, 23, 17, spark);
            p.Set(20, 20, Hex("#FFFFFF"));
            p.Outline(Ink);
            return p;
        }

        // =====================================================================
        // Mockup 2: rough prototype art (flat colors, no texture, no shading, choppy animation)
        // =====================================================================

        /// <summary>Mockup 2 uses rough greybox art for the map and the bugs. Set to true for the finished art.</summary>
        public static readonly bool Polished = false;

        /// <summary>Flat block with a 1px grid line on its left and bottom edges (optionally a flat strip on top).</summary>
        static Px RoughBlock(Color32 fill, Color32 grid, Color32 strip = default, int stripDepth = 0)
        {
            var p = new Px(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, y >= 32 - stripDepth ? strip : x == 0 || y == 0 ? grid : fill);
            return p;
        }

        static Px RoughGround(bool top) => RoughBlock(Hex("#56616E"), Hex("#454E59"), Hex("#4E8A3C"), top ? 5 : 0);

        static Px RoughWall() => RoughBlock(Hex("#1F2833"), Hex("#19212A"));

        static Px RoughPlatform()
        {
            var p = new Px(32, 32);
            for (int y = 23; y < 32; y++)
            for (int x = 0; x < 32; x++)
                p.Set(x, y, y == 23 || x == 0 ? Hex("#5A4B3A") : Hex("#7A6A55"));
            return p;
        }

        /// <summary>Same frame size and layout as the finished beetle: walk 0-3 (only two poses), tell 4, lunge 5.</summary>
        static Px[] RoughBeetleFrames()
        {
            var frames = new Px[6];
            var shell = Hex("#B5452E");
            var head = Hex("#5A3426");
            var leg = Hex("#3A2418");
            for (int f = 0; f < 6; f++)
            {
                var p = new Px(46, 28);
                int lift = f == 4 ? 2 : 0;
                int lean = f == 5 ? 3 : 0;
                bool stepA = f % 2 == 0;
                foreach (int lx in new[] { 12, 19, 26 })
                    p.Line(lx + lean, 9 + lift, lx + lean + (stepA ? -2 : 2), 0, leg, 2);
                p.Ellipse(19f + lean, 10f + lift, 14f, 10f, (nx, ny) => ny < -0.1f ? Clear : shell);
                p.Rect(31 + lean, 6 + lift, 8, 7, head);
                p.Rect(36 + lean, 9 + lift, 2, 2, Hex("#FFE066"));
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>Same frame size as the finished wasp: three wing positions.</summary>
        static Px[] RoughWaspFrames()
        {
            var frames = new Px[3];
            var yellow = Hex("#F2C14E");
            var dark = Hex("#3A2A1A");
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(36, 30);
                p.Line(17, 9, 16, 4, dark, 2);
                p.Line(22, 9, 23, 4, dark, 2);
                p.Ellipse(14f, 13f, 9f, 6f, (nx, ny) => Mathf.FloorToInt((nx + 1f) * 2.5f) % 2 == 0 ? yellow : dark);
                p.Line(5, 12, 1, 11, dark, 2);
                p.Rect(22, 10, 8, 8, dark);
                p.Rect(26, 13, 2, 2, Hex("#E04040"));
                p.Rect(9, f == 0 ? 21 : f == 1 ? 19 : 17, 13, 4, Hex("#CFEEFD", 190));
                frames[f] = p;
            }
            return frames;
        }

        // =====================================================================
        // Effects
        // =====================================================================

        static Px Exclaim()
        {
            var p = new Px(8, 18);
            var red = Hex("#FF4545");
            var yellow = Hex("#FFD34E");
            p.Rect(2, 6, 4, 12, red);
            p.Rect(3, 7, 2, 10, yellow);
            p.Rect(2, 0, 4, 4, red);
            p.Rect(3, 1, 2, 2, yellow);
            p.Outline(Ink);
            return p;
        }

        static Px Slash()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x + 0.5f - 22f, dy = y + 0.5f - 32f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < -75f || ang > 75f) continue;
                float thick = 7f * (1f - Mathf.Abs(ang) / 75f);
                if (r < 28f - thick || r > 28f) continue;
                float edge = (28f - r) / Mathf.Max(1f, thick);
                byte a = (byte)(255 * Mathf.Clamp01(1.2f - Mathf.Abs(ang) / 75f));
                p.Set(x, y, edge < 0.35f ? new Color32(255, 255, 255, a) : new Color32(150, 220, 255, (byte)(a * 0.8f)));
            }
            return p;
        }

        static Px Ring()
        {
            var p = new Px(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
                if (r > 26f && r < 30f) p.Set(x, y, new Color32(255, 255, 255, (byte)(r < 28f ? 255 : 160)));
            }
            return p;
        }

        static Px SoftCircle(int size, float power)
        {
            var p = new Px(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), power);
                p.Set(x, y, new Color32(255, 255, 255, (byte)(a * 255)));
            }
            return p;
        }

        static Px SoftEllipse(int w, int h)
        {
            var p = new Px(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f - w / 2f) / (w / 2f), ny = (y + 0.5f - h / 2f) / (h / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                p.Set(x, y, new Color32(255, 255, 255, (byte)(a * a * 255)));
            }
            return p;
        }

        static Px Shaft()
        {
            var p = new Px(32, 128);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 32; x++)
            {
                float nx = Mathf.Abs(x + 0.5f - 16f) / 16f;
                float a = (1f - nx * nx) * Mathf.Clamp01(y / 128f * 1.4f);
                p.Set(x, y, new Color32(255, 255, 255, (byte)(a * 255)));
            }
            return p;
        }

        static Px Vignette()
        {
            var p = new Px(256, 256);
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                float nx = (x + 0.5f - 128f) / 128f, ny = (y + 0.5f - 128f) / 128f;
                float r = Mathf.Sqrt(nx * nx * 0.8f + ny * ny);
                float a = Mathf.Clamp01((r - 0.55f) / 0.6f);
                p.Set(x, y, new Color32(4, 8, 12, (byte)(a * a * 230)));
            }
            return p;
        }

        // =====================================================================
        // Import
        // =====================================================================

        public static void GenerateAll(string dir)
        {
            Directory.CreateDirectory(dir);

            // Mockup 2: only what the basic level needs (greybox tiles, the door, the two bugs, two effects).

            // tiles
            for (int i = 0; i < 3; i++) Single(dir, $"tile_ground_fill_{i}", Polished ? GroundFill(i) : RoughGround(false), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 2; i++) Single(dir, $"tile_ground_top_{i}", Polished ? GroundTop(i + 3) : RoughGround(true), new Vector2(0.5f, 0.5f));
            Single(dir, "tile_ground_under", Polished ? GroundUnder(7) : RoughGround(false), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 3; i++) Single(dir, $"tile_platform_{i}", Polished ? Platform(i) : RoughPlatform(), new Vector2(0.5f, 0.5f));

            // the exit door
            Single(dir, "door_frame", DoorFrame(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "door_bars", Portcullis(), new Vector2(0.5f, 0f), PPU);
            Sheet(dir, "door_portal", PortalFrames(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "door_arrow", ArrowDown(), new Vector2(0.5f, 0.5f), PPU);

            // bugs
            Sheet(dir, "bug_beetle", Polished ? BeetleFrames(1f, 46, 28, Hex("#B5452E"), Hex("#E07A4F"), Hex("#6E2418"), Hex("#3B2418"), Hex("#FFE066"), false, false) : RoughBeetleFrames(), new Vector2(0.41f, 0f), PPU);
            Sheet(dir, "bug_wasp", Polished ? WaspFrames() : RoughWaspFrames(), new Vector2(0.5f, 0.5f), PPU);

            // effects (jump dust rings, the door's glow)
            Single(dir, "fx_ring", Ring(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_glow", SoftCircle(64, 1.8f), new Vector2(0.5f, 0.5f), 64f, FilterMode.Bilinear);

            AssetDatabase.Refresh();
        }

        static void WritePng(string path, Px px)
        {
            var tex = new Texture2D(px.W, px.H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px.C);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static TextureImporter BaseImporter(string path, float ppu, FilterMode filter)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = filter;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = ppu;
            return importer;
        }

        static void Single(string dir, string name, Px px, Vector2 pivot, float ppu = PPU, FilterMode filter = FilterMode.Point, bool fullRect = false)
        {
            var path = $"{dir}/{name}.png";
            WritePng(path, px);
            var importer = BaseImporter(path, ppu, filter);
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void Sheet(string dir, string name, Px[] frames, Vector2 pivot, float ppu)
        {
            int w = frames[0].W, h = frames[0].H;
            var sheet = new Px(w * frames.Length, h);
            for (int i = 0; i < frames.Length; i++) sheet.Paste(frames[i], i * w, 0);
            var path = $"{dir}/{name}.png";
            WritePng(path, sheet);
            var pivots = Enumerable.Repeat(pivot, frames.Length).ToArray();
            Slice(path, name, w, h, frames.Length, pivots, ppu, FilterMode.Point, false);
        }

        /// <summary>Cuts a horizontal strip into frames with the given pivots (uses the 2D Sprite data provider).</summary>
        public static void Slice(string path, string baseName, int frameW, int frameH, int count, Vector2[] pivots, float ppu, FilterMode filter, bool readable)
        {
            var importer = BaseImporter(path, ppu, filter);
            importer.isReadable = readable;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    rect = new Rect(i * frameW, 0, frameW, frameH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivots[i],
                    spriteID = GUID.Generate(),
                };
            }
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>All sprites in a sliced sheet, in frame order.</summary>
        public static Sprite[] Frames(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out var n) ? n : 0)
                .ToArray();
        }

        public static Sprite Load(string dir, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

        public static Sprite[] Pick(Sprite[] frames, params int[] indices) => indices.Select(i => frames[Mathf.Clamp(i, 0, frames.Length - 1)]).ToArray();
    }
}
