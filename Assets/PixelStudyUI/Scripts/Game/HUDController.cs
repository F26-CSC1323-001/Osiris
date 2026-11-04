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

    /// <summary>Health, stamina, flasks, tokens, coins, floor and boss bar. Refreshes itself when GameSession changes.</summary>
    public class HUDController : MonoBehaviour
    {
        public UIBar healthBar;
        public TMP_Text healthText;
        public UIBar staminaBar;

        [Header("Flask")]
        public GameObject flaskRoot;
        public UnityEngine.UI.Image flaskIcon;
        public TMP_Text flaskCountText;
        public TMP_Text flaskKeyText;

        [Header("Banner (YOU DIED / ENEMY FELLED)")]
        public CanvasGroup bannerGroup;
        public TMP_Text bannerText;
        public RectTransform bannerBand;
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

        [Header("Ultimate (after the skills and the flask)")]
        public GameObject ultRoot;
        public UnityEngine.UI.Image ultFrame;
        public UnityEngine.UI.Image ultIcon;
        [Tooltip("Gauge filling up behind the icon.")]
        public UnityEngine.UI.Image ultFill;
        [Tooltip("Gold outline that pulses while the ultimate is ready.")]
        public UnityEngine.UI.Image ultGlow;
        public TMP_Text ultText;
        public TMP_Text ultKeyText;
        public TMP_Text ultNameText;
        bool ultReady;
        float shownCharge = -1f;

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

        /// <summary>The big health bar at the top. name: what to call it (default: this floor's boss).</summary>
        public void SetBoss(bool visible, float normalized, bool instant = false, string name = null)
        {
            bossGroup.SetActive(visible);
            if (!visible) return;
            bossBar.SetValue(normalized, instant);
            if (bossNameText == null) return;
            if (!string.IsNullOrEmpty(name)) bossNameText.text = name.ToUpperInvariant();
            else if (GameSession.Run != null) bossNameText.text = GameSession.BossName(GameSession.Run.floor).ToUpperInvariant();
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
            if (skillSlots == null || index < 0 || !isActiveAndEnabled) return;
            // the knight's skill after the seven slots is the ultimate
            var target = index < skillSlots.Length ? skillSlots[index].root : ultRoot;
            if (target != null && target.activeInHierarchy) StartCoroutine(Shake((RectTransform)target.transform));
        }

        // ---------- ultimate ----------

        /// <summary>Shows the ultimate's slot (icon, key, name). A null icon hides it.</summary>
        public void SetUltimate(Sprite icon, string key, string displayName)
        {
            if (ultRoot == null) return;
            ultRoot.SetActive(icon != null);
            if (icon == null) return;
            ultIcon.sprite = icon;
            if (ultKeyText != null) ultKeyText.text = key;
            if (ultNameText != null) ultNameText.text = displayName.ToUpperInvariant();
            shownCharge = -1f;
        }

        /// <summary>The gauge, 0..1. Full: the slot lights up and pulses until it's used.</summary>
        public void SetUltimateCharge(float charge01)
        {
            if (ultRoot == null || !ultRoot.activeSelf) return;
            charge01 = Mathf.Clamp01(charge01);
            bool ready = charge01 >= 1f;
            if (Mathf.Approximately(charge01, shownCharge) && ready == ultReady) return;
            shownCharge = charge01;
            ultFill.fillAmount = charge01;
            ultText.text = ready ? "READY" : Mathf.FloorToInt(charge01 * 100f) + "%";
            ultIcon.color = ready ? Color.white : Color.Lerp(new Color(0.3f, 0.3f, 0.36f), new Color(0.75f, 0.72f, 0.65f), charge01);
            if (ready && !ultReady && isActiveAndEnabled) Punch((RectTransform)ultRoot.transform, 0.45f);
            ultReady = ready;
            if (ready) return;
            if (ultGlow != null) ultGlow.color = new Color(1f, 0.85f, 0.4f, 0f);
            ultText.color = new Color(0.95f, 0.9f, 0.8f);
        }

        void Update()
        {
            if (!ultReady || ultGlow == null || ultRoot == null || !ultRoot.activeSelf) return;
            // ready: the outline breathes gold
            float t = Time.unscaledTime;
            ultGlow.color = new Color(1f, 0.85f, 0.4f, 0.45f + 0.4f * Mathf.Sin(t * 5f));
            if (ultText != null) ultText.color = Color.Lerp(new Color(1f, 0.85f, 0.4f), Color.white, 0.5f + 0.5f * Mathf.Sin(t * 5f));
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

        // ---------- stamina ----------

        public void SetStamina(float normalized, bool instant = false)
        {
            if (staminaBar != null) staminaBar.SetValue(normalized, instant);
        }

        /// <summary>Tried to act with no stamina left: the bar shakes.</summary>
        public void DenyStamina()
        {
            if (staminaBar != null && isActiveAndEnabled) StartCoroutine(Shake((RectTransform)staminaBar.transform));
        }

        // ---------- flask ----------

        /// <summary>Shows the healing flask with how many sips are left. A null icon hides it.</summary>
        public void SetFlask(Sprite icon, int charges, string key)
        {
            if (flaskRoot == null) return;
            flaskRoot.SetActive(icon != null);
            if (icon == null) return;
            flaskIcon.sprite = icon;
            flaskIcon.color = charges > 0 ? Color.white : new Color(0.3f, 0.3f, 0.36f);
            flaskCountText.text = charges.ToString();
            flaskCountText.color = charges > 0 ? new Color(1f, 0.95f, 0.85f) : new Color(1f, 0.45f, 0.4f);
            if (flaskKeyText != null) flaskKeyText.text = key;
        }

        public void FlaskUsed()
        {
            if (flaskRoot != null) Punch((RectTransform)flaskRoot.transform, 0.3f);
        }

        public void DenyFlask()
        {
            if (flaskRoot != null && isActiveAndEnabled) StartCoroutine(Shake((RectTransform)flaskRoot.transform));
        }

        // ---------- banner ----------

        Coroutine bannerRoutine;

        /// <summary>Big centered title across a dark band ("YOU DIED", "ENEMY FELLED"). seconds &lt;= 0 keeps it up.</summary>
        public void ShowBanner(string text, Color color, float seconds)
        {
            if (bannerGroup == null || !isActiveAndEnabled) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text, color, seconds));
        }

        public void HideBanner()
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = null;
            if (bannerGroup != null) bannerGroup.gameObject.SetActive(false);
        }

        IEnumerator BannerRoutine(string text, Color color, float seconds)
        {
            bannerGroup.gameObject.SetActive(true);
            bannerText.text = text;
            bannerText.color = color;
            const float fadeIn = 0.9f, fadeOut = 0.8f;
            float total = seconds > 0f ? seconds : float.PositiveInfinity;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Clamp01(t / fadeIn);
                if (seconds > 0f && t > seconds - fadeOut) a = Mathf.Clamp01((seconds - t) / fadeOut);
                bannerGroup.alpha = a * a * (3f - 2f * a);
                // the title slowly grows, the band opens up
                bannerText.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Clamp01(t / 3f));
                if (bannerBand != null) bannerBand.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t / 0.5f)), 1f);
                yield return null;
            }
            bannerGroup.gameObject.SetActive(false);
            bannerRoutine = null;
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
