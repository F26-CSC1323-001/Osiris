using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Scorpion (from floor 25): a grounded duelist.
    ///   Pincer Grab — opens its claws, then lunges. Caught: it squeezes you (roll twice to break free)
    ///                 and then flings you away.
    ///   Tail Stab   — raises its tail over its head and stabs down a couple of steps in front, poisoning
    ///                 you. Sometimes it holds the tail up a beat longer to catch an early roll.
    /// </summary>
    public class Scorpion : BugEnemy
    {
        public float walkSpeed = 2.4f;
        public float grabTell = 0.5f;
        public float grabRange = 2.3f;
        public float tailTell = 0.55f;
        public float tailReach = 2.6f;
        public int poisonPerTick = 3;
        public float poisonTime = 3f;
        public float holdTime = 1.3f;
        public float cooldown = 1.1f;

        enum Mode { Stalk, GrabTell, GrabLunge, Squeeze, TailTell, TailStab, Recover }
        Mode mode;
        float timer, cooldownTimer, squeezeTick, vx;
        bool hitThisMove;

        static readonly Color Venom = new Color(0.55f, 1f, 0.35f);
        static readonly Color Dust = new Color(0.65f, 0.58f, 0.48f);

        Vector2 ClawPoint => Center + new Vector2(facing * 1.2f, 0.1f);

        protected override void Start()
        {
            base.Start();
            FacePlayer();
            cooldownTimer = Random.Range(0.5f, 1.2f);
            anim.Play("walk");
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            cooldownTimer -= dt;
            switch (mode)
            {
                case Mode.Stalk:
                {
                    var d = ToPlayer;
                    if (PlayerAlive) FacePlayer();
                    float dist = Mathf.Abs(d.x);
                    bool sameLevel = Mathf.Abs(d.y) < 1.6f;
                    bool blocked = WallAhead(0.9f) || !GroundAhead(0.7f);
                    vx = blocked || dist < 2.1f ? 0f : facing * walkSpeed;
                    anim.Play(Mathf.Abs(vx) > 0.1f ? "walk" : "idle");
                    if (!PlayerAlive || !sameLevel || cooldownTimer > 0f) break;
                    if (dist < grabRange && Random.value < 0.5f)
                    {
                        mode = Mode.GrabTell;
                        timer = DelayedTell(grabTell, 0.4f);
                        BeginTell(timer);
                        anim.Play("grabTell");
                        vx = 0f;
                    }
                    else if (dist < tailReach + 0.8f)
                    {
                        mode = Mode.TailTell;
                        // the tail sometimes hangs there a beat longer
                        timer = DelayedTell(tailTell, 1f);
                        BeginTell(timer);
                        anim.Play("tailTell");
                        vx = 0f;
                    }
                    break;
                }

                case Mode.GrabTell:
                    vx = -facing * 0.5f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.GrabLunge;
                        timer = 0.22f;
                        hitThisMove = false;
                        anim.Play("grab");
                        vx = facing * 10f;
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), Dust, 5, 4f, 0.4f, 0.1f);
                    }
                    break;

                case Mode.GrabLunge:
                    if (!hitThisMove && PlayerAlive && Vector2.Distance(player.Center, ClawPoint) < 1.1f)
                    {
                        hitThisMove = true;
                        if (player.TryGrab(this, ClawPoint, 2))
                        {
                            mode = Mode.Squeeze;
                            timer = holdTime;
                            squeezeTick = 0.3f;
                            vx = 0f;
                            ArenaFX.Burst(ClawPoint, Color.white, 8, 4f, 0.08f, 0.25f, 0.5f);
                            break;
                        }
                    }
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.6f; anim.Play("idle"); }
                    break;

                case Mode.Squeeze:
                    vx = 0f;
                    if (!PlayerAlive || !player.IsHeldBy(this))
                    {
                        // broke free
                        mode = Mode.Recover;
                        timer = 0.8f;
                        anim.Play("idle");
                        break;
                    }
                    player.HoldAt(ClawPoint + Vector2.down * 0.9f);
                    if ((squeezeTick -= dt) <= 0f)
                    {
                        squeezeTick = 0.4f;
                        player.TakePoison(Mathf.Max(1, damage / 3)); // a crushing squeeze
                        ArenaFX.Burst(ClawPoint, bloodColor, 4, 3f, 0.08f, 0.2f, 0.5f);
                        ArenaFX.Shake(0.1f);
                    }
                    if (timer <= 0f)
                    {
                        // flings you away
                        player.Release(Vector2.zero);
                        player.TakeHit(damage, Center, 13f);
                        mode = Mode.Recover;
                        timer = 0.7f;
                        anim.Play("idle");
                    }
                    break;

                case Mode.TailTell:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.TailStab;
                        timer = 0.3f;
                        anim.Play("tailStab");
                        var spot = (Vector2)transform.position + new Vector2(facing * tailReach, 0.6f);
                        var r = HitPlayerInBox(spot, new Vector2(1.6f, 1.6f), damage, 6f);
                        if (r == HitResult.Damaged) player.ApplyPoison(poisonPerTick, poisonTime);
                        ArenaFX.Spray(spot + Vector2.down * 0.5f, Vector2.up, Dust, 6, 5f, 0.4f, 0.1f);
                        ArenaFX.Burst(spot, Venom, 6, 4f, 0.08f, 0.3f, 0.5f);
                        ArenaFX.Shake(0.15f);
                    }
                    break;

                case Mode.TailStab:
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.55f; anim.Play("idle"); }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f) { mode = Mode.Stalk; cooldownTimer = cooldown; }
                    break;
            }
        }

        protected override bool HyperArmor => mode == Mode.GrabLunge || mode == Mode.Squeeze;

        protected override void OnStagger()
        {
            EndTell();
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            mode = Mode.Recover;
            timer = 0.3f;
            vx = rb.linearVelocity.x;
            anim.Play("idle");
        }

        void FixedUpdate()
        {
            if (IsDead || staggerTimer > 0f || stunTimer > 0f) return;
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
