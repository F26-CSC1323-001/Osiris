using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelStudy.Arena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Mockup 2: builds one super basic level (Assets/Scenes/Arena.unity) that repeats every floor:
    /// a greybox floor, walls and three platforms, a couple of beetles and wasps already placed (they
    /// don't move or attack), and an exit door that's open from the start. The knight only walks and
    /// jumps. Run from Tools > Pixel Study > Build Arena (or Build Everything).
    /// </summary>
    public static class ArenaBuilder
    {
        const string Root = "Assets/PixelStudyArena";
        const string ArtDir = Root + "/Art";
        const string KnightDir = ArtDir + "/Knight";
        const string TileDir = Root + "/Tiles";
        const string PrefabDir = Root + "/Prefabs";
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string GameUIPrefabPath = "Assets/PixelStudyUI/Prefabs/GameUI.prefab";

        // The level, in cells (1 cell = 1 unit; the floor's top is y = 3)
        const int W = 44, H = 16;

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
            var knight = ImportKnight();
            var mats = CreateMaterials();
            var tiles = CreateTiles();
            var prefabs = CreatePrefabs();
            BuildScene(knight, mats, tiles, prefabs);
            PixelStudyUIBuilder.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[PixelStudyArena] Built Arena scene.");
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

        // Movement only: no attack, shield, hurt or death sheets.
        static readonly (string file, string slug)[] KnightSheets = { ("Idle", "idle"), ("Run", "run"), ("Jump", "jump") };

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
            public Material additive, particle, particleAdditive;
            public PhysicsMaterial2D noFriction;
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

            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>($"{ArtDir}/Phys_NoFriction.physicsMaterial2D");
            if (noFriction == null)
            {
                noFriction = new PhysicsMaterial2D("Phys_NoFriction");
                AssetDatabase.CreateAsset(noFriction, $"{ArtDir}/Phys_NoFriction.physicsMaterial2D");
            }
            noFriction.friction = 0f;
            noFriction.bounciness = 0f;
            EditorUtility.SetDirty(noFriction);

            return new Mats
            {
                additive = Mat("Mat_Additive", "PixelStudy/SpriteAdditive"),
                particle = Mat("Mat_Particle", "Sprites/Default"),
                particleAdditive = Mat("Mat_ParticleAdditive", "PixelStudy/SpriteAdditive"),
                noFriction = noFriction,
            };
        }

        // ------------------------------------------------------------------ tiles

        class Tiles
        {
            public Tile[] fill, top, platform;
            public Tile under;
        }

        static Tile MakeTile(string name)
        {
            var path = $"{TileDir}/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.sprite = ArenaArt.Load(ArtDir, name);
            t.colliderType = Tile.ColliderType.Grid;
            t.flags = TileFlags.None;
            EditorUtility.SetDirty(t);
            return t;
        }

        static Tiles CreateTiles() => new Tiles
        {
            fill = Enumerable.Range(0, 3).Select(i => MakeTile($"tile_ground_fill_{i}")).ToArray(),
            top = Enumerable.Range(0, 2).Select(i => MakeTile($"tile_ground_top_{i}")).ToArray(),
            under = MakeTile("tile_ground_under"),
            platform = Enumerable.Range(0, 3).Select(i => MakeTile($"tile_platform_{i}")).ToArray(),
        };

        // ------------------------------------------------------------------ prefabs

        class Prefabs
        {
            public BugEnemy beetle, wasp;
        }

        static PixelClip Clip(string name, Sprite[] frames, float fps, bool loop = true) =>
            new PixelClip { name = name, frames = frames, fps = fps, loop = loop };

        static Prefabs CreatePrefabs()
        {
            // Just two idle bugs: no attack frames, no movement, no colliders (nothing to hit).
            var beetle = ArenaArt.Frames($"{ArtDir}/bug_beetle.png");
            var wasp = ArenaArt.Frames($"{ArtDir}/bug_wasp.png");
            return new Prefabs
            {
                beetle = MakeBug<BeetleBug>("Beetle", new[] { Clip("idle", ArenaArt.Pick(beetle, 0), 1f) }),
                wasp = MakeBug<WaspBug>("Wasp", new[] { Clip("idle", ArenaArt.Pick(wasp, 0, 1, 2, 1), 24f) }), // wings buzz
            };
        }

        static T MakeBug<T>(string name, PixelClip[] clips) where T : BugEnemy
        {
            var go = new GameObject(name) { layer = ArenaLayers.Enemy };
            var visual = new GameObject("Visual") { layer = ArenaLayers.Enemy };
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = clips[0].frames[0];
            sr.sortingOrder = 10;
            var anim = visual.AddComponent<PixelAnimator>();
            anim.target = sr;
            anim.clips = clips;

            var bug = go.AddComponent<T>();
            bug.visual = visual.transform;
            bug.body = sr;
            bug.anim = anim;
            return SavePrefab(go, name).GetComponent<T>();
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

        // ------------------------------------------------------------------ the level

        static void Fill(bool[,] map, int x, int y, int w, int h)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if (i >= 0 && j >= 0 && i < W && j < H) map[i, j] = true;
        }

        static void BuildScene(Dictionary<string, Sprite[]> knight, Mats m, Tiles tiles, Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- geometry (cell coordinates, y up): floor, walls, ceiling and three platforms ----
            var solid = new bool[W, H];
            Fill(solid, 0, 0, W, 3);          // floor
            Fill(solid, 0, 0, 2, H);          // left wall
            Fill(solid, W - 2, 0, 2, H);      // right wall
            Fill(solid, 0, H - 2, W, 2);      // ceiling

            var plat = new bool[W, H];
            void Ledge(int x0, int x1, int y) => Fill(plat, x0, y, x1 - x0 + 1, 1);
            Ledge(9, 14, 6);
            Ledge(19, 25, 9);
            Ledge(30, 35, 6);

            var grid = new GameObject("Grid", typeof(Grid));
            var groundMap = MakeTilemap(grid.transform, "Ground", 0);
            var platformMap = MakeTilemap(grid.transform, "Platforms", 1);
            groundMap.gameObject.layer = ArenaLayers.Ground;
            platformMap.gameObject.layer = ArenaLayers.Platform;

            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                if (!solid[x, y]) continue;
                bool airAbove = y + 1 < H && !solid[x, y + 1];
                bool airBelow = y > 0 && !solid[x, y - 1];
                Tile t = airAbove ? tiles.top[(x * 7 + y) % 2] : airBelow ? tiles.under : tiles.fill[(x * 13 + y * 7) % 3];
                groundMap.SetTile(new Vector3Int(x, y, 0), t);
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

            // ---- the exit door (open from the start) ----
            var door = BuildDoor(m, new Vector3(W - 4.5f, 3f));

            // ---- bugs, already placed (they just idle, facing the knight's side) ----
            var bugs = new GameObject("Bugs").transform;
            void Place(string prefab, Vector2 at)
            {
                // load from disk: opening the new scene can unload the prefab handles made earlier
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefab}.prefab");
                var bug = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                bug.transform.SetParent(bugs, true);
                bug.transform.position = at; // bugs face left (toward where the knight starts) by default
            }
            Place("Beetle", new Vector2(17f, 3f));     // on the floor
            Place("Beetle", new Vector2(22.5f, 10f));  // on the middle platform
            Place("Wasp", new Vector2(12f, 9.5f));     // in the air
            Place("Wasp", new Vector2(33f, 9.5f));

            // ---- player ----
            var player = BuildPlayer(knight, m, new Vector3(4f, 3.05f));

            // ---- camera ----
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(11.6f, 6.5f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ArenaArt.Hex("#1A2230");
            var follow = camGo.AddComponent<CameraFollow2D>();
            follow.player = player;
            follow.bounds = new Rect(0f, 0f, W, H);

            // ---- effects (jump and landing dust) ----
            var fx = new GameObject("ArenaFX").AddComponent<ArenaFX>();
            fx.particleMaterial = m.particle;
            fx.additiveMaterial = m.additive;
            fx.ringSprite = ArenaArt.Load(ArtDir, "fx_ring");
            fx.glowSprite = ArenaArt.Load(ArtDir, "fx_glow");

            // ---- director: opens the door and sends you on when you walk in ----
            var director = new GameObject("ArenaDirector").AddComponent<ArenaDirector>();
            director.player = player;
            director.exitDoor = door;

            var gameUI = AssetDatabase.LoadAssetAtPath<GameObject>(GameUIPrefabPath);
            if (gameUI != null) ((GameObject)PrefabUtility.InstantiatePrefab(gameUI, scene)).name = "GameUI";
            else Debug.LogWarning("[PixelStudyArena] GameUI prefab not found. Run Tools > Pixel Study > Build UI Screens first.");
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static Tilemap MakeTilemap(Transform grid, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var r = go.GetComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.mode = TilemapRenderer.Mode.Chunk;
            return go.GetComponent<Tilemap>();
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

        static SpriteRenderer Deco(Transform parent, string name, string sprite, Vector3 position, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.position = position;
            sr.sprite = ArenaArt.Load(ArtDir, sprite);
            sr.sortingOrder = order;
            return sr;
        }

        static ExitDoor BuildDoor(Mats m, Vector3 position)
        {
            var root = new GameObject("ExitDoor");
            root.transform.position = position;

            Deco(root.transform, "Frame", "door_frame", position, -30);

            var portalFrames = ArenaArt.Frames($"{ArtDir}/door_portal.png");
            var portal = new GameObject("Portal").AddComponent<SpriteRenderer>();
            portal.transform.SetParent(root.transform, false);
            portal.sprite = portalFrames[0];
            portal.sortingOrder = -29;
            var portalAnim = portal.gameObject.AddComponent<PixelAnimator>();
            portalAnim.target = portal;
            portalAnim.clips = new[] { Clip("swirl", portalFrames, 10f) };

            var bars = Deco(root.transform, "Bars", "door_bars", position, -28);
            var glow = AddGlow(root.transform, m, new Color(0.4f, 1f, 0.9f, 0f), 7f, -27, new Vector3(0f, 1.4f, 0f));

            var arrow = Deco(root.transform, "Arrow", "door_arrow", position + new Vector3(0f, 3.6f), 40);
            arrow.transform.localScale = Vector3.one * 1.5f;
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

        static ArenaPlayer BuildPlayer(Dictionary<string, Sprite[]> k, Mats m, Vector3 position)
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
            sr.sortingOrder = 20;
            sr.sprite = k["idle"][0];
            var anim = visual.AddComponent<PixelAnimator>();
            anim.target = sr;
            Sprite[] F(string sheet, params int[] idx) => idx.Length == 0 ? k[sheet] : ArenaArt.Pick(k[sheet], idx);
            // Movement only: idle, run and the jump poses.
            anim.clips = new[]
            {
                Clip("idle", F("idle"), 7f),
                Clip("run", F("run"), 14f),
                Clip("jumpUp", F("jump", 1, 2), 10f),
                Clip("jumpApex", F("jump", 3), 1f),
                Clip("fall", F("jump", 4), 1f),
                Clip("land", F("jump", 5), 1f),
            };

            var player = go.AddComponent<ArenaPlayer>();
            player.visual = visual.transform;
            player.body = sr;
            player.anim = anim;
            return player;
        }
    }
}
