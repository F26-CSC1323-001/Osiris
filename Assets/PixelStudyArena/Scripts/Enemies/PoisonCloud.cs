using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Lingering poison left by the Hornet Queen's poison sacs. Standing in it ticks away health
    /// (no knockback), so it's there to push you around the arena rather than to hit hard.
    /// </summary>
    public class PoisonCloud : MonoBehaviour
    {
        public SpriteRenderer[] puffs;
        public float lifetime = 5f;
        public Vector2 size = new Vector2(3.2f, 1.7f);
        public float tickInterval = 0.5f;
        public int damagePerTick = 3;

        static readonly Color Bubble = new Color(0.72f, 1f, 0.35f);
        static readonly Color BubbleDark = new Color(0.55f, 0.3f, 0.75f);

        float age, tickTimer, bubbleTimer;
        Vector3[] baseScales;
        Color[] baseColors;

        void Start()
        {
            baseScales = new Vector3[puffs.Length];
            baseColors = new Color[puffs.Length];
            for (int i = 0; i < puffs.Length; i++)
            {
                baseScales[i] = puffs[i].transform.localScale;
                baseColors[i] = puffs[i].color;
            }
            ArenaFX.Burst(Center, Bubble, 14, 4f, 0.12f, 0.6f, 1f);
        }

        Vector2 Center => (Vector2)transform.position + Vector2.up * size.y * 0.5f;

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;

            // puffs swell in, drift, and fade out at the end
            float grow = Mathf.Clamp01(age / 0.35f);
            float fade = Mathf.Clamp01((lifetime - age) / 0.8f);
            for (int i = 0; i < puffs.Length; i++)
            {
                float wobble = 1f + Mathf.Sin(age * 2.2f + i * 1.9f) * 0.08f;
                puffs[i].transform.localScale = baseScales[i] * (0.4f + 0.6f * grow) * wobble;
                var c = baseColors[i];
                c.a *= fade;
                puffs[i].color = c;
            }

            bubbleTimer -= dt;
            if (bubbleTimer <= 0f && fade > 0.3f)
            {
                bubbleTimer = 0.08f;
                var p = (Vector2)transform.position + new Vector2(Random.Range(-size.x, size.x) * 0.45f, Random.Range(0.1f, size.y * 0.8f));
                ArenaFX.Burst(p, Random.value < 0.6f ? Bubble : BubbleDark, 1, 0.8f, 0.1f, 0.6f, 1.2f);
            }

            tickTimer -= dt;
            if (tickTimer <= 0f && fade > 0.3f)
            {
                tickTimer = tickInterval;
                var player = ArenaPlayer.Instance;
                if (player != null && !player.IsDead && Physics2D.OverlapBox(Center, size, 0f, ArenaLayers.PlayerMask))
                {
                    player.TakePoison(damagePerTick);
                    ArenaFX.Burst(player.Center, Bubble, 4, 2f, 0.09f, 0.4f, 1f);
                }
            }

            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
