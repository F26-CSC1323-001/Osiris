using System;
using System.Linq;
using PixelStudy.Arena;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// The arena maps. Normal floors pick one of the first four at random (GameSession.Maps); each boss
    /// has its own arena built around its moves (GameSession.BossMaps). Every map is a scene built by
    /// ArenaBuilder.BuildMap from one of these definitions: solid cells and one-way platforms (cell
    /// coordinates, y up, the floor's top is y = 3), a colour theme, its own props, and spawn points.
    /// </summary>
    public static partial class ArenaBuilder
    {
        class Theme
        {
            public Color Back = new Color(0.82f, 0.88f, 0.95f);
            public Color Pillar = new Color(0.9f, 0.95f, 1f);
            public Color Ground = Color.white;
            public Color Platform = Color.white;
            public string SkySprite = "bg_far";
            public Color Sky = Color.white;
            public Color Ruins = new Color(0.85f, 0.95f, 1f);   // alpha 0 hides the ruins layer
            public Color Fog = new Color(0.45f, 0.75f, 0.8f, 0.12f);
            public Color CameraBg = ArenaArt.Hex("#070D16");
            public Color DustA = new Color(0.7f, 0.9f, 0.95f, 0.35f);
            public Color DustB = new Color(1f, 0.9f, 0.7f, 0.25f);
            public float DustRate = 9f;
            public Vector2 DustSize = new Vector2(0.03f, 0.07f);
            public Vector2 DustVelocity = new Vector2(0.15f, 0.1f);
            public float DustNoise = 0.3f;
            public bool Grass = true;
            public Color GrassTint = Color.white;
        }

        class MapDef
        {
            public string Scene, Title;
            public int W = 64, H = 26;
            public int BossIndex = -1;
            public Action<bool[,]> Solid, Platforms;
            /// <summary>Highest back-wall row in each column (open sky above it).</summary>
            public Func<int, int> BackTop;
            public (int x, int top, bool capped)[] Pillars = new (int, int, bool)[0];
            public Vector2 PlayerStart, Door, BossSpawn;
            public Vector2[] Ground = new Vector2[0], Air = new Vector2[0], BossProps = new Vector2[0];
            public Rect FlyBounds;
            public Theme Theme = new Theme();
            public Action<Transform, Mats, bool[,]> Decor;
            /// <summary>Runs after the level is built, with its tilemaps: ground, back wall, pillars (the throne room needs them).</summary>
            public Action<Tilemap, Tilemap, Tilemap, Transform, Mats> Setup;
        }

        // Mockup 2: the four normal maps and the arenas of its three bosses (floors 10, 20, 30)
        static MapDef[] AllMaps() => new[] { RuinedHall(), SunkenCrypt(), SkyRamparts(), EmberForge(), BroodNest(), HornetHive(), ObsidianShore() };

        static Vector2[] Pts(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return points;
        }

        static void Ledge(bool[,] plat, int x0, int x1, int y) => Fill(plat, x0, y, x1 - x0 + 1, 1);

        // =====================================================================
        // Normal maps
        // =====================================================================

        /// <summary>The original arena: a ruined hall with a broken roof, open in the middle to the night sky.</summary>
        static MapDef RuinedHall() => new MapDef
        {
            Scene = "Arena",
            Title = "Ruined Hall",
            Solid = s =>
            {
                Fill(s, 2, 3, 6, 2);          // left step (start)
                Fill(s, 55, 3, 7, 2);         // right step (exit)
                Fill(s, 28, 3, 8, 1);         // centre mound
                Fill(s, 2, 24, 13, 2);        // broken ceiling, left
                Fill(s, 49, 24, 13, 2);       // broken ceiling, right
                Fill(s, 2, 23, 4, 1);
                Fill(s, 11, 23, 3, 1);
                Fill(s, 58, 23, 4, 1);
            },
            Platforms = p =>
            {
                Ledge(p, 10, 17, 6); Ledge(p, 46, 53, 6);    // tier 1
                Ledge(p, 20, 27, 10); Ledge(p, 36, 43, 10);  // tier 2
                Ledge(p, 28, 35, 14);                        // tier 3 (centre)
                Ledge(p, 3, 8, 11); Ledge(p, 55, 60, 11);    // side balconies
                Ledge(p, 20, 26, 18); Ledge(p, 37, 43, 18);  // tier 4
            },
            BackTop = x => x <= 14 || x >= 49 ? 24 : 9 + Mathf.FloorToInt(Noise(x, 4) * 6f),
            Pillars = new[] { (9, 23, false), (32, 21, true), (54, 23, false) },
            PlayerStart = new Vector2(5f, 5.05f),
            Door = new Vector2(58.5f, 5f),
            BossSpawn = new Vector2(38f, 3.1f),
            Ground = Pts(13.5f, 7.05f, 49.5f, 7.05f, 24f, 11.05f, 40f, 11.05f, 31.5f, 15.05f, 20f, 3.05f, 44f, 3.05f, 31.5f, 4.05f, 5.5f, 12.05f, 58f, 12.05f),
            Air = Pts(16f, 10f, 31.5f, 20f, 48f, 10f, 24f, 16f, 40f, 16f, 8f, 18f, 56f, 18f),
            FlyBounds = new Rect(2.6f, 3.6f, 58.8f, 20f),
            Decor = (decor, m, solid) =>
            {
                foreach (var (x, y) in new[] { (18f, 3f), (45f, 3f) })
                {
                    Deco(decor, "Window", "prop_window", new Vector3(x, y + 0.6f), -50, Color.white, 1f);
                    AddGlow(decor, m, new Color(0.5f, 0.85f, 0.9f, 0.22f), 7f, -49, new Vector3(x, y + 2f));
                }
                foreach (var (x, angle) in new[] { (23f, -16f), (31.5f, 4f), (41f, 18f) })
                    Shaft(decor, m, x, 25.5f, angle, new Color(0.55f, 0.85f, 0.9f, 0.09f));
                foreach (var x in new[] { 9.5f, 54.5f }) Banner(decor, x, 20f);
                foreach (var (x, top, len) in new[] { (4.5f, 23f, 4f), (8.5f, 24f, 6.5f), (12.5f, 23f, 3f), (51.5f, 24f, 5f), (56.5f, 24f, 3.5f), (60.5f, 23f, 6f) })
                    Chain(decor, x, top, len, new Color(0.85f, 0.9f, 1f));
                foreach (var (x, y) in new[] { (11.5f, 9f), (24f, 6f), (31.5f, 9.5f), (40f, 6f), (52.5f, 9f), (6f, 16f), (57.5f, 16f), (31.5f, 19f) })
                    Torch(decor, m, new Vector3(x, y));
                foreach (var (x, y) in new[] { (3.5f, 5f), (24.5f, 3f), (47f, 3f), (36.5f, 4f) })
                    Deco(decor, "Bones", "prop_bones", new Vector3(x, y), 4, Color.white, 1f);
            },
        };

        /// <summary>A sealed crypt: low ceiling, a raised dais in the middle, green candlelight and cobwebs.</summary>
        static MapDef SunkenCrypt() => new MapDef
        {
            Scene = "Arena_Crypt",
            Title = "Sunken Crypt",
            Solid = s =>
            {
                Fill(s, 2, 21, 60, 5);        // ceiling
                Fill(s, 2, 20, 5, 1); Fill(s, 57, 20, 5, 1); Fill(s, 29, 20, 6, 1); // arches
                Fill(s, 16, 16, 2, 4); Fill(s, 46, 16, 2, 4);                       // hanging pillars
                Fill(s, 2, 3, 6, 2);          // start step
                Fill(s, 55, 3, 7, 2);         // exit step
                Fill(s, 26, 3, 12, 3);        // the dais
                Fill(s, 24, 3, 2, 1); Fill(s, 38, 3, 2, 1);                         // its steps
            },
            Platforms = p =>
            {
                Ledge(p, 9, 14, 8); Ledge(p, 49, 54, 8);
                Ledge(p, 18, 23, 11); Ledge(p, 40, 45, 11);
                Ledge(p, 28, 35, 12);
                Ledge(p, 3, 8, 13); Ledge(p, 55, 60, 13);
                Ledge(p, 20, 27, 16); Ledge(p, 36, 43, 16);
            },
            BackTop = x => 20,
            Pillars = new[] { (12, 20, false), (32, 19, false), (51, 20, false) },
            PlayerStart = new Vector2(5f, 5.05f),
            Door = new Vector2(58.5f, 5f),
            BossSpawn = new Vector2(31.5f, 6.1f),
            Ground = Pts(12f, 9.05f, 51.5f, 9.05f, 21f, 12.05f, 42.5f, 12.05f, 31.5f, 13.05f, 20f, 3.05f, 44f, 3.05f, 31.5f, 6.05f, 5.5f, 14.05f, 57.5f, 14.05f, 23.5f, 17.05f, 39.5f, 17.05f),
            Air = Pts(16f, 13f, 48f, 13f, 31.5f, 17f, 24f, 8f, 40f, 8f, 8f, 17f, 56f, 17f),
            FlyBounds = new Rect(2.6f, 3.6f, 58.8f, 15.8f),
            Theme = new Theme
            {
                Back = new Color(0.6f, 0.76f, 0.7f),
                Pillar = new Color(0.68f, 0.84f, 0.76f),
                Ground = new Color(0.8f, 0.96f, 0.86f),
                Platform = new Color(0.84f, 1f, 0.9f),
                Ruins = new Color(0f, 0f, 0f, 0f),
                Fog = new Color(0.35f, 0.9f, 0.55f, 0.12f),
                CameraBg = ArenaArt.Hex("#050E0A"),
                DustA = new Color(0.5f, 1f, 0.6f, 0.35f),
                DustB = new Color(0.8f, 1f, 0.7f, 0.2f),
                DustVelocity = new Vector2(0.05f, 0.25f),
                GrassTint = new Color(0.7f, 0.9f, 0.75f),
            },
            Decor = (decor, m, solid) =>
            {
                var ghostFlame = new Color(0.35f, 1f, 0.55f, 0.3f);
                var ghostTint = new Color(0.55f, 1f, 0.65f);
                foreach (var (x, y) in new[] { (6f, 9f), (58f, 9f), (24f, 8f), (40f, 8f), (32f, 15.5f), (13f, 15f), (51f, 15f) })
                    Torch(decor, m, new Vector3(x, y), ghostFlame, ghostTint);
                foreach (var (x, y) in new[] { (13.5f, 3f), (21f, 3f), (42.5f, 3f), (50.5f, 3f), (32f, 6f) })
                    Deco(decor, "Tomb", "prop_tomb", new Vector3(x, y), 3, new Color(0.85f, 0.95f, 0.9f), 1f);
                Web(decor, new Vector2(2f, 21f), false);
                Web(decor, new Vector2(62f, 21f), true);
                Web(decor, new Vector2(18f, 16f), false);
                Web(decor, new Vector2(46f, 16f), true);
                foreach (var (x, top, len) in new[] { (9.5f, 21f, 4f), (26.5f, 21f, 3.5f), (37.5f, 21f, 5f), (55f, 21f, 3f) })
                    Chain(decor, x, top, len, new Color(0.7f, 0.9f, 0.8f));
                foreach (var (x, y) in new[] { (4f, 5f), (27.5f, 6f), (36f, 6f), (47f, 3f) })
                    Deco(decor, "Bones", "prop_bones", new Vector3(x, y), 4, new Color(0.85f, 0.95f, 0.9f), 1f);
                foreach (var (x, y) in new[] { (20f, 14f), (44f, 14f), (32f, 9f) })
                    AddGlow(decor, m, new Color(0.3f, 1f, 0.6f, 0.13f), 7f, -49, new Vector3(x, y));
            },
        };

        /// <summary>Castle ramparts at sunset: two towers joined by a long bridge, open sky above.</summary>
        static MapDef SkyRamparts() => new MapDef
        {
            Scene = "Arena_Ramparts",
            Title = "Sky Ramparts",
            H = 28,
            Solid = s =>
            {
                Fill(s, 2, 3, 6, 2);          // start step
                Fill(s, 55, 3, 7, 2);         // exit step
                Fill(s, 14, 3, 4, 9);         // left tower
                Fill(s, 46, 3, 4, 9);         // right tower
                Fill(s, 14, 12, 1, 1); Fill(s, 17, 12, 1, 1); Fill(s, 46, 12, 1, 1); Fill(s, 49, 12, 1, 1); // merlons
                Fill(s, 29, 3, 6, 2);         // low wall in the yard
            },
            Platforms = p =>
            {
                Ledge(p, 18, 45, 11);         // the bridge between the towers
                Ledge(p, 5, 10, 8); Ledge(p, 53, 58, 8);
                Ledge(p, 7, 11, 13); Ledge(p, 52, 56, 13);
                Ledge(p, 21, 27, 16); Ledge(p, 36, 42, 16);
                Ledge(p, 28, 35, 20);
                // Mockup 2 has no double jump: a step up to the left tower, and steps from the yard back up to the bridge
                Ledge(p, 12, 13, 11);
                Ledge(p, 24, 27, 7); Ledge(p, 36, 39, 7);
            },
            BackTop = x => x >= 13 && x <= 18 || x >= 45 && x <= 50 ? 13 : x <= 4 || x >= 59 ? 9 : 6 + Mathf.FloorToInt(Noise(x, 11) * 2.5f),
            PlayerStart = new Vector2(5f, 5.05f),
            Door = new Vector2(58.5f, 5f),
            BossSpawn = new Vector2(31.5f, 5.1f),
            Ground = Pts(15.5f, 12.05f, 47.5f, 12.05f, 31.5f, 12.05f, 24f, 3.05f, 40f, 3.05f, 31.5f, 5.05f, 7.5f, 9.05f, 55.5f, 9.05f, 24f, 17.05f, 39f, 17.05f, 31.5f, 21.05f),
            Air = Pts(12f, 17f, 52f, 17f, 31.5f, 24f, 24f, 21f, 40f, 21f, 20f, 7f, 44f, 7f),
            FlyBounds = new Rect(2.6f, 3.6f, 58.8f, 22f),
            Theme = new Theme
            {
                Back = new Color(0.92f, 0.76f, 0.7f),
                Pillar = new Color(1f, 0.84f, 0.74f),
                Ground = new Color(1f, 0.9f, 0.8f),
                Platform = new Color(1f, 0.88f, 0.78f),
                SkySprite = "bg_dusk",
                Ruins = new Color(0.55f, 0.32f, 0.48f),
                Fog = new Color(1f, 0.55f, 0.55f, 0.1f),
                CameraBg = ArenaArt.Hex("#160C2A"),
                DustA = new Color(1f, 0.8f, 0.6f, 0.35f),
                DustB = new Color(1f, 0.6f, 0.7f, 0.25f),
                DustVelocity = new Vector2(0.7f, 0.05f),
                DustNoise = 0.5f,
                GrassTint = new Color(0.95f, 0.85f, 0.6f),
            },
            Decor = (decor, m, solid) =>
            {
                Banner(decor, 15.5f, 11.5f);
                Banner(decor, 47.5f, 11.5f);
                Banner(decor, 3.5f, 9f);
                Banner(decor, 60.5f, 9f);
                var brazier = new Color(1f, 0.6f, 0.25f, 0.34f);
                foreach (var (x, y) in new[] { (13.5f, 8f), (18.5f, 8f), (45.5f, 8f), (50.5f, 8f), (9f, 6f), (55f, 6f), (31.5f, 6.5f) })
                    Torch(decor, m, new Vector3(x, y), brazier, Color.white);
                // clouds drifting across the sunset
                for (int i = 0; i < 6; i++)
                {
                    var cloud = Deco(decor, "Cloud", "fx_fog", new Vector3(6f + i * 11f, 17f + (i % 3) * 3f), -85, new Color(1f, 0.65f, 0.7f, 0.16f), 1f);
                    cloud.transform.localScale = new Vector3(3f + (i % 2), 1.1f, 1f);
                    cloud.sharedMaterial = m.additive;
                    var drift = cloud.gameObject.AddComponent<Drift>();
                    drift.amplitude = new Vector2(4f, 0.2f);
                    drift.speed = 0.05f + i * 0.015f;
                }
                AddGlow(decor, m, new Color(1f, 0.6f, 0.35f, 0.18f), 18f, -90, new Vector3(20f, 10f));
            },
        };

        /// <summary>A forge in the deep: stepped plateaus, molten falls glowing behind, braziers and embers.</summary>
        static MapDef EmberForge() => new MapDef
        {
            Scene = "Arena_Forge",
            Title = "Ember Forge",
            Solid = s =>
            {
                Fill(s, 2, 3, 13, 4);         // left plateau (start)
                Fill(s, 15, 3, 2, 3); Fill(s, 17, 3, 2, 2); Fill(s, 19, 3, 2, 1);   // stairs down
                Fill(s, 49, 3, 13, 4);        // right plateau (exit)
                Fill(s, 47, 3, 2, 3); Fill(s, 45, 3, 2, 2); Fill(s, 43, 3, 2, 1);
                Fill(s, 30, 3, 4, 1);         // the great anvil
                Fill(s, 2, 23, 22, 3); Fill(s, 42, 23, 20, 3);                      // roof, open over the chimney
            },
            Platforms = p =>
            {
                Ledge(p, 22, 28, 8); Ledge(p, 36, 42, 8);
                Ledge(p, 28, 36, 12);
                Ledge(p, 4, 10, 11); Ledge(p, 54, 60, 11);
                Ledge(p, 15, 21, 15); Ledge(p, 43, 49, 15);
                Ledge(p, 26, 38, 18);
            },
            BackTop = x => x >= 24 && x <= 41 ? 23 : 22,
            Pillars = new[] { (13, 22, false), (51, 22, false) },
            PlayerStart = new Vector2(5f, 7.05f),
            Door = new Vector2(58.5f, 7f),
            BossSpawn = new Vector2(32f, 4.1f),
            Ground = Pts(8f, 7.05f, 56f, 7.05f, 25f, 9.05f, 39f, 9.05f, 32f, 13.05f, 7f, 12.05f, 57f, 12.05f, 18f, 16.05f, 46f, 16.05f, 32f, 19.05f, 25f, 3.05f, 39f, 3.05f),
            Air = Pts(16f, 11f, 48f, 11f, 32f, 22f, 24f, 15f, 40f, 15f, 10f, 18f, 54f, 18f),
            FlyBounds = new Rect(2.6f, 3.6f, 58.8f, 18f),
            Theme = new Theme
            {
                Back = new Color(0.78f, 0.54f, 0.48f),
                Pillar = new Color(0.85f, 0.6f, 0.5f),
                Ground = new Color(1f, 0.8f, 0.7f),
                Platform = new Color(1f, 0.8f, 0.68f),
                Ruins = new Color(0f, 0f, 0f, 0f),
                Fog = new Color(1f, 0.4f, 0.15f, 0.1f),
                CameraBg = ArenaArt.Hex("#120706"),
                DustA = new Color(1f, 0.6f, 0.25f, 0.6f),
                DustB = new Color(1f, 0.85f, 0.4f, 0.4f),
                DustRate = 14f,
                DustSize = new Vector2(0.03f, 0.06f),
                DustVelocity = new Vector2(0.1f, 0.9f),
                Grass = false,
            },
            Decor = (decor, m, solid) =>
            {
                // molten falls pouring down the back wall
                foreach (var x in new[] { 26f, 38f })
                {
                    var fall = Shaft(decor, m, x, 23f, 180f, new Color(1f, 0.45f, 0.1f, 0.3f));
                    fall.transform.localScale = new Vector3(1.6f, 5.2f, 1f);
                    fall.transform.position = new Vector3(x, 3f);
                    AddGlow(decor, m, new Color(1f, 0.45f, 0.12f, 0.35f), 7f, -44, new Vector3(x, 4f));
                }
                var brazier = new Color(1f, 0.4f, 0.12f, 0.36f);
                foreach (var (x, y) in new[] { (4f, 10f), (60f, 10f), (22f, 6f), (42f, 6f), (32f, 16f), (12f, 15f), (52f, 15f) })
                    Torch(decor, m, new Vector3(x, y), brazier, new Color(1f, 0.85f, 0.7f));
                foreach (var (x, top, len) in new[] { (24.5f, 23f, 4f), (30.5f, 23f, 7f), (33.5f, 23f, 6f), (39.5f, 23f, 4f) })
                    Chain(decor, x, top, len, new Color(1f, 0.75f, 0.6f));
                // glowing cracks in the forge floor
                foreach (var (x, y) in new[] { (25f, 3f), (39f, 3f), (32f, 4f), (9f, 7f), (55f, 7f) })
                {
                    var crack = Deco(decor, "Embers", "fx_crack", new Vector3(x, y), 4, new Color(1f, 0.55f, 0.2f), 1f);
                    crack.sharedMaterial = m.additive;
                }
                AddGlow(decor, m, new Color(1f, 0.35f, 0.1f, 0.12f), 22f, -48, new Vector3(32f, 6f));
            },
        };

        // =====================================================================
        // Boss arenas
        // =====================================================================

        /// <summary>The Brood Mother's Nest: a long low cave with a flat floor to charge across, egg clutches
        /// (where her brood hatches) and a ceiling full of rocks to shake loose.</summary>
        static MapDef BroodNest() => new MapDef
        {
            Scene = "Boss_Nest",
            Title = "The Nest",
            BossIndex = 0,
            H = 22,
            Solid = s =>
            {
                Fill(s, 2, 18, 60, 4);        // cave ceiling
                Fill(s, 9, 17, 2, 1); Fill(s, 20, 16, 1, 2); Fill(s, 21, 17, 1, 1); Fill(s, 33, 17, 2, 1);
                Fill(s, 43, 16, 1, 2); Fill(s, 44, 17, 1, 1); Fill(s, 53, 17, 2, 1); // rock teeth
                Fill(s, 2, 3, 5, 1);          // start ledge
                Fill(s, 57, 3, 5, 1);         // exit ledge
            },
            Platforms = p =>
            {
                Ledge(p, 9, 14, 7); Ledge(p, 49, 54, 7);
                Ledge(p, 27, 36, 10);
                Ledge(p, 17, 22, 13); Ledge(p, 41, 46, 13);
            },
            BackTop = x => 17,
            PlayerStart = new Vector2(4.5f, 4.05f),
            Door = new Vector2(59.5f, 4f),
            BossSpawn = new Vector2(40f, 3.1f),
            BossProps = Pts(11f, 3f, 22.5f, 3f, 31.5f, 11f, 43f, 3f, 53f, 3f),
            Ground = Pts(10f, 3.05f, 20f, 3.05f, 32f, 3.05f, 44f, 3.05f, 54f, 3.05f),
            Air = Pts(16f, 9f, 32f, 14f, 48f, 9f),
            FlyBounds = new Rect(2.6f, 3.6f, 58.8f, 13f),
            Theme = new Theme
            {
                Back = new Color(0.6f, 0.48f, 0.72f),
                Pillar = new Color(0.7f, 0.55f, 0.8f),
                Ground = new Color(0.85f, 0.72f, 0.95f),
                Platform = new Color(0.85f, 0.75f, 0.95f),
                Ruins = new Color(0f, 0f, 0f, 0f),
                Fog = new Color(0.6f, 0.35f, 0.9f, 0.14f),
                CameraBg = ArenaArt.Hex("#0B0612"),
                DustA = new Color(0.7f, 1f, 0.5f, 0.4f),
                DustB = new Color(0.8f, 0.5f, 1f, 0.3f),
                DustVelocity = new Vector2(0.05f, 0.2f),
                GrassTint = new Color(0.6f, 0.5f, 0.85f),
            },
            Decor = (decor, m, solid) =>
            {
                // the egg clutches her brood hatches from
                foreach (var (x, y) in new[] { (11f, 3f), (22.5f, 3f), (31.5f, 11f), (43f, 3f), (53f, 3f) })
                {
                    Deco(decor, "Eggs", "prop_eggs", new Vector3(x, y), 6, Color.white, 1.15f);
                    var glow = AddGlow(decor, m, new Color(0.6f, 1f, 0.35f, 0.25f), 3.5f, 5, new Vector3(x, y + 0.5f));
                    var pulse = glow.gameObject.AddComponent<GlowFlicker>();
                    pulse.glow = glow;
                    pulse.baseAlpha = 0.25f;
                    pulse.speed = 1.5f;
                }
                foreach (var (x, y) in new[] { (5f, 4f), (17f, 3f), (36.5f, 3f), (48f, 3f), (59f, 4f), (12f, 8f), (51.5f, 8f) })
                {
                    Deco(decor, "Mushrooms", "prop_mushrooms", new Vector3(x, y), 5, Color.white, 1f);
                    AddGlow(decor, m, new Color(0.65f, 0.4f, 1f, 0.3f), 2.6f, 4, new Vector3(x, y + 0.4f));
                }
                // stalactites hanging from the cave roof
                for (int x = 4; x < 60; x += 3)
                {
                    int y = 18;
                    while (y > 0 && solid[x, y - 1]) y--;
                    float len = 0.7f + Noise(x, 31) * 0.9f;
                    var st = Deco(decor, "Stalactite", "prop_stalactite", new Vector3(x + 0.5f, y), -20, new Color(0.85f, 0.8f, 0.95f), 1f);
                    st.transform.localScale = new Vector3(0.9f + Noise(x, 32) * 0.4f, len, 1f);
                }
                Web(decor, new Vector2(2f, 18f), false);
                Web(decor, new Vector2(62f, 18f), true);
                foreach (var (x, y) in new[] { (7.5f, 3f), (26f, 3f), (38f, 3f), (56f, 4f) })
                    Deco(decor, "Bones", "prop_bones", new Vector3(x, y), 4, new Color(0.9f, 0.85f, 1f), 1f);
                foreach (var x in new[] { 12f, 32f, 52f })
                    AddGlow(decor, m, new Color(0.55f, 0.3f, 1f, 0.16f), 10f, -49, new Vector3(x, 11f));
            },
        };

        /// <summary>The Hornet Queen's Hive: a tall golden shaft of honeycomb with floating combs to climb, so you can
        /// reach her in the air and she has room to dive and lance.</summary>
        static MapDef HornetHive() => new MapDef
        {
            Scene = "Boss_Hive",
            Title = "The Hive",
            BossIndex = 1,
            W = 56,
            H = 34,
            Solid = s =>
            {
                Fill(s, 2, 31, 52, 3);        // ceiling
                Fill(s, 2, 3, 5, 1);          // start ledge
                Fill(s, 49, 3, 5, 1);         // exit ledge
                Fill(s, 2, 10, 2, 2); Fill(s, 52, 14, 2, 2); Fill(s, 2, 20, 2, 2); Fill(s, 52, 24, 2, 2); // comb lumps on the walls
            },
            Platforms = p =>
            {
                Ledge(p, 8, 13, 7); Ledge(p, 42, 47, 7);
                Ledge(p, 22, 33, 9);
                Ledge(p, 4, 9, 13); Ledge(p, 46, 51, 13);
                Ledge(p, 16, 22, 15); Ledge(p, 33, 39, 15);
                Ledge(p, 25, 30, 19);
                Ledge(p, 8, 14, 21); Ledge(p, 41, 47, 21);
                Ledge(p, 18, 24, 25); Ledge(p, 31, 37, 25);
            },
            BackTop = x => 30,
            PlayerStart = new Vector2(4.5f, 4.05f),
            Door = new Vector2(51.5f, 4f),
            BossSpawn = new Vector2(28f, 13f),
            Ground = Pts(10f, 8.05f, 45f, 8.05f, 27.5f, 10.05f, 19f, 16.05f, 36f, 16.05f, 27.5f, 20.05f, 11f, 22.05f, 44f, 22.05f),
            Air = Pts(14f, 12f, 42f, 12f, 28f, 24f, 20f, 28f, 36f, 28f),
            FlyBounds = new Rect(2.6f, 3.6f, 50.8f, 25.8f),
            Theme = new Theme
            {
                Back = new Color(0.95f, 0.74f, 0.42f),
                Pillar = new Color(1f, 0.8f, 0.5f),
                Ground = new Color(1f, 0.85f, 0.55f),
                Platform = new Color(1f, 0.82f, 0.5f),
                Ruins = new Color(0f, 0f, 0f, 0f),
                Fog = new Color(1f, 0.75f, 0.3f, 0.12f),
                CameraBg = ArenaArt.Hex("#120C04"),
                DustA = new Color(1f, 0.85f, 0.35f, 0.5f),
                DustB = new Color(1f, 1f, 0.7f, 0.3f),
                DustVelocity = new Vector2(0.1f, -0.15f),
                GrassTint = new Color(1f, 0.85f, 0.4f),
            },
            Decor = (decor, m, solid) =>
            {
                foreach (var (x, y, scale) in new[] { (10f, 8f, 1.8f), (30f, 5.5f, 2.2f), (46f, 10f, 1.6f), (20f, 18f, 2f), (38f, 21f, 1.8f), (8f, 26f, 1.6f), (48f, 27f, 1.9f), (28f, 27.5f, 1.5f), (16f, 12f, 1.3f) })
                    Deco(decor, "Honeycomb", "prop_honeycomb", new Vector3(x, y), -50, Color.white, scale);
                foreach (var x in new[] { 12f, 27.5f, 43f })
                {
                    Deco(decor, "Hive", "prop_hive", new Vector3(x, 31f), -30, Color.white, 1.4f);
                    AddGlow(decor, m, new Color(1f, 0.7f, 0.25f, 0.2f), 5f, -31, new Vector3(x, 29.5f));
                }
                foreach (var (x, angle) in new[] { (20f, -10f), (36f, 12f) })
                    Shaft(decor, m, x, 33.5f, angle, new Color(1f, 0.85f, 0.45f, 0.1f)).transform.localScale = new Vector3(3.2f, 8f, 1f);
                // honey dripping from the combs
                foreach (var (x, y) in new[] { (10.5f, 6.8f), (27f, 8.8f), (44f, 6.8f), (19f, 14.8f), (36f, 14.8f), (11f, 20.8f), (44f, 20.8f) })
                {
                    var drip = AddGlow(decor, m, new Color(1f, 0.7f, 0.2f, 0.4f), 0.8f, 3, new Vector3(x, y));
                    drip.transform.localScale = new Vector3(0.35f, 0.9f, 1f);
                }
                foreach (var (x, y) in new[] { (14f, 10f), (42f, 10f), (28f, 16f), (28f, 26f) })
                    AddGlow(decor, m, new Color(1f, 0.7f, 0.25f, 0.14f), 9f, -49, new Vector3(x, y));
            },
        };

        /// <summary>The Obsidian Crab's Shore: a dark volcanic cave with a long flat floor to rush across, low ledges
        /// to get up out of its way (one jump), black glass rocks and lava glowing through the cracks.</summary>
        static MapDef ObsidianShore() => new MapDef
        {
            Scene = "Boss_Crab",
            Title = "Obsidian Shore",
            BossIndex = 2,
            W = 60,
            H = 22,
            Solid = s =>
            {
                Fill(s, 2, 19, 56, 3);        // cave roof
                Fill(s, 2, 18, 6, 1); Fill(s, 27, 18, 6, 1); Fill(s, 52, 18, 6, 1); // roof teeth
                Fill(s, 2, 3, 8, 2);          // left terrace (start)
                Fill(s, 50, 3, 8, 2);         // right terrace (exit)
            },
            Platforms = p =>
            {
                // all within one jump: get up out of the Scuttle Rush and the shockwaves
                Ledge(p, 14, 21, 6); Ledge(p, 38, 45, 6);
                Ledge(p, 25, 34, 9);
            },
            BackTop = x => 17,
            Pillars = new[] { (11, 17, false), (48, 17, false) },
            PlayerStart = new Vector2(5f, 5.05f),
            Door = new Vector2(55f, 5f),
            BossSpawn = new Vector2(40f, 3.1f),
            Ground = Pts(15f, 3.05f, 30f, 3.05f, 45f, 3.05f, 17f, 7.05f, 42f, 7.05f, 29.5f, 10.05f),
            Air = Pts(20f, 12f, 40f, 12f, 30f, 15f),
            FlyBounds = new Rect(2.6f, 3.6f, 54.8f, 14f),
            Theme = new Theme
            {
                Back = new Color(0.5f, 0.42f, 0.48f),
                Pillar = new Color(0.6f, 0.48f, 0.52f),
                Ground = new Color(0.7f, 0.62f, 0.68f),
                Platform = new Color(0.8f, 0.68f, 0.66f),
                Ruins = new Color(0f, 0f, 0f, 0f),
                Fog = new Color(1f, 0.4f, 0.15f, 0.1f),
                CameraBg = ArenaArt.Hex("#0C0608"),
                DustA = new Color(1f, 0.55f, 0.25f, 0.6f),
                DustB = new Color(1f, 0.8f, 0.4f, 0.35f),
                DustRate = 12f,
                DustSize = new Vector2(0.03f, 0.06f),
                DustVelocity = new Vector2(0.1f, 0.8f),
                Grass = false,
            },
            Decor = (decor, m, solid) =>
            {
                // lava glowing through cracks in the floor
                foreach (var (x, y) in new[] { (14f, 3f), (22f, 3f), (30f, 3f), (37f, 3f), (45f, 3f), (6f, 5f), (54f, 5f) })
                {
                    var crack = Deco(decor, "Lava", "fx_crack", new Vector3(x, y), 4, new Color(1f, 0.5f, 0.2f), 1f);
                    crack.sharedMaterial = m.additive;
                }
                // molten falls pouring down the back of the cave
                foreach (var x in new[] { 20f, 40f })
                {
                    var fall = Shaft(decor, m, x, 19f, 180f, new Color(1f, 0.45f, 0.1f, 0.28f));
                    fall.transform.localScale = new Vector3(1.4f, 4.2f, 1f);
                    fall.transform.position = new Vector3(x, 3f);
                    AddGlow(decor, m, new Color(1f, 0.45f, 0.12f, 0.3f), 6f, -44, new Vector3(x, 4f));
                }
                var brazier = new Color(1f, 0.45f, 0.15f, 0.34f);
                foreach (var (x, y) in new[] { (6f, 8f), (54f, 8f), (17.5f, 9.5f), (41.5f, 9.5f), (29.5f, 12.5f) })
                    Torch(decor, m, new Vector3(x, y), brazier, new Color(1f, 0.85f, 0.7f));
                // black glass
                foreach (var x in new[] { 11.5f, 25f, 34.5f, 48f })
                    Deco(decor, "Obsidian", "fx_rock", new Vector3(x, 3.3f), 3, new Color(0.25f, 0.2f, 0.28f), 1.6f);
                foreach (var (x, y) in new[] { (4f, 5f), (56f, 5f) })
                    Deco(decor, "Bones", "prop_bones", new Vector3(x, y), 4, new Color(1f, 0.85f, 0.8f), 1f);
                AddGlow(decor, m, new Color(1f, 0.35f, 0.1f, 0.12f), 22f, -48, new Vector3(30f, 6f));
            },
        };

        // =====================================================================
        // Decor helpers
        // =====================================================================

        static SpriteRenderer Shaft(Transform parent, Mats m, float x, float top, float angle, Color color)
        {
            var shaft = Deco(parent, "LightShaft", "fx_shaft", new Vector3(x, top), -45, color, 1f);
            shaft.sharedMaterial = m.additive;
            shaft.transform.localScale = new Vector3(3.2f, 5.5f, 1f);
            shaft.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var f = shaft.gameObject.AddComponent<GlowFlicker>();
            f.baseAlpha = color.a;
            f.alphaVariation = 0.5f;
            f.scaleVariation = 0f;
            f.speed = 0.4f;
            return shaft;
        }

        static void Banner(Transform parent, float x, float y)
        {
            var banner = Deco(parent, "Banner", "prop_banner", new Vector3(x, y), -47, Color.white, 1f);
            var sway = banner.gameObject.AddComponent<Drift>();
            sway.amplitude = Vector2.zero;
            sway.swayDegrees = 1.5f;
            sway.speed = 0.6f;
        }

        static void Chain(Transform parent, float x, float top, float length, Color tint)
        {
            var chain = Deco(parent, "Chain", "prop_chain", new Vector3(x, top), -48, tint, 1f);
            chain.drawMode = SpriteDrawMode.Tiled;
            chain.size = new Vector2(0.25f, length);
            var sway = chain.gameObject.AddComponent<Drift>();
            sway.amplitude = Vector2.zero;
            sway.swayDegrees = 1.2f;
            sway.speed = 0.5f;
        }

        static void Web(Transform parent, Vector2 corner, bool mirrored)
        {
            // anchored at its top corner; mirrored it hangs to the left of the corner instead
            var web = Deco(parent, "Web", "prop_web", corner, -25, new Color(1f, 1f, 1f, 0.7f), 1.2f);
            web.flipX = mirrored;
        }
    }
}
