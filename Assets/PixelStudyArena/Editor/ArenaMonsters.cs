using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelStudy.Arena;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// The group's own monsters as arena enemies: the Slime and the Little, Tall and Fly enemies from
    /// Jayden's sprite packs (Assets/Sprites/Enemy), and the Obsidian Crab boss (Assets/Sprites/Boss).
    /// They keep their own art and animations (walk, attack, death) and run on the arena's bug AI, so
    /// they fight the knight like the other bugs. The original sprites are left untouched: the frames
    /// each monster needs are copied into one strip in Art/Group and sliced there.
    /// </summary>
    public static partial class ArenaBuilder
    {
        const string GroupArtDir = ArtDir + "/Group";
        const string EnemySprites = "Assets/Sprites/Enemy";
        const string CrabSprites = "Assets/Sprites/Boss/Obsidian_Crab_Pixel_Art_v1.0_AllColors/Red";

        // Their sprites are small (a 48px frame for a bug about 15px across), so they're imported at fewer
        // pixels per unit than the arena's 32 to come out about as big as the beetles and wasps.
        const float MonsterPPU = 12f, CrabPPU = 16f;

        static readonly Color MonsterBlood = new Color(0.6f, 0.4f, 0.85f);

        /// <summary>One animation: square frames read left to right from one row of a sheet (top row = 0), or one file per frame.</summary>
        struct Anim
        {
            public string name;
            public int size, row;
            public string[] files;

            public Anim(string name, int size, int row, params string[] files)
            {
                this.name = name;
                this.size = size;
                this.row = row;
                this.files = files;
            }
        }

        static void CreateGroupMonsters(Prefabs p, Mats m)
        {
            Directory.CreateDirectory(GroupArtDir);
            string Insect(string monster, string sheet) => $"{EnemySprites}/Insect-Enemy-Pack-V.1/{monster}/Sprite-Sheet/{monster}-{sheet}-Sheet.png";
            string Slime(string sheet) => $"{EnemySprites}/Slime1/Without_shadow/Slime1_{sheet}_without_shadow.png";
            string[] Crab(string anim) => Directory.GetFiles($"{CrabSprites}/{anim}/east", "frame_*.png").OrderBy(f => f).ToArray();

            // Slime: wobbles up, then hops forward with a splash (row 3 of its sheets faces right)
            var slime = ImportMonster("monster_slime", MonsterPPU, true,
                new Anim("walk", 64, 3, Slime("Walk")), new Anim("attack", 64, 3, Slime("Attack")), new Anim("death", 64, 3, Slime("Death")));
            p.slime = MakeBug<BeetleBug>("Slime", slime["walk"],
                new[]
                {
                    Clip("walk", slime["walk"], 10f), Clip("tell", ArenaArt.Pick(slime["attack"], 0, 1, 2, 3), 8f, false),
                    Clip("lunge", ArenaArt.Pick(slime["attack"], 4, 5, 6, 7, 8, 9), 17f, false), Clip("death", slime["death"], 14f, false),
                },
                m, new Color(0.45f, 0.95f, 0.75f), 24, 10, new Vector2Int(1, 3), false, new Vector2(1.2f, 0.9f), new Vector2(0f, 0.45f), 0.5f, 1.5f,
                b =>
                {
                    b.walkSpeed = 2f;
                    b.tellTime = 0.5f;
                    b.lungeSpeed = 7f;
                    b.lungeTime = 0.35f;
                    b.stance = 18f;
                });

            // Little enemy: quick, draws back and stabs
            var little = ImportMonster("monster_little", MonsterPPU, true,
                new Anim("walk", 48, 0, Insect("Little-Enemy", "Walk")), new Anim("attack", 48, 0, Insect("Little-Enemy", "Attack")),
                new Anim("death", 48, 0, Insect("Little-Enemy", "Death")));
            p.littleEnemy = MakeBug<BeetleBug>("LittleEnemy", little["walk"],
                new[]
                {
                    Clip("walk", little["walk"], 9f), Clip("tell", ArenaArt.Pick(little["attack"], 0, 1, 2), 7f, false),
                    Clip("lunge", ArenaArt.Pick(little["attack"], 3, 4, 5, 6), 18f, false), Clip("death", little["death"], 12f, false),
                },
                m, MonsterBlood, 26, 11, new Vector2Int(2, 3), false, new Vector2(1.1f, 1.1f), new Vector2(0f, 0.55f), 0.6f, 1.6f,
                b =>
                {
                    b.walkSpeed = 3f;
                    b.tellTime = 0.4f;
                    b.lungeSpeed = 11f;
                    b.lungeTime = 0.2f;
                    b.stance = 20f;
                });

            // Tall enemy: slow and tough, a long sweeping slash
            var tall = ImportMonster("monster_tall", MonsterPPU, true,
                new Anim("walk", 48, 0, Insect("Tall-Enemy", "Walk")), new Anim("attack", 48, 0, Insect("Tall-Enemy", "Attack")),
                new Anim("death", 48, 0, Insect("Tall-Enemy", "Death")));
            p.tallEnemy = MakeBug<BeetleBug>("TallEnemy", tall["walk"],
                new[]
                {
                    Clip("walk", tall["walk"], 6f), Clip("tell", ArenaArt.Pick(tall["attack"], 0, 1, 2), 5f, false),
                    Clip("lunge", ArenaArt.Pick(tall["attack"], 3, 4, 5), 12f, false), Clip("death", tall["death"], 10f, false),
                },
                m, MonsterBlood, 48, 15, new Vector2Int(3, 5), false, new Vector2(0.7f, 1.7f), new Vector2(0f, 0.85f), 0.95f, 2.3f,
                b =>
                {
                    b.walkSpeed = 1.7f;
                    b.attackRange = 2.8f;
                    b.tellTime = 0.65f;
                    b.lungeSpeed = 6f;
                    b.lungeTime = 0.28f;
                    b.recoverTime = 0.8f;
                    b.stance = 34f;
                });

            // Fly enemy: hovers like the wasps, swipes as it dives
            var fly = ImportMonster("monster_fly", MonsterPPU, false,
                new Anim("fly", 48, 0, Insect("Fly-Enemy", "Idle")), new Anim("attack", 48, 0, Insect("Fly-Enemy", "Attack")),
                new Anim("death", 48, 0, Insect("Fly-Enemy", "Death")));
            p.flyEnemy = MakeBug<WaspBug>("FlyEnemy", fly["fly"],
                new[] { Clip("fly", fly["fly"], 10f), Clip("attack", fly["attack"], 15f, false), Clip("death", fly["death"], 12f, false) },
                m, MonsterBlood, 16, 9, new Vector2Int(1, 3), true, new Vector2(1f, 1f), Vector2.zero, 0f, 0.9f,
                b =>
                {
                    b.flySpeed = 4.5f;
                    b.diveSpeed = 11f;
                    b.stance = 14f;
                });

            // The Obsidian Crab: the floor 30 boss (its east-facing frames)
            var crab = ImportMonster("boss_crab", CrabPPU, true,
                new Anim("idle", 96, 0, Crab("Idle")), new Anim("walk", 96, 0, Crab("Walk")),
                new Anim("attack", 96, 0, Crab("Attack")), new Anim("death", 96, 0, Crab("Death")));
            p.crab = MakeBug<ObsidianCrab>("ObsidianCrab", crab["idle"],
                new[]
                {
                    Clip("idle", crab["idle"], 8f), Clip("walk", crab["walk"], 12f),
                    Clip("tell", ArenaArt.Pick(crab["attack"], 0, 1, 2, 3), 7f, false),    // claw rises and opens
                    Clip("attack", ArenaArt.Pick(crab["attack"], 4, 5, 6, 7, 8), 12f, false), // snaps shut
                    Clip("death", crab["death"], 8f, false),
                },
                m, new Color(1f, 0.45f, 0.3f), 380, 18, new Vector2Int(40, 50), false, new Vector2(3f, 2f), new Vector2(0f, 1f), 1.3f, 3.2f,
                b =>
                {
                    b.simple = true;
                    b.shotPrefab = p.bossShot;
                    b.lineSprite = ArenaArt.Load(ArtDir, "fx_cutline");
                    b.debrisSprite = ArenaArt.Load(ArtDir, "fx_rock");
                    b.superArmor = true;
                    b.stance = 140f;
                    b.stanceBreakTime = 3f;
                    b.heartDropChance = 1f;
                    b.GetComponent<Rigidbody2D>().mass = 20f;
                });
        }

        /// <summary>
        /// Copies the frames of each animation into one strip (Art/Group/name.png), slices it and returns
        /// the frames by animation name. feet: each animation stands on the lowest solid pixel of its
        /// first frame; otherwise the frames hang from their middle (flyers).
        /// </summary>
        static Dictionary<string, Sprite[]> ImportMonster(string name, float ppu, bool feet, params Anim[] anims)
        {
            int size = anims[0].size;
            var frames = new List<Color[]>();
            var pivots = new List<Vector2>();
            var ranges = new List<(string anim, int start, int count)>();
            foreach (var a in anims)
            {
                int start = frames.Count;
                foreach (var file in a.files)
                {
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(file));
                    int y = tex.height - (a.row + 1) * a.size; // texture rows count from the bottom
                    for (int x = 0; x + a.size <= tex.width; x += a.size) frames.Add(tex.GetPixels(x, y, a.size, a.size));
                    Object.DestroyImmediate(tex);
                }
                float pivotY = feet ? Bottom(frames[start], size) / size : 0.5f;
                for (int i = start; i < frames.Count; i++) pivots.Add(new Vector2(0.5f, pivotY));
                ranges.Add((a.name, start, frames.Count - start));
            }

            var strip = new Texture2D(size * frames.Count, size, TextureFormat.RGBA32, false);
            for (int i = 0; i < frames.Count; i++) strip.SetPixels(i * size, 0, size, size, frames[i]);
            var path = $"{GroupArtDir}/{name}.png";
            File.WriteAllBytes(path, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ArenaArt.Slice(path, name, size, size, frames.Count, pivots.ToArray(), ppu, FilterMode.Point, false);

            var all = ArenaArt.Frames(path);
            return ranges.ToDictionary(r => r.anim, r => all.Skip(r.start).Take(r.count).ToArray());
        }

        /// <summary>The lowest row (from the bottom) with a solid pixel.</summary>
        static float Bottom(Color[] px, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (px[y * size + x].a > 0.5f) return y;
            return 0f;
        }
    }
}
