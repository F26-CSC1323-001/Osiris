using System.Collections;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>The way out. Mockup 2 opens it as soon as the level starts (OpenInstantly).</summary>
    public class ExitDoor : MonoBehaviour
    {
        public Transform bars;
        public SpriteRenderer portal;
        public SpriteRenderer glow;
        public Transform arrow;
        public PixelAnimator portalAnim;
        public GameObject sparks;
        public float triggerWidth = 1.4f;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            SetAlpha(portal, 0f);
            SetAlpha(glow, 0f);
            if (arrow != null) arrow.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            StartCoroutine(OpenRoutine());
        }

        /// <summary>Already open when the level starts: gate up, portal swirling, no animation.</summary>
        public void OpenInstantly()
        {
            if (IsOpen) return;
            IsOpen = true;
            bars.localPosition += Vector3.up * 2.6f;
            SetAlpha(portal, 1f);
            SetAlpha(glow, 0.7f);
            if (portalAnim != null) portalAnim.Play("swirl");
            if (sparks != null) sparks.SetActive(true);
            if (arrow != null) arrow.gameObject.SetActive(true);
        }

        IEnumerator OpenRoutine()
        {
            ArenaFX.Shake(0.3f);
            var start = bars.localPosition;
            var end = start + Vector3.up * 2.6f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                bars.localPosition = Vector3.Lerp(start, end, t * t) + new Vector3(Random.Range(-0.03f, 0.03f), 0f, 0f);
                SetAlpha(portal, t);
                SetAlpha(glow, t * 0.7f);
                if (Random.value < 0.3f) ArenaFX.Burst(bars.position, new Color(0.6f, 0.6f, 0.65f), 1, 2f, 0.1f, 0.4f, 0f);
                yield return null;
            }
            bars.localPosition = end;
            SetAlpha(portal, 1f);
            if (portalAnim != null) portalAnim.Play("swirl");
            if (sparks != null) sparks.SetActive(true);
            ArenaFX.Ring(transform.position + Vector3.up * 1.4f, new Color(0.5f, 1f, 0.9f, 0.9f), 0.5f, 5f, 0.5f);
            if (arrow != null) arrow.gameObject.SetActive(true);
        }

        void Update()
        {
            if (!IsOpen) return;
            if (glow != null) SetAlpha(glow, 0.55f + 0.2f * Mathf.Sin(Time.time * 3f));
            if (arrow != null) arrow.localPosition = new Vector3(0f, 3.6f + Mathf.Round(Mathf.Sin(Time.time * 5f) * 4f) / 32f, 0f);
        }

        public bool Contains(Vector2 point)
        {
            var p = (Vector2)transform.position;
            return Mathf.Abs(point.x - p.x) < triggerWidth * 0.5f && point.y > p.y - 0.5f && point.y < p.y + 3f;
        }

        static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            var c = r.color;
            c.a = a;
            r.color = c;
        }
    }
}
