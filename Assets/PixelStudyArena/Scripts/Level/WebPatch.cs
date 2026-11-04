using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Sticky silk on the floor (the Silk Widow's Cocoon Spit, the Parasite's webs). Anything walking
    /// through it is slowed. Fades away after a while.
    /// </summary>
    public class WebPatch : MonoBehaviour
    {
        public SpriteRenderer body;
        public float life = 7f;
        public float width = 2.4f;

        static readonly List<WebPatch> Active = new List<WebPatch>();

        float age;

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        /// <summary>Speed multiplier for something with its feet here: slow inside a patch, 1 elsewhere.</summary>
        public static float SpeedAt(Vector2 feet, float slow)
        {
            foreach (var p in Active)
            {
                var c = (Vector2)p.transform.position;
                if (Mathf.Abs(feet.x - c.x) < p.width * 0.5f && feet.y > c.y - 0.3f && feet.y < c.y + 0.7f) return slow;
            }
            return 1f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float grow = Mathf.Clamp01(age / 0.15f);
            float fade = age > life - 1f ? Mathf.Clamp01(life - age) : 1f;
            transform.localScale = new Vector3(width / 2.25f * grow, 1f, 1f);
            if (body != null) body.color = new Color(1f, 1f, 1f, 0.9f * fade);
            if (age >= life) Destroy(gameObject);
        }
    }
}
