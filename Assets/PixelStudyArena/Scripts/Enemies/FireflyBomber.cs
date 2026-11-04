using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Firefly Bomber (from floor 42): drifts near you, then its lantern glows brighter and brighter
    /// until it dive-bombs where you were and explodes. Bait the dive, then roll. Kill it before it
    /// blows and it just fizzles.
    /// </summary>
    public class FireflyBomber : BugEnemy
    {
        public SpriteRenderer lantern;
        public float flySpeed = 4.5f;
        public float glowTime = 1.1f;
        public float diveSpeed = 14f;
        public float blastRadius = 2f;
        public float cooldown = 2.2f;

        enum Mode { Hover, Glow, Dive }
        Mode mode;
        float timer, cooldownTimer, bobSeed;
        int side = 1;
        Vector2 velocity, diveDir;
        bool exploded;

        static readonly Color LanternColor = new Color(0.85f, 1f, 0.45f);
        static readonly Color Hot = new Color(1f, 0.95f, 0.65f);

        protected override void Start()
        {
            base.Start();
            bobSeed = Random.value * 10f;
            side = Random.value < 0.5f ? -1 : 1;
            cooldownTimer = Random.Range(1f, 2.2f);
            anim.Play("fly");
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            cooldownTimer -= dt;
            var pos = (Vector2)transform.position;
            float glow = 0.35f + 0.15f * Mathf.Sin(Time.time * 4f + bobSeed);

            switch (mode)
            {
                case Mode.Hover:
                {
                    if (!PlayerAlive) { velocity = Vector2.Lerp(velocity, Vector2.zero, dt * 2f); break; }
                    if (Mathf.Abs(player.Center.x - pos.x) > 1.5f) side = pos.x >= player.Center.x ? 1 : -1;
                    var target = player.Center + new Vector2(side * 3.2f, 3f + Mathf.Sin(Time.time * 1.8f + bobSeed) * 0.6f);
                    velocity = Vector2.MoveTowards(velocity, Vector2.ClampMagnitude((target - pos) * 2f, flySpeed), 12f * dt);
                    FacePlayer();
                    if (cooldownTimer <= 0f && ToPlayer.magnitude < 8f)
                    {
                        mode = Mode.Glow;
                        timer = glowTime * telegraphScale;
                        BeginTell(timer);
                    }
                    break;
                }

                case Mode.Glow:
                {
                    // swells with light, shaking
                    float t = 1f - Mathf.Clamp01(timer / (glowTime * telegraphScale));
                    glow = 0.5f + 0.8f * t;
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, 10f * dt);
                    if (visual != null) visual.localPosition = Random.insideUnitCircle * 0.05f * t;
                    if (Random.value < 0.3f + t) ArenaFX.Burst(Center + Random.insideUnitCircle * 0.3f, LanternColor, 1, 2f, 0.07f, 0.25f, 0f);
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        if (visual != null) visual.localPosition = Vector3.zero;
                        mode = Mode.Dive;
                        timer = 1f;
                        diveDir = PlayerAlive ? (player.Center - Center).normalized : Vector2.down;
                        velocity = diveDir * diveSpeed;
                    }
                    break;
                }

                case Mode.Dive:
                    glow = 1.4f;
                    ArenaFX.Afterimage(body, new Color(0.85f, 1f, 0.45f, 0.4f), 0.15f);
                    bool hitPlayer = PlayerAlive && Vector2.Distance(player.Center, Center) < 0.8f;
                    bool hitWall = Physics2D.OverlapCircle(Center + diveDir * 0.3f, 0.25f, ArenaLayers.SolidMask);
                    if (hitPlayer || hitWall || timer <= 0f) { Explode(); return; }
                    break;
            }

            if (lantern != null) lantern.color = new Color(LanternColor.r, LanternColor.g, LanternColor.b, Mathf.Clamp01(glow));
            if (lantern != null) lantern.transform.localScale = Vector3.one * (1.6f + glow * 1.2f);

            pos += velocity * dt;
            var bounds = ArenaDirector.Instance != null ? ArenaDirector.Instance.flyBounds : new Rect(-100f, -100f, 200f, 200f);
            pos.x = Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin - (mode == Mode.Dive ? 1f : 0f), bounds.yMax);
            transform.position = pos;
        }

        void Explode()
        {
            if (exploded) return;
            exploded = true;
            var at = Center;
            ArenaFX.Glow(at, new Color(0.9f, 1f, 0.5f, 0.9f), blastRadius * 3f, 0.35f);
            ArenaFX.Ring(at, new Color(0.9f, 1f, 0.55f, 0.95f), 0.3f, blastRadius * 2.2f, 0.25f);
            ArenaFX.Burst(at, Hot, 26, 10f, 0.12f, 0.45f, 1f);
            ArenaFX.Burst(at, LanternColor, 18, 7f, 0.1f, 0.6f, 2f);
            ArenaFX.Shake(0.45f);
            ArenaFX.HitStop(0.05f);
            HitPlayerInBox(at, new Vector2(blastRadius * 2f, blastRadius * 2f), Mathf.RoundToInt(damage * 1.6f), 12f);
            // nearby bugs get caught in it too
            var filter = new ContactFilter2D();
            filter.SetLayerMask(ArenaLayers.EnemyMask);
            filter.useTriggers = true;
            Scratch.Clear();
            Physics2D.OverlapCircle(at, blastRadius, filter, Scratch);
            foreach (var h in Scratch)
            {
                var bug = h.GetComponentInParent<BugEnemy>();
                if (bug != null && bug != this && !bug.IsDead) bug.TakeChipDamage(damage, Hot);
            }
            ForceKill();
        }

        // committed to the dive: light hits don't stop it
        protected override bool HyperArmor => mode == Mode.Dive;

        protected override void OnStagger()
        {
            EndTell();
            if (visual != null) visual.localPosition = Vector3.zero;
            if (mode != Mode.Dive) { mode = Mode.Hover; cooldownTimer = cooldown; }
        }

        protected override void WhileStaggered(float dt)
        {
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, 14f * dt);
            transform.position += (Vector3)(velocity * dt);
        }

        protected override void WhileStunned(float dt)
        {
            velocity = Vector2.MoveTowards(velocity, Vector2.down * 1.5f, 10f * dt);
            transform.position += (Vector3)(velocity * dt);
        }

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
            if (!IsDead && mode != Mode.Dive) velocity = new Vector2((Center.x >= from.x ? 1 : -1) * knockback, 1f);
        }

        void FixedUpdate()
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }
}
