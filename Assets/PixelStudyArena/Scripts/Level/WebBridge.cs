using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// A bridge of silk across the Widow's Web (a one-way platform). When the Silk Widow is enraged she
    /// cuts some of them and they fall away, shrinking the arena.
    /// </summary>
    public class WebBridge : MonoBehaviour
    {
        public SpriteRenderer body;
        public Collider2D platform;

        static readonly List<WebBridge> Active = new List<WebBridge>();

        public static IReadOnlyList<WebBridge> All => Active;

        bool cut;
        float fall, spin;

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        /// <summary>Snaps the bridge: it stops being solid and drops away.</summary>
        public void Cut()
        {
            if (cut) return;
            cut = true;
            Active.Remove(this);
            if (platform != null) platform.enabled = false;
            spin = Random.Range(-40f, 40f);
            var c = (Vector2)transform.position;
            float half = body != null ? body.size.x * 0.5f : 2f;
            for (int i = 0; i < 6; i++)
                ArenaFX.Burst(c + new Vector2(Random.Range(-half, half), 0f), new Color(0.92f, 0.92f, 1f), 4, 4f, 0.08f, 0.5f, 1f);
            ArenaFX.Shake(0.2f);
        }

        void Update()
        {
            if (!cut) return;
            fall += Time.deltaTime;
            transform.position += Vector3.down * (6f * fall) * Time.deltaTime * 3f;
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            if (body != null) body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - fall / 1.2f));
            if (fall > 1.3f) Destroy(gameObject);
        }
    }
}
