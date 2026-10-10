using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>Little square "dust" pixels drifting upward behind menus.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIAmbientParticles : MonoBehaviour
    {
        public Sprite sprite;
        public Color color = new Color(1f, 0.85f, 0.45f, 0.5f);
        public int count = 36;
        public Vector2 sizeRange = new Vector2(4f, 10f);
        public Vector2 speedRange = new Vector2(12f, 40f);

        RectTransform area;
        RectTransform[] dots;
        Image[] images;
        float[] speeds, phases;

        void Start()
        {
            area = (RectTransform)transform;
            dots = new RectTransform[count];
            images = new Image[count];
            speeds = new float[count];
            phases = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                float size = Mathf.Round(Random.Range(sizeRange.x, sizeRange.y) / 2f) * 2f;
                rt.sizeDelta = new Vector2(size, size);
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                dots[i] = rt;
                images[i] = img;
                speeds[i] = Random.Range(speedRange.x, speedRange.y);
                phases[i] = Random.value * 10f;
                Respawn(i, Random.Range(0f, 1f));
            }
        }

        void Respawn(int i, float heightFraction)
        {
            var r = area.rect;
            dots[i].anchoredPosition = new Vector2(Random.Range(0f, r.width), heightFraction * r.height);
        }

        void Update()
        {
            if (dots == null) return;
            float h = area.rect.height;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < dots.Length; i++)
            {
                var p = dots[i].anchoredPosition;
                p.y += speeds[i] * dt;
                p.x += Mathf.Sin(Time.unscaledTime * 0.7f + phases[i]) * 6f * dt;
                if (p.y > h + 10f)
                {
                    Respawn(i, 0f);
                    p = dots[i].anchoredPosition;
                    p.y = -10f;
                }
                dots[i].anchoredPosition = new Vector2(Mathf.Round(p.x), p.y);
                var c = color;
                c.a *= 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.3f + phases[i]));
                images[i].color = c;
            }
        }
    }
}
