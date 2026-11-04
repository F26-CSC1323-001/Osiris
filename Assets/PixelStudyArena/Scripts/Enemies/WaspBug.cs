using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Flying wasp: hovers above and to the side of the player, pulls back with a tell, then
    /// dive-bombs in a straight line. Flies through platforms but stays inside the arena.
    /// </summary>
    public class WaspBug : BugEnemy
    {
        public float flySpeed = 5f;
        public float attackRange = 5f;
        public float tellTime = 0.55f;
        public float diveSpeed = 13f;
        public float diveTime = 0.42f;
        public float cooldown = 1.4f;

        enum Mode { Hover, Tell, Dive, Recover }
        Mode mode;
        float timer, cooldownTimer;
        Vector2 velocity, diveDir;
        bool hitThisDive;
        int side = 1;
        float bobSeed;

        protected override void Start()
        {
            base.Start();
            bobSeed = Random.value * 10f;
            side = Random.value < 0.5f ? -1 : 1;
            cooldownTimer = Random.Range(0.5f, 1.5f);
            anim.Play("fly");
        }

        protected override void Think(float dt)
        {
            cooldownTimer -= dt;
            timer -= dt;
            var pos = (Vector2)transform.position;

            switch (mode)
            {
                case Mode.Hover:
                {
                    if (!PlayerAlive)
                    {
                        velocity = Vector2.Lerp(velocity, new Vector2(Mathf.Sin(Time.time + bobSeed), 0.5f), dt * 2f);
                        break;
                    }
                    if (Mathf.Abs(player.Center.x - pos.x) > 1.5f) side = pos.x >= player.Center.x ? 1 : -1;
                    var target = player.Center + new Vector2(side * 2.6f, 2.3f + Mathf.Sin(Time.time * 2.2f + bobSeed) * 0.5f);
                    var desired = Vector2.ClampMagnitude((target - pos) * 2.2f, flySpeed);
                    velocity = Vector2.MoveTowards(velocity, desired, 14f * dt);
                    FacePlayer();
                    if (cooldownTimer <= 0f && ToPlayer.magnitude < attackRange)
                    {
                        mode = Mode.Tell;
                        timer = DelayedTell(tellTime, 0.5f);
                        BeginTell(timer);
                    }
                    break;
                }

                case Mode.Tell:
                    FacePlayer();
                    velocity = Vector2.MoveTowards(velocity, -ToPlayer.normalized * 1.5f, 20f * dt);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Dive;
                        timer = diveTime;
                        hitThisDive = false;
                        diveDir = ToPlayer.normalized;
                        velocity = diveDir * diveSpeed;
                        if (anim.Has("attack")) anim.Play("attack", true); // flyers drawn with an attack (the group's Fly enemy)
                    }
                    break;

                case Mode.Dive:
                    ArenaFX.Afterimage(body, new Color(1f, 0.8f, 0.3f, 0.35f), 0.15f);
                    if (!hitThisDive && HitPlayerInBox(Center, new Vector2(0.9f, 0.8f), damage, 6f) != HitResult.Ignored)
                        hitThisDive = true;
                    if (timer <= 0f || Physics2D.OverlapCircle(Center, 0.3f, ArenaLayers.SolidMask))
                    {
                        mode = Mode.Recover;
                        timer = 0.6f;
                        anim.Play("fly");
                    }
                    break;

                case Mode.Recover:
                    velocity = Vector2.MoveTowards(velocity, Vector2.up * 2f, 18f * dt);
                    if (timer <= 0f)
                    {
                        mode = Mode.Hover;
                        cooldownTimer = cooldown;
                    }
                    break;
            }

            pos += velocity * dt;
            var bounds = ArenaDirector.Instance != null ? ArenaDirector.Instance.flyBounds : new Rect(-100f, -100f, 200f, 200f);
            pos.x = Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax);
            transform.position = pos;
        }

        protected override void OnStagger()
        {
            EndTell();
            mode = Mode.Recover;
            timer = 0.3f;
            anim.Play("fly");
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

        void FixedUpdate()
        {
            // Movement is driven by the transform; the kinematic body only carries the collider.
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            bool armored = HyperArmor && !heavy;
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
            if (!IsDead && !armored && stunTimer <= 0f) velocity = new Vector2((Center.x >= from.x ? 1 : -1) * knockback, 1f);
        }

        // committed to the dive: light hits don't stop it
        protected override bool HyperArmor => mode == Mode.Dive;
    }
}
