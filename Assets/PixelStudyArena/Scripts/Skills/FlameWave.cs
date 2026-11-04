using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Flame Slash's crescent of fire: flies forward, growing as it goes, burns through every bug it
    /// touches, then breaks apart into embers.
    /// </summary>
    public class FlameWave : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteRenderer glow;
        public float speed = 12f;
        public float lifetime = 0.85f;
        public float startScale = 0.7f;
        public float endScale = 2.3f;
        [Tooltip("Hitbox size at scale 1 (the crescent is ~1.3 units tall at scale 1).")]
        public Vector2 hitSize = new Vector2(1.1f, 2.4f);
        [Tooltip("How far it lifts per unit of growth, so the bottom tip skims the floor instead of sinking into it.")]
        public float riseWithScale = 0.9f;

        static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        static readonly Color FireHot = new Color(1f, 0.85f, 0.35f);
        static readonly List<Collider2D> Hits = new List<Collider2D>();

        readonly HashSet<BugEnemy> alreadyHit = new HashSet<BugEnemy>();
        int dir = 1;
        int damage;
        float burnDps, burnTime, age, emberTimer, baseY;
        bool landedHit, done;
        ContactFilter2D enemyFilter;

        public void Launch(int direction, int amount, float dps, float burnSeconds)
        {
            dir = direction >= 0 ? 1 : -1;
            damage = amount;
            burnDps = dps;
            burnTime = burnSeconds;
            if (body != null) body.flipX = dir < 0;
            enemyFilter = new ContactFilter2D();
            enemyFilter.SetLayerMask(ArenaLayers.EnemyMask);
            enemyFilter.useTriggers = true;
            baseY = transform.position.y;
            Apply(0f);
        }

        void Update()
        {
            if (done || Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;
            float t = Mathf.Clamp01(age / lifetime);
            // slows down slightly as it spreads out
            transform.position += new Vector3(dir * speed * Mathf.Lerp(1.15f, 0.6f, t) * dt, 0f, 0f);
            Apply(t);

            float scale = transform.localScale.y;
            var center = (Vector2)transform.position + new Vector2(dir * 1.0f * scale, 0f); // the crescent's body sits ~1 unit ahead of its pivot

            emberTimer -= dt;
            if (emberTimer <= 0f)
            {
                emberTimer = 0.025f;
                var p = center + new Vector2(0f, Random.Range(-0.55f, 0.55f) * scale);
                ArenaFX.Spray(p, new Vector2(-dir, Random.Range(-0.3f, 0.6f)), Random.value < 0.5f ? Fire : FireHot, 2, 3f, 0.5f, 0.13f);
            }

            Hits.Clear();
            Physics2D.OverlapBox(center, hitSize * scale, 0f, enemyFilter, Hits);
            foreach (var h in Hits)
            {
                var bug = h.GetComponentInParent<BugEnemy>();
                if (bug == null || bug.IsDead || !alreadyHit.Add(bug)) continue;
                bug.TakeHit(damage, (Vector2)transform.position - new Vector2(dir, 0f), 8f, true, true);
                if (!bug.IsDead) bug.ApplyBurn(burnDps, burnTime);
                ArenaFX.Burst(bug.Center, Fire, 12, 6f, 0.13f, 0.45f, 1.5f);
                if (!landedHit)
                {
                    landedHit = true;
                    ArenaFX.HitStop(0.06f);
                    ArenaFX.Shake(0.25f);
                }
            }

            if (age >= lifetime || Physics2D.OverlapCircle(center + new Vector2(dir * 0.25f * scale, 0f), 0.2f, ArenaLayers.SolidMask))
                Dissipate();
        }

        void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(startScale, endScale, ease);
            transform.localScale = new Vector3(s, s, 1f);
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY + (s - startScale) * riseWithScale, p.z);
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            if (body != null) body.color = new Color(1f, 1f, 1f, alpha);
            if (glow != null) glow.color = new Color(1f, 0.5f, 0.15f, 0.6f * alpha);
        }

        void Dissipate()
        {
            done = true;
            float scale = transform.localScale.y;
            var pos = (Vector2)transform.position + new Vector2(dir * 1.0f * scale, 0f);
            for (int i = 0; i < 6; i++)
            {
                var p = pos + new Vector2(Random.Range(-0.2f, 0.4f) * dir, Random.Range(-0.7f, 0.7f) * scale);
                ArenaFX.Burst(p, Random.value < 0.5f ? Fire : FireHot, 4, 3.5f, 0.12f, 0.6f, 1.5f);
            }
            ArenaFX.Glow(pos, new Color(1f, 0.5f, 0.15f, 0.4f), 2.5f * scale, 0.3f);
            Destroy(gameObject);
        }
    }
}
