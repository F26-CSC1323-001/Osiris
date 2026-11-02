using System;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Game
{
    [Serializable]
    public class UpgradeCardView
    {
        public Button button;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text current;
    }

    /// <summary>
    /// "Choose Your Upgrade". The tier (Bronze / Silver / Gold) comes from the quiz score;
    /// an upgrade token boosts it one tier.
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        public UIScreen screen;
        public UpgradeCardView[] cards;
        public TMP_Text tierText;
        public Button boostButton;
        public TMP_Text boostLabel;

        Action onDone;
        int tier;
        string newSkill;

        public bool IsOpen => screen.IsOpen;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int n = i;
                cards[i].button.onClick.AddListener(() => Pick(n));
            }
            boostButton.onClick.AddListener(Boost);
        }

        /// <param name="upgradeTier">1 Bronze, 2 Silver, 3 Gold.</param>
        /// <param name="unlockedSkill">Name + key of a skill unlocked this floor, or null.</param>
        public void Open(int upgradeTier, string unlockedSkill, Action done)
        {
            tier = Mathf.Clamp(upgradeTier, 1, Upgrades.MaxTier);
            newSkill = unlockedSkill;
            onDone = done;
            Refresh();
            screen.Show();
        }

        void Refresh()
        {
            var run = GameSession.Run;
            var line = $"<color={Upgrades.TierColor(tier)}>{Upgrades.TierName(tier).ToUpperInvariant()} UPGRADE</color>";
            if (!string.IsNullOrEmpty(newSkill)) line += $"    ·    <color=#5EC8F2>New skill unlocked: {newSkill}</color>";
            tierText.text = line;

            for (int i = 0; i < cards.Length && i < Upgrades.All.Length; i++)
            {
                cards[i].title.text = Upgrades.All[i].Name;
                cards[i].description.text = Upgrades.All[i].Describe(tier);
                cards[i].current.text = Upgrades.All[i].Current(run);
            }

            bool canBoost = tier < Upgrades.MaxTier;
            boostButton.gameObject.SetActive(canBoost);
            if (canBoost)
            {
                boostButton.interactable = run.upgradeTokens > 0;
                boostLabel.text = run.upgradeTokens > 0
                    ? $"Boost to {Upgrades.TierName(tier + 1)}  ·  1 token"
                    : $"Boost to {Upgrades.TierName(tier + 1)}  ·  no tokens";
            }
        }

        void Boost()
        {
            var run = GameSession.Run;
            if (run.upgradeTokens <= 0 || tier >= Upgrades.MaxTier) return;
            run.upgradeTokens--;
            tier++;
            GameSession.NotifyChanged();
            GameSession.Save();
            Refresh();
        }

        void Pick(int i)
        {
            var run = GameSession.Run;
            Upgrades.All[i].Apply(run, tier);
            GameSession.NotifyChanged();
            GameSession.Save();
            screen.Hide();
            var callback = onDone;
            onDone = null;
            callback?.Invoke();
        }
    }
}
