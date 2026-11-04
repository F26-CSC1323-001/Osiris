using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Lobbed acid. Splashes on the ground; a parry knocks it back at the bugs.</summary>
    public class AcidBlob : MonoBehaviour
    {
        public SpriteRenderer body;
        public float gravity = 22f;
        public float radius = 0.28f;
        public float lifetime = 4f;
        public Color splashColor = new Color(0.55f, 1f, 0.3f);
        [Tooltip("Optional: leaves a poison cloud where it lands (the Hornet Queen's poison sacs).")]
        public PoisonCloud cloudPrefab;

        Vector2 velocity;
        int damage;
        bool reflected;
        float age;
        static readonly List<Collider2D> Hits = new List<Collider2D>();

        /// <summary>Every blob in flight (Whirlwind knocks nearby ones away).</summary>
        public static readonly List<AcidBlob> Active = new List<AcidBlob>();

        void OnEnable() => Active.Add(this);

        void OnDisable() => Active.Remove(this);

        /// <summary>Sends the blob back at the bugs (parries, Whirlwind).</summary>
        public void Deflect()
        {
            if (!reflected) Reflect();
        }

        /// <summary>Launches toward a target so that it lands there after flightTime seconds.</summary>
        public void Launch(Vector2 target, float flightTime, int amount)
        {
            damage = amount;
            var start = (Vector2)transform.position;
            velocity = new Vector2((target.x - start.x) / flightTime,
                (target.y - start.y + 0.5f * gravity * flightTime * flightTime) / flightTime);
        }

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;
            velocity.y -= gravity * dt;
            transform.position += (Vector3)(velocity * dt);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

            var pos = (Vector2)transform.position;
            if (!reflected)
            {
                var p = ArenaPlayer.Instance;
                if (p != null && !p.IsDead && Physics2D.OverlapCircle(pos, radius, ArenaLayers.PlayerMask))
                {
                    var result = p.TakeHit(damage, pos, 5f);
                    if (result == HitResult.Parried) { Reflect(); return; }
                    if (result != HitResult.Ignored) { Splash(); return; }
                }
            }
            else
            {
                Hits.Clear();
                var filter = new ContactFilter2D();
                filter.SetLayerMask(ArenaLayers.EnemyMask);
                filter.useTriggers = true;
                Physics2D.OverlapCircle(pos, radius + 0.15f, filter, Hits);
                foreach (var h in Hits)
                {
                    var bug = h.GetComponentInParent<BugEnemy>();
                    if (bug == null || bug.IsDead) continue;
                    bug.TakeHit(damage * 2, pos, 4f, false, true);
                    Splash();
                    return;
                }
            }

            if (Physics2D.OverlapCircle(pos, radius * 0.6f, ArenaLayers.GroundMask) && velocity.y < 0f) { Splash(); return; }
            if (age > lifetime) Destroy(gameObject);
        }

        void Reflect()
        {
            reflected = true;
            velocity = new Vector2(-velocity.x * 1.4f, Mathf.Abs(velocity.y) * 0.5f + 4f);
            body.color = new Color(1f, 0.9f, 0.4f);
            splashColor = new Color(1f, 0.85f, 0.35f);
        }

        void Splash()
        {
            if (cloudPrefab != null && !reflected)
            {
                var hit = Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.3f, Vector2.down, 8f, ArenaLayers.GroundMask);
                if (hit)
                {
                    var cloud = Instantiate(cloudPrefab, hit.point, Quaternion.identity);
                    cloud.damagePerTick = Mathf.Max(2, damage / 5);
                }
            }
            ArenaFX.Burst(transform.position, splashColor, 12, 5f, 0.12f, 0.5f, 2f);
            ArenaFX.Glow(transform.position, new Color(splashColor.r, splashColor.g, splashColor.b, 0.5f), 1.2f, 0.3f);
            Destroy(gameObject);
        }
    }
}
