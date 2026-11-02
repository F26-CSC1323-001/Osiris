using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Draws the small pixel-art sprites the UI uses (9-slice frames + icons) and saves them as PNGs.
    /// Frames are grayscale so an Image's color tints them.
    /// </summary>
    public static class PixelArt
    {
        // 25 pixels per unit on a 100 PPU canvas = every texel is drawn 4x4 screen pixels.
        const float FramePPU = 25f;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Ink = new Color32(14, 10, 22, 255);

        public static void GenerateAll(string dir)
        {
            Frame(dir, "px_button", 16, 16, ButtonPixel, new Vector4(3, 4, 3, 3));
            Frame(dir, "px_panel", 16, 16, PanelPixel, new Vector4(4, 4, 4, 4));
            Frame(dir, "px_outline", 16, 16, OutlinePixel, new Vector4(4, 4, 4, 4));
            Frame(dir, "px_bar", 8, 8, BarPixel, new Vector4(2, 2, 2, 2));
            Frame(dir, "px_white", 4, 4, (x, y, w, h) => Gray(1f), Vector4.zero);
            Frame(dir, "px_ramp", 4, 32, RampPixel, Vector4.zero);

            Icon(dir, "icon_heart", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['r'] = C(229, 72, 77), ['w'] = C(255, 214, 214), ['d'] = C(158, 36, 52),
            },
                "................",
                "................",
                "..kkkk....kkkk..",
                ".krrrrk..krrrrk.",
                "krrwwrrkkrrrrrrk",
                "krwwrrrrrrrrrrrk",
                "krwrrrrrrrrrrrrk",
                "krrrrrrrrrrrrrdk",
                ".krrrrrrrrrrrdk.",
                "..krrrrrrrrrdk..",
                "...krrrrrrrdk...",
                "....krrrrrdk....",
                ".....krrrdk.....",
                "......krdk......",
                ".......kk.......",
                "................");

            Icon(dir, "icon_coin", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['y'] = C(242, 193, 78), ['o'] = C(176, 120, 32), ['w'] = C(255, 244, 190),
            },
                "................",
                ".....kkkkkk.....",
                "...kkyyyyyykk...",
                "..kyyywwyyyyyk..",
                "..kyywyyyyyyok..",
                ".kyywyyoyyyyyok.",
                ".kyyyyyoyyyyyok.",
                ".kyyyyyoyyyyyok.",
                ".kyyyyyoyyyyyok.",
                ".kyyyyyoyyyyyok.",
                ".kyyyyyyyyyyook.",
                "..kyyyyyyyyyok..",
                "..kyyyyyyyyook..",
                "...kkooooookk...",
                ".....kkkkkk.....",
                "................");

            Icon(dir, "icon_gem", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['c'] = C(94, 200, 242), ['w'] = C(225, 250, 255), ['d'] = C(38, 120, 180),
            },
                "................",
                "................",
                "....kkkkkkkk....",
                "...kwwcccccdk...",
                "..kwccccccccdk..",
                ".kcccccccccccdk.",
                ".kddddddddddddk.",
                "..kcccccccccdk..",
                "...kccccccddk...",
                "....kcccccdk....",
                ".....kcccdk.....",
                "......kcdk......",
                ".......kk.......",
                "................",
                "................",
                "................");

            Icon(dir, "icon_sword", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['w'] = C(240, 240, 250), ['s'] = C(170, 180, 200), ['g'] = C(242, 193, 78), ['b'] = C(120, 72, 40),
            },
                ".......kk.......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "......kwsk......",
                "...kkkkkkkkkk...",
                "...kggggggggk...",
                "...kkkkbbkkkk...",
                "......kbbk......",
                "......kbbk......",
                "......kggk......",
                ".......kk.......");

            Icon(dir, "icon_shield", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['w'] = C(235, 240, 255), ['s'] = C(120, 140, 180), ['d'] = C(70, 85, 120), ['g'] = C(242, 193, 78),
            },
                "................",
                "..kkkkkkkkkkkk..",
                "..kwwsssssssdk..",
                "..kwssssssssdk..",
                "..kssssggsssdk..",
                "..kssggggggsdk..",
                "..kssssggsssdk..",
                "..kssssggsssdk..",
                "...ksssggssdk...",
                "....kssggsdk....",
                "....ksssssdk....",
                ".....ksssdk.....",
                "......ksdk......",
                ".......kk.......",
                "................",
                "................");

            Icon(dir, "icon_boot", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['b'] = C(150, 95, 55), ['w'] = C(210, 160, 110), ['d'] = C(90, 55, 30), ['f'] = C(240, 240, 250),
            },
                "................",
                "................",
                "....kkkkkk......",
                "....kbbwbk......",
                "..f.kbbbbk......",
                ".ff.kbbbbk......",
                "fff.kbbbbk......",
                "....kbbbbk......",
                "....kbbbbkk.....",
                "....kbbbbbbkk...",
                "....kbbbbbbbbk..",
                "....kbbbbbbbbk..",
                "....kddddddddk..",
                "....kkkkkkkkkk..",
                "................",
                "................");

            Icon(dir, "icon_skull", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['w'] = C(239, 230, 210),
            },
                "................",
                "................",
                ".....kkkkkk.....",
                "...kkwwwwwwkk...",
                "..kwwwwwwwwwwk..",
                "..kwwwwwwwwwwk..",
                "..kwkkkwwkkkwk..",
                "..kwkkkwwkkkwk..",
                "..kwwwwkkwwwwk..",
                "...kwwwwwwwwk...",
                "....kwkwwkwk....",
                "....kkkkkkkk....",
                "................",
                "................",
                "................",
                "................");

            Icon(dir, "icon_book", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['w'] = C(245, 238, 220), ['l'] = C(160, 150, 170), ['b'] = C(110, 80, 170),
            },
                "................",
                "................",
                "................",
                ".kkkkkk..kkkkkk.",
                "kwwwwwwkkwwwwwwk",
                "kwlllwwkkwllllwk",
                "kwwwwwwkkwwwwwwk",
                "kwllllwkkwlllwwk",
                "kwwwwwwkkwwwwwwk",
                "kwlllwwkkwllllwk",
                "kwwwwwwkkwwwwwwk",
                "kbbbbbbkkbbbbbbk",
                ".kkkkkkkkkkkkkk.",
                "................",
                "................",
                "................");

            Icon(dir, "icon_check", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['g'] = C(107, 209, 107),
            },
                "................",
                "................",
                "..............kk",
                ".............kgk",
                "............kggk",
                "...........kggk.",
                "kk........kggk..",
                "kgk......kggk...",
                "kggk....kggk....",
                ".kggk..kggk.....",
                "..kggkkggk......",
                "...kggggk.......",
                "....kggk........",
                ".....kk.........",
                "................",
                "................");

            Icon(dir, "icon_lock", new Dictionary<char, Color32>
            {
                ['k'] = Ink, ['g'] = C(200, 205, 215), ['d'] = C(120, 126, 140), ['y'] = C(242, 193, 78),
            },
                "................",
                ".....kkkkkk.....",
                "....kddddddk....",
                "...kdk....kdk...",
                "...kdk....kdk...",
                "...kdk....kdk...",
                "..kkkkkkkkkkkk..",
                "..kggggggggggk..",
                "..kgggggggggdk..",
                "..kggggkkggddk..",
                "..kggggkkggddk..",
                "..kgggggkgggdk..",
                "..kggggggggddk..",
                "..kdddddddddyk..",
                "..kkkkkkkkkkkk..",
                "................");

            // Tintable (white) arrow for menu cursors.
            Icon(dir, "icon_arrow", new Dictionary<char, Color32> { ['w'] = C(255, 255, 255) },
                "w.....",
                "ww....",
                "www...",
                "wwww..",
                "wwwww.",
                "wwww..",
                "www...",
                "ww....",
                "w.....");

            AssetDatabase.Refresh();
        }

        public static Sprite Load(string dir, string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

        // ---------- frame shapes (x, y from bottom-left) ----------

        static bool IsCorner(int x, int y, int w, int h) =>
            (x == 0 || x == w - 1) && (y == 0 || y == h - 1);

        static bool IsInnerCorner(int x, int y, int w, int h) =>
            (x == 1 || x == w - 2) && (y == 1 || y == h - 2);

        static Color32 ButtonPixel(int x, int y, int w, int h)
        {
            if (IsCorner(x, y, w, h)) return Clear;
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return Ink;
            if (y <= 2) return Gray(0.6f);          // bottom bevel
            if (y == h - 2 || x == 1) return Gray(1f); // top/left highlight
            if (x == w - 2) return Gray(0.78f);     // right shade
            return Gray(0.9f);
        }

        static Color32 PanelPixel(int x, int y, int w, int h)
        {
            if (IsCorner(x, y, w, h)) return Clear;
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1 || IsInnerCorner(x, y, w, h)) return Ink;
            if (x == 1 || y == 1 || x == w - 2 || y == h - 2) return Gray(1f);
            if (x == 2 || y == 2 || x == w - 3 || y == h - 3) return Gray(0.62f);
            return Gray(0.86f);
        }

        static Color32 OutlinePixel(int x, int y, int w, int h)
        {
            if (IsCorner(x, y, w, h)) return Clear;
            if (x <= 1 || y <= 1 || x >= w - 2 || y >= h - 2) return Gray(1f);
            return Clear;
        }

        static Color32 BarPixel(int x, int y, int w, int h)
        {
            if (IsCorner(x, y, w, h)) return Clear;
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return Ink;
            return Gray(1f);
        }

        // Banded vertical gradient: bright at the bottom, dark at the top.
        static Color32 RampPixel(int x, int y, int w, int h)
        {
            const int bands = 8;
            int band = Mathf.Min(bands - 1, y * bands / h);
            return Gray(Mathf.Lerp(1f, 0.32f, band / (float)(bands - 1)));
        }

        // ---------- writers ----------

        delegate Color32 PixelFn(int x, int y, int w, int h);

        static void Frame(string dir, string name, int w, int h, PixelFn fn, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = fn(x, y, w, h);
            tex.SetPixels32(pixels);
            Save(dir, name, tex, FramePPU, border);
        }

        static void Icon(string dir, string name, Dictionary<char, Color32> palette, params string[] rows)
        {
            int h = rows.Length;
            int w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int row = 0; row < h; row++)
            for (int x = 0; x < w; x++)
            {
                char c = x < rows[row].Length ? rows[row][x] : '.';
                pixels[(h - 1 - row) * w + x] = palette.TryGetValue(c, out var col) ? col : Clear;
            }
            tex.SetPixels32(pixels);
            Save(dir, name, tex, FramePPU, Vector4.zero);
        }

        static void Save(string dir, string name, Texture2D tex, float ppu, Vector4 border)
        {
            var path = $"{dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = ppu;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        static Color32 Gray(float v)
        {
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            return new Color32(b, b, b, 255);
        }

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
