using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelStudy
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Everything about the current run. Saved to PlayerPrefs as JSON.</summary>
    [Serializable]
    public class RunData
    {
        public Difficulty difficulty = Difficulty.Normal;
        public int floor = 1;
        public int coins;
        public int upgradeTokens;
        public int maxHealth = 100;
        public int health = 100;
        public int correctAnswers;
        public int totalCoinsEarned;
        public int floorsCleared;
        public int attackBonusPercent;
        public int dodgeBonusPercent;
        /// <summary>Run speed bonus from Swift Boots (Mockup 2's knight has no roll for a dodge bonus).</summary>
        public int speedBonusPercent;
        public int newGamePlus;
        /// <summary>Skills unlocked during the run (the starting skills aren't listed). See SkillCatalog.</summary>
        public List<string> unlockedSkills = new List<string>();
        /// <summary>Scene of the map this (normal) floor is played on. Picked at random each floor.</summary>
        public string map;
        /// <summary>The ultimate's gauge, 0..1 (carries over between floors).</summary>
        public float ultCharge;
    }

    /// <summary>
    /// Shared game state between the menu scene and the game scene.
    /// Gameplay code can read <see cref="Run"/> and call the helpers below; the HUD updates
    /// itself whenever <see cref="NotifyChanged"/> is called.
    /// </summary>
    public static class GameSession
    {
        public const string MainMenuScene = "MainMenu";
        // The default arena (SampleScene is the group's own test scene).
        public const string GameScene = "Arena";

        /// <summary>Maps for normal floors; each floor picks one at random (never the same one twice in a row).</summary>
        public static readonly string[] Maps = { "Arena", "Arena_Crypt", "Arena_Ramparts", "Arena_Forge" };

        /// <summary>Each boss fights in its own arena (same order as BossNames).</summary>
        public static readonly string[] BossMaps = { "Boss_Nest", "Boss_Hive", "Boss_Crab" };

        // Mockup 2: 30 floors, a boss on 10, 20 and 30.
        public const int FinalFloor = 30;
        public const int BossEvery = 10;
        public const int ContinueCost = 25;
        public const int QuestionsPerFloor = 3;
        public const int CoinsPerCorrectAnswer = 6;

        // Its own save slot: Main_Dev uses the same product name, so they would share PlayerPrefs otherwise.
        const string SaveKey = "PixelStudy.Mockup2.Save";
        const string DifficultyKey = "PixelStudy.Difficulty";

        public static RunData Run { get; private set; }

        /// <summary>Raised whenever run values (health, coins, floor...) change.</summary>
        public static event Action Changed;

        /// <summary>One-off message shown by the game UI after the next scene load.</summary>
        public static string PendingMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Run = null;
            Changed = null;
            PendingMessage = null;
        }

        public static Difficulty SelectedDifficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
            set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
        }

        public static float EnemyDamageMultiplier(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.75f,
            Difficulty.Hard => 1.35f,
            _ => 1f,
        };

        public static float EnemyDamageMultiplier() => Run == null ? 1f : EnemyDamageMultiplier(Run.difficulty);

        public static bool IsBossFloor(int floor) => floor % BossEvery == 0;

        /// <summary>Bosses in order: floor 10, 20 and the final boss on 30 (the group's Obsidian Crab). Each fights in its own arena.</summary>
        public static readonly string[] BossNames = { "The Brood Mother", "The Hornet Queen", "The Obsidian Crab" };

        /// <summary>0 Brood Mother, 1 Hornet Queen, 2 Obsidian Crab.</summary>
        public static int BossIndex(int floor) => Mathf.Max(0, floor / BossEvery - 1) % BossNames.Length;

        public static string BossName(int floor) => BossNames[BossIndex(floor)];

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);

        public static RunData PeekSave()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<RunData>(PlayerPrefs.GetString(SaveKey)); }
            catch (ArgumentException) { return null; }
        }

        public static void StartNewRun(Difficulty difficulty)
        {
            Run = new RunData { difficulty = difficulty };
            PickMap();
            Save();
            NotifyChanged();
        }

        /// <summary>Picks the map for the current floor (a different one from last floor).</summary>
        public static void PickMap()
        {
            if (Run == null) return;
            var choices = Array.FindAll(Maps, m => m != Run.map);
            Run.map = choices[UnityEngine.Random.Range(0, choices.Length)];
        }

        /// <summary>The scene the current floor is played in: the boss's own arena, or this floor's map.</summary>
        public static string SceneForFloor(RunData run)
        {
            if (run == null) return GameScene;
            if (IsBossFloor(run.floor)) return BossMaps[BossIndex(run.floor) % BossMaps.Length];
            return string.IsNullOrEmpty(run.map) || Array.IndexOf(Maps, run.map) < 0 ? GameScene : run.map;
        }

        /// <summary>New Game+: keeps upgrades, coins and tokens, starts again from floor 1.</summary>
        public static void StartNewGamePlus()
        {
            var old = Run ?? new RunData { difficulty = SelectedDifficulty };
            Run = new RunData
            {
                difficulty = old.difficulty,
                newGamePlus = old.newGamePlus + 1,
                coins = old.coins,
                upgradeTokens = old.upgradeTokens,
                maxHealth = old.maxHealth,
                health = old.maxHealth,
                attackBonusPercent = old.attackBonusPercent,
                dodgeBonusPercent = old.dodgeBonusPercent,
                speedBonusPercent = old.speedBonusPercent,
                unlockedSkills = new List<string>(old.unlockedSkills ?? new List<string>()),
            };
            PickMap();
            Save();
            NotifyChanged();
        }

        public static bool LoadSave()
        {
            var data = PeekSave();
            if (data == null) return false;
            if (data.unlockedSkills == null) data.unlockedSkills = new List<string>();
            Run = data;
            NotifyChanged();
            return true;
        }

        /// <summary>Makes sure there is a run, so the game scene can be played directly from the editor.</summary>
        public static void EnsureRun()
        {
            if (Run == null && !LoadSave()) StartNewRun(SelectedDifficulty);
        }

        public static void Save()
        {
            if (Run == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Run));
            PlayerPrefs.Save();
        }

        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static void AddCoins(int amount)
        {
            if (Run == null) return;
            Run.coins += amount;
            if (amount > 0) Run.totalCoinsEarned += amount;
            NotifyChanged();
        }

        /// <summary>Applies damage (already scaled however the caller wants). Returns true if the player died.</summary>
        public static bool Damage(int amount)
        {
            if (Run == null) return false;
            Run.health = Mathf.Max(0, Run.health - amount);
            NotifyChanged();
            return Run.health <= 0;
        }

        public static void Heal(int amount)
        {
            if (Run == null) return;
            Run.health = Mathf.Min(Run.maxHealth, Run.health + amount);
            NotifyChanged();
        }

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuScene);
        }

        /// <summary>Loads the current floor (its random map, or the boss's arena on boss floors).</summary>
        public static void LoadGame()
        {
            Time.timeScale = 1f;
            EnsureRun();
            SceneManager.LoadScene(SceneForFloor(Run));
        }
    }
}
