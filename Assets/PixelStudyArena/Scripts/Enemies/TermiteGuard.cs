using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Termite Guard (from floor 16): advances behind a big shield that blocks every light hit from the
    /// front. Break its guard with a heavy hit (a charged stab, a skill), or roll behind it: it turns
    /// round slowly. Shield-bashes you when you're close.
    /// </summary>
    public class TermiteGuard : BugEnemy
    {
        public float walkSpeed = 1.6f;
        [Tooltip("How long you have to be behind it before it turns round.")]
        public float turnDelay = 0.75f;
        public float bashRange = 2.1f;
        public float bashTell = 0.55f;
        public float cooldown = 1.3f;
        public float guardBreakTime = 2.2f;

        enum Mode { Advance, BashTell, Bash, Recover }
        Mode mode;
        float timer, cooldownTimer, behindTimer, vx;
        bool hitThisBash;

        static readonly Color Spark = new Color(1f, 0.9f, 0.6f);

        protected override void Start()
        {
            base.Start();
            FacePlayer();
            anim.Play("walk");
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            cooldownTimer -= dt;

            // turns round slowly when you get behind it
            if (PlayerAlive && mode == Mode.Advance)
            {
                int want = player.transform.position.x >= transform.position.x ? 1 : -1;
                if (want != facing) { if ((behindTimer += dt) >= turnDelay) { facing = want; behindTimer = 0f; } }
                else behindTimer = 0f;
            }

            switch (mode)
            {
                case Mode.Advance:
                {
                    anim.Play("walk");
                    var d = ToPlayer;
                    bool facingIt = d.x * facing > 0f;
                    bool blocked = WallAhead(0.9f) || !GroundAhead(0.7f);
                    vx = !facingIt || blocked || Mathf.Abs(d.x) < 1.3f ? 0f : facing * walkSpeed;
                    if (PlayerAlive && facingIt && Mathf.Abs(d.y) < 1.5f && Mathf.Abs(d.x) < bashRange && cooldownTimer <= 0f)
                    {
                        mode = Mode.BashTell;
                        timer = DelayedTell(bashTell, 0.5f);
                        BeginTell(timer);
                        anim.Play("tell");
                        vx = 0f;
                    }
                    break;
                }

                case Mode.BashTell:
                    vx = -facing * 0.6f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Bash;
                        timer = 0.22f;
                        hitThisBash = false;
                        anim.Play("bash");
                        vx = facing * 9f;
                    }
                    break;

                case Mode.Bash:
                    if (!hitThisBash && HitPlayerInBox(Center + new Vector2(facing * 0.9f, 0.1f), new Vector2(1.5f, 1.4f), damage, 11f) != HitResult.Ignored)
                        hitThisBash = true;
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.55f; anim.Play("walk"); }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f) { mode = Mode.Advance; cooldownTimer = cooldown; }
                    break;
            }
        }

        /// <summary>Hit from the front with the shield up.</summary>
        bool Shielded(Vector2 from) => stunTimer <= 0f && (from.x - transform.position.x) * facing > 0f;

        protected override bool HyperArmor => mode == Mode.Bash;

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            if (!IsDead && Shielded(from))
            {
                var spot = Center + new Vector2(facing * 0.7f, 0.1f);
                if (!heavy)
                {
                    // blocked
                    ArenaFX.Burst(spot, Spark, 8, 6f, 0.07f, 0.2f, 0.3f);
                    ArenaFX.DamageNumber(Center + Vector2.up * 0.7f, "BLOCKED", new Color(0.85f, 0.85f, 0.9f), 0.6f);
                    ArenaFX.HitStop(0.03f);
                    flash.Flash(Spark, 0.08f);
                    return;
                }
                // a heavy blow knocks the shield aside
                ArenaFX.Burst(spot, Spark, 18, 8f, 0.1f, 0.4f, 1f);
                ArenaFX.Ring(spot, new Color(1f, 0.85f, 0.5f, 0.9f), 0.3f, 2.4f, 0.22f);
                ArenaFX.DamageNumber(Center + Vector2.up * 1f, "GUARD BROKEN", new Color(1f, 0.6f, 0.35f), 0.8f);
                ArenaFX.Shake(0.25f);
                base.TakeHit(amount, from, knockback, heavy, crit, 0f);
                if (!IsDead)
                {
                    anim.Play("broken");
                    Break(guardBreakTime);
                }
                return;
            }
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
        }

        protected override void OnStagger()
        {
            EndTell();
            mode = Mode.Recover;
            timer = 0.3f;
            vx = rb.linearVelocity.x;
            if (stunTimer > 0f) anim.Play("broken");
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Advance;
            anim.Play("walk");
        }

        void FixedUpdate()
        {
            if (IsDead || staggerTimer > 0f || stunTimer > 0f) return;
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
