using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Menus
{
    /// <summary>Main menu scene: wires the five menu buttons to their screens.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public UIScreen mainScreen;
        public Button newGameButton;
        public Button continueButton;
        public Button difficultyButton;
        public Button generatorButton;
        public Button settingsButton;
        public TMP_Text infoText;

        [Header("New game confirmation")]
        public UIScreen confirmScreen;
        public TMP_Text confirmBody;
        public Button confirmStartButton;
        public Button confirmCancelButton;

        [Header("Sub screens")]
        public DifficultySelectController difficulty;
        public QuestionGeneratorController generator;
        public SettingsPanel settings;

        void Awake()
        {
            UIUtil.EnsureEventSystem();
            newGameButton.onClick.AddListener(RequestNewGame);
            continueButton.onClick.AddListener(ContinueGame);
            difficultyButton.onClick.AddListener(OpenDifficulty);
            generatorButton.onClick.AddListener(OpenGenerator);
            settingsButton.onClick.AddListener(OpenSettings);
            confirmStartButton.onClick.AddListener(StartNewGame);
            confirmCancelButton.onClick.AddListener(() => confirmScreen.Hide());
        }

        void Start()
        {
            Time.timeScale = 1f;
            RefreshInfo();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        void HandleBack()
        {
            if (confirmScreen.IsOpen) confirmScreen.Hide();
            else if (settings.HandleBack()) { }
            else if (difficulty.IsOpen) BackToMain();
            else if (generator.IsOpen) BackToMain();
        }

        void RefreshInfo()
        {
            var save = GameSession.PeekSave();
            continueButton.interactable = save != null;
            var text = $"Difficulty: <color=#F2C14E>{GameSession.SelectedDifficulty}</color>";
            if (save != null) text += $"    ·    Saved run: <color=#F2C14E>Floor {save.floor}</color> ({save.difficulty})";
            infoText.text = text;
        }

        void BackToMain()
        {
            if (difficulty.IsOpen) difficulty.Close();
            if (generator.IsOpen) generator.Close();
            RefreshInfo();
            mainScreen.Show();
        }

        void RequestNewGame()
        {
            var save = GameSession.PeekSave();
            if (save == null)
            {
                StartNewGame();
                return;
            }
            confirmBody.text = $"Your saved run on Floor {save.floor} ({save.difficulty}) will be replaced.";
            confirmScreen.Show();
        }

        void StartNewGame()
        {
            GameSession.StartNewRun(GameSession.SelectedDifficulty);
            GameSession.LoadGame();
        }

        void ContinueGame()
        {
            if (GameSession.LoadSave()) GameSession.LoadGame();
        }

        void OpenDifficulty()
        {
            mainScreen.Hide();
            difficulty.Open(BackToMain, d => RequestNewGame());
        }

        void OpenGenerator()
        {
            mainScreen.Hide();
            generator.Open(BackToMain);
        }

        void OpenSettings()
        {
            mainScreen.Hide();
            settings.Open(BackToMain);
        }
    }
}
