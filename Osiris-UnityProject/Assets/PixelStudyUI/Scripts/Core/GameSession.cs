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
        public int newGamePlus;
        /// <summary>Skills unlocked during the run (the starting skills aren't listed). See SkillCatalog.</summary>
        public List<string> unlockedSkills = new List<string>();
    }

    /// <summary>
    /// Shared game state between the menu scene and the game scene.
    /// Gameplay code can read <see cref="Run"/> and call the helpers below; the HUD updates
    /// itself whenever <see cref="NotifyChanged"/> is called.
    /// </summary>
    public static class GameSession
    {
        public const string MainMenuScene = "MainMenu";
        // The scene each floor is played in. (SampleScene is still there as the movement test scene.)
        public const string GameScene = "Arena";

        public const int FinalFloor = 30;
        public const int BossEvery = 10;
        public const int ContinueCost = 25;
        public const int QuestionsPerFloor = 3;
        public const int CoinsPerCorrectAnswer = 6;

        const string SaveKey = "PixelStudy.Save";
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

        /// <summary>Mockup 2 has no bosses: every floor is beetles and wasps. Set to true to bring them back.</summary>
        public static readonly bool BossesEnabled = false;

        public static bool IsBossFloor(int floor) => BossesEnabled && floor % BossEvery == 0;

        /// <summary>Bosses in order: floor 10, 20, 30 (then repeating). The arena spawns the matching one.</summary>
        public static readonly string[] BossNames = { "The Brood Mother", "The Hornet Queen", "The Reaper Mantis" };

        /// <summary>0 = Brood Mother, 1 = Hornet Queen, 2 = Reaper Mantis.</summary>
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
            Save();
            NotifyChanged();
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
                unlockedSkills = new List<string>(old.unlockedSkills ?? new List<string>()),
            };
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

        public static void LoadGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameScene);
        }
    }
}
