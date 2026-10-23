using System;
using PixelStudy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Menus
{
    [Serializable]
    public class DifficultyCard
    {
        public Difficulty difficulty;
        public Button button;
        public GameObject selectedFrame;
        public GameObject selectedBadge;
    }

    /// <summary>Easy / Normal / Hard cards with Back and Start.</summary>
    public class DifficultySelectController : MonoBehaviour
    {
        public UIScreen screen;
        public DifficultyCard[] cards;
        public Button backButton;
        public Button startButton;

        Action onBack;
        Action<Difficulty> onStart;
        Difficulty current;

        public bool IsOpen => screen.IsOpen;

        void Awake()
        {
            foreach (var card in cards)
            {
                var c = card;
                c.button.onClick.AddListener(() => Select(c.difficulty));
            }
            backButton.onClick.AddListener(() => onBack?.Invoke());
            startButton.onClick.AddListener(() => onStart?.Invoke(current));
        }

        public void Open(Action back, Action<Difficulty> start)
        {
            onBack = back;
            onStart = start;
            Select(GameSession.SelectedDifficulty);
            screen.Show();
        }

        public void Close() => screen.Hide();

        void Select(Difficulty difficulty)
        {
            current = difficulty;
            GameSession.SelectedDifficulty = difficulty;
            foreach (var card in cards)
            {
                bool on = card.difficulty == difficulty;
                if (card.selectedFrame != null) card.selectedFrame.SetActive(on);
                if (card.selectedBadge != null) card.selectedBadge.SetActive(on);
            }
        }
    }
}
