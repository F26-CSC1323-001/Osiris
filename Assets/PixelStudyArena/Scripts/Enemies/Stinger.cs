using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// The Hornet Queen's stinger: flies in a straight line, sticks in walls and floors for a moment.
    /// A parry sends it straight back at the bugs.
    /// </summary>
    public class Stinger : MonoBehaviour
    {
        public SpriteRenderer body;
        public float radius = 0.2f;
        public float lifetime = 3f;
        public float stuckTime = 0.6f;

        static readonly List<Collider2D> Hits = new List<Collider2D>();
        static readonly Color Spark = new Color(1f, 0.85f, 0.35f);

        Vector2 velocity;
        int damage;
        bool reflected, stuck;
        float age, stuckTimer;

        public void Launch(Vector2 direction, float speed, int amount)
        {
            velocity = direction.normalized * speed;
            damage = amount;
            Face();
        }

        void Face() => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;

            if (stuck)
            {
                stuckTimer -= dt;
                if (body != null) body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(stuckTimer / 0.25f));
                if (stuckTimer <= 0f) Destroy(gameObject);
                return;
            }

            var pos = (Vector2)transform.position + velocity * dt;
            transform.position = pos;

            if (!reflected)
            {
                var p = ArenaPlayer.Instance;
                if (p != null && !p.IsDead && Physics2D.OverlapCircle(pos, radius, ArenaLayers.PlayerMask))
                {
                    var result = p.TakeHit(damage, pos, 4f);
                    if (result == HitResult.Parried) { Reflect(); return; }
                    if (result != HitResult.Ignored) { Break(); return; }
                }
            }
            else
            {
                Hits.Clear();
                var filter = new ContactFilter2D();
                filter.SetLayerMask(ArenaLayers.EnemyMask);
                filter.useTriggers = true;
                Physics2D.OverlapCircle(pos, radius + 0.25f, filter, Hits);
                foreach (var h in Hits)
                {
                    var bug = h.GetComponentInParent<BugEnemy>();
                    if (bug == null || bug.IsDead) continue;
                    bug.TakeHit(damage * 2, pos, 3f, false, true);
                    Break();
                    return;
                }
            }

            if (Physics2D.OverlapCircle(pos, 0.08f, ArenaLayers.SolidMask)) { Stick(); return; }
            if (age > lifetime) Destroy(gameObject);
        }

        void Reflect()
        {
            reflected = true;
            velocity = -velocity * 1.4f;
            Face();
            if (body != null) body.color = new Color(1f, 0.9f, 0.45f);
            ArenaFX.Burst(transform.position, Spark, 8, 5f, 0.08f, 0.3f, 0.5f);
        }

        void Stick()
        {
            stuck = true;
            stuckTimer = stuckTime;
            ArenaFX.Spray(transform.position, -velocity, new Color(0.7f, 0.68f, 0.62f), 4, 3f, 0.6f, 0.08f);
        }

        void Break()
        {
            ArenaFX.Burst(transform.position, Spark, 6, 4f, 0.08f, 0.3f, 0.5f);
            Destroy(gameObject);
        }
    }
}
