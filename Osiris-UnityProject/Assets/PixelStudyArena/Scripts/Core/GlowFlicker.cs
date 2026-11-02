using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Flickers a glow sprite (torches) or slowly breathes it (portals, windows).</summary>
    public class GlowFlicker : MonoBehaviour
    {
        public SpriteRenderer glow;
        public float baseAlpha = 0.5f;
        public float alphaVariation = 0.35f;
        public float scaleVariation = 0.08f;
        public float speed = 3f;

        float seed;
        Vector3 baseScale;

        void Start()
        {
            if (glow == null) glow = GetComponent<SpriteRenderer>();
            seed = Random.value * 100f;
            baseScale = glow.transform.localScale;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            var c = glow.color;
            c.a = baseAlpha * (1f - alphaVariation * 0.5f + alphaVariation * n);
            glow.color = c;
            glow.transform.localScale = baseScale * (1f - scaleVariation * 0.5f + scaleVariation * n);
        }
    }
}
