using System.Collections;
using PixelStudy.UI;
using TMPro;
using UnityEngine;

namespace PixelStudy.Game
{
    [System.Serializable]
    public class SkillSlotView
    {
        public GameObject root;
        public UnityEngine.UI.Image frame;
        public UnityEngine.UI.Image icon;
        public UnityEngine.UI.Image cooldownFill;
        public TMP_Text cooldownText;
        public TMP_Text keyText;
        public TMP_Text nameText;
        public UnityEngine.UI.Image keyBadge;
        public GameObject lockIcon;
        [System.NonSerialized] public bool wasReady = true;
        [System.NonSerialized] public bool locked;
    }

    /// <summary>Health, tokens, coins, floor and boss bar. Refreshes itself when GameSession changes.</summary>
    public class HUDController : MonoBehaviour
    {
        public UIBar healthBar;
        public TMP_Text healthText;
        public TMP_Text tokensText;
        public TMP_Text coinsText;
        public TMP_Text floorText;
        public TMP_Text floorSubText;
        public RectTransform heartIcon;
        public RectTransform tokensIcon;
        public RectTransform coinsIcon;
        public GameObject bossFloorIcon;

        [Header("Boss")]
        public GameObject bossGroup;
        public UIBar bossBar;
        public TMP_Text bossNameText;

        [Header("Objective")]
        public GameObject objectiveGroup;
        public TMP_Text objectiveText;

        [Header("Skills")]
        public SkillSlotView[] skillSlots;
        public Color skillReadyFlash = new Color(1f, 0.8f, 0.35f);

        int lastCoins = -1, lastTokens = -1, lastHealth = -1;

        void OnEnable() => GameSession.Changed += OnChanged;

        void OnDisable() => GameSession.Changed -= OnChanged;

        void OnChanged() => Refresh(false);

        public void Refresh(bool instant)
        {
            var run = GameSession.Run;
            if (run == null) return;

            healthBar.SetValue(run.health / (float)Mathf.Max(1, run.maxHealth), instant);
            healthText.text = $"{run.health} / {run.maxHealth}";
            tokensText.text = $"Upgrade Tokens: {run.upgradeTokens}";
            coinsText.text = $"Coins: {run.coins}";
            floorText.text = $"FLOOR {run.floor}";
            bool boss = GameSession.IsBossFloor(run.floor);
            floorSubText.text = boss ? "BOSS FLOOR" : $"of {GameSession.FinalFloor}";
            if (bossFloorIcon != null) bossFloorIcon.SetActive(boss);

            if (!instant)
            {
                if (run.coins != lastCoins) Punch(coinsIcon);
                if (run.upgradeTokens != lastTokens) Punch(tokensIcon);
                if (run.health < lastHealth) Punch(heartIcon);
            }
            lastCoins = run.coins;
            lastTokens = run.upgradeTokens;
            lastHealth = run.health;
        }

        public void SetBoss(bool visible, float normalized, bool instant = false)
        {
            bossGroup.SetActive(visible);
            if (!visible) return;
            bossBar.SetValue(normalized, instant);
            if (bossNameText != null && GameSession.Run != null)
                bossNameText.text = GameSession.BossName(GameSession.Run.floor).ToUpperInvariant();
        }

        /// <summary>
        /// Shows a skill in a slot (icon, key, name). A null icon hides the slot. Locked skills show
        /// dimmed with a padlock.
        /// </summary>
        public void SetSkillSlot(int index, Sprite icon, string key, string displayName, bool locked = false)
        {
            if (skillSlots == null || index < 0 || index >= skillSlots.Length) return;
            var s = skillSlots[index];
            bool show = icon != null;
            s.root.SetActive(show);
            if (!show) return;
            bool justUnlocked = s.locked && !locked;
            s.locked = locked;
            s.icon.sprite = icon;
            s.icon.color = locked ? new Color(0.18f, 0.18f, 0.24f) : Color.white;
            s.keyText.text = key;
            s.nameText.text = locked ? "LOCKED" : displayName.ToUpperInvariant();
            s.nameText.alpha = locked ? 0.45f : 1f;
            if (s.keyBadge != null) s.keyBadge.color = locked ? new Color(0.45f, 0.42f, 0.5f) : new Color(0.95f, 0.76f, 0.31f);
            if (s.lockIcon != null) s.lockIcon.SetActive(locked);
            s.cooldownFill.fillAmount = 0f;
            s.cooldownText.text = "";
            s.wasReady = true;
            if (justUnlocked && isActiveAndEnabled)
            {
                Punch((RectTransform)s.root.transform, 0.4f);
                StartCoroutine(FlashFrame(s));
            }
        }

        /// <summary>Cooldown sweep: normalized 1 = just used, 0 = ready.</summary>
        public void SetSkillCooldown(int index, float normalized, float secondsLeft)
        {
            if (skillSlots == null || index < 0 || index >= skillSlots.Length) return;
            var s = skillSlots[index];
            if (!s.root.activeSelf || s.locked) return;
            bool ready = normalized <= 0f;
            s.cooldownFill.fillAmount = normalized;
            s.cooldownText.text = ready ? "" : Mathf.CeilToInt(secondsLeft).ToString();
            s.icon.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.6f);
            if (ready && !s.wasReady)
            {
                Punch((RectTransform)s.root.transform, 0.25f);
                StartCoroutine(FlashFrame(s));
            }
            s.wasReady = ready;
        }

        /// <summary>Little shake when the player presses a skill that is still cooling down.</summary>
        public void DenySkill(int index)
        {
            if (skillSlots == null || index < 0 || index >= skillSlots.Length || !isActiveAndEnabled) return;
            StartCoroutine(Shake((RectTransform)skillSlots[index].root.transform));
        }

        IEnumerator FlashFrame(SkillSlotView s)
        {
            var baseColor = s.frame.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                s.frame.color = Color.Lerp(skillReadyFlash, baseColor, t);
                yield return null;
            }
            s.frame.color = baseColor;
        }

        static IEnumerator Shake(RectTransform target)
        {
            var start = target.anchoredPosition;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                target.anchoredPosition = start + new Vector2(Mathf.Round(Random.Range(-6f, 6f)), 0f);
                yield return null;
            }
            target.anchoredPosition = start;
        }

        /// <summary>Small line under the floor badge ("BUGS LEFT: 4", "EXIT OPEN"). Empty hides it.</summary>
        public void SetObjective(string text)
        {
            if (objectiveGroup == null) return;
            bool show = !string.IsNullOrEmpty(text);
            objectiveGroup.SetActive(show);
            if (show)
            {
                objectiveText.text = text;
                Punch((RectTransform)objectiveGroup.transform, 0.12f);
            }
        }

        void Punch(RectTransform target, float amount = 0.35f)
        {
            if (target != null && isActiveAndEnabled) StartCoroutine(PunchRoutine(target, amount));
        }

        static IEnumerator PunchRoutine(RectTransform target, float amount)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.25f;
                target.localScale = Vector3.one * (1f + amount * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
