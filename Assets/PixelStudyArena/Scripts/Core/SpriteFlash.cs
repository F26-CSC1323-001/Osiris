using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// White hit flashes and colored "about to attack" glows. Needs the PixelStudy/SpriteFlash material
    /// on the renderers.
    /// </summary>
    public class SpriteFlash : MonoBehaviour
    {
        public SpriteRenderer[] renderers;

        static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

        MaterialPropertyBlock block;
        Color flashColor = Color.white;
        float flash, flashDecay = 10f;
        Color holdColor = Color.white;
        float hold, holdPulse;

        void Awake() => block = new MaterialPropertyBlock();

        /// <summary>Short full-color flash that fades out.</summary>
        public void Flash(Color color, float duration = 0.1f)
        {
            flashColor = color;
            flash = 1f;
            flashDecay = 1f / Mathf.Max(0.01f, duration);
        }

        /// <summary>Keeps a pulsing tint on until cleared (attack wind-ups).</summary>
        public void Hold(Color color, float amount = 0.6f, float pulseSpeed = 18f)
        {
            holdColor = color;
            hold = amount;
            holdPulse = pulseSpeed;
        }

        public void ClearHold() => hold = 0f;

        void LateUpdate()
        {
            flash = Mathf.MoveTowards(flash, 0f, flashDecay * Time.unscaledDeltaTime);
            float held = hold > 0f ? hold * (0.55f + 0.45f * Mathf.Sin(Time.time * holdPulse)) : 0f;
            Color c = flash >= held ? flashColor : holdColor;
            float a = Mathf.Max(flash, held);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(FlashColor, c);
                block.SetFloat(FlashAmount, a);
                r.SetPropertyBlock(block);
            }
        }
    }
}
