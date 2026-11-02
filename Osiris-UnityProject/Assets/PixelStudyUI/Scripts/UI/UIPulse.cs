using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>Throbs scale (and optionally color) — used for the Boss Ahead warning.</summary>
    public class UIPulse : MonoBehaviour
    {
        public float scaleAmount = 0.05f;
        public float speed = 3f;
        public Graphic graphic;
        public Color pulseColor = Color.white;

        Color baseColor;

        void OnEnable()
        {
            if (graphic != null) baseColor = graphic.color;
        }

        void OnDisable()
        {
            transform.localScale = Vector3.one;
            if (graphic != null) graphic.color = baseColor;
        }

        void Update()
        {
            float s = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
            transform.localScale = Vector3.one * (1f + scaleAmount * s);
            if (graphic != null) graphic.color = Color.Lerp(baseColor, pulseColor, s);
        }
    }
}
