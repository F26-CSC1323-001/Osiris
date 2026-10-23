using System.Collections;
using System.Linq;
using PixelStudy.Menus;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelStudy.Game
{
    /// <summary>
    /// Runs every in-game screen (HUD, pause, quiz, upgrades, floor cleared, boss warning,
    /// game over, victory) and pauses the game while one of them is open.
    ///
    /// Gameplay code hooks in through the static helpers:
    ///   GameUIController.Instance.CompleteFloor();   // player reached the end of the floor
    ///   GameUIController.Instance.DamagePlayer(10);  // an enemy hit the player
    ///   GameSession.AddCoins(5);                     // picked up coins
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        public static GameUIController Instance { get; private set; }

        [Header("Panels")]
        public HUDController hud;
        public QuizPanel quiz;
        public UpgradePanel upgrade;
        public SettingsPanel settings;

        [Header("Pause")]
        public UIScreen pauseScreen;
        public Button resumeButton;
        public Button pauseSettingsButton;
        public Button quitButton;
        public TMP_Text pauseInfoText;

        [Header("Floor Cleared")]
        public UIScreen floorClearedScreen;
        public TMP_Text floorClearedTitle;
        public TMP_Text floorCoinsText;
        public TMP_Text floorCorrectText;
        public TMP_Text floorUpgradeText;
        public GameObject floorSkillRow;
        public TMP_Text floorSkillText;
        public Button floorContinueButton;
        public TMP_Text floorContinueLabel;

        [Header("Boss Warning")]
        public UIScreen bossWarningScreen;
        public TMP_Text bossFloorText;
        public TMP_Text bossNameText;
        public Button bossEnterButton;

        [Header("Game Over")]
        public UIScreen gameOverScreen;
        public TMP_Text gameOverFloorText;
        public TMP_Text gameOverCorrectText;
        public TMP_Text gameOverCoinsText;
        public TMP_Text gameOverWarningText;
        public Button gameOverContinueButton;
        public TMP_Text gameOverContinueLabel;
        public Button gameOverRetryButton;
        public Button gameOverMenuButton;

        [Header("Victory")]
        public UIScreen victoryScreen;
        public TMP_Text victoryFloorsText;
        public TMP_Text victoryCoinsText;
        public TMP_Text victoryCorrectText;
        public Button victoryNewGamePlusButton;
        public Button victoryMenuButton;

        [Header("Toast")]
        public CanvasGroup toastGroup;
        public TMP_Text toastText;

        [Header("Testing")]
        [Tooltip("F5 finish floor · F6 take damage · F7 jump to boss floor · F8 victory. Editor / development builds only.")]
        public bool devHotkeys = true;
        public GameObject devHint;

        UIScreen[] modals;
        bool wasModal;
        int floorCoins;
        int pendingTier;
        string pendingSkill;
        Coroutine toastRoutine;
        Coroutine gameOverRoutine;
        bool bossIntroDone;
        System.Action bossEnterCallback;

        RunData Run => GameSession.Run;

        bool AnyModalOpen => modals.Any(m => m != null && m.IsOpen);

        void Awake()
        {
            Instance = this;
            UIUtil.EnsureEventSystem();
            GameSession.EnsureRun();

            modals = new[]
            {
                quiz.screen, upgrade.screen, settings.screen, settings.keyBindingsScreen, pauseScreen,
                floorClearedScreen, bossWarningScreen, gameOverScreen, victoryScreen,
            };
            foreach (var m in modals) m.HideImmediate();
            toastGroup.alpha = 0f;
            hud.SetBoss(false, 1f, true);

            resumeButton.onClick.AddListener(() => pauseScreen.Hide());
            pauseSettingsButton.onClick.AddListener(() => { pauseScreen.Hide(); settings.Open(() => pauseScreen.Show()); });
            quitButton.onClick.AddListener(QuitToMenu);
            floorContinueButton.onClick.AddListener(OnFloorContinue);
            bossEnterButton.onClick.AddListener(OnBossEnter);
            gameOverContinueButton.onClick.AddListener(OnGameOverContinue);
            gameOverRetryButton.onClick.AddListener(OnRetry);
            gameOverMenuButton.onClick.AddListener(OnGameOverMenu);
            victoryNewGamePlusButton.onClick.AddListener(() => { GameSession.StartNewGamePlus(); GameSession.LoadGame(); });
            victoryMenuButton.onClick.AddListener(() => { GameSession.ClearSave(); GameSession.LoadMainMenu(); });
        }

        void Start()
        {
            hud.Refresh(true);
            if (devHint != null) devHint.SetActive(devHotkeys && Debug.isDebugBuild);
            if (!string.IsNullOrEmpty(GameSession.PendingMessage))
            {
                ShowToast(GameSession.PendingMessage);
                GameSession.PendingMessage = null;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settings.HandleBack()) { }
                else if (pauseScreen.IsOpen) pauseScreen.Hide();
                else if (!AnyModalOpen) OpenPause();
            }

            if (devHotkeys && Debug.isDebugBuild && !AnyModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.F5)) CompleteFloor();
                if (Input.GetKeyDown(KeyCode.F7)) JumpToNextBossFloor();
                if (Input.GetKeyDown(KeyCode.F8)) ShowVictory();
            }

            // Freeze gameplay while any popup is open.
            bool modal = AnyModalOpen;
            if (modal != wasModal)
            {
                Time.timeScale = modal ? 0f : 1f;
                wasModal = modal;
            }
        }

        // ---------- Hooks for gameplay code ----------

        /// <summary>
        /// Call when the player reaches the end of a floor. Boss floors use the boss quiz rules; if the
        /// boss warning hasn't been shown yet this floor, it's shown first.
        /// </summary>
        public void CompleteFloor()
        {
            if (AnyModalOpen) return;
            if (!GameSession.IsBossFloor(Run.floor)) StartQuiz(false);
            else if (bossIntroDone) StartQuiz(true);
            else ShowBossWarning(null);
        }

        /// <summary>
        /// Shows "Boss Ahead". When the player presses Enter, onEnter runs (the arena spawns the boss);
        /// without a callback the boss quiz starts straight away.
        /// </summary>
        public void ShowBossWarning(System.Action onEnter)
        {
            bossEnterCallback = onEnter;
            bossFloorText.text = $"FLOOR {Run.floor}";
            if (bossNameText != null) bossNameText.text = GameSession.BossName(Run.floor).ToUpperInvariant();
            bossWarningScreen.Show();
        }

        void OnBossEnter()
        {
            bossWarningScreen.Hide();
            bossIntroDone = true;
            var callback = bossEnterCallback;
            bossEnterCallback = null;
            if (callback != null) callback();
            else StartQuiz(true);
        }

        /// <summary>Damages the player, scaled by difficulty. Shows Game Over at 0 health.</summary>
        public void DamagePlayer(int amount)
        {
            int scaled = Mathf.Max(1, Mathf.RoundToInt(amount * GameSession.EnemyDamageMultiplier()));
            if (GameSession.Damage(scaled) && gameOverRoutine == null)
                gameOverRoutine = StartCoroutine(GameOverAfterDelay());
        }

        IEnumerator GameOverAfterDelay()
        {
            // Let the death animation play before the menu pauses everything.
            yield return new WaitForSecondsRealtime(quiz.IsOpen ? 0f : 1.2f);
            gameOverRoutine = null;
            if (Run.health <= 0) ShowGameOver();
        }

        public void ShowToast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        // ---------- Flow ----------

        void OpenPause()
        {
            pauseInfoText.text = $"Floor {Run.floor} of {GameSession.FinalFloor}    ·    {Run.difficulty}" +
                                 (Run.newGamePlus > 0 ? $"    ·    NG+{Run.newGamePlus}" : "");
            pauseScreen.Show();
        }

        void StartQuiz(bool boss)
        {
            floorCoins = 0;
            int hits = 0;
            var questions = QuestionBank.Draw(GameSession.QuestionsPerFloor);
            if (boss) hud.SetBoss(true, 1f, true);

            quiz.Begin(questions, Run.floor, boss,
                right =>
                {
                    if (right)
                    {
                        Run.correctAnswers++;
                        floorCoins += GameSession.CoinsPerCorrectAnswer;
                        GameSession.AddCoins(GameSession.CoinsPerCorrectAnswer);
                        if (boss) hud.SetBoss(true, 1f - ++hits / (float)questions.Count);
                        return true;
                    }
                    int damage = Mathf.Max(1, Mathf.RoundToInt((boss ? 15 : 10) * GameSession.EnemyDamageMultiplier()));
                    if (GameSession.Damage(damage))
                    {
                        quiz.Abort();
                        hud.SetBoss(false, 0f);
                        ShowGameOver();
                        return false;
                    }
                    return true;
                },
                (correct, total) => FinishQuiz(boss, correct, total));
        }

        void FinishQuiz(bool boss, int correct, int total)
        {
            hud.SetBoss(false, 0f);
            if (boss && correct < total)
            {
                // Boss rule from the wireframe: miss one and you go back to the start of the floor.
                GameSession.PendingMessage = $"The boss held its ground. Back to the start of Floor {Run.floor}!";
                GameSession.Save();
                ReloadFloor();
                return;
            }

            // Reward depends on the score: 1/3 Bronze, 2/3 Silver, 3/3 Gold (+ a token and a shot at a new skill).
            bool perfect = correct == total;
            pendingTier = Upgrades.TierFor(correct, total);
            pendingSkill = null;
            if (perfect)
            {
                Run.upgradeTokens++;
                if (Random.value < SkillCatalog.PerfectUnlockChance)
                {
                    var skill = SkillCatalog.UnlockRandom(Run);
                    if (skill != null) pendingSkill = $"{skill.Name} [{skill.Key}]";
                }
            }
            Run.floorsCleared++;
            GameSession.NotifyChanged();
            GameSession.Save();

            if (Run.floor >= GameSession.FinalFloor)
            {
                ShowVictory();
                return;
            }

            floorClearedTitle.text = $"Floor {Run.floor} Cleared!";
            floorCoinsText.text = $"+{floorCoins}";
            floorCorrectText.text = $"{correct} of {total}";
            floorUpgradeText.text = pendingTier > 0
                ? $"<color={Upgrades.TierColor(pendingTier)}>{Upgrades.TierName(pendingTier)}</color>" + (perfect ? "  +1 token" : "")
                : "<color=#A597C4>None</color>";
            floorSkillRow.SetActive(pendingSkill != null);
            if (pendingSkill != null) floorSkillText.text = pendingSkill;
            floorContinueLabel.text = $"Continue to Floor {Run.floor + 1}";
            floorClearedScreen.Show();
        }

        void OnFloorContinue()
        {
            floorClearedScreen.Hide();
            if (pendingTier > 0) upgrade.Open(pendingTier, pendingSkill, AdvanceFloor);
            else AdvanceFloor();
        }

        void AdvanceFloor()
        {
            Run.floor++;
            GameSession.Save();
            if (GameSession.IsBossFloor(Run.floor)) GameSession.PendingMessage = $"Floor {Run.floor}: a boss waits at the end of this floor.";
            ReloadFloor();
        }

        /// <summary>Restarts the current floor. Replace with your own level loading when it exists.</summary>
        void ReloadFloor()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void ShowGameOver()
        {
            if (gameOverScreen.IsOpen) return;
            gameOverFloorText.text = Run.floor.ToString();
            gameOverCorrectText.text = Run.correctAnswers.ToString();
            gameOverCoinsText.text = Run.coins.ToString();
            gameOverContinueLabel.text = $"Continue: {GameSession.ContinueCost} Coins";
            bool canAfford = Run.coins >= GameSession.ContinueCost;
            gameOverContinueButton.interactable = canAfford;
            gameOverWarningText.gameObject.SetActive(!canAfford);
            gameOverScreen.Show();
        }

        void OnGameOverContinue()
        {
            if (Run.coins < GameSession.ContinueCost) return;
            Run.coins -= GameSession.ContinueCost;
            Run.health = Mathf.CeilToInt(Run.maxHealth * 0.5f);
            GameSession.NotifyChanged();
            GameSession.Save();
            gameOverScreen.Hide();
            ShowToast($"Back on your feet! -{GameSession.ContinueCost} coins");
        }

        void OnRetry()
        {
            Run.health = Run.maxHealth;
            GameSession.Save();
            ReloadFloor();
        }

        void OnGameOverMenu()
        {
            Run.health = Run.maxHealth;
            GameSession.Save();
            GameSession.LoadMainMenu();
        }

        void QuitToMenu()
        {
            GameSession.Save();
            GameSession.LoadMainMenu();
        }

        void ShowVictory()
        {
            victoryFloorsText.text = Run.floorsCleared.ToString();
            victoryCoinsText.text = Run.totalCoinsEarned.ToString();
            victoryCorrectText.text = Run.correctAnswers.ToString();
            victoryScreen.Show();
        }

        void JumpToNextBossFloor()
        {
            Run.floor = Mathf.Min(GameSession.FinalFloor, (Run.floor / GameSession.BossEvery + 1) * GameSession.BossEvery);
            GameSession.Save();
            GameSession.PendingMessage = $"Dev: jumped to Floor {Run.floor}.";
            ReloadFloor();
        }

        IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(2.8f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 0f;
            toastRoutine = null;
        }
    }
}
