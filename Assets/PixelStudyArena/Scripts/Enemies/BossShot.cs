using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// A boss projectile with any sprite: the Reaper Mantis's crescent waves, the Brood Mother's
    /// falling rocks. Flies straight (or falls), leaves a trail, breaks on walls and floors.
    /// A parry sends it back at the bugs.
    /// </summary>
    public class BossShot : MonoBehaviour
    {
        public SpriteRenderer body;

        /// <summary>Set to handle touching the knight yourself (web snares, sticky globs). Return true to break the shot.</summary>
        public System.Func<ArenaPlayer, bool> OnTouchPlayer;
        /// <summary>Called with the spot where it hit a floor or wall (globs leave webs there).</summary>
        public System.Action<Vector2> OnLand;

        static readonly List<Collider2D> Hits = new List<Collider2D>();

        Vector2 velocity;
        float gravity, life, age, radius, trailTimer, spin;
        int damage;
        bool reflected, faceVelocity, hasTrail;
        Color trail, burst;

        /// <param name="sprite">What it looks like (drawn pointing along +x when faceVelocity is on).</param>
        /// <param name="tint">Sprite color.</param>
        /// <param name="trailColor">Afterimage trail and the burst when it breaks (alpha 0 = no trail).</param>
        public void Launch(Sprite sprite, Vector2 startVelocity, int amount, float gravityScale, float lifetime, Color tint, Color trailColor,
            float scale = 1f, float hitRadius = 0.4f, bool face = true, float spinDegrees = 0f)
        {
            if (body != null)
            {
                body.sprite = sprite;
                body.color = tint;
            }
            transform.localScale = new Vector3(scale, scale, 1f);
            velocity = startVelocity;
            damage = amount;
            gravity = gravityScale;
            life = lifetime;
            radius = hitRadius;
            faceVelocity = face;
            spin = spinDegrees;
            trail = trailColor;
            burst = new Color(trailColor.r, trailColor.g, trailColor.b, 1f);
            hasTrail = trailColor.a > 0.01f;
            Orient();
        }

        void Orient()
        {
            if (faceVelocity && velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;
            velocity.y -= gravity * dt;
            var pos = (Vector2)transform.position + velocity * dt;
            transform.position = pos;
            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
            else Orient();

            if (hasTrail && body != null && (trailTimer -= dt) <= 0f)
            {
                trailTimer = 0.03f;
                ArenaFX.Afterimage(body, trail, 0.16f);
            }

            if (!reflected)
            {
                var p = ArenaPlayer.Instance;
                if (p != null && !p.IsDead && Physics2D.OverlapCircle(pos, radius, ArenaLayers.PlayerMask))
                {
                    if (OnTouchPlayer != null)
                    {
                        if (OnTouchPlayer(p)) Break();
                        return;
                    }
                    var result = p.TakeHit(damage, pos - velocity.normalized, 6f);
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
                    bug.TakeHit(damage * 2, pos, 4f, true, true);
                    Break();
                    return;
                }
            }

            if (Physics2D.OverlapCircle(pos, Mathf.Min(radius, 0.25f), ArenaLayers.SolidMask))
            {
                OnLand?.Invoke(pos);
                Break();
                return;
            }
            if (age > life) Break();
        }

        public Vector2 Position => transform.position;

        void Reflect()
        {
            reflected = true;
            velocity = new Vector2(-velocity.x * 1.3f, Mathf.Abs(velocity.y) * 0.4f + 2f);
            gravity = 0f;
            if (body != null) body.color = new Color(1f, 0.9f, 0.45f);
            ArenaFX.Burst(transform.position, new Color(1f, 0.85f, 0.35f), 8, 5f, 0.08f, 0.3f, 0.5f);
        }

        void Break()
        {
            ArenaFX.Burst(transform.position, burst, 10, 5f, 0.12f, 0.45f, 1.5f);
            ArenaFX.Burst(transform.position, Color.white, 4, 3f, 0.07f, 0.2f, 0.5f);
            Destroy(gameObject);
        }
    }
}
