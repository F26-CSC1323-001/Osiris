using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>
    /// Health / boss bar. The fill eases to the new value and a lighter "damage trail"
    /// catches up a moment later. Pulses when low.
    /// </summary>
    public class UIBar : MonoBehaviour
    {
        public RectTransform fill;
        public RectTransform trail;
        public Image fillImage;
        public Color fullColor = new Color(0.9f, 0.3f, 0.32f);
        public Color lowColor = new Color(1f, 0.55f, 0.3f);
        [Range(0f, 1f)] public float lowThreshold = 0.3f;
        public float speed = 10f;
        public float trailDelay = 0.35f;
        public float trailSpeed = 1.2f;

        float target = 1f, shown = 1f, trailShown = 1f, trailWait;

        public void SetValue(float value, bool instant = false)
        {
            value = Mathf.Clamp01(value);
            if (value < target) trailWait = trailDelay;
            target = value;
            if (instant) shown = trailShown = value;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-speed * dt));
            if (trailShown < shown) trailShown = shown;
            else if (trailWait > 0f) trailWait -= dt;
            else trailShown = Mathf.MoveTowards(trailShown, shown, trailSpeed * dt);
            Apply();
        }

        void Apply()
        {
            SetWidth(fill, shown);
            SetWidth(trail, trailShown);
            if (fillImage != null)
            {
                fillImage.color = shown <= lowThreshold
                    ? Color.Lerp(fullColor, lowColor, Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                    : fullColor;
            }
        }

        static void SetWidth(RectTransform rt, float value)
        {
            if (rt == null) return;
            var max = rt.anchorMax;
            max.x = value;
            rt.anchorMax = max;
        }
    }
}
