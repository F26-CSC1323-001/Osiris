using System.Collections;
using System.Collections.Generic;
using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Runs a floor: spawns waves of bugs (or the boss on every 10th floor), opens the exit when
    /// they're all dead, and hands off to the quiz / Floor Cleared flow when the player walks out.
    /// Difficulty and floor number scale bug health, damage and how fast they attack.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        public static ArenaDirector Instance { get; private set; }

        // Read by bugs when they spawn.
        public static float HealthScale = 1f;
        public static float DamageScale = 1f;
        public static float TelegraphScale = 1f;

        [Header("Bugs")]
        public BugEnemy beetlePrefab;
        public BugEnemy waspPrefab;
        public BugEnemy tickPrefab;
        [Tooltip("From floor 5.")]
        public BugEnemy pillBugPrefab;
        [Tooltip("From floor 11.")]
        public BugEnemy termitePrefab;
        [Tooltip("From floor 15.")]
        public BugEnemy scorpionPrefab;
        [Tooltip("From floor 21.")]
        public BugEnemy fireflyPrefab;
        [Tooltip("Elite: one per floor on floors 25-29.")]
        public BugEnemy stagBeetlePrefab;

        [Header("The group's monsters (Assets/Sprites/Enemy)")]
        [Tooltip("From floor 1.")]
        public BugEnemy slimePrefab;
        [Tooltip("From floor 2.")]
        public BugEnemy littleEnemyPrefab;
        [Tooltip("From floor 4. Flies.")]
        public BugEnemy flyEnemyPrefab;
        [Tooltip("From floor 7.")]
        public BugEnemy tallEnemyPrefab;

        [Tooltip("One per boss floor, in order: floor 10, 20, 30 (see GameSession.BossNames).")]
        public BugEnemy[] bossPrefabs;

        [Header("Drops")]
        public Pickup coinPrefab;
        public Pickup heartPrefab;

        [Header("Arena")]
        [Tooltip("Shown when the floor starts (\"SUNKEN CRYPT\").")]
        public string mapTitle = "Ruined Hall";
        [Tooltip("Boss arenas: which boss lives here (0 Brood Mother, 1 Hornet Queen, 2 Obsidian Crab). -1 for normal maps.")]
        public int arenaBoss = -1;
        [Tooltip("Spots the boss uses in its arena (the Brood Mother's egg nests).")]
        public Transform[] bossProps;
        public ArenaPlayer player;
        public Transform[] groundSpawns;
        public Transform[] airSpawns;
        public Transform bossSpawn;
        public ExitDoor exitDoor;
        public Rect flyBounds = new Rect(2.5f, 3.5f, 59f, 19f);

        [Header("Testing")]
        [Tooltip("F9 kills every bug on screen. Editor / development builds only.")]
        public bool devHotkeys = true;

        readonly List<BugEnemy> alive = new List<BugEnemy>();

        /// <summary>Bugs currently in the arena (used by targeting skills).</summary>
        public IReadOnlyList<BugEnemy> Alive => alive;
        int pendingSpawns;
        bool exitTriggered, bossEntered;
        string objective = "";

        void Awake()
        {
            Instance = this;
            Pickup.MagnetAll = false;
            ArenaLayers.ConfigureCollisions();
            GameSession.EnsureRun();

            var run = GameSession.Run;
            int floor = Mathf.Max(1, run.floor);
            // Rises early on, then levels off: ~2.5x health and ~1.6x damage by floor 30. Later floors
            // get harder through new enemies instead.
            float curve = 1f - Mathf.Exp(-(floor - 1) / 30f);
            HealthScale = (1f + 2.4f * curve) * (1f + 0.5f * run.newGamePlus);
            DamageScale = 1f + 1f * curve + 0.25f * run.newGamePlus;
            TelegraphScale = run.difficulty switch
            {
                Difficulty.Easy => 1.3f,
                Difficulty.Hard => 0.75f,
                _ => 1f,
            };
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => StartCoroutine(RunFloor());

        IEnumerator RunFloor()
        {
            var run = GameSession.Run;
            // a boss arena always fields its boss (so it can also be played straight from the editor)
            bool boss = GameSession.IsBossFloor(run.floor) || arenaBoss >= 0;
            SetObjective("");
            yield return new WaitForSeconds(0.8f);

            if (boss)
            {
                var ui = GameUIController.Instance;
                if (ui != null) ui.ShowBossWarning(() => bossEntered = true);
                else bossEntered = true;
                yield return new WaitUntil(() => bossEntered);
                yield return new WaitForSeconds(0.4f);
                int index = arenaBoss >= 0 ? arenaBoss : GameSession.BossIndex(run.floor);
                SetObjective("DEFEAT " + GameSession.BossNames[index % GameSession.BossNames.Length].ToUpperInvariant());
                SpawnBug(BossAt(index), bossSpawn.position);
                yield return new WaitUntil(() => pendingSpawns == 0 && alive.Count == 0);
            }
            else
            {
                int waves = Mathf.Clamp(2 + (run.floor - 1) / 4, 2, 4);
                for (int w = 1; w <= waves; w++)
                {
                    Toast(w == 1 && !string.IsNullOrEmpty(mapTitle) ? $"{mapTitle.ToUpperInvariant()}   ·   WAVE 1 / {waves}" : $"WAVE {w} / {waves}");
                    var wave = BuildWave(run.floor, w);
                    // floors 25-29: an elite Stag Beetle joins the last wave
                    if (w == waves && run.floor >= 25 && run.floor < GameSession.FinalFloor && stagBeetlePrefab != null) wave.Add(stagBeetlePrefab);
                    foreach (var prefab in wave)
                    {
                        bool flyer = prefab == waspPrefab || prefab == fireflyPrefab || prefab == flyEnemyPrefab;
                        SpawnBug(prefab, PickSpawn(flyer ? airSpawns : groundSpawns));
                        yield return new WaitForSeconds(0.25f);
                    }
                    yield return new WaitUntil(() => pendingSpawns == 0 && alive.Count == 0);
                    yield return new WaitForSeconds(w < waves ? 1.2f : 0.6f);
                }
            }

            OpenExit();
        }

        public BugEnemy BossFor(int floor) => BossAt(GameSession.BossIndex(floor));

        BugEnemy BossAt(int index)
        {
            if (bossPrefabs == null || bossPrefabs.Length == 0) return null;
            return bossPrefabs[Mathf.Clamp(index, 0, bossPrefabs.Length - 1)];
        }

        /// <summary>
        /// A wave: bugs picked at random (weighted) until the floor's budget is spent. Tougher bugs cost
        /// more and only show up from their floor on (Mockup 2's 30 floors): ticks 3, pill bugs 5,
        /// termite guards 11, scorpions 15, firefly bombers 21. The group's monsters mix in from the
        /// start: slimes 1, little enemies 2, fly enemies 4, tall enemies 7.
        /// </summary>
        List<BugEnemy> BuildWave(int floor, int wave)
        {
            var pool = new List<(BugEnemy prefab, int cost, float weight)> { (beetlePrefab, 1, 3f) };
            if (slimePrefab != null) pool.Add((slimePrefab, 1, 2.5f));
            if (floor >= 2 && littleEnemyPrefab != null) pool.Add((littleEnemyPrefab, 1, 2.2f));
            if (floor >= 4 && flyEnemyPrefab != null) pool.Add((flyEnemyPrefab, 1, 2f));
            if (floor >= 7 && tallEnemyPrefab != null) pool.Add((tallEnemyPrefab, 2, 1.6f));
            if (floor >= 2 && waspPrefab != null) pool.Add((waspPrefab, 1, 2.5f));
            if (floor >= 3 && tickPrefab != null) pool.Add((tickPrefab, 2, 1.6f));
            if (floor >= 5 && pillBugPrefab != null) pool.Add((pillBugPrefab, 2, 1.8f));
            if (floor >= 11 && termitePrefab != null) pool.Add((termitePrefab, 2, 1.5f));
            if (floor >= 15 && scorpionPrefab != null) pool.Add((scorpionPrefab, 3, 1.5f));
            if (floor >= 21 && fireflyPrefab != null) pool.Add((fireflyPrefab, 2, 1.6f));

            var list = new List<BugEnemy>();
            int budget = Mathf.Min(3 + floor / 2 + wave, 12);
            while (budget > 0)
            {
                float total = 0f;
                foreach (var e in pool) if (e.cost <= budget) total += e.weight;
                float r = Random.value * total;
                var pick = pool[0];
                foreach (var e in pool)
                {
                    if (e.cost > budget) continue;
                    pick = e;
                    if ((r -= e.weight) <= 0f) break;
                }
                list.Add(pick.prefab);
                budget -= pick.cost;
            }
            return list;
        }

        Vector2 PickSpawn(Transform[] points)
        {
            var p = player != null ? (Vector2)player.transform.position : Vector2.zero;
            for (int tries = 0; tries < 8; tries++)
            {
                var s = points[Random.Range(0, points.Length)].position;
                if (Vector2.Distance(s, p) > 5f) return s;
            }
            return points[Random.Range(0, points.Length)].position;
        }

        /// <summary>Spawns a bug with a portal effect (also used by the boss to summon wasps).</summary>
        public void SpawnBug(BugEnemy prefab, Vector2 position)
        {
            if (prefab == null) return;
            StartCoroutine(SpawnRoutine(prefab, position));
        }

        IEnumerator SpawnRoutine(BugEnemy prefab, Vector2 position)
        {
            pendingSpawns++;
            Sfx.Play("spawn", 0.35f);
            var color = new Color(0.75f, 0.4f, 1f, 0.9f);
            ArenaFX.Ring(position + Vector2.up * 0.5f, color, 2.5f, 0.3f, 0.5f);
            ArenaFX.Glow(position + Vector2.up * 0.5f, new Color(0.7f, 0.35f, 1f, 0.6f), 2f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                ArenaFX.Burst(position + Vector2.up * 0.5f, color, 3, 2f, 0.1f, 0.4f, 1f);
                yield return new WaitForSeconds(0.15f);
            }
            var bug = Instantiate(prefab, position, Quaternion.identity);
            bug.Died += OnBugDied;
            alive.Add(bug);
            pendingSpawns--;
            UpdateObjective();
        }

        void OnBugDied(BugEnemy bug)
        {
            alive.Remove(bug);
            UpdateObjective();
        }

        void UpdateObjective()
        {
            if (exitDoor != null && exitDoor.IsOpen) return;
            if (GameSession.IsBossFloor(GameSession.Run.floor)) return;
            int left = alive.Count + pendingSpawns;
            SetObjective(left > 0 ? $"BUGS LEFT: {left}" : "");
        }

        void OpenExit()
        {
            if (exitDoor == null) return;
            Pickup.MagnetAll = true;
            exitDoor.Open();
            SetObjective("EXIT OPEN  →");
            Toast("Arena cleared! Head for the exit.");
            if (heartPrefab != null && player != null) DropHeart(player.Center + Vector2.up * 2f);
        }

        public void DropCoins(Vector2 position, int count)
        {
            if (coinPrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                var c = Instantiate(coinPrefab, position, Quaternion.identity);
                c.Pop(new Vector2(Random.Range(-4f, 4f), Random.Range(5f, 9f)));
            }
        }

        public void DropHeart(Vector2 position)
        {
            if (heartPrefab == null) return;
            var h = Instantiate(heartPrefab, position, Quaternion.identity);
            h.Pop(new Vector2(Random.Range(-1.5f, 1.5f), 7f));
        }

        void Update()
        {
            if (Time.timeScale < 0.01f) return;

            if (!exitTriggered && exitDoor != null && exitDoor.IsOpen && player != null && !player.IsDead && exitDoor.Contains(player.transform.position))
            {
                exitTriggered = true;
                SetObjective("");
                var ui = GameUIController.Instance;
                if (ui != null) ui.CompleteFloor();
            }

            if (devHotkeys && Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F9)) KillAll();
        }

        /// <summary>Testing helper: kills every bug currently alive.</summary>
        public void KillAll()
        {
            foreach (var bug in alive.ToArray())
                if (bug != null && !bug.IsDead) bug.ForceKill();
        }

        void SetObjective(string text)
        {
            if (objective == text) return;
            objective = text;
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null) ui.hud.SetObjective(text);
        }

        static void Toast(string message)
        {
            var ui = GameUIController.Instance;
            if (ui != null) ui.ShowToast(message);
        }
    }
}
