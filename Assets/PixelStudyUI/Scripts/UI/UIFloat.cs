using UnityEngine;

namespace PixelStudy.UI
{
    /// <summary>Gently bobs a UI element up and down (titles, icons).</summary>
    public class UIFloat : MonoBehaviour
    {
        public float amplitude = 6f;
        public float speed = 1.6f;
        public float phase;

        RectTransform rt;
        Vector2 basePos;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
        }

        void OnDisable()
        {
            if (rt != null) rt.anchoredPosition = basePos;
        }

        void Update()
        {
            // Snap to whole pixels so the pixel-art look stays crisp.
            float y = Mathf.Round(Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
            rt.anchoredPosition = basePos + new Vector2(0f, y);
        }
    }
}
