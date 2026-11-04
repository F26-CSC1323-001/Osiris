using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Pill Bug (from floor 5): plods toward you, then curls into an armored ball and rolls at you,
    /// bouncing. While it's rolled up your hits just bounce off. If it rolls into a wall it's dazed on
    /// its back: open to a critical. Otherwise it uncurls after a few seconds.
    /// </summary>
    public class PillBug : BugEnemy
    {
        public float walkSpeed = 1.8f;
        public float range = 9f;
        public float curlTime = 0.5f;
        public float rollSpeed = 11f;
        public float rollTime = 2.4f;
        public float dazeTime = 1.8f;
        public float cooldown = 1.4f;

        enum Mode { Walk, Curl, Roll, Uncurl }
        Mode mode;
        float timer, cooldownTimer, bounceTimer, spin, vx;
        bool hitThisRoll;

        static readonly Color Spark = new Color(0.85f, 0.9f, 1f);
        static readonly Color Dust = new Color(0.62f, 0.6f, 0.58f);

        bool Rolled => mode == Mode.Roll || mode == Mode.Curl;
        bool Grounded => rb.linearVelocity.y <= 0.1f &&
                         Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.1f, Vector2.down, 0.2f, ArenaLayers.GroundMask);

        protected override void Start()
        {
            base.Start();
            facing = Random.value < 0.5f ? -1 : 1;
            cooldownTimer = Random.Range(0.6f, 1.5f);
            anim.Play("walk");
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            cooldownTimer -= dt;
            switch (mode)
            {
                case Mode.Walk:
                {
                    anim.Play("walk");
                    var d = ToPlayer;
                    // feet, not centers: it's a lot shorter than the knight
                    bool sameLevel = PlayerAlive && Mathf.Abs(player.transform.position.y - transform.position.y) < 1.3f;
                    if (PlayerAlive && Mathf.Abs(d.x) < 40f) FacePlayer();
                    bool blocked = WallAhead(0.8f) || !GroundAhead(0.6f);
                    vx = blocked ? 0f : facing * walkSpeed;
                    if (PlayerAlive && sameLevel && Mathf.Abs(d.x) < range && cooldownTimer <= 0f)
                    {
                        mode = Mode.Curl;
                        timer = DelayedTell(curlTime, 0.4f);
                        BeginTell(timer);
                        anim.Play("curl");
                        vx = 0f;
                    }
                    break;
                }

                case Mode.Curl:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Roll;
                        timer = rollTime;
                        hitThisRoll = false;
                        bounceTimer = 0.25f;
                        anim.Play("ball");
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), Dust, 6, 4f, 0.4f, 0.12f);
                    }
                    break;

                case Mode.Roll:
                {
                    vx = facing * rollSpeed;
                    spin -= facing * rollSpeed * 90f * dt;
                    if (visual != null)
                    {
                        // spin around the middle of the ball, not its feet
                        var rot = Quaternion.Euler(0f, 0f, spin);
                        var pivot = new Vector3(0f, centerHeight, 0f);
                        visual.localRotation = rot;
                        visual.localPosition = pivot - rot * pivot;
                    }
                    // little bounces as it rolls
                    if ((bounceTimer -= dt) <= 0f && Grounded)
                    {
                        bounceTimer = 0.45f;
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 4.5f);
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.5f), Dust, 3, 3f, 0.4f, 0.1f);
                    }
                    if (!hitThisRoll && HitPlayerInBox(Center, new Vector2(1.1f, 1.1f), damage, 9f) != HitResult.Ignored)
                        hitThisRoll = true;
                    if (WallAhead(0.65f))
                    {
                        // smacks into the wall and ends up dazed on its back
                        ArenaFX.Shake(0.25f);
                        ArenaFX.Burst(Center + new Vector2(facing * 0.6f, 0f), Dust, 14, 6f, 0.12f, 0.5f, 2f);
                        ArenaFX.DamageNumber(Center + Vector2.up * 1f, "DAZED", new Color(1f, 0.9f, 0.45f), 0.7f);
                        Uncurl(true);
                    }
                    else if (timer <= 0f) Uncurl(false);
                    break;
                }

                case Mode.Uncurl:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f) { mode = Mode.Walk; cooldownTimer = cooldown; }
                    break;
            }
        }

        void Uncurl(bool dazed)
        {
            spin = 0f;
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
            vx = 0f;
            mode = Mode.Uncurl;
            timer = 0.4f;
            if (dazed)
            {
                anim.Play("dazed");
                Break(dazeTime);
            }
            else anim.Play("walk");
        }

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            if (Rolled && !IsDead && !Unavoidable) // (the ultimate cracks it open anyway)
            {
                // the shell turns the blade aside
                ArenaFX.Burst(Center, Spark, 8, 6f, 0.07f, 0.2f, 0.3f);
                ArenaFX.DamageNumber(Center + Vector2.up * 0.6f, "CLANG", new Color(0.8f, 0.85f, 0.95f), 0.6f);
                ArenaFX.HitStop(0.03f);
                flash.Flash(Spark, 0.08f);
                return;
            }
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
        }

        protected override void OnStagger()
        {
            EndTell();
            if (Rolled) return;
            mode = Mode.Uncurl;
            timer = 0.3f;
        }

        protected override void OnStunEnd()
        {
            spin = 0f;
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
            mode = Mode.Walk;
            cooldownTimer = cooldown;
            anim.Play("walk");
        }

        void FixedUpdate()
        {
            if (IsDead || staggerTimer > 0f || stunTimer > 0f) return;
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
