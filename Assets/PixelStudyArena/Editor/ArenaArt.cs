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

        /// <summary>Sky Ramparts: a sunset. Purple overhead, orange at the horizon, the sun sinking behind castles.</summary>
        static Px SkyDusk()
        {
            const int w = 480, h = 270;
            var p = new Px(w, h);
            var top = Hex("#160C2A");
            var mid = Hex("#6A2552");
            var low = Hex("#E0743A");
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;
                int band = Mathf.FloorToInt(t * 16f);
                float k = band / 16f;
                var c = k < 0.45f ? Mix(low, mid, k / 0.45f) : Mix(mid, top, (k - 0.45f) / 0.55f);
                for (int x = 0; x < w; x++) p.Set(x, y, c);
            }
            for (int i = 0; i < 70; i++)
                p.Blend((int)(Hash(i, 0, 31) * w), 170 + (int)(Hash(i, 1, 31) * 100), new Color32(255, 230, 240, (byte)(60 + Hash(i, 2, 31) * 120)));
            // the setting sun and its glow
            int sx = 150, sy = 92;
            for (int r = 90; r > 34; r -= 8) p.Ellipse(sx, sy, r, r, (nx, ny) => new Color32(255, 170, 90, 14), true);
            p.Ellipse(sx, sy, 34, 34, (nx, ny) => ny > 0.3f ? Hex("#FFE3A0") : ny > -0.2f ? Hex("#FFC46B") : Hex("#FF9C4A"));
            // streaks of cloud across it
            for (int y = 60; y < 200; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.012f, y * 0.09f);
                if (n > 0.62f) p.Blend(x, y, new Color32(255, 150, 160, (byte)((n - 0.62f) * 300f)));
            }
            // castle silhouettes
            var far = Hex("#2A1230");
            var lit = Hex("#FFD27A");
            for (int x = 0; x < w; x++)
            {
                int hgt = 64 + (int)(Mathf.PerlinNoise(x * 0.02f, 7.7f) * 26f);
                for (int y = 0; y < hgt; y++) p.Set(x, y, far);
            }
            for (int i = 0; i < 8; i++)
            {
                int tx = 30 + i * 58 + (int)(Hash(i, 0, 32) * 18f);
                int tw = 10 + (int)(Hash(i, 1, 32) * 12f);
                int th = 100 + (int)(Hash(i, 2, 32) * 60f);
                p.Rect(tx, 0, tw, th, far);
                for (int k = 0; k < tw; k += 3) p.Rect(tx + k, th, 2, 4, far); // crenellations
                for (int k = 0; k < 3; k++)
                    if (Hash(i, k + 3, 32) < 0.7f) p.Rect(tx + 3 + (int)(Hash(i, k, 33) * (tw - 6)), th - 18 - k * 20, 2, 3, lit);
            }
            return p;
        }

        /// <summary>Moonlit Shrine: a deep blue night with a huge moon over layered mountains.</summary>
        static Px SkyMoon()
        {
            const int w = 480, h = 270;
            var p = new Px(w, h);
            var top = Hex("#03050D");
            var low = Hex("#1A2650");
            for (int y = 0; y < h; y++)
            {
                int band = Mathf.FloorToInt(Mathf.Pow(y / (float)h, 0.8f) * 14f);
                var c = Mix(low, top, band / 14f);
                for (int x = 0; x < w; x++) p.Set(x, y, c);
            }
            for (int i = 0; i < 220; i++)
                p.Blend((int)(Hash(i, 0, 41) * w), 90 + (int)(Hash(i, 1, 41) * 180), new Color32(220, 230, 255, (byte)(80 + Hash(i, 2, 41) * 175)));
            // the moon
            int mx = 300, my = 175;
            for (int r = 100; r > 48; r -= 6) p.Ellipse(mx, my, r, r, (nx, ny) => new Color32(150, 170, 230, 10), true);
            p.Ellipse(mx, my, 48, 48, (nx, ny) =>
            {
                var c = Hex("#EEF1FA");
                if (-nx * 0.5f + ny * 0.6f < -0.45f) c = Hex("#C9D0E6");
                return c;
            });
            foreach (var (cx, cy, r) in new[] { (-14, 10, 7), (12, -8, 9), (18, 16, 4), (-6, -20, 5), (-24, -4, 4) })
                p.Ellipse(mx + cx, my + cy, r, r * 0.85f, (a, b) => Hex("#D3D9EC"));
            // layered mountains with a pagoda on the near ridge
            var farHill = Hex("#0F1834");
            var nearHill = Hex("#080D20");
            for (int x = 0; x < w; x++)
            {
                int h1 = 70 + (int)(Mathf.PerlinNoise(x * 0.01f, 2.1f) * 70f);
                for (int y = 0; y < h1; y++) p.Set(x, y, farHill);
                int h2 = 40 + (int)(Mathf.PerlinNoise(x * 0.018f, 5.3f) * 40f);
                for (int y = 0; y < h2; y++) p.Set(x, y, nearHill);
            }
            int px = 110;
            int baseY = 40 + (int)(Mathf.PerlinNoise(px * 0.018f, 5.3f) * 40f);
            for (int tier = 0; tier < 4; tier++)
            {
                int ty = baseY + tier * 12, half = 16 - tier * 3;
                p.Rect(px - half / 2, ty, half, 9, nearHill);
                for (int k = -half; k <= half; k++) p.Set(px + k, ty + 9 + (Mathf.Abs(k) > half - 3 ? 1 : 0), nearHill);
                p.Rect(px - 1, ty + 3, 2, 2, Hex("#FFB86B"));
            }
            p.Rect(px, baseY + 48, 1, 8, nearHill);
            // mist in front of the hills
            for (int y = 20; y < 90; y++)
            for (int x = 0; x < w; x++)
            {
                float m = Mathf.PerlinNoise(x * 0.014f, y * 0.06f) * (1f - Mathf.Abs(y - 55) / 35f);
                if (m > 0.25f) p.Blend(x, y, new Color32(110, 130, 200, (byte)(m * 60)));
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

        // ---------- map props ----------

        /// <summary>Sunken Crypt: a stone sarcophagus (pivot bottom middle).</summary>
        static Px Tomb()
        {
            var p = new Px(48, 22);
            var stone = Hex("#5B6A6E");
            var light = Hex("#7C8E90");
            var dark = Hex("#3B4648");
            p.Rect(2, 0, 44, 14, stone);
            p.Rect(2, 12, 44, 2, light);
            p.Rect(0, 14, 48, 6, light);   // lid
            p.Rect(0, 14, 48, 1, dark);
            p.Rect(1, 19, 46, 2, Hex("#9FB1B2"));
            for (int x = 4; x < 44; x += 8) p.Rect(x, 2, 4, 8, dark); // carved panels
            // a carved cross on the lid end
            p.Rect(22, 15, 4, 4, dark);
            p.Rect(23, 15, 2, 6, dark);
            // moss
            for (int x = 0; x < 48; x++) if (Hash(x, 0, 50) < 0.35f) p.Set(x, 21, Hex("#3F6934"));
            p.Outline(Ink);
            return p;
        }

        /// <summary>A cobweb in a corner (anchored at the top-left; mirror it for the right side).</summary>
        static Px Web()
        {
            var p = new Px(40, 40);
            var thread = new Color32(220, 225, 235, 150);
            var faint = new Color32(220, 225, 235, 80);
            for (int i = 0; i <= 6; i++)
            {
                float a = -90f + i * 15f;
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                p.Line(0, 39, d.x * 40f, 39 + d.y * 40f, thread);
            }
            for (int ring = 1; ring <= 5; ring++)
            {
                float r = ring * 7.5f;
                for (int i = 0; i < 6; i++)
                {
                    float a0 = (-90f + i * 15f) * Mathf.Deg2Rad, a1 = (-90f + (i + 1) * 15f) * Mathf.Deg2Rad;
                    float sag = 1.5f;
                    var m = new Vector2(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f)) * (r - sag);
                    p.Line(Mathf.Cos(a0) * r, 39 + Mathf.Sin(a0) * r, m.x, 39 + m.y, faint);
                    p.Line(m.x, 39 + m.y, Mathf.Cos(a1) * r, 39 + Mathf.Sin(a1) * r, faint);
                }
            }
            return p;
        }

        /// <summary>The Brood Mother's eggs: a slimy clutch with glowing yolks (pivot bottom middle).</summary>
        static Px EggCluster()
        {
            var p = new Px(44, 26);
            // slime base
            p.Ellipse(22f, 2f, 21f, 3f, (nx, ny) => Hex("#3D6B2E"));
            var eggs = new[] { (9f, 9f, 6f, 8f), (21f, 11f, 7f, 10f), (33f, 9f, 6f, 8f), (15f, 6f, 5f, 6f), (28f, 6f, 5f, 6f) };
            foreach (var (cx, cy, rx, ry) in eggs)
                p.Ellipse(cx, cy, rx, ry, (nx, ny) =>
                {
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (d < 0.35f) return Hex("#E9FF9A");                          // glowing yolk
                    if (-nx * 0.5f + ny * 0.7f > 0.55f) return Hex("#D7F5C0");     // shine
                    if (d < 0.75f) return Hex("#9BD46A", 235);
                    return Hex("#5F9A45", 235);
                });
            p.Outline(Hex("#1A2A12"));
            return p;
        }

        /// <summary>Hive wall: a patch of honeycomb, most cells full of honey.</summary>
        static Px Honeycomb()
        {
            var p = new Px(72, 60);
            const float r = 6f;
            float hw = r * Mathf.Sqrt(3f);
            for (int row = -1; row < 8; row++)
            for (int col = -1; col < 7; col++)
            {
                float cx = col * hw + (row % 2 == 0 ? 0f : hw * 0.5f) + 4f;
                float cy = row * r * 1.5f + 4f;
                bool full = Hash(row, col, 60) < 0.7f;
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - hw); x <= cx + hw; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx), dy = Mathf.Abs(y + 0.5f - cy);
                    // hexagon (pointy top) distance
                    float hex = Mathf.Max(dx * 0.866f + dy * 0.5f, dy);
                    if (hex > r) continue;
                    // a rounded, ragged patch: cells crumble away toward its edge
                    float ex = (x + 0.5f - 36f) / 36f, ey = (y + 0.5f - 30f) / 30f;
                    float edge = (1f - Mathf.Sqrt(ex * ex + ey * ey)) * 3.2f + (Hash(row, col, 62) - 0.5f) * 0.8f;
                    if (edge < 0.15f || Hash(x, y, 61) > edge) continue;
                    Color32 c;
                    if (hex > r - 1.4f) c = Hex("#C9862A");                              // wax walls
                    else if (full) c = hex < r * 0.45f && dx < dy ? Hex("#FFD36B") : Hex("#E9A23B"); // honey
                    else c = Hex("#4A2C10");                                               // empty cell
                    p.Set(x, y, c);
                }
            }
            return p;
        }

        /// <summary>A papery hive hanging from the ceiling (pivot top middle).</summary>
        static Px HiveChunk()
        {
            var p = new Px(36, 46);
            p.Rect(16, 40, 4, 6, Hex("#5A3A1C"));
            p.Ellipse(18f, 22f, 16f, 19f, (nx, ny) =>
            {
                int band = Mathf.FloorToInt((ny + 1f) * 6f);
                var c = band % 2 == 0 ? Hex("#D9A64B") : Hex("#B9842E");
                if (nx > 0.55f) c = Shade(c, 0.75f);
                if (-nx * 0.6f + ny * 0.4f > 0.6f) c = Hex("#F2CB76");
                return c;
            });
            p.Ellipse(18f, 10f, 4f, 3f, (nx, ny) => Hex("#2A1606")); // the entrance
            p.Outline(Ink);
            return p;
        }

        /// <summary>Moonlit Shrine: a red torii gate (pivot bottom middle).</summary>
        static Px ShrineGate()
        {
            var p = new Px(176, 128);
            var red = Hex("#B3261E");
            var redLight = Hex("#D9432F");
            var redDark = Hex("#7A1712");
            var black = Hex("#1A1414");
            // pillars
            foreach (int x in new[] { 30, 134 })
            {
                p.Rect(x, 8, 12, 96, red);
                p.Rect(x, 8, 3, 96, redLight);
                p.Rect(x + 9, 8, 3, 96, redDark);
                p.Rect(x - 2, 0, 16, 10, black); // stone feet
            }
            // the lower beam (nuki)
            p.Rect(20, 84, 136, 8, red);
            p.Rect(20, 84, 136, 2, redDark);
            // the plaque between the beams
            p.Rect(80, 92, 16, 16, black);
            p.Rect(83, 95, 10, 10, Hex("#C9A24B"));
            // the curved top beams (kasagi + shimaki)
            for (int x = 0; x < 176; x++)
            {
                float nx = (x - 88f) / 88f;
                int lift = (int)(nx * nx * 10f);
                p.Rect(x, 104 + lift, 1, 8, red);
                p.Rect(x, 112 + lift, 1, 8, black);
                p.Set(x, 104 + lift, redDark);
            }
            p.Outline(Ink);
            return p;
        }

        /// <summary>A paper lantern (pivot at the hook on top).</summary>
        static Px Lantern()
        {
            var p = new Px(14, 20);
            p.Rect(6, 17, 2, 3, Hex("#2A2020"));
            p.Rect(4, 15, 6, 2, Hex("#2A2020"));
            p.Ellipse(7f, 9f, 6f, 6.5f, (nx, ny) =>
            {
                int rib = Mathf.FloorToInt((ny + 1f) * 4f);
                var c = Mathf.Abs(nx) < 0.5f ? Hex("#FFE6B0") : Hex("#F2A65A");
                if (rib % 2 == 1 && Mathf.Abs(ny) < 0.85f) c = Shade(c, 0.9f);
                return c;
            });
            p.Rect(4, 1, 6, 2, Hex("#2A2020"));
            p.Rect(5, 6, 4, 6, Hex("#C0392B")); // a painted mark
            p.Outline(Ink);
            return p;
        }

        /// <summary>A rock spike hanging from a cave ceiling (pivot top middle).</summary>
        static Px Stalactite()
        {
            var p = new Px(16, 34);
            for (int y = 0; y < 34; y++)
            {
                float t = 1f - y / 33f; // 1 at the tip (bottom) .. 0 at the root
                float half = 7f * (1f - t * t * 0.95f);
                float sway = Mathf.Sin(y * 0.3f) * 0.8f;
                for (int x = 0; x < 16; x++)
                {
                    float dx = x + 0.5f - 8f - sway;
                    if (Mathf.Abs(dx) > half) continue;
                    p.Set(x, y, dx < -half * 0.3f ? Hex("#5A4E66") : dx > half * 0.4f ? Hex("#2E2638") : Hex("#463C52"));
                }
            }
            p.Outline(Ink);
            return p;
        }

        /// <summary>A falling rock (the Brood Mother's cave-ins).</summary>
        static Px Rock()
        {
            var p = new Px(20, 22);
            var pts = new[] { new Vector2(10, 21), new Vector2(18, 15), new Vector2(17, 5), new Vector2(11, 1), new Vector2(3, 4), new Vector2(2, 14) };
            for (int y = 0; y < 22; y++)
            for (int x = 0; x < 20; x++)
            {
                // inside the polygon?
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
                if (!inside) continue;
                float lightK = (x - y * 0.6f) / 20f;
                p.Set(x, y, lightK < -0.3f ? Hex("#7A6C8A") : lightK > 0.25f ? Hex("#3A3046") : Hex("#5A4E66"));
            }
            p.Line(6, 12, 11, 9, Hex("#2A2232"));
            p.Line(11, 9, 13, 14, Hex("#2A2232"));
            p.Outline(Ink);
            return p;
        }

        /// <summary>Glowing mushrooms for the Nest floor (pivot bottom middle).</summary>
        static Px Mushrooms()
        {
            var p = new Px(26, 18);
            var specs = new[] { (6f, 7f, 5f, 3.2f), (14f, 11f, 6f, 3.8f), (21f, 6f, 4f, 2.6f) };
            foreach (var (x, h, r, rh) in specs)
            {
                p.Rect((int)x - 1, 0, 2, (int)h, Hex("#C9D6E0"));
                p.Ellipse(x, h, r, rh, (nx, ny) => ny > 0.2f ? Hex("#B58CFF") : nx < -0.3f ? Hex("#E0CCFF") : Hex("#8E5CE0"));
                p.Set((int)x - 2, (int)h + 1, Hex("#F4EAFF"));
                p.Set((int)x + 1, (int)h, Hex("#F4EAFF"));
            }
            p.Outline(Ink);
            return p;
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

        // =====================================================================
        // Normal-floor bugs added for floors 5-49
        // =====================================================================

        /// <summary>Pill Bug (40x28): walk 0-1, curl 2, ball 3, dazed on its back 4.</summary>
        static Px[] PillBugFrames()
        {
            var shell = Hex("#7D8796");
            var light = Hex("#A9B3C2");
            var dark = Hex("#4E5663");
            var leg = Hex("#2A2E36");
            var frames = new List<Px>();
            for (int f = 0; f < 5; f++)
            {
                var p = new Px(40, 28);
                if (f <= 1)
                {
                    // walking: a long segmented dome on lots of little legs
                    for (int i = 0; i < 6; i++)
                    {
                        float x = 9 + i * 4.2f, off = ((i + f) % 2 == 0 ? -1f : 1f);
                        p.Line(x, 4, x + off, 0, leg);
                    }
                    p.Ellipse(19f, 6f, 15f, 10f, (nx, ny) =>
                    {
                        if (ny < -0.2f) return Clear;
                        int seg = Mathf.FloorToInt((nx + 1f) * 4.5f);
                        var c = ny > 0.55f && nx < 0.3f ? light : seg % 2 == 0 ? shell : Shade(shell, 0.85f);
                        if (Mathf.Repeat((nx + 1f) * 4.5f, 1f) < 0.12f) c = dark;
                        return c;
                    });
                    p.Ellipse(34f, 5f, 3.5f, 3f, (a, b) => dark); // head
                    p.Line(36, 7, 39, 10, leg);
                    p.Set(35, 6, Hex("#E8E0C8"));
                }
                else if (f == 2 || f == 3)
                {
                    // curling up (2) / a tight armored ball (3)
                    float r = f == 2 ? 9.5f : 10.5f;
                    p.Ellipse(19f, r + 0.5f, f == 2 ? 12f : r, r, (nx, ny) =>
                    {
                        float a = Mathf.Atan2(ny, nx);
                        var c = -nx * 0.5f + ny * 0.7f > 0.5f ? light : -nx * 0.5f + ny * 0.7f < -0.5f ? dark : shell;
                        if (Mathf.Repeat((a + Mathf.PI) / (Mathf.PI * 2f) * 9f, 1f) < 0.12f && nx * nx + ny * ny > 0.2f) c = dark;
                        return c;
                    });
                    if (f == 2) for (int i = 0; i < 4; i++) p.Line(13 + i * 4, 2, 14 + i * 4, 0, leg);
                }
                else
                {
                    // dazed on its back, legs waving
                    p.Ellipse(19f, 7f, 15f, 8f, (nx, ny) => ny > 0.2f ? Clear : ny < -0.6f ? dark : Hex("#C9B79A"));
                    for (int i = 0; i < 6; i++) p.Line(9 + i * 4.2f, 7, 8 + i * 4.2f + (i % 2) * 2, 13, leg);
                    p.Ellipse(19f, 4f, 15f, 5f, (nx, ny) => ny > 0f ? Clear : shell);
                }
                p.Outline(Ink);
                frames.Add(p);
            }
            return frames.ToArray();
        }

        /// <summary>Termite Guard (44x40) behind a big chitin shield: walk 0-1, tell 2 (shield drawn back), bash 3, broken 4.</summary>
        static Px[] TermiteFrames()
        {
            var body = Hex("#E8DCC0");
            var bodyDark = Hex("#B8A888");
            var head = Hex("#C2552A");
            var headLight = Hex("#E07A44");
            var mand = Hex("#3A1A0E");
            var shield = Hex("#6B4A2A");
            var shieldLight = Hex("#9A6E3E");
            var rim = Hex("#C9A15A");
            var leg = Hex("#8A7A5E");
            var frames = new List<Px>();
            for (int f = 0; f < 5; f++)
            {
                var p = new Px(44, 40);
                float shieldX = f == 2 ? 29f : f == 3 ? 35f : 32f;
                float shieldTilt = f == 4 ? -35f : f == 2 ? 8f : 0f;
                float shieldY = f == 4 ? 7f : 14f;
                // legs
                for (int i = 0; i < 3; i++)
                {
                    float hx = 12 + i * 6, off = ((i + f) % 2 == 0 ? -1.5f : 1.5f);
                    p.Line(hx, 8, hx + 2, 4, leg, 2);
                    p.Line(hx + 2, 4, hx + off, 0, leg, 2);
                }
                // segmented abdomen and thorax
                p.Ellipse(11f, 11f, 9f, 6.5f, (nx, ny) => Mathf.Repeat((nx + 1f) * 3f, 1f) < 0.18f ? bodyDark : ny > 0.4f ? Hex("#FFF4DC") : body);
                p.Ellipse(21f, 11f, 5f, 4.5f, (nx, ny) => ny > 0.4f ? Hex("#FFF4DC") : body);
                // big armored head with mandibles
                float hy = f == 4 ? 10f : 14f;
                p.Ellipse(27f, hy, 6f, 5.5f, (nx, ny) => ny > 0.35f && nx < 0.3f ? headLight : head);
                p.Line(31, hy - 2, 36, hy - 4, mand, 2);
                p.Line(31, hy - 3, 35, hy - 6, mand, 2);
                p.Set(29, (int)hy + 1, Hex("#1A0E08"));
                // the shield: a tall plate carried in front
                RotEllipse(p, shieldX, shieldY + 5f, 5f, 12f, shieldTilt, (u, v) =>
                {
                    float r = u * u + v * v;
                    if (r > 0.75f) return rim;
                    return u < -0.3f ? shieldLight : shield;
                });
                RotEllipse(p, shieldX, shieldY + 5f, 1.4f, 9f, shieldTilt, (u, v) => Shade(shield, 0.75f));
                p.Outline(Ink);
                frames.Add(p);
            }
            return frames.ToArray();
        }

        /// <summary>Scorpion (72x48): walk 0-1, grab tell 2 (claws high and open), grab 3, tail raised 4, tail stab 5.</summary>
        static Px[] ScorpionFrames()
        {
            var body = Hex("#8A4022");
            var light = Hex("#C0622E");
            var dark = Hex("#4E2010");
            var claw = Hex("#9E4A26");
            var sting = Hex("#F0D86A");
            var frames = new List<Px>();
            for (int f = 0; f < 6; f++)
            {
                var p = new Px(72, 48);
                // legs (4 pairs, low)
                for (int i = 0; i < 4; i++)
                {
                    float hx = 22 + i * 5, off = ((i + f) % 2 == 0 ? -2f : 2f);
                    p.Line(hx, 7, hx - 3, 4, dark, 2);
                    p.Line(hx - 3, 4, hx - 4 + off, 0, dark, 2);
                }
                // tail: a curl of segments from the back of the body, stinger at the end
                Vector2[] tail = f == 4
                    ? new[] { new Vector2(16, 9), new Vector2(9, 15), new Vector2(8, 25), new Vector2(15, 34), new Vector2(27, 38), new Vector2(38, 35), new Vector2(44, 30) }
                    : f == 5
                    ? new[] { new Vector2(16, 9), new Vector2(11, 17), new Vector2(15, 27), new Vector2(28, 33), new Vector2(42, 29), new Vector2(54, 19), new Vector2(60, 8) }
                    : new[] { new Vector2(16, 9), new Vector2(9, 14), new Vector2(7, 22), new Vector2(11, 29), new Vector2(19, 31), new Vector2(25, 27), new Vector2(26, 22) };
                for (int i = 0; i < tail.Length - 1; i++)
                    Capsule(p, tail[i], tail[i + 1], 4f - i * 0.4f, 3.6f - i * 0.4f, i % 2 == 0 ? body : light);
                var tip = tail[tail.Length - 1];
                var dir = (tail[tail.Length - 1] - tail[tail.Length - 2]).normalized;
                Capsule(p, tip, tip + dir * 5f, 2.2f, 0.6f, sting);
                // body
                p.Ellipse(27f, 10f, 13f, 6f, (nx, ny) =>
                {
                    var c = ny > 0.4f ? light : ny < -0.4f ? dark : body;
                    if (Mathf.Repeat((nx + 1f) * 3.5f, 1f) < 0.15f) c = dark;
                    return c;
                });
                p.Ellipse(41f, 10f, 5f, 4.5f, (nx, ny) => ny > 0.3f ? light : body); // head
                p.Set(43, 12, Hex("#1A0A04"));
                p.Set(44, 12, Hex("#1A0A04"));
                // claws: arm to a big pincer, open or shut
                bool open = f == 2;
                float armY = f == 2 ? 20f : 11f, reach = f == 3 ? 66f : f == 2 ? 58f : 60f;
                foreach (var (dy, col) in new[] { (2f, dark), (0f, claw) })
                {
                    var shoulder = new Vector2(44f, 10f + dy);
                    var wrist = new Vector2(reach - 8f, armY + dy);
                    Capsule(p, shoulder, new Vector2(50f, (armY + 10f) * 0.5f + dy + 2f), 2.2f, 2f, col);
                    Capsule(p, new Vector2(50f, (armY + 10f) * 0.5f + dy + 2f), wrist, 2f, 2.4f, col);
                    p.Ellipse(wrist.x + 2f, wrist.y, 4.5f, 3.2f, (a, b) => b > 0.3f ? light : col);
                    float gap = open ? 3.5f : 0.8f;
                    Capsule(p, wrist + new Vector2(5f, 1f), wrist + new Vector2(10f, 1f + gap), 1.6f, 0.6f, col);
                    Capsule(p, wrist + new Vector2(5f, -1f), wrist + new Vector2(10f, -1f - gap * 0.6f), 1.4f, 0.5f, col);
                }
                p.Outline(Ink);
                frames.Add(p);
            }
            return frames.ToArray();
        }

        /// <summary>Firefly Bomber (36x30): three wing positions; its lantern glows (the glow itself is a separate sprite).</summary>
        static Px[] FireflyFrames()
        {
            var frames = new Px[3];
            var black = Hex("#241E1A");
            var shell = Hex("#4A2E22");
            var lamp = Hex("#D8FF6A");
            var lampHot = Hex("#F6FFD0");
            var wing = Hex("#E8F4D8", 160);
            var wingEdge = Hex("#B8D49A", 220);
            for (int f = 0; f < 3; f++)
            {
                var p = new Px(36, 30);
                p.Line(18, 10, 16, 5, black);
                p.Line(22, 10, 23, 5, black);
                // glowing lantern abdomen
                p.Ellipse(10f, 12f, 8f, 6f, (nx, ny) => nx * nx + ny * ny < 0.25f ? lampHot : lamp);
                // thorax + head
                p.Ellipse(20f, 14f, 5f, 4.5f, (nx, ny) => ny > 0.4f ? Shade(shell, 1.4f) : shell);
                p.Ellipse(26f, 15f, 3.5f, 3.5f, (nx, ny) => black);
                p.Set(27, 16, Hex("#FF8A3A"));
                p.Line(26, 18, 30, 24, black);
                p.Line(27, 18, 33, 22, black);
                p.Outline(Ink);
                float wy = f == 0 ? 23f : f == 1 ? 20f : 17f;
                p.Ellipse(17f, wy, 8f, f == 1 ? 2.5f : 3.5f, (nx, ny) => nx * nx + ny * ny > 0.7f ? wingEdge : wing, true);
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>Stag Beetle elite (104x64): walk 0-3, tell 4 (head up, jaws wide), charge 5 (head down), lift 6 (jaws raised).</summary>
        static Px DrawStag(int legPhase, int pose)
        {
            var p = new Px(104, 64);
            var shell = Hex("#5A2A1A");
            var shellLight = Hex("#8E4A2C");
            var shellDark = Hex("#2E140C");
            var head = Hex("#3A1C10");
            var jaw = Hex("#B87A3A");
            var jawLight = Hex("#E8B068");
            var leg = Hex("#1E0E08");
            float lift = pose == 4 ? 4f : pose == 6 ? 3f : 0f;
            float lean = pose == 5 ? 4f : 0f;
            // legs
            for (int i = 0; i < 3; i++)
            {
                float hx = 24 + i * 16 + lean;
                int ph = (legPhase + i) % 4;
                float off = ph == 0 ? -4f : ph == 2 ? 4f : 0f, footLift = ph == 3 ? 3f : 0f;
                p.Line(hx, 16 + lift * 0.5f, hx + 6, 9 + footLift, leg, 3);
                p.Line(hx + 6, 9 + footLift, hx + off + 2, footLift, leg, 3);
            }
            // shell
            p.Ellipse(40f + lean, 24f + lift, 28f, 17f, (nx, ny) =>
            {
                if (ny < -0.45f) return Clear;
                float l = -nx * 0.45f + ny * 0.8f;
                var c = l > 0.62f ? shellLight : l < -0.25f ? shellDark : shell;
                if (ny < -0.25f) c = shellDark;
                return c;
            });
            for (float x = 14 + lean; x < 66 + lean; x++) p.Set((int)x, (int)(20 + lift), shellDark);
            // head and the huge antler-jaws
            float hx2 = 70f + lean * 1.5f, hy2 = 19f + lift * 1.4f + (pose == 5 ? -3f : 0f);
            p.Ellipse(hx2, hy2, 9f, 8f, (nx, ny) => ny > 0.4f && nx < 0.2f ? Shade(head, 1.5f) : head);
            p.Rect((int)hx2 + 3, (int)hy2 + 2, 3, 3, Hex("#FFB040"));
            float angle = pose == 4 ? 34f : pose == 5 ? -12f : pose == 6 ? 62f : 14f;
            float spread = pose == 4 ? 16f : pose == 6 ? 4f : 8f;
            foreach (var (deg, col) in new[] { (angle + spread, Shade(jaw, 0.75f)), (angle - spread * 0.4f, jaw) })
            {
                var root = new Vector2(hx2 + 6f, hy2 + 2f);
                var mid = root + Dir(deg) * 16f;
                var tip = mid + Dir(deg - 40f) * 12f;
                Capsule(p, root, mid, 3.2f, 2.6f, col);
                Capsule(p, mid, tip, 2.4f, 0.8f, col);
                Capsule(p, mid, mid + Dir(deg + 50f) * 5f, 1.6f, 0.6f, col); // the antler tine
                p.Line(root.x, root.y + 1, mid.x, mid.y + 1, jawLight);
            }
            p.Outline(Ink);
            return p;
        }

        static Px[] StagFrames()
        {
            var frames = new List<Px>();
            for (int i = 0; i < 4; i++) frames.Add(DrawStag(i, 0));
            frames.Add(DrawStag(0, 4));
            frames.Add(DrawStag(2, 5));
            frames.Add(DrawStag(0, 6));
            return frames.ToArray();
        }

        // =====================================================================
        // Floor 40: the Silk Widow (and her spiderlings)
        // =====================================================================

        /// <summary>
        /// A spider from the side, facing right (base canvas 44x26 scaled by s, feet at y = 0).
        /// pose 0 walk, 1 rear up (tell), 2 bite, 3 spit (abdomen tipped forward), 4 splayed flat.
        /// </summary>
        static Px DrawSpider(float s, int legPhase, int pose, Color32 body, Color32 light, Color32 mark, Color32 eye, bool hourglass)
        {
            var p = new Px(Mathf.RoundToInt(44 * s), Mathf.RoundToInt(28 * s));
            var legFar = Shade(body, 0.7f);
            int w = Mathf.Max(1, Mathf.RoundToInt(s * 1.1f));
            float rear = pose == 1 ? 3f : 0f, lunge = pose == 2 ? 3f : 0f, flat = pose == 4 ? -3f : 0f;
            Vector2 S(float x, float y) => new Vector2(x * s, y * s);

            float[] attach = { 22f, 24f, 26f, 28f };
            void Legs(Color32 col, float shift, bool far)
            {
                for (int i = 0; i < 4; i++)
                {
                    float ax = attach[i] + lunge + shift, ay = 10f + rear * (i >= 2 ? 1f : 0.5f) + flat;
                    float side = i < 2 ? -1f : 1f;
                    float reach = i == 0 || i == 3 ? 13f : 7f;
                    int ph = (legPhase + i + (far ? 1 : 0)) % 4;
                    float step = ph == 0 ? -2f : ph == 2 ? 2f : 0f;
                    var knee = new Vector2(ax + side * reach * 0.55f, 17f + flat * 2f);
                    var foot = new Vector2(ax + side * reach + step, ph == 3 ? 1.5f : 0f);
                    if (pose == 4) { knee = new Vector2(ax + side * reach * 0.7f, 9f); foot = new Vector2(ax + side * (reach + 4f), 0f); }
                    if (pose == 1 && i >= 2)
                    {
                        // front legs raised high, menacing
                        knee = new Vector2(ax + 5f + (i - 2) * 2f, 22f + (i - 2) * 2f);
                        foot = new Vector2(ax + 11f + (i - 2) * 3f, 16f + (i - 2) * 3f);
                    }
                    p.Line(ax * s, ay * s, knee.x * s, knee.y * s, col, w + 1);
                    p.Line(knee.x * s, knee.y * s, foot.x * s, foot.y * s, col, w);
                }
            }

            Legs(legFar, 1.5f, true);
            // abdomen: big and round, tipped forward when spitting
            RotEllipse(p, (13f + lunge * 0.5f) * s, (13f + rear * 0.6f + flat + (pose == 3 ? 3f : 0f)) * s, 10.5f * s, 8.5f * s, pose == 3 ? -28f : rear * 4f, (u, v) =>
            {
                var c = v > 0.45f && u > -0.3f ? light : body;
                if (hourglass && Mathf.Abs(u + 0.1f) < 0.22f - Mathf.Abs(v + 0.62f) * 0.5f && v < -0.25f) c = mark;
                return c;
            });
            if (!hourglass) p.Ellipse((13f + lunge * 0.5f) * s, (16f + rear * 0.6f + flat) * s, 1.6f * s, 1.6f * s, (a, b) => mark);
            // cephalothorax and head
            var ceph = S(26f + lunge, 10f + rear + flat);
            p.Ellipse(ceph.x, ceph.y, 6.5f * s, 4.8f * s, (nx, ny) => ny > 0.4f ? light : body);
            // eyes: a cluster of red beads
            foreach (var (ex, ey, er) in new[] { (30.5f, 12f, 1.3f), (31.8f, 10.8f, 1f), (29.2f, 12.8f, 0.9f), (31.2f, 13f, 0.8f) })
                p.Ellipse((ex + lunge) * s, (ey + rear + flat) * s, er * s, er * s, (a, b) => a < -0.2f && b > 0.2f ? Hex("#FFD0D0") : eye);
            // fangs
            float open = pose == 2 ? 2.5f : pose == 1 ? 1.5f : 0.5f;
            var fang = Hex("#E8D8C8");
            p.Line((31f + lunge) * s, (8f + rear + flat) * s, (33.5f + lunge) * s, (4.5f + rear + flat - open) * s, fang, w);
            p.Line((30f + lunge) * s, (8f + rear + flat) * s, (31.5f + lunge + open * 0.6f) * s, (4f + rear + flat) * s, fang, w);
            Legs(body, 0f, false);
            p.Outline(Ink);
            return p;
        }

        static Px[] SpiderFrames(float s, Color32 body, Color32 light, Color32 mark, Color32 eye, bool hourglass, bool boss)
        {
            // walk 0-3, tell 4, lunge/bite 5 (+ boss: spit 6, splayed 7)
            var frames = new List<Px>();
            for (int i = 0; i < 4; i++) frames.Add(DrawSpider(s, i, 0, body, light, mark, eye, hourglass));
            frames.Add(DrawSpider(s, 0, 1, body, light, mark, eye, hourglass));
            frames.Add(DrawSpider(s, 2, 2, body, light, mark, eye, hourglass));
            if (boss)
            {
                frames.Add(DrawSpider(s, 0, 3, body, light, mark, eye, hourglass));
                frames.Add(DrawSpider(s, 0, 4, body, light, mark, eye, hourglass));
            }
            return frames.ToArray();
        }

        /// <summary>A strand of silk, 1 unit long (pivot at the left end).</summary>
        static Px SilkStrand()
        {
            var p = new Px(32, 3);
            for (int x = 0; x < 32; x++)
            {
                p.Set(x, 1, new Color32(245, 245, 255, 235));
                if (Hash(x, 0, 701) < 0.3f) p.Set(x, Hash(x, 1, 702) < 0.5f ? 0 : 2, new Color32(220, 220, 240, 120));
            }
            return p;
        }

        /// <summary>A sticky glob of silk.</summary>
        static Px SilkGlob()
        {
            var p = new Px(16, 16);
            p.Ellipse(8f, 8f, 5.5f, 5f, (nx, ny) => nx * nx + ny * ny < 0.3f ? Hex("#FFFFFF") : Hex("#D8D8E8"));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + 0.3f;
                p.Line(8f, 8f, 8f + Mathf.Cos(a) * 7.5f, 8f + Mathf.Sin(a) * 7.5f, new Color32(235, 235, 250, 180));
            }
            p.Outline(Hex("#5A5A70"));
            return p;
        }

        /// <summary>Sticky web on the floor (pivot bottom middle).</summary>
        static Px WebPatchArt()
        {
            var p = new Px(72, 14);
            var thread = new Color32(235, 235, 250, 200);
            var faint = new Color32(220, 220, 240, 110);
            for (int i = 0; i < 9; i++)
            {
                float x0 = 2 + i * 8.5f;
                p.Line(36, 1, x0, 1 + Hash(i, 0, 711) * 10f, thread);
            }
            for (int r = 0; r < 4; r++)
                for (int x = 4; x < 68; x++)
                {
                    float y = 2 + r * 2.6f + Mathf.Sin(x * 0.35f + r) * 1.2f;
                    if (Hash(x, r, 712) < 0.75f) p.Set(x, Mathf.Clamp(Mathf.RoundToInt(y), 0, 13), faint);
                }
            for (int x = 0; x < 72; x++) p.Set(x, 0, thread);
            return p;
        }

        /// <summary>Silk wrapped round the knight (centered on him).</summary>
        static Px CocoonArt()
        {
            var p = new Px(40, 68);
            p.Ellipse(20f, 34f, 17f, 32f, (nx, ny) =>
            {
                float band = Mathf.Repeat(ny * 6f + nx * 1.4f, 1f);
                var c = band < 0.18f ? Hex("#B8B8CC") : -nx * 0.5f + ny * 0.3f > 0.4f ? Hex("#FFFFFF") : Hex("#E2E2EE");
                return c;
            });
            for (int i = 0; i < 6; i++)
                p.Line(4 + Hash(i, 0, 721) * 32, 10 + i * 9, 36 - Hash(i, 1, 722) * 32, 15 + i * 9, Hex("#C8C8DA"));
            p.Outline(Hex("#5A5A70"));
            return p;
        }

        /// <summary>A silk bridge (tiles sideways: 32x10, full rect).</summary>
        static Px WebBridgeArt()
        {
            var p = new Px(32, 10);
            var cable = Hex("#E8E8F4");
            var shade = Hex("#A8A8C0");
            for (int x = 0; x < 32; x++)
            {
                float sag = Mathf.Sin(x / 31f * Mathf.PI) * 1.5f;
                p.Set(x, Mathf.RoundToInt(9 - sag), cable);
                p.Set(x, Mathf.RoundToInt(8 - sag), shade);
                p.Set(x, Mathf.RoundToInt(5 - sag * 0.6f), cable);
                if (x % 4 == 0) p.Line(x, 9 - sag, x + 2, 2, new Color32(220, 220, 240, 150));
            }
            for (int x = 0; x < 32; x++) if (Hash(x, 3, 731) < 0.4f) p.Set(x, 1, new Color32(220, 220, 240, 110));
            return p;
        }

        /// <summary>A huge radial web for the back of the Widow's arena.</summary>
        static Px BigWeb()
        {
            const int n = 256;
            var p = new Px(n, n);
            var c = new Vector2(n * 0.5f, n * 0.5f);
            var thread = new Color32(255, 255, 255, 150);
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f * Mathf.Deg2Rad + Hash(i, 0, 741) * 0.15f;
                p.Line(c.x, c.y, c.x + Mathf.Cos(a) * 128f, c.y + Mathf.Sin(a) * 128f, thread);
            }
            for (int ring = 1; ring < 14; ring++)
            {
                float r = ring * 9.5f + Hash(ring, 0, 742) * 3f;
                for (int i = 0; i < 18; i++)
                {
                    float a0 = i * 20f * Mathf.Deg2Rad, a1 = (i + 1) * 20f * Mathf.Deg2Rad;
                    var m = new Vector2(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f)) * (r - 2f);
                    if (Hash(ring, i, 743) < 0.12f) continue; // torn
                    p.Line(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, c.x + m.x, c.y + m.y, new Color32(255, 255, 255, 110));
                    p.Line(c.x + m.x, c.y + m.y, c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, new Color32(255, 255, 255, 110));
                }
            }
            return p;
        }

        /// <summary>A silk egg sac clutch (pivot bottom middle).</summary>
        static Px EggSac()
        {
            var p = new Px(30, 26);
            foreach (var (x, y, r) in new[] { (9f, 8f, 7f), (20f, 9f, 8f), (15f, 17f, 7f) })
                p.Ellipse(x, y, r, r * 0.95f, (nx, ny) => -nx * 0.5f + ny * 0.6f > 0.45f ? Hex("#FFFFFF") : nx * nx + ny * ny > 0.75f ? Hex("#B8B8CC") : Hex("#E2E2EE"));
            for (int i = 0; i < 5; i++) p.Line(2 + i * 6, 0, 6 + i * 5, 14 + (i % 2) * 4, new Color32(200, 200, 220, 160));
            p.Outline(Hex("#4A4A60"));
            return p;
        }

        /// <summary>Something wrapped in silk, hanging from a thread (pivot at the top of the thread).</summary>
        static Px HangingCocoon()
        {
            var p = new Px(20, 52);
            p.Line(10, 51, 10, 38, new Color32(235, 235, 250, 220));
            p.Ellipse(10f, 20f, 7.5f, 17f, (nx, ny) =>
            {
                float band = Mathf.Repeat(ny * 5f + nx, 1f);
                return band < 0.2f ? Hex("#9A9AB0") : -nx * 0.5f > 0.2f ? Hex("#E8E8F2") : Hex("#C8C8DA");
            });
            p.Set(8, 30, Hex("#3A1A1A"));
            p.Outline(Hex("#3A3A50"));
            return p;
        }

        // =====================================================================
        // Floor 50: the Parasite, the throne room and its storm
        // =====================================================================

        /// <summary>
        /// The Parasite (176x136, faces right): a bloated horror wearing the knight's helmet, with mantis
        /// scythes and wasp wings. pose 0 idle, 1 idle swell, 2-3 walk, 4 rear (tell), 5 slam, 6-7 fly, 8 spit.
        /// </summary>
        static Px DrawParasite(int pose)
        {
            var p = new Px(176, 136);
            var flesh = Hex("#9C84B4");
            var fleshLight = Hex("#C8B4DC");
            var fleshDark = Hex("#5E4A78");
            var vein = Hex("#D46BFF");
            var legCol = Hex("#2E2236");
            var bone = Hex("#E6E0D0");
            var boneDark = Hex("#A8A090");
            var steel = Hex("#8A909C");
            var steelLight = Hex("#C4CAD4");
            var wingFill = Hex("#E0D0FF", 120);
            var wingEdge = Hex("#B090E0", 210);
            float swell = pose == 1 ? 2.5f : 0f;
            float rear = pose == 4 ? 10f : pose == 8 ? 4f : 0f;
            float low = pose == 5 ? -10f : pose == 6 || pose == 7 ? 8f : 0f;
            bool fly = pose == 6 || pose == 7;
            int legPhase = pose == 2 ? 0 : pose == 3 ? 2 : 1;

            // wings (open when flying)
            if (fly)
            {
                float tilt = pose == 6 ? 30f : -5f;
                RotEllipse(p, 62f, 92f + low, 46f, 14f, tilt, (u, v) => u * u + v * v > 0.8f ? wingEdge : Mathf.Repeat(u * 7f + v * 2f, 1f) < 0.1f ? wingEdge : wingFill, true);
                RotEllipse(p, 74f, 86f + low, 38f, 10f, tilt - 10f, (u, v) => u * u + v * v > 0.8f ? wingEdge : wingFill, true);
            }

            // legs: spindly, six of them
            for (int i = 0; i < 6; i++)
            {
                float hx = 58 + i * 11, hy = 34 + low + rear * 0.3f;
                int ph = (legPhase + i) % 4;
                float step = ph == 0 ? -5f : ph == 2 ? 5f : 0f;
                var knee = new Vector2(hx + (i < 3 ? -10f : 10f), 46f + low);
                var foot = fly ? new Vector2(hx + (i < 3 ? -6f : 6f), 14f + low) : new Vector2(hx + (i < 3 ? -16f : 16f) + step, 0f);
                var col = i % 2 == 0 ? legCol : Shade(legCol, 1.4f);
                p.Line(hx, hy, knee.x, knee.y, col, 3);
                p.Line(knee.x, knee.y, foot.x, foot.y, col, 2);
            }

            // bloated, segmented abdomen with glowing veins
            RotEllipse(p, 48f, 50f + low + swell * 0.5f, 42f + swell, 30f + swell, 8f + rear * 0.6f, (u, v) =>
            {
                var c = v > 0.5f ? fleshLight : v < -0.4f ? fleshDark : flesh;
                if (Mathf.Repeat(u * 5f, 1f) < 0.1f) c = fleshDark;
                if (Mathf.Abs(Mathf.Sin(u * 9f + v * 4f)) < 0.07f && v > -0.6f) c = vein;
                return c;
            });
            // thorax
            var thorax = new Vector2(98f, 52f + low + rear);
            p.Ellipse(thorax.x, thorax.y, 22f, 19f, (nx, ny) => ny > 0.45f ? fleshLight : ny < -0.5f ? fleshDark : flesh);
            // spines along the back
            for (int i = 0; i < 6; i++)
            {
                var b = new Vector2(30f + i * 13f, 74f + low - Mathf.Abs(i - 2.5f) * 2f + (i > 3 ? rear : 0f));
                Capsule(p, b, b + new Vector2(-4f, 10f), 2.2f, 0.5f, boneDark);
            }
            if (!fly)
                RotEllipse(p, 60f, 82f + low, 36f, 6f, 6f, (u, v) => v > 0.2f ? Hex("#D8C8F0") : Hex("#9A84C0")); // folded wings

            // mantis scythes
            var shoulder = thorax + new Vector2(10f, 6f);
            float armDeg = pose == 4 ? 70f : pose == 5 ? -55f : 15f;
            float bladeDeg = pose == 4 ? 150f : pose == 5 ? -95f : 115f;
            foreach (var (dx, col, bl) in new[] { (-6f, fleshDark, boneDark), (0f, flesh, bone) })
            {
                var s0 = shoulder + new Vector2(dx, 0f);
                var elbow = s0 + Dir(armDeg) * 26f;
                var tip = elbow + Dir(bladeDeg) * 34f;
                Capsule(p, s0, elbow, 5f, 4f, col);
                Capsule(p, elbow, tip, 4.2f, 0.8f, bl);
                if (dx == 0f) p.Line(elbow.x, elbow.y - 2f, tip.x, tip.y, Hex("#FFFFFF"));
            }

            // the knight's helmet, jammed on its head
            var headC = thorax + new Vector2(24f + (pose == 8 ? 6f : 0f), 10f + rear * 0.5f);
            p.Ellipse(headC.x, headC.y, 13f, 12f, (nx, ny) => -nx * 0.4f + ny * 0.6f > 0.45f ? steelLight : ny < -0.5f ? Shade(steel, 0.7f) : steel);
            p.Rect((int)headC.x - 2, (int)headC.y - 1, 14, 3, Hex("#1A0E22"));       // visor slit
            p.Rect((int)headC.x + 2, (int)headC.y - 1, 8, 2, vein);                   // glowing eyes behind it
            p.Line(headC.x - 6, headC.y + 12, headC.x - 2, headC.y + 20, Hex("#B83A3A"), 2); // the plume, torn
            // mouth: mandibles under the helmet, open to spit
            float open = pose == 8 ? 6f : 2f;
            p.Line(headC.x + 6, headC.y - 10, headC.x + 14, headC.y - 12 - open, boneDark, 2);
            p.Line(headC.x + 6, headC.y - 12, headC.x + 13, headC.y - 16 + open * 0.4f, boneDark, 2);
            if (pose == 8) p.Ellipse(headC.x + 15f, headC.y - 14f, 3f, 3f, (a, b) => Hex("#9CFF7A"));

            p.Outline(Ink);
            return p;
        }

        static Px[] ParasiteFrames() => Enumerable.Range(0, 9).Select(DrawParasite).ToArray();

        /// <summary>The throne (64x80, pivot bottom middle).</summary>
        static Px Throne()
        {
            var p = new Px(64, 80);
            var stone = Hex("#3A3044");
            var stoneLight = Hex("#5A4C6A");
            var gold = Hex("#D8A84A");
            var velvet = Hex("#5A1E3A");
            p.Rect(8, 0, 48, 10, stone);
            p.Rect(8, 9, 48, 2, stoneLight);
            p.Rect(14, 10, 36, 62, stone);
            p.Rect(16, 12, 32, 56, velvet);
            p.Rect(18, 14, 28, 52, Shade(velvet, 1.25f));
            for (int x = 14; x < 50; x += 6) p.Rect(x, 72, 4, 8 - Mathf.Abs(x - 32) / 6, stone);
            p.Rect(12, 70, 40, 2, gold);
            p.Rect(4, 10, 12, 26, stoneLight);
            p.Rect(48, 10, 12, 26, stoneLight);
            p.Rect(4, 34, 12, 2, gold);
            p.Rect(48, 34, 12, 2, gold);
            p.Ellipse(32f, 76f, 4f, 4f, (a, b) => gold);
            p.Rect(31, 60, 2, 8, gold);
            p.Outline(Ink);
            return p;
        }

        /// <summary>Storm sky over the ruined tower: violet clouds, a sickly glow, shattered spires.</summary>
        static Px StormSky()
        {
            const int w = 480, h = 270;
            var p = new Px(w, h);
            var top = Hex("#0B0614");
            var low = Hex("#3A1A3E");
            for (int y = 0; y < h; y++)
            {
                int band = Mathf.FloorToInt(Mathf.Pow(y / (float)h, 0.8f) * 14f);
                var c = Mix(low, top, band / 14f);
                for (int x = 0; x < w; x++) p.Set(x, y, c);
            }
            // the eye of the storm
            int ex = 300, ey = 185;
            for (int r = 110; r > 30; r -= 8) p.Ellipse(ex, ey, r, r * 0.7f, (nx, ny) => new Color32(190, 90, 255, 12), true);
            p.Ellipse(ex, ey, 26, 26, (nx, ny) => nx * nx + ny * ny < 0.3f ? Hex("#F4D8FF") : Hex("#B46BFF"));
            // swirling clouds
            for (int y = 70; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x - ex, dy = (y - ey) * 1.4f;
                float a = Mathf.Atan2(dy, dx), r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Mathf.PerlinNoise(a * 2f + r * 0.02f, r * 0.03f);
                if (n > 0.58f) p.Blend(x, y, new Color32(120, 60, 150, (byte)((n - 0.58f) * 400f)));
            }
            // broken spires
            var dark = Hex("#07040C");
            for (int x = 0; x < w; x++)
            {
                int hgt = 40 + (int)(Mathf.PerlinNoise(x * 0.025f, 9.1f) * 30f);
                for (int y = 0; y < hgt; y++) p.Set(x, y, dark);
            }
            for (int i = 0; i < 7; i++)
            {
                int tx = 30 + i * 66 + (int)(Hash(i, 0, 751) * 20f), tw = 8 + (int)(Hash(i, 1, 751) * 8f), th = 80 + (int)(Hash(i, 2, 751) * 70f);
                p.Rect(tx, 0, tw, th, dark);
                for (int k = 0; k < tw; k++) p.Rect(tx + k, th, 1, (int)(Hash(k, i, 752) * 12f), dark); // shattered tops
            }
            return p;
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

        /// <summary>
        /// Flame Slash's arc of fire, left along the path of the rising swing (facing +x): it starts low
        /// in front (tail, thin and red) and sweeps up over the head (head, thick and white-hot), with
        /// flame tongues licking off the outer edge. Pivot at (50, 62), the center of the swing.
        /// </summary>
        static Px FireArc(int seed, float reveal = 1f)
        {
            const int size = 160;
            const float cx = 50f, cy = 62f, R = 70f, from = -40f, to = 118f;
            var p = new Px(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < from || ang > from + (to - from) * reveal) continue;
                // 0 tail .. 1 head; while the swing is still pouring it out, the head is wherever the blade is now
                float along = (ang - from) / ((to - from) * reveal);
                float thick = 9f + 27f * Mathf.Pow(along, 1.15f);
                if (along > 0.95f) thick *= 1f - (along - 0.95f) / 0.05f * 0.5f; // rounded head
                float inner = R - thick;

                // flame tongues: pointed licks along the outer edge, longer toward the head
                float segment = ang / 7f;
                int s = Mathf.FloorToInt(segment);
                float u = segment - s;
                float tongueLen = (5f + 15f * along) * (0.35f + 0.65f * Hash(s, seed, 410)) * Mathf.Clamp01(along * 4f);
                float tongue = Mathf.Pow(1f - Mathf.Abs(2f * u - 1f), 1.6f) * tongueLen;
                float outer = R + tongue;
                if (r < inner - 3f || r > outer) continue;

                float heat;
                if (r <= R)
                {
                    // hottest just inside the blade's path, cooling toward the inside of the arc
                    float k = (R - 3f - r) / Mathf.Max(1f, thick);
                    heat = 1f - Mathf.Abs(k) * 0.95f;
                    if (r < inner) heat = 0.1f * (1f - (inner - r) / 3f);
                }
                else heat = 0.62f * (1f - (r - R) / Mathf.Max(1f, tongue));
                heat *= 0.5f + 0.55f * along;
                heat += (Hash(x, y, 411 + seed) - 0.5f) * 0.14f;
                var c = FireColor(heat);
                if (c.a > 0) p.Set(x, y, c);
            }
            // loose wisps of flame drifting off the top of the arc
            for (int i = 0; i < 9 * reveal; i++)
            {
                float a = Mathf.Lerp(from + 40f, from + (to - from) * reveal - 5f, Hash(i, seed, 412)) * Mathf.Deg2Rad;
                float rr = R + 12f + Hash(seed, i, 413) * 14f;
                float wx = cx + Mathf.Cos(a) * rr, wy = cy + Mathf.Sin(a) * rr;
                float wr = 1.5f + Hash(i, i, 414 + seed) * 2f;
                p.Ellipse(wx, wy, wr, wr * 1.4f, (nx, ny) => FireColor(0.75f - (nx * nx + ny * ny) * 0.5f), true);
            }
            return p;
        }

        /// <summary>3 frames of the fire pouring out along the swing, then 4 flickering frames of the full arc.</summary>
        static Px[] FireArcFrames() => new[] { FireArc(5, 0.4f), FireArc(6, 0.7f), FireArc(7, 0.9f), FireArc(1), FireArc(2), FireArc(3), FireArc(4) };

        /// <summary>Repaints fire frames as corrupted violet fire: the same shapes, brightness mapped onto a violet ramp.</summary>
        static Px[] VoidFire(Px[] frames)
        {
            var ramp = new[]
            {
                (0f, new Color(0.1f, 0.02f, 0.16f)),
                (0.35f, new Color(0.4f, 0.1f, 0.7f)),
                (0.65f, new Color(0.7f, 0.35f, 1f)),
                (1f, new Color(1f, 0.92f, 1f)),
            };
            foreach (var f in frames)
                for (int i = 0; i < f.C.Length; i++)
                {
                    var c = f.C[i];
                    if (c.a == 0) continue;
                    float lum = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
                    int k = 1;
                    while (k < ramp.Length - 1 && lum > ramp[k].Item1) k++;
                    var (t0, c0) = ramp[k - 1];
                    var (t1, c1) = ramp[k];
                    var v = Color.Lerp(c0, c1, Mathf.InverseLerp(t0, t1, lum));
                    f.C[i] = new Color32((byte)(v.r * 255f), (byte)(v.g * 255f), (byte)(v.b * 255f), c.a);
                }
            return frames;
        }

        /// <summary>A blob of burning fire flying toward +x (head at the pivot, tail streaming behind).</summary>
        static Px[] FireGlobFrames()
        {
            var frames = new Px[4];
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(18, 14);
                const float hx = 12f, hy = 7f;
                for (int y = 0; y < 14; y++)
                for (int x = 0; x < 18; x++)
                {
                    float dx = x + 0.5f - hx, dy = y + 0.5f - hy;
                    float heat;
                    if (dx >= 0f)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 4.2f;
                        if (d > 1f) continue;
                        heat = 1f - d * 0.85f;
                    }
                    else
                    {
                        // tail: narrows and cools behind the head, wavering per frame
                        float back = -dx / 7f;
                        if (back > 1f) continue;
                        float half = 4.2f * (1f - back) + Mathf.Sin(back * 9f + f * 1.7f) * 0.8f * back;
                        float off = Mathf.Abs(dy + Mathf.Sin(back * 6f + f) * back * 1.5f);
                        if (off > half) continue;
                        heat = (1f - off / Mathf.Max(0.5f, half)) * (1f - back * 0.75f) * 0.9f;
                    }
                    heat += (Hash(x, y, 420 + f) - 0.5f) * 0.15f;
                    var c = FireColor(heat);
                    if (c.a > 0) p.Set(x, y, c);
                }
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>A small patch of fire burning on the floor (pivot at the bottom middle).</summary>
        static Px[] GroundFireFrames()
        {
            var frames = new Px[4];
            for (int f = 0; f < 4; f++)
            {
                var p = new Px(22, 26);
                // three licks of flame of different heights, swaying per frame
                float[] centers = { 6f, 11f, 16f };
                float[] heights = { 15f, 23f, 17f };
                for (int k = 0; k < 3; k++)
                {
                    float h = heights[k] * (0.85f + 0.15f * Mathf.Sin(f * 1.6f + k * 2.1f));
                    for (int y = 0; y < 26; y++)
                    for (int x = 0; x < 22; x++)
                    {
                        float t = y / h;
                        if (t > 1f) continue;
                        float sway = Mathf.Sin(y * 0.45f + f * 1.6f + k) * t * 1.6f;
                        float half = (k == 1 ? 4.6f : 3.6f) * (1f - t * t) + 0.4f;
                        float dx = Mathf.Abs(x + 0.5f - centers[k] - sway);
                        if (dx > half) continue;
                        float heat = (1f - dx / half) * (1f - t * 0.75f);
                        heat += (Hash(x, y, 430 + f + k * 7) - 0.5f) * 0.12f;
                        var c = FireColor(heat);
                        if (c.a == 0) continue;
                        var old = p.Get(x, y);
                        if (old.a == 0 || c.r + c.g > old.r + old.g) p.Set(x, y, c); // keep the hotter color where licks overlap
                    }
                }
                // glowing coals along the bottom
                for (int x = 2; x < 20; x++)
                    if (p.Get(x, 0).a == 0) p.Set(x, 0, Hash(x, f, 431) < 0.5f ? Hex("#E0401C") : Hex("#8A1E10"));
                frames[f] = p;
            }
            return frames;
        }

        /// <summary>The crimson flask the knight drinks from (pivot at its middle).</summary>
        static Px FlaskSprite()
        {
            var p = new Px(10, 14);
            var glass = Hex("#C9D6E6");
            var red = Hex("#D7263D");
            var redLight = Hex("#FF5A5F");
            var redDark = Hex("#7E1020");
            var cork = Hex("#8A5A2E");
            p.Ellipse(5f, 4.5f, 4.5f, 4.5f, (nx, ny) => ny > 0.35f ? glass : nx < -0.3f && ny > -0.2f ? redLight : ny < -0.5f ? redDark : red);
            p.Rect(3, 8, 4, 3, glass);
            p.Rect(4, 8, 2, 2, red);
            p.Rect(3, 11, 4, 2, cork);
            p.Set(2, 5, Color.white);
            p.Outline(Ink);
            return p;
        }

        /// <summary>HUD icon: the crimson flask, bigger, with a warm glow.</summary>
        static Px FlaskIcon()
        {
            var p = new Px(24, 24);
            var glass = Hex("#D8E2F0");
            var glassDark = Hex("#8C9AB0");
            var red = Hex("#D7263D");
            var redLight = Hex("#FF6B6B");
            var redDark = Hex("#7E1020");
            var cork = Hex("#9C6A38");
            var corkDark = Hex("#6B4422");
            p.Ellipse(12f, 8.5f, 8f, 8f, (nx, ny) =>
                ny > 0.45f ? (nx > 0.4f ? glassDark : glass) :
                nx < -0.35f && ny > -0.3f ? redLight : ny < -0.55f || nx > 0.6f ? redDark : red);
            p.Rect(9, 15, 6, 4, glass);
            p.Rect(13, 15, 2, 4, glassDark);
            p.Rect(10, 15, 4, 1, red);
            p.Rect(8, 19, 8, 3, cork);
            p.Rect(8, 19, 8, 1, corkDark);
            // shine
            p.Set(7, 11, Color.white);
            p.Set(6, 10, Color.white);
            p.Set(6, 9, Hex("#FFD0D0"));
            p.Outline(Ink);
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
        // Sword combat: smear trails for the 3 slashes, the heavy stab
        // =====================================================================

        /// <summary>
        /// Sword smear: a crescent swept from tailDeg to headDeg around (cx, cy) (degrees, counter-clockwise
        /// from +x; a negative sweep swings clockwise). Thick and bright at the head (where the blade is now),
        /// thin and fading toward the tail.
        /// </summary>
        static Px Smear(int size, float cx, float cy, float outer, float tailDeg, float headDeg, float maxThick,
            Color32 edge, Color32 mid, Color32 inner, int seed)
        {
            var p = new Px(size, size);
            float span = Mathf.Abs(headDeg - tailDeg);
            bool clockwise = headDeg < tailDeg;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float travelled = clockwise ? Mathf.Repeat(tailDeg - ang, 360f) : Mathf.Repeat(ang - tailDeg, 360f);
                if (travelled > span) continue;
                float along = travelled / span; // 0 tail .. 1 head
                float thick = maxThick * (0.12f + 0.88f * Mathf.Pow(along, 1.3f));
                if (along > 0.94f) thick *= 1f - (along - 0.94f) / 0.06f * 0.45f; // rounded head
                if (r > outer || r < outer - thick) continue;
                float k = (outer - r) / Mathf.Max(1f, thick); // 0 outer edge .. 1 inner edge
                float a = Mathf.Clamp01(along * 1.7f);
                Color32 c = k < 0.22f ? edge : k < 0.58f ? mid : inner;
                if (k >= 0.4f && Hash((int)(travelled * 0.7f), 0, seed) < 0.3f) c = mid; // speed streaks
                c.a = (byte)(c.a * a);
                p.Set(x, y, c);
            }
            return p;
        }

        static readonly Color32 SmearEdge = new Color32(255, 255, 255, 255);
        static readonly Color32 SmearMid = new Color32(200, 236, 255, 235);
        static readonly Color32 SmearInner = new Color32(110, 175, 235, 150);

        /// <summary>Slash 1: diagonal down-swing from behind the head to low in front.</summary>
        static Px SmearDiagonal() => Smear(112, 44f, 56f, 46f, 118f, -38f, 15f, SmearEdge, SmearMid, SmearInner, 701);

        /// <summary>Slash 2: rising uppercut from low in front up over the head.</summary>
        static Px SmearRising() => Smear(112, 44f, 56f, 46f, -58f, 98f, 14f, SmearEdge, SmearMid, SmearInner, 702);

        /// <summary>Slash 3: big overhead cleave that comes down into the ground in front (warmer, it's the finisher).</summary>
        static Px SmearCleave() => Smear(144, 56f, 66f, 60f, 165f, -78f, 21f, SmearEdge,
            new Color32(255, 236, 190, 240), new Color32(255, 180, 90, 160), 703);

        /// <summary>Heavy stab: a long streak of light, thin at the hand and bright at the point (pivot at the hand).</summary>
        static Px ThrustStreak()
        {
            var p = new Px(128, 24);
            for (int x = 0; x < 128; x++)
            {
                float t = x / 127f;
                float half = t < 0.88f ? 1f + 7f * t : 8f * (1f - (t - 0.88f) / 0.12f); // widens, then comes to a point
                for (int y = 0; y < 24; y++)
                {
                    float d = Mathf.Abs(y + 0.5f - 12f);
                    if (d > half) continue;
                    float k = d / Mathf.Max(1f, half);
                    byte a = (byte)(255 * Mathf.Clamp01(t * 2.2f));
                    Color32 c = k < 0.3f ? new Color32(255, 255, 255, a) : k < 0.7f ? new Color32(255, 240, 200, (byte)(a * 0.85f)) : new Color32(255, 190, 110, (byte)(a * 0.5f));
                    if (k > 0.5f && Hash(x / 6, y, 710) < 0.25f) c.a = (byte)(c.a * 0.5f);
                    p.Set(x, y, c);
                }
            }
            return p;
        }

        /// <summary>Four-point impact star (stab on a stunned bug).</summary>
        static Px ImpactStar()
        {
            var p = new Px(48, 48);
            for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - 24f), dy = Mathf.Abs(y + 0.5f - 24f);
                float star = Mathf.Min(dx * 0.22f + dy, dy * 0.22f + dx); // thin cross
                float diag = Mathf.Abs(dx - dy) + (dx + dy) * 0.35f;     // short diagonal rays
                float v = Mathf.Min(star / 4f, diag / 6f);
                if (v > 1f) continue;
                byte a = (byte)(255 * (1f - v));
                p.Set(x, y, v < 0.45f ? new Color32(255, 255, 255, a) : new Color32(255, 220, 150, a));
            }
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

        /// <summary>Thousand Cuts: a razor-thin cut line, bright in the middle and tapering to points (pivot in the middle).</summary>
        static Px CutLine()
        {
            var p = new Px(160, 8);
            for (int x = 0; x < 160; x++)
            {
                float t = Mathf.Abs(x + 0.5f - 80f) / 80f; // 0 middle .. 1 tips
                float half = 3.6f * (1f - t * t);
                for (int y = 0; y < 8; y++)
                {
                    float d = Mathf.Abs(y + 0.5f - 4f);
                    if (d > half) continue;
                    float k = d / Mathf.Max(0.5f, half);
                    byte a = (byte)(255 * Mathf.Clamp01((1f - t) * 1.6f));
                    p.Set(x, y, k < 0.35f ? new Color32(255, 255, 255, a) : new Color32(160, 225, 255, (byte)(a * 0.75f)));
                }
            }
            return p;
        }

        /// <summary>A rune circle (Arcane Orbs, Riposte's ward, orb bursts): two rings, runes between them, a star inside. White, tinted in code.</summary>
        static Px MagicCircle()
        {
            const int size = 96;
            const float c = 48f;
            var p = new Px(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                byte a = 0;
                if (Mathf.Abs(r - 45f) < 1.2f || Mathf.Abs(r - 33f) < 1f) a = 255;          // outer and inner rings
                else if (Mathf.Abs(r - 42f) < 0.6f) a = 150;                                  // thin ring inside the outer one
                else if (r > 34.5f && r < 41f)
                {
                    // runes: little blocky glyphs around the band
                    int seg = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 28f);
                    float u = (ang + Mathf.PI) / (Mathf.PI * 2f) * 28f - seg;
                    float v = (r - 34.5f) / 6.5f;
                    int gx = Mathf.FloorToInt(u * 3f), gy = Mathf.FloorToInt(v * 3f);
                    if (u > 0.15f && u < 0.85f && Hash(seg * 9 + gx * 3 + gy, 0, 600) < 0.55f) a = 210;
                }
                if (a > 0) p.Set(x, y, new Color32(255, 255, 255, a));
            }
            // a six-pointed star inside
            for (int k = 0; k < 2; k++)
            for (int i = 0; i < 3; i++)
            {
                float a0 = (k * 60f + i * 120f + 90f) * Mathf.Deg2Rad, a1 = (k * 60f + (i + 1) * 120f + 90f) * Mathf.Deg2Rad;
                p.Line(c + Mathf.Cos(a0) * 32f, c + Mathf.Sin(a0) * 32f, c + Mathf.Cos(a1) * 32f, c + Mathf.Sin(a1) * 32f, new Color32(255, 255, 255, 230));
            }
            p.Ellipse(c, c, 5f, 5f, (nx, ny) => new Color32(255, 255, 255, 180));
            return p;
        }

        /// <summary>Cracks splitting out across the floor from an impact (pivot at the bottom middle).</summary>
        static Px GroundCrack()
        {
            var p = new Px(96, 24);
            var dark = Hex("#0E0C14");
            var glow = Hex("#BFEAFF", 220);
            for (int s = 0; s < 7; s++)
            {
                float dir = s < 3 ? -1f : s < 6 ? 1f : 0f;
                float x = 48f, y = 1f;
                float ax = dir * (6f + Hash(s, 0, 610) * 7f), ay = 1.2f + Hash(s, 1, 611) * 2.5f;
                if (dir == 0f) { ax = (Hash(s, 2, 612) - 0.5f) * 3f; ay = 4f; }
                for (int k = 0; k < 6; k++)
                {
                    float nx = x + ax * (0.6f + Hash(s, k, 613)), ny = y + ay * (0.5f + Hash(k, s, 614));
                    p.Line(x, y, nx, ny, dark, k < 2 ? 2 : 1);
                    p.Line(x, y + 1, nx, ny + 1, glow);
                    x = nx;
                    y = ny;
                    if (y > 22f || x < 1f || x > 94f) break;
                }
            }
            // chips of stone thrown up
            for (int i = 0; i < 8; i++)
                p.Rect((int)(30 + Hash(i, 3, 615) * 36), (int)(6 + Hash(i, 4, 616) * 14), 2, 2, Hash(i, 5, 617) < 0.5f ? Hex("#62748A") : Hex("#8196AD"));
            return p;
        }

        /// <summary>A scorched patch on the floor where lightning hit (pivot in the middle).</summary>
        static Px Scorch()
        {
            var p = new Px(64, 14);
            p.Ellipse(32f, 7f, 31f, 6.5f, (nx, ny) =>
            {
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                byte a = (byte)(230 * Mathf.Clamp01((1f - d) * 2.2f));
                return d < 0.35f ? new Color32(20, 16, 24, a) : new Color32(30, 26, 34, (byte)(a * 0.8f));
            });
            // glowing embers in the char
            for (int i = 0; i < 12; i++)
            {
                float a = Hash(i, 0, 620) * Mathf.PI * 2f, r = Hash(i, 1, 621) * 0.8f;
                p.Set(Mathf.RoundToInt(32f + Mathf.Cos(a) * r * 28f), Mathf.RoundToInt(7f + Mathf.Sin(a) * r * 5f), Hash(i, 2, 622) < 0.5f ? Hex("#BFEAFF") : Hex("#6FB8FF"));
            }
            return p;
        }

        /// <summary>Thousand Cuts icon: three bright cuts slashed across the frame.</summary>
        static Px IconThousandCuts()
        {
            var p = new Px(24, 24);
            void Cut(float x0, float y0, float x1, float y1)
            {
                int steps = 40;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float w = Mathf.Sin(Mathf.PI * t) * 1.6f;
                    float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
                    for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > w + 0.4f) continue;
                        var old = p.Get((int)x + dx, (int)y + dy);
                        var c = d < w * 0.5f ? Hex("#FFFFFF") : Hex("#8FD8FF");
                        if (old.a == 0 || c.r > old.r) p.Set((int)x + dx, (int)y + dy, c);
                    }
                }
            }
            Cut(2f, 18f, 21f, 4f);
            Cut(3f, 6f, 22f, 19f);
            Cut(1f, 12f, 23f, 11f);
            p.Outline(Ink);
            return p;
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

        /// <summary>Heavenfall: a sword of light falling point-first out of a burst of rays.</summary>
        static Px IconHeavenfall()
        {
            var p = new Px(24, 24);
            var ray = Hex("#FFD86B");
            var rayDim = Hex("#C9902E");
            // rays bursting from where the point lands
            for (int i = 0; i < 9; i++)
            {
                float a = (200f + i * 17.5f) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var from = new Vector2(12f, 4f) - d * 3f;
                var to = new Vector2(12f, 4f) - d * (i % 2 == 0 ? 11f : 8f);
                p.Line(from.x, from.y, to.x, to.y, i % 2 == 0 ? ray : rayDim);
            }
            // the blade, point down
            var white = Hex("#FFFFFF");
            var pale = Hex("#FFF2C2");
            var gold = Hex("#F2C14E");
            for (int y = 3; y <= 17; y++)
            {
                int half = y < 6 ? (y - 3) / 2 : 2;
                for (int x = 12 - half; x <= 12 + half; x++) p.Set(x, y, x == 12 ? white : pale);
            }
            p.Rect(7, 17, 11, 2, gold);                  // crossguard
            p.Set(7, 19, gold); p.Set(17, 19, gold);
            p.Rect(11, 19, 3, 3, Hex("#B8862E"));        // grip
            p.Rect(11, 22, 3, 2, white);                 // pommel
            p.Set(12, 1, white); p.Set(12, 2, white);    // the glint at the point
            p.Set(11, 1, ray); p.Set(13, 1, ray);
            p.Outline(Ink);
            return p;
        }

        /// <summary>The Reaper Mantis's huge crescent moon (72x72), rising behind it for its execution.</summary>
        static Px MoonCrescent()
        {
            const int s = 72;
            var p = new Px(s, s);
            var lit = Hex("#EFFFF3");
            var mid = Hex("#CFF5DA");
            var rim = Hex("#8FE0A8");
            var crater = Hex("#B6E8C4");
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - 36f, dy = y + 0.5f - 36f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 33f) continue;
                // bite out of the right side
                float bx = x + 0.5f - 50f, by = y + 0.5f - 42f;
                if (bx * bx + by * by < 29f * 29f) continue;
                var c = d > 31f ? rim : d > 26f ? mid : lit;
                if (Hash(x / 3, y / 3, 77) > 0.86f && d < 28f) c = crater;
                p.Set(x, y, c);
            }
            return p;
        }

        /// <summary>The colossal sword of light, pointing down (28x120, tip at the bottom middle).</summary>
        static Px HolyBlade()
        {
            const int w = 28, h = 120;
            var p = new Px(w, h);
            var white = Hex("#FFFFFF");
            var pale = Hex("#FFF4CF");
            var gold = Hex("#FFD36A");
            var deep = Hex("#E0A43A");
            var glow = new Color32(255, 214, 120, 110);
            const float cx = 13.5f;
            // blade: tapers to the point, a bright fuller down the middle
            for (int y = 0; y < 88; y++)
            {
                float half = y < 22 ? Mathf.Lerp(0.6f, 6.5f, y / 22f) : 6.5f;
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - cx);
                    if (d > half + 2f) continue;
                    if (d > half) { p.Set(x, y, glow); continue; }
                    var c = d < 1f ? white : d > half - 1.2f ? gold : pale;
                    if (d >= 1.5f && d < 2.6f && y > 26 && y < 84) c = (y / 3) % 4 == 0 ? deep : gold; // runes in the fuller
                    p.Set(x, y, c);
                }
            }
            // crossguard, wings swept up
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x + 0.5f - cx);
                int lift = Mathf.RoundToInt(d * d / 40f);
                for (int y = 88 + lift; y < 94 + lift && y < h; y++) p.Set(x, y, y == 93 + lift ? white : y == 88 + lift ? deep : gold);
            }
            // grip and pommel
            for (int y = 94; y < 110; y++)
            for (int x = 11; x < 17; x++)
                p.Set(x, y, (y % 3 == 0) ? deep : Hex("#C8913A"));
            p.Ellipse(cx, 114f, 5f, 5f, (nx, ny) => nx * nx + ny * ny < 0.25f ? white : ny > 0.2f ? pale : gold);
            return p;
        }

        /// <summary>A spear of light, pointing down (10x56, tip at the bottom middle), its tail fading out.</summary>
        static Px LightSpear()
        {
            const int w = 10, h = 56;
            var p = new Px(w, h);
            for (int y = 0; y < h; y++)
            {
                float half = y < 10 ? Mathf.Lerp(0.5f, 3.6f, y / 10f) : y < 13 ? 2.2f : 1.4f;
                byte a = (byte)(255 * Mathf.Clamp01(1.25f - y / (float)h));
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - 5f);
                    if (d > half) continue;
                    Color32 c = d < 0.8f ? new Color32(255, 255, 255, a) : new Color32(255, 214, 110, a);
                    p.Set(x, y, c);
                }
            }
            return p;
        }

        /// <summary>A soft vertical beam of light (32x128, bottom middle pivot), brightest in the core, fading out at the top.</summary>
        static Px LightPillar()
        {
            const int w = 32, h = 128;
            var p = new Px(w, h);
            for (int y = 0; y < h; y++)
            {
                float v = 1f - y / (float)h;
                float fade = Mathf.Clamp01(v * 1.4f) * (0.85f + 0.15f * Mathf.Clamp01(1f - y / 12f));
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - 16f) / 16f;
                    float core = Mathf.Exp(-d * d * 18f);
                    float halo = Mathf.Exp(-d * d * 4f) * 0.45f;
                    float a = Mathf.Clamp01((core + halo) * fade);
                    if (a < 0.02f) continue;
                    var c = Color.Lerp(new Color(1f, 0.8f, 0.4f), Color.white, core);
                    p.Set(x, y, new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f)));
                }
            }
            return p;
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

            // tiles
            for (int i = 0; i < 3; i++) Single(dir, $"tile_ground_fill_{i}", GroundFill(i), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 2; i++) Single(dir, $"tile_ground_top_{i}", GroundTop(i + 3), new Vector2(0.5f, 0.5f));
            Single(dir, "tile_ground_under", GroundUnder(7), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 3; i++) Single(dir, $"tile_platform_{i}", Platform(i), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 3; i++) Single(dir, $"tile_backwall_{i}", BackWall(i), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 2; i++) Single(dir, $"tile_backwall_top_{i}", BackWallTop(i), new Vector2(0.5f, 0.5f));
            Single(dir, "tile_pillar", Pillar(false), new Vector2(0.5f, 0.5f));
            Single(dir, "tile_pillar_top", Pillar(true), new Vector2(0.5f, 0.5f));

            // backgrounds (16 px per unit: chunkier pixels read as "far away")
            Single(dir, "bg_far", FarBackground(), new Vector2(0.5f, 0.5f), 16f);
            Single(dir, "bg_ruins", MidRuins(), new Vector2(0.5f, 0f), 16f);
            Single(dir, "bg_dusk", SkyDusk(), new Vector2(0.5f, 0.5f), 16f);
            Single(dir, "bg_moon", SkyMoon(), new Vector2(0.5f, 0.5f), 16f);

            // map props
            Single(dir, "prop_tomb", Tomb(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "prop_web", Web(), new Vector2(0f, 1f), PPU);
            Single(dir, "prop_eggs", EggCluster(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "prop_honeycomb", Honeycomb(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "prop_hive", HiveChunk(), new Vector2(0.5f, 1f), PPU);
            Single(dir, "prop_gate", ShrineGate(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "prop_lantern", Lantern(), new Vector2(0.5f, 1f), PPU);
            Single(dir, "prop_stalactite", Stalactite(), new Vector2(0.5f, 1f), PPU);
            Single(dir, "prop_mushrooms", Mushrooms(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_rock", Rock(), new Vector2(0.5f, 0.5f), PPU);

            // props
            Single(dir, "prop_torch", TorchBody(), new Vector2(0.5f, 0.5f), 24f);
            Sheet(dir, "prop_flame", TorchFlames(), new Vector2(0.5f, 0f), 24f);
            Single(dir, "prop_chain", ChainLink(), new Vector2(0.5f, 1f), PPU, fullRect: true);
            Single(dir, "prop_banner", Banner(), new Vector2(0.5f, 1f), 16f);
            Single(dir, "prop_window", Window(), new Vector2(0.5f, 0f), 16f);
            var tufts = GrassTufts();
            for (int i = 0; i < tufts.Length; i++) Single(dir, $"prop_grass_{i}", tufts[i], new Vector2(0.5f, 0f), PPU);
            Single(dir, "prop_bones", Bones(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "door_frame", DoorFrame(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "door_bars", Portcullis(), new Vector2(0.5f, 0f), PPU);
            Sheet(dir, "door_portal", PortalFrames(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "door_arrow", ArrowDown(), new Vector2(0.5f, 0.5f), PPU);

            // bugs
            Sheet(dir, "bug_beetle", BeetleFrames(1f, 46, 28, Hex("#B5452E"), Hex("#E07A4F"), Hex("#6E2418"), Hex("#3B2418"), Hex("#FFE066"), false, false), new Vector2(0.41f, 0f), PPU);
            Sheet(dir, "bug_wasp", WaspFrames(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "bug_tick", TickFrames(), new Vector2(0.45f, 0f), PPU);
            Sheet(dir, "bug_boss", BeetleFrames(2.4f, 112, 76, Hex("#6B2D8C"), Hex("#A35AD1"), Hex("#3A1550"), Hex("#2A1238"), Hex("#FFDD55"), true, true), new Vector2(0.41f, 0f), PPU);
            Sheet(dir, "fx_acid", AcidFrames(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "bug_hornetqueen", HornetQueenFrames(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "bug_mantis", MantisFrames(), new Vector2(44f / 104f, 0f), PPU);
            Single(dir, "fx_stinger", StingerSprite(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "fx_poisonsac", PoisonSacFrames(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_scythe", ScytheSlash(), new Vector2(30f / 96f, 0.5f), PPU);

            // normal-floor bugs for floors 5-49
            Sheet(dir, "bug_pillbug", PillBugFrames(), new Vector2(0.48f, 0f), PPU);
            Sheet(dir, "bug_termite", TermiteFrames(), new Vector2(0.45f, 0f), PPU);
            Sheet(dir, "bug_scorpion", ScorpionFrames(), new Vector2(0.45f, 0f), PPU);
            Sheet(dir, "bug_firefly", FireflyFrames(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "bug_stag", StagFrames(), new Vector2(0.45f, 0f), PPU);

            // floor 40: the Silk Widow
            Sheet(dir, "bug_widow", SpiderFrames(3f, Hex("#2A1E2E"), Hex("#4E3E5C"), Hex("#E0283A"), Hex("#FF4A4A"), true, true), new Vector2(0.5f, 0f), PPU);
            Sheet(dir, "bug_spiderling", SpiderFrames(0.9f, Hex("#3A2A30"), Hex("#5E4650"), Hex("#C83A3A"), Hex("#FF6A5A"), false, false), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_silk", SilkStrand(), new Vector2(0f, 0.5f), PPU);
            Single(dir, "fx_silkglob", SilkGlob(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_webpatch", WebPatchArt(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_cocoon", CocoonArt(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "prop_webbridge", WebBridgeArt(), new Vector2(0.5f, 1f), PPU, fullRect: true);
            Single(dir, "bg_bigweb", BigWeb(), new Vector2(0.5f, 0.5f), 16f);
            Single(dir, "prop_eggsac", EggSac(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "prop_cocooned", HangingCocoon(), new Vector2(0.5f, 1f), PPU);

            // floor 50: the Parasite and the throne room
            Sheet(dir, "bug_parasite", ParasiteFrames(), new Vector2(0.45f, 0f), PPU);
            Single(dir, "prop_throne", Throne(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "bg_storm", StormSky(), new Vector2(0.5f, 0.5f), 16f);

            // fire (Flame Slash)
            Single(dir, "fx_fireslash", FireSlash(), new Vector2(0.31f, 0.5f), PPU);
            Sheet(dir, "fx_firewave", new[] { FireSlash(1), FireSlash(2), FireSlash(3) }, new Vector2(0.31f, 0.5f), PPU);
            Single(dir, "icon_flameslash", FlameSlashIcon(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "fx_firearc", FireArcFrames(), new Vector2(50f / 160f, 62f / 160f), PPU);
            Sheet(dir, "fx_fireglob", FireGlobFrames(), new Vector2(12f / 18f, 0.5f), PPU);
            Sheet(dir, "fx_groundfire", GroundFireFrames(), new Vector2(0.5f, 0f), PPU);
            // the Infested Knight's corrupted version of it
            Sheet(dir, "fx_voidarc", VoidFire(FireArcFrames()), new Vector2(50f / 160f, 62f / 160f), PPU);
            Sheet(dir, "fx_voidglob", VoidFire(FireGlobFrames()), new Vector2(12f / 18f, 0.5f), PPU);
            Sheet(dir, "fx_voidfire", VoidFire(GroundFireFrames()), new Vector2(0.5f, 0f), PPU);

            // crimson flask
            Single(dir, "fx_flask", FlaskSprite(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_flask", FlaskIcon(), new Vector2(0.5f, 0.5f), PPU);

            // sword: smears for the 3 slashes, the heavy stab
            Single(dir, "fx_smear_diagonal", SmearDiagonal(), new Vector2(44f / 112f, 56f / 112f), PPU);
            Single(dir, "fx_smear_rising", SmearRising(), new Vector2(44f / 112f, 56f / 112f), PPU);
            Single(dir, "fx_smear_cleave", SmearCleave(), new Vector2(56f / 144f, 66f / 144f), PPU);
            Single(dir, "fx_thrust", ThrustStreak(), new Vector2(0f, 0.5f), PPU);
            Single(dir, "fx_star", ImpactStar(), new Vector2(0.5f, 0.5f), PPU);

            // other skills
            Single(dir, "fx_overhead", OverheadArc(), new Vector2(48f / 128f, 44f / 128f), PPU);
            Single(dir, "fx_cutline", CutLine(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_magiccircle", MagicCircle(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_crack", GroundCrack(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_scorch", Scorch(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "fx_zap", ZapFrames(), new Vector2(0f, 0.5f), PPU);
            Sheet(dir, "fx_orb", OrbFrames(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_shockring", ShockRing(), new Vector2(0.5f, 0.5f), PPU);
            Sheet(dir, "fx_bolt", BoltFrames(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "icon_overhead", IconOverhead(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_thousandcuts", IconThousandCuts(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_orbs", IconOrbs(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_thunder", IconThunder(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_thunderclap", IconThunderclap(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "icon_riposte", IconRiposte(), new Vector2(0.5f, 0.5f), PPU);
            // the ultimate
            Single(dir, "icon_heavenfall", IconHeavenfall(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_holyblade", HolyBlade(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_moon", MoonCrescent(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_lightspear", LightSpear(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_lightpillar", LightPillar(), new Vector2(0.5f, 0f), PPU, FilterMode.Bilinear);

            // effects
            Single(dir, "fx_exclaim", Exclaim(), new Vector2(0.5f, 0f), PPU);
            Single(dir, "fx_slash", Slash(), new Vector2(0.35f, 0.5f), PPU);
            Single(dir, "fx_ring", Ring(), new Vector2(0.5f, 0.5f), PPU);
            Single(dir, "fx_glow", SoftCircle(64, 1.8f), new Vector2(0.5f, 0.5f), 64f, FilterMode.Bilinear);
            Single(dir, "fx_fog", SoftEllipse(128, 48), new Vector2(0.5f, 0.5f), 16f, FilterMode.Bilinear);
            Single(dir, "fx_shaft", Shaft(), new Vector2(0.5f, 1f), 32f, FilterMode.Bilinear);
            Single(dir, "fx_vignette", Vignette(), new Vector2(0.5f, 0.5f), 100f, FilterMode.Bilinear);

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
