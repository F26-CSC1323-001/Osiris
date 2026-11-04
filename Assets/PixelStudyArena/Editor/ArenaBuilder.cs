using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelStudy.Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Builds the Dead Cells-style arenas: every map in ArenaMaps (the random normal maps and the boss
    /// arenas, each its own scene in Assets/Scenes) plus their art, tiles and prefabs.
    /// Run from Tools > Pixel Study > Build Arena (or Build Everything). Re-running overwrites the
    /// scenes and the generated assets in Assets/PixelStudyArena.
    /// Mockup 2: Main_Dev's maps, bugs and arena, with a knight that walks, jumps once, swings the sword
    /// and has Flame Slash (no roll, shield, flask or other skills), simple versions of the Brood Mother and
    /// Hornet Queen, the group's own monsters and Obsidian Crab boss (ArenaMonsters.cs), and generated
    /// sound effects (ArenaSounds.cs).
    /// </summary>
    public static partial class ArenaBuilder
    {
        const string Root = "Assets/PixelStudyArena";
        const string ArtDir = Root + "/Art";
        const string KnightDir = ArtDir + "/Knight";
        const string TileDir = Root + "/Tiles";
        const string PrefabDir = Root + "/Prefabs";
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string GameUIPrefabPath = "Assets/PixelStudyUI/Prefabs/GameUI.prefab";
        const string TitleTextMaterial = "Assets/PixelStudyUI/Art/Text_Title.mat";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string UIArt = "Assets/PixelStudyUI/Art";

        // ------------------------------------------------------------------ menu

        [MenuItem("Tools/Pixel Study/Build Arena")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(ScenePath);
            EditorUtility.DisplayDialog("Pixel Study Arena", "Built Assets/Scenes/Arena.unity.\n\nPress Play in MainMenu (New Game) or straight from the Arena scene.", "OK");
        }

        [MenuItem("Tools/Pixel Study/Build Everything (UI + Arena)")]
        public static void BuildEverythingFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        }

        public static void BuildEverything()
        {
            PixelStudyUIBuilder.BuildAll();
            BuildAll();
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(KnightDir);
            Directory.CreateDirectory(TileDir);
            Directory.CreateDirectory(PrefabDir);

            EnsureLayers();
            ArenaArt.GenerateAll(ArtDir);
            ArenaSounds.GenerateAll();
            var knight = ImportKnight();
            var mats = CreateMaterials();
            var tiles = CreateTiles();
            var prefabs = CreatePrefabs(mats);
            AssetDatabase.SaveAssets();
            foreach (var map in AllMaps())
            {
                // opening a new scene unloads assets held only in memory: reload the tiles and materials for each map
                BuildMap(map, knight, CreateMaterials(), CreateTiles(), prefabs);
            }
            PixelStudyUIBuilder.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[PixelStudyArena] Built the arena maps and boss arenas.");
        }

        // ------------------------------------------------------------------ project settings

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            void Name(int index, string name)
            {
                var p = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
                else if (p.stringValue != name) Debug.LogWarning($"[PixelStudyArena] Layer {index} is '{p.stringValue}', expected '{name}'.");
            }
            Name(ArenaLayers.Ground, "Ground");
            Name(ArenaLayers.Platform, "Platform");
            Name(ArenaLayers.Player, "Player");
            Name(ArenaLayers.Enemy, "Enemy");
            Name(ArenaLayers.Pickup, "Pickup");
            tagManager.ApplyModifiedProperties();
            ArenaLayers.ConfigureCollisions();
        }

        // ------------------------------------------------------------------ knight

        static readonly (string file, string slug)[] KnightSheets =
        {
            ("Idle", "idle"), ("Run", "run"), ("Jump", "jump"), ("Attack 1", "attack1"), ("Attack 2", "attack2"),
            ("Attack 3", "attack3"), ("Defend", "defend"), ("Protect", "protect"), ("Hurt", "hurt"), ("Dead", "dead"),
            ("Run+Attack", "runattack"), ("Walk", "walk"),
        };

        /// <summary>
        /// Copies the group's knight sheets (originals untouched), slices them on a 128px grid and lines
        /// every frame up on the knight's body so animations don't jitter.
        /// </summary>
        static Dictionary<string, Sprite[]> ImportKnight()
        {
            var result = new Dictionary<string, Sprite[]>();
            foreach (var (file, slug) in KnightSheets)
            {
                var src = $"Assets/Sprites/Knight_1/{file}.png";
                if (!File.Exists(src))
                {
                    Debug.LogWarning($"[PixelStudyArena] Missing knight sheet {src}");
                    continue;
                }
                var dst = $"{KnightDir}/knight_{slug}.png";
                File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);

                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(src));
                int frames = Mathf.Max(1, tex.width / 128);
                var pivots = KnightPivots(tex, frames, slug == "run" || slug == "jump");
                Object.DestroyImmediate(tex);

                ArenaArt.Slice(dst, $"knight_{slug}", 128, 128, frames, pivots, ArenaArt.PPU, FilterMode.Point, false);
                result[slug] = ArenaArt.Frames(dst);
            }
            return result;
        }

        static Vector2[] KnightPivots(Texture2D tex, int frames, bool perFrame)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var anchors = new float[frames];
            var bottoms = new int[frames];
            for (int f = 0; f < frames; f++)
            {
                int minY = int.MaxValue, maxY = int.MinValue;
                for (int y = 0; y < h; y++)
                for (int x = f * 128; x < f * 128 + 128 && x < w; x++)
                {
                    if (px[y * w + x].a <= 128) continue;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                if (minY == int.MaxValue) { anchors[f] = 40f; continue; }
                bottoms[f] = minY;
                float lo = minY + (maxY - minY) * 0.45f, hi = minY + (maxY - minY) * 0.65f;
                var xs = new List<int>();
                for (int y = Mathf.CeilToInt(lo); y <= hi; y++)
                for (int x = f * 128; x < f * 128 + 128 && x < w; x++)
                    if (px[y * w + x].a > 128) xs.Add(x - f * 128);
                xs.Sort();
                anchors[f] = xs.Count > 0 ? xs[xs.Count / 2] : 40f;
            }
            var sorted = anchors.OrderBy(a => a).ToArray();
            float sheetAnchor = sorted[sorted.Length / 2];
            var pivots = new Vector2[frames];
            for (int f = 0; f < frames; f++)
                pivots[f] = new Vector2((perFrame ? anchors[f] : sheetAnchor) / 128f, bottoms[f] / (float)h);
            return pivots;
        }

        // ------------------------------------------------------------------ materials

        class Mats
        {
            public Material flash, additive, particle, particleAdditive, impactFrame;
            public PhysicsMaterial2D noFriction, bouncy;
        }

        static Mats CreateMaterials()
        {
            Material Mat(string name, string shader)
            {
                var path = $"{ArtDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                var s = Shader.Find(shader);
                if (s == null) throw new Exception($"[PixelStudyArena] Shader not found: {shader}");
                if (m == null)
                {
                    m = new Material(s);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = s;
                EditorUtility.SetDirty(m);
                return m;
            }

            PhysicsMaterial2D Phys(string name, float friction, float bounce)
            {
                var path = $"{ArtDir}/{name}.physicsMaterial2D";
                var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
                if (m == null)
                {
                    m = new PhysicsMaterial2D(name);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.friction = friction;
                m.bounciness = bounce;
                EditorUtility.SetDirty(m);
                return m;
            }

            return new Mats
            {
                flash = Mat("Mat_SpriteFlash", "PixelStudy/SpriteFlash"),
                additive = Mat("Mat_Additive", "PixelStudy/SpriteAdditive"),
                particle = Mat("Mat_Particle", "Sprites/Default"),
                particleAdditive = Mat("Mat_ParticleAdditive", "PixelStudy/SpriteAdditive"),
                impactFrame = Mat("Mat_ImpactFrame", "PixelStudy/ImpactFrame"),
                noFriction = Phys("Phys_NoFriction", 0f, 0f),
                bouncy = Phys("Phys_Bouncy", 0.4f, 0.45f),
            };
        }

        // ------------------------------------------------------------------ tiles

        class Tiles
        {
            public Tile[] fill, top, platform, back, backTop;
            public Tile under, pillar, pillarTop;
        }

        static Tile MakeTile(string name, Tile.ColliderType collider)
        {
            var path = $"{TileDir}/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.sprite = ArenaArt.Load(ArtDir, name);
            t.colliderType = collider;
            t.flags = TileFlags.None; // lets the builder darken deep stone per cell
            EditorUtility.SetDirty(t);
            return t;
        }

        static Tiles CreateTiles()
        {
            return new Tiles
            {
                fill = Enumerable.Range(0, 3).Select(i => MakeTile($"tile_ground_fill_{i}", Tile.ColliderType.Grid)).ToArray(),
                top = Enumerable.Range(0, 2).Select(i => MakeTile($"tile_ground_top_{i}", Tile.ColliderType.Grid)).ToArray(),
                under = MakeTile("tile_ground_under", Tile.ColliderType.Grid),
                platform = Enumerable.Range(0, 3).Select(i => MakeTile($"tile_platform_{i}", Tile.ColliderType.Grid)).ToArray(),
                back = Enumerable.Range(0, 3).Select(i => MakeTile($"tile_backwall_{i}", Tile.ColliderType.None)).ToArray(),
                backTop = Enumerable.Range(0, 2).Select(i => MakeTile($"tile_backwall_top_{i}", Tile.ColliderType.None)).ToArray(),
                pillar = MakeTile("tile_pillar", Tile.ColliderType.None),
                pillarTop = MakeTile("tile_pillar_top", Tile.ColliderType.None),
            };
        }

        // ------------------------------------------------------------------ prefabs

        class Prefabs
        {
            public BugEnemy beetle, wasp, tick, boss, hornetQueen, crab;
            public BugEnemy pillBug, termite, scorpion, firefly, stag;
            public BugEnemy slime, littleEnemy, flyEnemy, tallEnemy; // the group's monsters
            public AcidBlob acid, poisonSac;
            public Stinger stinger;
            public BossShot bossShot;
            public PoisonCloud poisonCloud;
            public Pickup coin, heart;
            public FireArc fireArc;
        }

        static PixelClip Clip(string name, Sprite[] frames, float fps, bool loop = true) =>
            new PixelClip { name = name, frames = frames, fps = fps, loop = loop };

        static Prefabs CreatePrefabs(Mats m)
        {
            var p = new Prefabs();

            // acid projectile
            {
                var frames = ArenaArt.Frames($"{ArtDir}/fx_acid.png");
                var go = new GameObject("AcidBlob");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 25;
                var anim = go.AddComponent<PixelAnimator>();
                anim.target = sr;
                anim.clips = new[] { Clip("blob", frames, 8f) };
                AddGlow(go.transform, m, new Color(0.5f, 1f, 0.3f, 0.35f), 1.6f, 24);
                var blob = go.AddComponent<AcidBlob>();
                blob.body = sr;
                p.acid = SavePrefab(go, "AcidBlob").GetComponent<AcidBlob>();
            }

            // Hornet Queen: stingers
            {
                var go = new GameObject("Stinger");
                go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ArenaArt.Load(ArtDir, "fx_stinger");
                sr.sortingOrder = 25;
                AddGlow(go.transform, m, new Color(1f, 0.8f, 0.3f, 0.3f), 0.9f, 24);
                var s = go.AddComponent<Stinger>();
                s.body = sr;
                p.stinger = SavePrefab(go, "Stinger").GetComponent<Stinger>();
            }

            // boss projectiles (crescent waves, falling rocks): the boss sets the sprite when it fires
            {
                var go = new GameObject("BossShot");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 26;
                AddGlow(go.transform, m, new Color(1f, 1f, 1f, 0.18f), 1.6f, 25);
                var shot = go.AddComponent<BossShot>();
                shot.body = sr;
                p.bossShot = SavePrefab(go, "BossShot").GetComponent<BossShot>();
            }

            // Hornet Queen: poison clouds, and the sacs that leave them
            {
                var go = new GameObject("PoisonCloud");
                var fog = ArenaArt.Load(ArtDir, "fx_fog");
                var puffs = new List<SpriteRenderer>();
                var specs = new[]
                {
                    (new Vector3(-0.8f, 0.6f, 0f), 0.34f, new Color(0.55f, 0.9f, 0.3f, 0.55f)),
                    (new Vector3(0.8f, 0.7f, 0f), 0.32f, new Color(0.55f, 0.32f, 0.75f, 0.45f)),
                    (new Vector3(0f, 0.95f, 0f), 0.38f, new Color(0.65f, 1f, 0.4f, 0.45f)),
                };
                foreach (var (pos, scale, color) in specs)
                {
                    var puff = new GameObject("Puff");
                    puff.transform.SetParent(go.transform, false);
                    puff.transform.localPosition = pos;
                    puff.transform.localScale = new Vector3(scale, scale * 1.4f, 1f);
                    var sr = puff.AddComponent<SpriteRenderer>();
                    sr.sprite = fog;
                    sr.color = color;
                    sr.sortingOrder = 22;
                    puffs.Add(sr);
                }
                AddGlow(go.transform, m, new Color(0.5f, 1f, 0.3f, 0.22f), 3.5f, 21, new Vector3(0f, 0.7f, 0f));
                var cloud = go.AddComponent<PoisonCloud>();
                cloud.puffs = puffs.ToArray();
                p.poisonCloud = SavePrefab(go, "PoisonCloud").GetComponent<PoisonCloud>();
            }
            {
                var frames = ArenaArt.Frames($"{ArtDir}/fx_poisonsac.png");
                var go = new GameObject("PoisonSac");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 25;
                var anim = go.AddComponent<PixelAnimator>();
                anim.target = sr;
                anim.clips = new[] { Clip("blob", frames, 8f) };
                AddGlow(go.transform, m, new Color(0.7f, 0.35f, 1f, 0.4f), 1.6f, 24);
                var blob = go.AddComponent<AcidBlob>();
                blob.body = sr;
                blob.splashColor = new Color(0.65f, 1f, 0.35f);
                blob.cloudPrefab = p.poisonCloud;
                p.poisonSac = SavePrefab(go, "PoisonSac").GetComponent<AcidBlob>();
            }

            // Flame Slash: the arc of fire the rising swing leaves behind, and what pours off it
            {
                var frames = ArenaArt.Frames($"{ArtDir}/fx_firearc.png");
                var go = new GameObject("FireArc");
                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(go.transform, false);
                glowGo.transform.localScale = new Vector3(1.06f, 1.06f, 1f);
                var glow = glowGo.AddComponent<SpriteRenderer>();
                glow.sprite = frames[0];
                glow.sharedMaterial = m.additive;
                glow.sortingOrder = 18;
                // just behind the knight (he stays readable inside his own swing), in front of the bugs
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 19;
                AddGlow(go.transform, m, new Color(1f, 0.5f, 0.15f, 0.35f), 5f, 17, new Vector3(0.8f, 0.4f, 0f));
                var fireArc = go.AddComponent<FireArc>();
                fireArc.arc = sr;
                fireArc.arcGlow = glow;
                fireArc.arcFrames = frames;
                fireArc.globFrames = ArenaArt.Frames($"{ArtDir}/fx_fireglob.png");
                fireArc.groundFireFrames = ArenaArt.Frames($"{ArtDir}/fx_groundfire.png");
                fireArc.additiveMaterial = m.additive;
                p.fireArc = SavePrefab(go, "FireArc").GetComponent<FireArc>();
            }

            // pickups
            p.coin = MakePickup("Coin", AssetDatabase.LoadAssetAtPath<Sprite>($"{UIArt}/icon_coin.png"), Pickup.Kind.Coin, 1, 0.55f, m, new Color(1f, 0.8f, 0.3f, 0.3f));
            p.heart = MakePickup("Heart", AssetDatabase.LoadAssetAtPath<Sprite>($"{UIArt}/icon_heart.png"), Pickup.Kind.Heart, 15, 0.7f, m, new Color(1f, 0.35f, 0.4f, 0.4f));

            // bugs
            var beetle = ArenaArt.Frames($"{ArtDir}/bug_beetle.png");
            p.beetle = MakeBug<BeetleBug>("Beetle", beetle,
                new[] { Clip("walk", ArenaArt.Pick(beetle, 0, 1, 2, 3), 10f), Clip("tell", ArenaArt.Pick(beetle, 4), 1f), Clip("lunge", ArenaArt.Pick(beetle, 5), 1f) },
                m, new Color(1f, 0.55f, 0.3f), 30, 12, new Vector2Int(2, 4), false, new Vector2(1.1f, 0.7f), new Vector2(0f, 0.35f), 0.45f, 1.3f,
                b =>
                {
                    b.stance = 24f;
                    b.gameObject.AddComponent<BugStats>(); // the stats component from the gameDataManagement branch
                });

            var wasp = ArenaArt.Frames($"{ArtDir}/bug_wasp.png");
            p.wasp = MakeBug<WaspBug>("Wasp", wasp,
                new[] { Clip("fly", ArenaArt.Pick(wasp, 0, 1, 2, 1), 24f) },
                m, new Color(1f, 0.85f, 0.3f), 18, 10, new Vector2Int(1, 3), true, new Vector2(0.9f, 0.9f), Vector2.zero, 0f, 0.9f,
                b => b.stance = 18f);

            var tick = ArenaArt.Frames($"{ArtDir}/bug_tick.png");
            p.tick = MakeBug<AcidTick>("AcidTick", tick,
                new[]
                {
                    Clip("idle", ArenaArt.Pick(tick, 0, 1), 3f), Clip("walk", ArenaArt.Pick(tick, 2, 3), 6f),
                    Clip("charge", ArenaArt.Pick(tick, 4, 1), 10f), Clip("spit", ArenaArt.Pick(tick, 5), 4f, false),
                },
                m, new Color(0.55f, 1f, 0.3f), 26, 10, new Vector2Int(3, 5), false, new Vector2(1f, 0.8f), new Vector2(0f, 0.4f), 0.45f, 1.4f,
                t =>
                {
                    t.blobPrefab = p.acid;
                    t.stance = 22f;
                });

            var boss = ArenaArt.Frames($"{ArtDir}/bug_boss.png");
            p.boss = MakeBug<BroodMother>("BroodMother", boss,
                new[] { Clip("walk", ArenaArt.Pick(boss, 0, 1, 2, 3), 7f), Clip("tell", ArenaArt.Pick(boss, 4), 1f), Clip("charge", ArenaArt.Pick(boss, 5), 1f) },
                m, new Color(0.75f, 0.4f, 1f), 340, 18, new Vector2Int(30, 40), false, new Vector2(2.6f, 1.6f), new Vector2(0f, 0.8f), 0.9f, 2.6f,
                b =>
                {
                    b.simple = true; // Mockup 2: Charge, Seismic Leap and Acid Volley (Main_Dev: 520 health, 22 damage)
                    b.blobPrefab = p.acid;
                    b.summonPrefab = p.wasp;
                    b.hatchlingPrefab = p.tick;
                    b.debrisSprite = ArenaArt.Load(ArtDir, "fx_rock");
                    b.shotPrefab = p.bossShot;
                    b.lineSprite = ArenaArt.Load(ArtDir, "fx_cutline");
                    b.superArmor = true;
                    b.stance = 130f;
                    b.stanceBreakTime = 3f;
                    b.heartDropChance = 1f;
                    b.GetComponent<Rigidbody2D>().mass = 20f;
                });

            var hornet = ArenaArt.Frames($"{ArtDir}/bug_hornetqueen.png");
            p.hornetQueen = MakeBug<HornetQueen>("HornetQueen", hornet,
                new[]
                {
                    Clip("fly", ArenaArt.Pick(hornet, 0, 1, 2, 1), 22f), Clip("tell", ArenaArt.Pick(hornet, 3, 6), 16f),
                    Clip("stuck", ArenaArt.Pick(hornet, 4), 1f), Clip("dive", ArenaArt.Pick(hornet, 5), 1f),
                },
                m, new Color(1f, 0.8f, 0.3f), 300, 16, new Vector2Int(34, 44), true, new Vector2(2.1f, 2.1f), Vector2.zero, 0f, 1.9f,
                q =>
                {
                    q.simple = true; // Mockup 2: Dive-bomb, Royal Lance and Barrage (Main_Dev: 440 health, 20 damage)
                    q.stingerPrefab = p.stinger;
                    q.sacPrefab = p.poisonSac;
                    q.summonPrefab = p.wasp;
                    q.shotPrefab = p.bossShot;
                    q.lineSprite = ArenaArt.Load(ArtDir, "fx_cutline");
                    q.superArmor = true;
                    q.stance = 110f;
                    q.stanceBreakTime = 3f;
                    q.heartDropChance = 1f;
                });

            // (floor 30's boss, the Obsidian Crab, is with the group's monsters in ArenaMonsters.cs)

            // ---- normal-floor bugs for floors 5-29 ----
            var pill = ArenaArt.Frames($"{ArtDir}/bug_pillbug.png");
            p.pillBug = MakeBug<PillBug>("PillBug", pill,
                new[] { Clip("walk", ArenaArt.Pick(pill, 0, 1), 6f), Clip("curl", ArenaArt.Pick(pill, 2), 1f), Clip("ball", ArenaArt.Pick(pill, 3), 1f), Clip("dazed", ArenaArt.Pick(pill, 4), 1f) },
                m, new Color(0.75f, 0.8f, 0.6f), 34, 12, new Vector2Int(2, 4), false, new Vector2(1.1f, 0.8f), new Vector2(0f, 0.4f), 0.4f, 1.3f,
                b => b.stance = 26f);

            var termite = ArenaArt.Frames($"{ArtDir}/bug_termite.png");
            p.termite = MakeBug<TermiteGuard>("TermiteGuard", termite,
                new[] { Clip("walk", ArenaArt.Pick(termite, 0, 1), 5f), Clip("tell", ArenaArt.Pick(termite, 2), 1f), Clip("bash", ArenaArt.Pick(termite, 3), 1f), Clip("broken", ArenaArt.Pick(termite, 4), 1f) },
                m, new Color(0.95f, 0.85f, 0.6f), 40, 13, new Vector2Int(3, 5), false, new Vector2(1.1f, 1.2f), new Vector2(0f, 0.6f), 0.6f, 1.6f,
                b => b.stance = 30f);

            var scorpion = ArenaArt.Frames($"{ArtDir}/bug_scorpion.png");
            p.scorpion = MakeBug<Scorpion>("Scorpion", scorpion,
                new[]
                {
                    Clip("walk", ArenaArt.Pick(scorpion, 0, 1), 7f), Clip("idle", ArenaArt.Pick(scorpion, 0), 1f), Clip("grabTell", ArenaArt.Pick(scorpion, 2), 1f),
                    Clip("grab", ArenaArt.Pick(scorpion, 3), 1f), Clip("tailTell", ArenaArt.Pick(scorpion, 4), 1f), Clip("tailStab", ArenaArt.Pick(scorpion, 5), 1f),
                },
                m, new Color(0.65f, 1f, 0.4f), 48, 14, new Vector2Int(4, 6), false, new Vector2(1.8f, 1f), new Vector2(0f, 0.5f), 0.55f, 1.8f,
                b => b.stance = 34f);

            var firefly = ArenaArt.Frames($"{ArtDir}/bug_firefly.png");
            p.firefly = MakeBug<FireflyBomber>("FireflyBomber", firefly,
                new[] { Clip("fly", ArenaArt.Pick(firefly, 0, 1, 2, 1), 22f) },
                m, new Color(0.85f, 1f, 0.45f), 16, 12, new Vector2Int(2, 3), true, new Vector2(0.9f, 0.9f), Vector2.zero, 0f, 0.9f,
                b =>
                {
                    b.stance = 14f;
                    b.lantern = AddGlow(b.transform, m, new Color(0.85f, 1f, 0.45f, 0.4f), 2f, 9, new Vector3(-0.25f, -0.1f, 0f));
                });

            var stag = ArenaArt.Frames($"{ArtDir}/bug_stag.png");
            p.stag = MakeBug<StagBeetle>("StagBeetle", stag,
                new[]
                {
                    Clip("walk", ArenaArt.Pick(stag, 0, 1, 2, 3), 7f), Clip("tell", ArenaArt.Pick(stag, 4), 1f),
                    Clip("charge", ArenaArt.Pick(stag, 5), 1f), Clip("lift", ArenaArt.Pick(stag, 6), 1f),
                },
                m, new Color(1f, 0.55f, 0.35f), 260, 18, new Vector2Int(14, 20), false, new Vector2(2.6f, 1.4f), new Vector2(0f, 0.7f), 0.8f, 2.4f,
                b =>
                {
                    b.superArmor = true;
                    b.stance = 90f;
                    b.heartDropChance = 0.5f;
                    b.GetComponent<Rigidbody2D>().mass = 15f;
                });

            CreateGroupMonsters(p, m);
            return p;
        }

        static T MakeBug<T>(string name, Sprite[] frames, PixelClip[] clips, Mats m, Color blood, int hp, int damage, Vector2Int coins,
            bool flyer, Vector2 colliderSize, Vector2 colliderOffset, float centerHeight, float exclaimHeight, Action<T> configure = null) where T : BugEnemy
        {
            var go = new GameObject(name) { layer = ArenaLayers.Enemy };
            var rb = go.AddComponent<Rigidbody2D>();
            if (flyer)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                var c = go.AddComponent<CircleCollider2D>();
                c.isTrigger = true;
                c.radius = colliderSize.x * 0.5f;
                c.offset = colliderOffset;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 4f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var c = go.AddComponent<BoxCollider2D>();
                c.size = colliderSize;
                c.offset = colliderOffset;
                c.sharedMaterial = m.noFriction;
            }

            var visual = new GameObject("Visual") { layer = ArenaLayers.Enemy };
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sharedMaterial = m.flash;
            sr.sortingOrder = 10;
            var anim = visual.AddComponent<PixelAnimator>();
            anim.target = sr;
            anim.clips = clips;

            var flash = go.AddComponent<SpriteFlash>();
            flash.renderers = new[] { sr };

            var bug = go.AddComponent<T>();
            bug.visual = visual.transform;
            bug.body = sr;
            bug.anim = anim;
            bug.flash = flash;
            bug.bloodColor = blood;
            bug.maxHealth = hp;
            bug.damage = damage;
            bug.coinDrop = coins;
            bug.centerHeight = centerHeight;
            bug.exclaimHeight = exclaimHeight;
            configure?.Invoke(bug);
            return SavePrefab(go, name).GetComponent<T>();
        }

        static Pickup MakePickup(string name, Sprite sprite, Pickup.Kind kind, int amount, float scale, Mats m, Color glow)
        {
            var go = new GameObject(name) { layer = ArenaLayers.Pickup };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = 0.16f;
            c.sharedMaterial = m.bouncy;
            var art = new GameObject("Sprite");
            art.transform.SetParent(go.transform, false);
            art.transform.localScale = Vector3.one * scale;
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 12;
            AddGlow(go.transform, m, glow, 1.4f, 11);
            var pickup = go.AddComponent<Pickup>();
            pickup.kind = kind;
            pickup.amount = amount;
            return SavePrefab(go, name).GetComponent<Pickup>();
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static SpriteRenderer AddGlow(Transform parent, Mats m, Color color, float size, int order, Vector3? local = null)
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local ?? Vector3.zero;
            g.transform.localScale = Vector3.one * size;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = ArenaArt.Load(ArtDir, "fx_glow");
            sr.sharedMaterial = m.additive;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ level layout

        static void Fill(bool[,] map, int x, int y, int w, int h)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if (i >= 0 && j >= 0 && i < map.GetLength(0) && j < map.GetLength(1)) map[i, j] = true;
        }

        static float Noise(int x, int seed) => Mathf.PerlinNoise(x * 0.37f + seed * 3.1f, seed * 0.77f);

        /// <summary>For each solid cell, how many steps to the nearest empty cell (1 = touching air).</summary>
        static int[,] DistanceToAir(bool[,] solid)
        {
            int W = solid.GetLength(0), H = solid.GetLength(1);
            var d = new int[W, H];
            var queue = new Queue<Vector2Int>();
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                d[x, y] = solid[x, y] ? int.MaxValue : 0;
                if (!solid[x, y]) queue.Enqueue(new Vector2Int(x, y));
            }
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right, new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var dir in dirs)
                {
                    var n = c + dir;
                    if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || d[n.x, n.y] <= d[c.x, c.y] + 1) continue;
                    d[n.x, n.y] = d[c.x, c.y] + 1;
                    queue.Enqueue(n);
                }
            }
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (d[x, y] == int.MaxValue) d[x, y] = 9;
            return d;
        }

        static void BuildMap(MapDef d, Dictionary<string, Sprite[]> knight, Mats m, Tiles tiles, Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int W = d.W, H = d.H;
            var theme = d.Theme;

            // ---- geometry (cell coordinates, y up) ----
            var solid = new bool[W, H];
            Fill(solid, 0, 0, W, 3);          // floor
            Fill(solid, 0, 0, 2, H);          // left wall
            Fill(solid, W - 2, 0, 2, H);      // right wall
            d.Solid?.Invoke(solid);
            var plat = new bool[W, H];
            d.Platforms?.Invoke(plat);

            // ---- tilemaps ----
            var grid = new GameObject("Grid", typeof(Grid));
            var backMap = MakeTilemap(grid.transform, "BackWall", -60, theme.Back);
            var pillarMap = MakeTilemap(grid.transform, "Pillars", -58, theme.Pillar);
            var groundMap = MakeTilemap(grid.transform, "Ground", 0, Color.white);
            var platformMap = MakeTilemap(grid.transform, "Platforms", 1, theme.Platform);
            groundMap.gameObject.layer = ArenaLayers.Ground;
            platformMap.gameObject.layer = ArenaLayers.Platform;

            for (int x = 2; x < W - 2; x++)
            {
                int top = Mathf.Min(H - 2, d.BackTop != null ? d.BackTop(x) : H - 2);
                for (int y = 3; y <= top; y++)
                {
                    var tile = y == top ? tiles.backTop[(x + y) % 2] : tiles.back[Mathf.FloorToInt(Noise(x * 7 + y, 9) * 2.99f)];
                    backMap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
            foreach (var (px, ptop, capped) in d.Pillars)
                for (int y = 3; y <= ptop; y++) pillarMap.SetTile(new Vector3Int(px, y, 0), y == ptop && capped ? tiles.pillarTop : tiles.pillar);

            var depth = DistanceToAir(solid);
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (!solid[x, y]) continue;
                bool airAbove = y + 1 < H && !solid[x, y + 1];
                bool airBelow = y > 0 && !solid[x, y - 1];
                Tile t = airAbove ? tiles.top[(x * 7 + y) % 2] : airBelow ? tiles.under : tiles.fill[(x * 13 + y * 7) % 3];
                var cell = new Vector3Int(x, y, 0);
                groundMap.SetTile(cell, t);
                // Stone gets darker the further it is from open air (Dead Cells-style depth).
                float f = depth[x, y] <= 1 ? 1f : depth[x, y] == 2 ? 0.72f : depth[x, y] == 3 ? 0.52f : 0.4f;
                groundMap.SetColor(cell, new Color(f * 0.95f * theme.Ground.r, f * 0.98f * theme.Ground.g, f * 1.05f * theme.Ground.b));
            }
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (!plat[x, y]) continue;
                bool left = x > 0 && plat[x - 1, y], right = x + 1 < W && plat[x + 1, y];
                platformMap.SetTile(new Vector3Int(x, y, 0), !left ? tiles.platform[0] : !right ? tiles.platform[2] : tiles.platform[1]);
            }
            AddColliders(groundMap.gameObject, false);
            AddColliders(platformMap.gameObject, true);

            // ---- background ----
            var anchor = new Vector2(W * 0.5f, H * 0.5f);
            float bgScale = 2.2f * Mathf.Max(1f, H / 26f, W / 64f);
            var bg = new GameObject("Background").transform;
            var far = Deco(bg, "Sky", theme.SkySprite, new Vector3(W * 0.5f, H * 0.6f), -100, theme.Sky, bgScale);
            AddParallax(far.gameObject, new Vector2(0.88f, 0.6f), anchor);
            if (theme.Ruins.a > 0f)
            {
                var ruins = Deco(bg, "Ruins", "bg_ruins", new Vector3(W * 0.5f, 2f), -80, theme.Ruins, bgScale);
                AddParallax(ruins.gameObject, new Vector2(0.55f, 0.35f), anchor);
            }
            for (int i = 0; i < Mathf.CeilToInt(W / 12f); i++)
            {
                var fog = Deco(bg, "Fog", "fx_fog", new Vector3(8f + i * 12f, 7f + (i % 3) * 4f * H / 26f), -70, theme.Fog, 1f);
                fog.transform.localScale = new Vector3(1.4f + (i % 2) * 0.6f, 1.2f, 1f);
                fog.sharedMaterial = m.additive;
                var drift = fog.gameObject.AddComponent<Drift>();
                drift.amplitude = new Vector2(2.5f, 0.3f);
                drift.speed = 0.08f + i * 0.02f;
            }

            // ---- decor (each map's own props) ----
            var decor = new GameObject("Decor").transform;
            d.Decor?.Invoke(decor, m, solid);
            d.Setup?.Invoke(groundMap, backMap, pillarMap, decor, m);

            if (theme.Grass)
            {
                var grass = new[] { ArenaArt.Load(ArtDir, "prop_grass_0"), ArenaArt.Load(ArtDir, "prop_grass_1"), ArenaArt.Load(ArtDir, "prop_grass_2") };
                for (int x = 2; x < W - 2; x++)
                for (int y = 0; y < H - 1; y++)
                {
                    if (!solid[x, y] || solid[x, y + 1] || Noise(x * 3 + y, 21) < 0.45f) continue;
                    var g = new GameObject("Grass").AddComponent<SpriteRenderer>();
                    g.transform.SetParent(decor, false);
                    g.transform.position = new Vector3(x + 0.5f, y + 1f);
                    g.sprite = grass[(x + y) % 3];
                    g.color = theme.GrassTint;
                    g.flipX = x % 2 == 0;
                    g.sortingOrder = 5;
                }
            }

            // foreground silhouettes (slide past faster than the level)
            var fg = new GameObject("Foreground").transform;
            AddParallax(fg.gameObject, new Vector2(-0.15f, -0.05f), anchor);
            for (int i = 0; i < 3; i++)
            {
                var fog = Deco(fg, "FgFog", "fx_fog", new Vector3(10f + i * W / 3f, 3f), 49, new Color(theme.Fog.r, theme.Fog.g, theme.Fog.b, theme.Fog.a * 0.5f), 1f);
                fog.sharedMaterial = m.additive;
                fog.transform.localScale = new Vector3(2.5f, 1.5f, 1f);
                fog.gameObject.AddComponent<Drift>().amplitude = new Vector2(3f, 0.2f);
            }

            // ---- exit door ----
            var door = BuildDoor(m, d.Door);

            // ---- spawn points ----
            var spawns = new GameObject("SpawnPoints").transform;
            Transform Point(string name, Vector2 p)
            {
                var t = new GameObject(name).transform;
                t.SetParent(spawns, false);
                t.position = p;
                return t;
            }
            var groundSpawns = d.Ground.Select(p => Point("Ground", p)).ToArray();
            var airSpawns = d.Air.Select(p => Point("Air", p)).ToArray();
            var bossSpawn = Point("Boss", d.BossSpawn);
            var bossProps = d.BossProps.Select(p => Point("BossProp", p)).ToArray();

            // ---- player ----
            var player = BuildPlayer(knight, m, prefabs, d.PlayerStart);

            // ---- camera ----
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(d.PlayerStart.x + 6.6f, Mathf.Max(6.5f, d.PlayerStart.y + 1.6f), -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.CameraBg;
            var follow = camGo.AddComponent<CameraFollow2D>();
            follow.player = player;
            follow.bounds = new Rect(0f, 0f, W, H);
            camGo.AddComponent<ImpactFrame>().material = m.impactFrame; // anime impact frames (Flame Slash, criticals)
            var dust = new GameObject("DustMotes");
            dust.transform.SetParent(camGo.transform, false);
            dust.transform.localPosition = new Vector3(0f, 0f, 10f);
            var dustEmitter = dust.AddComponent<AmbientEmitter>();
            dustEmitter.material = m.particleAdditive;
            dustEmitter.colorA = theme.DustA;
            dustEmitter.colorB = theme.DustB;
            dustEmitter.area = new Vector2(26f, 15f);
            dustEmitter.rate = theme.DustRate;
            dustEmitter.lifetime = new Vector2(3f, 6f);
            dustEmitter.size = theme.DustSize;
            dustEmitter.velocity = theme.DustVelocity;
            dustEmitter.velocityRandom = 0.15f;
            dustEmitter.noise = theme.DustNoise;
            dustEmitter.sortingOrder = 45;
            dustEmitter.fadeIn = true;

            // ---- effects ----
            var fx = new GameObject("ArenaFX").AddComponent<ArenaFX>();
            fx.particleMaterial = m.particle;
            fx.additiveMaterial = m.additive;
            fx.numberFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(FontPath);
            fx.numberMaterial = AssetDatabase.LoadAssetAtPath<Material>(TitleTextMaterial);
            fx.exclaimSprite = ArenaArt.Load(ArtDir, "fx_exclaim");
            fx.slashSprite = ArenaArt.Load(ArtDir, "fx_slash");
            fx.ringSprite = ArenaArt.Load(ArtDir, "fx_ring");
            fx.glowSprite = ArenaArt.Load(ArtDir, "fx_glow");
            fx.fireSlashSprite = ArenaArt.Load(ArtDir, "fx_fireslash");
            fx.zapFrames = ArenaArt.Frames($"{ArtDir}/fx_zap.png");

            // ---- director ----
            var director = new GameObject("ArenaDirector").AddComponent<ArenaDirector>();
            director.beetlePrefab = prefabs.beetle;
            director.waspPrefab = prefabs.wasp;
            director.tickPrefab = prefabs.tick;
            director.pillBugPrefab = prefabs.pillBug;
            director.termitePrefab = prefabs.termite;
            director.scorpionPrefab = prefabs.scorpion;
            director.fireflyPrefab = prefabs.firefly;
            director.stagBeetlePrefab = prefabs.stag;
            director.slimePrefab = prefabs.slime;
            director.littleEnemyPrefab = prefabs.littleEnemy;
            director.flyEnemyPrefab = prefabs.flyEnemy;
            director.tallEnemyPrefab = prefabs.tallEnemy;
            director.bossPrefabs = new[] { prefabs.boss, prefabs.hornetQueen, prefabs.crab };
            director.coinPrefab = prefabs.coin;
            director.heartPrefab = prefabs.heart;
            director.player = player;
            director.groundSpawns = groundSpawns;
            director.airSpawns = airSpawns;
            director.bossSpawn = bossSpawn;
            director.bossProps = bossProps;
            director.arenaBoss = d.BossIndex;
            director.mapTitle = d.Title;
            director.exitDoor = door;
            director.flyBounds = d.FlyBounds;

            // ---- vignette + UI ----
            var vignetteCanvas = new GameObject("Vignette", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            vignetteCanvas.sortingOrder = 5;
            var vignette = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            vignette.transform.SetParent(vignetteCanvas.transform, false);
            var vr = vignette.rectTransform;
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = vr.offsetMax = Vector2.zero;
            vignette.sprite = ArenaArt.Load(ArtDir, "fx_vignette");
            vignette.raycastTarget = false;

            var gameUI = AssetDatabase.LoadAssetAtPath<GameObject>(GameUIPrefabPath);
            if (gameUI != null) ((GameObject)PrefabUtility.InstantiatePrefab(gameUI, scene)).name = "GameUI";
            else Debug.LogWarning("[PixelStudyArena] GameUI prefab not found. Run Tools > Pixel Study > Build UI Screens first.");
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{d.Scene}.unity");
        }

        static Tilemap MakeTilemap(Transform grid, string name, int order, Color color)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var map = go.GetComponent<Tilemap>();
            map.color = color;
            var r = go.GetComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.mode = TilemapRenderer.Mode.Chunk;
            return map;
        }

        static void AddColliders(GameObject go, bool oneWay)
        {
            var tc = go.AddComponent<TilemapCollider2D>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
            if (oneWay)
            {
                composite.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
            }
        }

        static SpriteRenderer Deco(Transform parent, string name, string sprite, Vector3 position, int order, Color color, float scale)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.position = position;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = ArenaArt.Load(ArtDir, sprite);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static void AddParallax(GameObject go, Vector2 factor, Vector2 anchor)
        {
            var p = go.AddComponent<ParallaxLayer>();
            p.factor = factor;
            p.anchor = anchor;
        }

        static void Torch(Transform parent, Mats m, Vector3 position) => Torch(parent, m, position, new Color(1f, 0.55f, 0.22f, 0.32f), Color.white);

        /// <summary>A wall torch; glow and flame tint let maps recolor it (green crypt candles, forge braziers).</summary>
        static void Torch(Transform parent, Mats m, Vector3 position, Color glowColor, Color flameTint)
        {
            var root = new GameObject("Torch").transform;
            root.SetParent(parent, false);
            root.position = position;

            var body = root.gameObject.AddComponent<SpriteRenderer>();
            body.sprite = ArenaArt.Load(ArtDir, "prop_torch");
            body.sortingOrder = -35;

            var flameGo = new GameObject("Flame");
            flameGo.transform.SetParent(root, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var flame = flameGo.AddComponent<SpriteRenderer>();
            flame.sortingOrder = -34;
            flame.color = flameTint;
            var frames = ArenaArt.Frames($"{ArtDir}/prop_flame.png");
            flame.sprite = frames[0];
            var anim = flameGo.AddComponent<PixelAnimator>();
            anim.target = flame;
            anim.clips = new[] { Clip("burn", frames, 10f) };
            flameGo.AddComponent<AutoPlay>().clip = "burn";

            var glow = AddGlow(root, m, glowColor, 7f, -40, new Vector3(0f, 0.6f, 0f));
            var flicker = glow.gameObject.AddComponent<GlowFlicker>();
            flicker.glow = glow;
            flicker.baseAlpha = glowColor.a;
            var coreColor = Color.Lerp(glowColor, new Color(1f, 0.95f, 0.8f), 0.4f);
            coreColor.a = glowColor.a * 1.4f;
            var core = AddGlow(root, m, coreColor, 2.2f, -33, new Vector3(0f, 0.6f, 0f));
            var coreFlicker = core.gameObject.AddComponent<GlowFlicker>();
            coreFlicker.glow = core;
            coreFlicker.baseAlpha = core.color.a;
            coreFlicker.speed = 6f;

            var embers = new GameObject("Embers");
            embers.transform.SetParent(root, false);
            embers.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var e = embers.AddComponent<AmbientEmitter>();
            e.material = m.particleAdditive;
            e.area = new Vector2(0.25f, 0.05f);
            e.rate = 5f;
            e.velocity = new Vector2(0f, 1.1f);
            e.sortingOrder = -32;
        }

        static ExitDoor BuildDoor(Mats m, Vector3 position)
        {
            var root = new GameObject("ExitDoor");
            root.transform.position = position;

            var frame = Deco(root.transform, "Frame", "door_frame", position, -30, Color.white, 1f);

            var portalFrames = ArenaArt.Frames($"{ArtDir}/door_portal.png");
            var portal = new GameObject("Portal").AddComponent<SpriteRenderer>();
            portal.transform.SetParent(root.transform, false);
            portal.sprite = portalFrames[0];
            portal.color = new Color(1f, 1f, 1f, 0f); // fades in when the arena is cleared
            portal.sortingOrder = -29;
            var portalAnim = portal.gameObject.AddComponent<PixelAnimator>();
            portalAnim.target = portal;
            portalAnim.clips = new[] { Clip("swirl", portalFrames, 10f) };

            var bars = Deco(root.transform, "Bars", "door_bars", position, -28, Color.white, 1f);

            var glow = AddGlow(root.transform, m, new Color(0.4f, 1f, 0.9f, 0f), 7f, -27, new Vector3(0f, 1.4f, 0f));

            var arrow = Deco(root.transform, "Arrow", "door_arrow", position + new Vector3(0f, 3.6f), 40, Color.white, 1.5f);
            arrow.gameObject.SetActive(false);

            var sparks = new GameObject("Sparks");
            sparks.transform.SetParent(root.transform, false);
            sparks.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var e = sparks.AddComponent<AmbientEmitter>();
            e.material = m.particleAdditive;
            e.colorA = new Color(0.5f, 1f, 0.9f, 0.8f);
            e.colorB = new Color(0.8f, 1f, 1f, 0.6f);
            e.area = new Vector2(1.3f, 2.2f);
            e.rate = 6f;
            e.velocity = new Vector2(0f, 0.6f);
            e.sortingOrder = -26;
            sparks.SetActive(false);

            var door = root.AddComponent<ExitDoor>();
            door.bars = bars.transform;
            door.portal = portal;
            door.glow = glow;
            door.arrow = arrow.transform;
            door.portalAnim = portalAnim;
            door.sparks = sparks;
            return door;
        }

        static ArenaPlayer BuildPlayer(Dictionary<string, Sprite[]> k, Mats m, Prefabs prefabs, Vector3 position)
        {
            var go = new GameObject("Knight") { layer = ArenaLayers.Player };
            go.transform.position = position;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 4.2f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.7f, 1.8f);
            capsule.offset = new Vector2(0f, 0.9f);
            capsule.sharedMaterial = m.noFriction;

            var visual = new GameObject("Visual") { layer = ArenaLayers.Player };
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = m.flash;
            sr.sortingOrder = 20;
            sr.sprite = k["idle"][0];
            var anim = visual.AddComponent<PixelAnimator>();
            anim.target = sr;
            Sprite[] F(string sheet, params int[] idx) => idx.Length == 0 ? k[sheet] : ArenaArt.Pick(k[sheet], idx);
            // frames from several sheets in one clip
            Sprite[] Mix(params (string sheet, int frame)[] parts) => parts.Select(p => k[p.sheet][Mathf.Clamp(p.frame, 0, k[p.sheet].Length - 1)]).ToArray();
            anim.clips = new[]
            {
                Clip("idle", F("idle"), 7f),
                Clip("run", F("run"), 14f),
                Clip("roll", F("jump", 2), 1f), // tucked; the knight spins through the roll in code
                Clip("jumpUp", F("jump", 1, 2), 10f),
                Clip("jumpApex", F("jump", 3), 1f),
                Clip("fall", F("jump", 4), 1f),
                Clip("land", F("jump", 5), 1f),
                Clip("attack1", F("attack1"), 16f, false),
                Clip("attack2", F("attack2"), 15f, false),
                Clip("attack3", F("attack3"), 12f, false),
                Clip("defend", F("defend"), 20f, false),
                Clip("protect", F("protect"), 1f),
                Clip("hurt", F("hurt"), 8f, false),
                Clip("dead", F("dead"), 9f, false),
                Clip("walk", F("walk"), 10f),
                Clip("runAttack", F("runattack"), 14f, false),
                // Sword: three different slashes (the active frames are set in ArenaPlayer.Combo)
                Clip("slash1", F("attack1", 1, 2, 4, 4, 3), 20f, false),                                                          // diagonal down-swing
                Clip("slash2", Mix(("runattack", 4), ("runattack", 4), ("attack2", 1), ("attack2", 0), ("attack2", 0)), 20f, false), // rising uppercut
                Clip("slash3", F("runattack", 0, 1, 2, 2, 3, 4, 5), 18f, false),                                                  // overhead cleave
                Clip("airSlash", F("attack1", 2, 4, 4, 3), 22f, false),
                Clip("jumpAttack", F("runattack", 1, 2, 3, 4, 5), 20f, false),                  // overhead chop on the way down
                Clip("rollSlash", F("attack3", 2, 2, 3, 3), 18f, false),                       // quick thrust out of a roll
                Clip("guardCounter", Mix(("protect", 0), ("runattack", 2), ("runattack", 3), ("runattack", 4), ("runattack", 5), ("runattack", 5)), 18f, false),
                Clip("backstep", F("jump", 4, 5, 5), 10f, false),
                // Heavy stab: hold the drawn-back guard while charging, then thrust
                Clip("heavyCharge", F("attack3", 1), 1f),
                Clip("heavyStab", F("attack3", 2, 2, 3, 3, 3, 3, 3), 16f, false),
                // Critical: draw back, run the bug through, rip the blade out upward
                Clip("critWind", F("attack3", 1), 1f),
                Clip("critStab", F("attack3", 2, 2, 2, 3), 14f, false),
                Clip("critRip", Mix(("runattack", 4), ("attack2", 1), ("attack2", 0), ("attack2", 0), ("attack2", 0)), 16f, false),
                // Crimson flask
                Clip("drink", F("idle", 0), 1f),
                // Thousand Cuts: at rest past the bugs, blade lowered, waiting for them to fall apart
                Clip("cutsRest", F("attack1", 0), 1f),
                // Skills
                Clip("flameLower", F("attack2", 3), 1f),                                         // low guard, sword down by the heel: it catches fire
                Clip("flameRise", Mix(("attack3", 3), ("attack3", 2), ("attack2", 1), ("attack2", 0), ("attack2", 0), ("attack2", 0), ("attack2", 0)), 22f, false), // rising slash, fire erupts on frame 1
                Clip("overhead", F("runattack", 0, 1, 2, 2, 3, 3, 4, 5), 20f, false),        // sword over the head, comes down on frame 4
                Clip("cast", F("attack2", 0, 1, 1, 1, 1), 12f, false),                        // sword raised to summon the orbs
                Clip("thunderCall", F("attack2", 1), 1f),                                      // sword pointed at the sky
                Clip("clapStrike", F("runattack", 3, 4, 5, 5), 12f, false),                   // brings the sword down with the bolt
                Clip("riposte", F("attack2", 1, 2, 3), 18f, false),                           // shield bash
            };

            AddGlow(go.transform, m, new Color(0.6f, 0.85f, 1f, 0.1f), 5f, 19, new Vector3(0f, 1f, 0f));

            var flash = go.AddComponent<SpriteFlash>();
            flash.renderers = new[] { sr };

            var player = go.AddComponent<ArenaPlayer>();
            player.visual = visual.transform;
            player.body = sr;
            player.anim = anim;
            player.flash = flash;
            player.smearSprites = new[] { ArenaArt.Load(ArtDir, "fx_smear_diagonal"), ArenaArt.Load(ArtDir, "fx_smear_rising"), ArenaArt.Load(ArtDir, "fx_smear_cleave") };
            player.thrustSprite = ArenaArt.Load(ArtDir, "fx_thrust");
            player.starSprite = ArenaArt.Load(ArtDir, "fx_star");
            player.flaskSprite = ArenaArt.Load(ArtDir, "fx_flask");
            player.flaskIcon = ArenaArt.Load(ArtDir, "icon_flask");
            player.cocoonSprite = ArenaArt.Load(ArtDir, "fx_cocoon");

            // Mockup 2: walk, one jump, the sword (M1) and Flame Slash. No roll, heavy stab, shield or flask.
            player.doubleJump = false;
            player.dodge = false;
            player.heavyStab = false;
            player.shield = false;
            player.flask = false;
            player.useStamina = false;

            // Flame Slash (Z): the knight's one skill; it takes the first slot on the HUD
            var flameSlash = go.AddComponent<FlameSlashSkill>();
            flameSlash.skillId = PixelStudy.SkillCatalog.FlameSlash;
            flameSlash.displayName = PixelStudy.SkillCatalog.Get(flameSlash.skillId).Name;
            flameSlash.key = KeyCode.Z;
            flameSlash.gamepadButton = KeyCode.JoystickButton3;
            flameSlash.cooldown = 7f;
            flameSlash.icon = ArenaArt.Load(ArtDir, "icon_flameslash");
            flameSlash.arcPrefab = prefabs.fireArc;
            flameSlash.flameSprites = ArenaArt.Frames($"{ArtDir}/prop_flame.png");
            return player;
        }
    }
}
