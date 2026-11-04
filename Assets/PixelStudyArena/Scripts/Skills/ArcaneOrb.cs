using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// One of the Arcane Orbs: circles the knight for a moment, then hunts down the nearest bug no other
    /// orb is chasing and explodes on it (splashing anything next to it).
    /// </summary>
    public class ArcaneOrb : MonoBehaviour
    {
        public SpriteRenderer body;
        [Tooltip("Rune circle that flashes where the orb bursts.")]
        public Sprite burstSprite;
        public float orbitRadius = 1.4f;
        public float orbitSpeed = 280f;
        public float seekRange = 11f;
        public float maxSpeed = 15f;
        public float acceleration = 50f;
        public float lifetime = 6f;
        public float hitRadius = 0.45f;
        public float splashRadius = 1.2f;

        static readonly Color Purple = new Color(0.78f, 0.45f, 1f);
        static readonly Color PurpleLight = new Color(0.95f, 0.8f, 1f);
        static readonly HashSet<BugEnemy> Claimed = new HashSet<BugEnemy>();
        static readonly List<Collider2D> Hits = new List<Collider2D>();

        float angle, orbitTime, age, trailTimer;
        int damage;
        BugEnemy target;
        Vector2 velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Claimed.Clear();

        public void Init(float startAngle, float orbitSeconds, int amount)
        {
            angle = startAngle;
            orbitTime = orbitSeconds;
            damage = amount;
        }

        static Vector2 Owner => ArenaPlayer.Instance != null ? ArenaPlayer.Instance.Center : Vector2.zero;

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;
            var pos = (Vector2)transform.position;

            // 1) circle the knight for a moment
            if (orbitTime > 0f)
            {
                orbitTime -= dt;
                Orbit(dt);
                return;
            }

            // 2) lock on to a bug (re-target if ours died); keep circling if there's nothing to chase
            if (target == null || target.IsDead)
            {
                Release();
                PickTarget(pos);
            }
            if (target == null)
            {
                Orbit(dt);
                if (age > lifetime) Fizzle();
                return;
            }

            // 3) home in
            var desired = (target.Center - pos).normalized * maxSpeed;
            velocity = Vector2.MoveTowards(Vector2.ClampMagnitude(velocity, maxSpeed), desired, acceleration * dt);
            pos += velocity * dt;
            transform.position = pos;
            Trail(dt);

            if (Vector2.Distance(pos, target.Center) <= hitRadius + 0.3f) Explode();
            else if (age > lifetime) Fizzle();
        }

        void Orbit(float dt)
        {
            angle += orbitSpeed * dt;
            var a = angle * Mathf.Deg2Rad;
            transform.position = Owner + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbitRadius;
            velocity = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * (orbitSpeed * Mathf.Deg2Rad * orbitRadius);
            Trail(dt);
        }

        void Trail(float dt)
        {
            float pulse = 1f + Mathf.Sin(age * 18f) * 0.12f;
            transform.localScale = new Vector3(pulse, pulse, 1f);
            trailTimer -= dt;
            if (trailTimer > 0f) return;
            trailTimer = 0.02f;
            // a ribbon of fading copies behind it, plus a few sparkles
            ArenaFX.Afterimage(body, new Color(0.75f, 0.4f, 1f, 0.5f), 0.22f);
            if (Random.value < 0.5f) ArenaFX.Spray(transform.position, -velocity, Random.value < 0.5f ? Purple : PurpleLight, 1, 1.5f, 0.6f, 0.1f);
        }

        void PickTarget(Vector2 from)
        {
            var director = ArenaDirector.Instance;
            if (director == null) return;
            var inRange = director.Alive.Where(b => b != null && !b.IsDead && Vector2.Distance(b.Center, from) <= seekRange)
                .OrderBy(b => Vector2.Distance(b.Center, from)).ToList();
            target = inRange.FirstOrDefault(b => !Claimed.Contains(b)) ?? inRange.FirstOrDefault();
            if (target != null) Claimed.Add(target);
        }

        void Release()
        {
            if (target != null) Claimed.Remove(target);
            target = null;
        }

        void Explode()
        {
            var pos = (Vector2)transform.position;
            if (target != null && !target.IsDead) target.TakeHit(damage, pos, 4f, false, true);

            var filter = new ContactFilter2D();
            filter.SetLayerMask(ArenaLayers.EnemyMask);
            filter.useTriggers = true;
            Hits.Clear();
            Physics2D.OverlapCircle(pos, splashRadius, filter, Hits);
            foreach (var h in Hits)
            {
                var bug = h.GetComponentInParent<BugEnemy>();
                if (bug == null || bug.IsDead || bug == target) continue;
                bug.TakeHit(Mathf.Max(1, damage / 2), pos, 3f, false, false);
            }

            ArenaFX.Burst(pos, Purple, 22, 8f, 0.13f, 0.5f, 1f);
            ArenaFX.Burst(pos, PurpleLight, 10, 4f, 0.09f, 0.3f, 0.5f);
            ArenaFX.Ring(pos, new Color(0.8f, 0.5f, 1f, 0.9f), 0.3f, 2.6f, 0.25f);
            ArenaFX.Glow(pos, new Color(0.7f, 0.35f, 1f, 0.7f), 3.5f, 0.35f);
            // a rune circle flashes open and a star bursts at the center
            if (burstSprite != null)
                ArenaFX.PlaySprite(burstSprite, pos, new Color(0.85f, 0.55f, 1f, 0.95f), 0.35f, true, Vector3.one * 0.3f, Vector3.one * 1.3f, Random.Range(0f, 360f), 260f);
            var star = ArenaPlayer.Instance != null ? ArenaPlayer.Instance.starSprite : null;
            if (star != null)
                ArenaFX.PlaySprite(star, pos, new Color(0.9f, 0.7f, 1f, 1f), 0.22f, true, Vector3.one * 0.5f, Vector3.one * 2.2f, Random.Range(0f, 45f), 90f);
            ArenaFX.Shake(0.15f);
            ArenaFX.HitStop(0.035f);
            Release();
            Destroy(gameObject);
        }

        void Fizzle()
        {
            ArenaFX.Burst(transform.position, Purple, 8, 3f, 0.09f, 0.35f, 0.5f);
            Release();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (target != null) Claimed.Remove(target);
        }
    }
}
