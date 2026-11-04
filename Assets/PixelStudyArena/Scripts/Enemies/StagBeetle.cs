using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Stag Beetle (elite, one per floor on floors 41-49): a mini-boss with its own health bar. Hits
    /// never make it flinch, only stance breaks do.
    ///   Charge      — lowers its antlers and rams across the floor. Into a wall: dazed (critical!).
    ///   Grab &amp; Throw — clamps you in its mandibles, lifts you overhead and hurls you across the arena.
    ///                 Can't be broken out of: roll through the lunge instead.
    ///   Antler Sweep — a wide upward swing in front.
    /// </summary>
    public class StagBeetle : BugEnemy
    {
        public string displayName = "Stag Beetle";
        public float walkSpeed = 2.4f;
        public float chargeSpeed = 13f;
        public float chargeTell = 0.65f;
        public float grabTell = 0.6f;
        public float sweepTell = 0.55f;
        public float dazeTime = 1.6f;

        enum Mode { Walk, ChargeTell, Charge, GrabTell, GrabLunge, Lift, SweepTell, Sweep, Recover }
        Mode mode, lastAttack;
        float timer, vx;
        bool hitThisMove;

        static readonly Color Dust = new Color(0.6f, 0.55f, 0.5f);
        static readonly Color Horn = new Color(1f, 0.85f, 0.6f);

        Vector2 Jaws => Center + new Vector2(facing * 1.5f, 0.2f);

        protected override void Start()
        {
            base.Start();
            FacePlayer();
            timer = 1f;
            anim.Play("walk");
            ArenaFX.Ring(Center, new Color(1f, 0.6f, 0.35f, 0.9f), 0.8f, 5f, 0.4f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.6f, "ELITE", new Color(1f, 0.6f, 0.35f), 1f);
            UpdateBar();
        }

        void UpdateBar()
        {
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null) ui.hud.SetBoss(!IsDead, Mathf.Max(0, Health) / (float)maxHealth, false, displayName);
        }

        protected override void OnDamaged() => UpdateBar();

        protected override void Die()
        {
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null) ui.hud.SetBoss(false, 0f);
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            ArenaFX.Shake(0.5f);
            base.Die();
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            switch (mode)
            {
                case Mode.Walk:
                {
                    FacePlayer();
                    anim.Play("walk");
                    var d = ToPlayer;
                    vx = WallAhead(1.6f) || !GroundAhead(1f) || Mathf.Abs(d.x) < 2.2f ? 0f : facing * walkSpeed;
                    if (timer <= 0f && PlayerAlive) ChooseAttack(Mathf.Abs(d.x), Mathf.Abs(d.y) < 2f);
                    break;
                }

                case Mode.ChargeTell:
                    vx = -facing * 0.6f;
                    if (Random.value < 0.4f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), Dust, 1, 4f, 0.4f, 0.12f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Charge;
                        timer = 1.8f;
                        hitThisMove = false;
                        anim.Play("charge");
                        ArenaFX.Shake(0.2f);
                    }
                    break;

                case Mode.Charge:
                    vx = facing * chargeSpeed;
                    ArenaFX.Afterimage(body, new Color(1f, 0.6f, 0.35f, 0.3f), 0.15f);
                    if (Random.value < 0.5f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.5f), Dust, 2, 4f, 0.4f, 0.13f);
                    if (!hitThisMove)
                    {
                        var r = HitPlayerInBox(Center + new Vector2(facing * 1f, 0f), new Vector2(2.4f, 1.6f), damage, 13f);
                        if (r != HitResult.Ignored) hitThisMove = true;
                    }
                    if (WallAhead(1.5f))
                    {
                        vx = 0f;
                        ArenaFX.Shake(0.5f);
                        ArenaFX.Burst(Center + new Vector2(facing * 1.3f, 0f), Dust, 20, 8f, 0.15f, 0.6f, 2.5f);
                        anim.Play("walk");
                        Break(dazeTime);
                    }
                    else if (timer <= 0f || !GroundAhead(0.8f)) { mode = Mode.Recover; timer = 0.6f; anim.Play("walk"); }
                    break;

                case Mode.GrabTell:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.GrabLunge;
                        timer = 0.25f;
                        hitThisMove = false;
                        anim.Play("charge");
                        vx = facing * 11f;
                    }
                    break;

                case Mode.GrabLunge:
                    if (!hitThisMove && PlayerAlive && Vector2.Distance(player.Center, Jaws) < 1.2f)
                    {
                        hitThisMove = true;
                        if (player.TryGrab(this, Jaws, 0)) // no breaking out of this one
                        {
                            mode = Mode.Lift;
                            timer = 0.75f;
                            vx = 0f;
                            anim.Play("lift");
                            ArenaFX.Burst(Jaws, Horn, 10, 5f, 0.08f, 0.25f, 0.5f);
                            ArenaFX.Shake(0.2f);
                            break;
                        }
                    }
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.6f; anim.Play("walk"); }
                    break;

                case Mode.Lift:
                {
                    vx = 0f;
                    if (!PlayerAlive || !player.IsHeldBy(this)) { mode = Mode.Recover; timer = 0.5f; break; }
                    // raises the knight up in its antlers (the tips of the raised jaws)...
                    float t = 1f - Mathf.Clamp01(timer / 0.75f);
                    var from = Jaws + Vector2.down * 0.9f;
                    var over = (Vector2)transform.position + new Vector2(facing * 1.15f, 1.45f);
                    player.HoldAt(Vector2.Lerp(from, over, Mathf.SmoothStep(0f, 1f, t)));
                    if (timer <= 0f)
                    {
                        // ...and hurls him behind it
                        player.Release(Vector2.zero);
                        player.TakeHit(Mathf.RoundToInt(damage * 1.4f), (Vector2)transform.position + new Vector2(facing * 3f, 0f), 17f);
                        ArenaFX.Shake(0.4f);
                        ArenaFX.Burst(over, Horn, 12, 6f, 0.1f, 0.3f, 0.5f);
                        mode = Mode.Recover;
                        timer = 0.8f;
                        anim.Play("walk");
                    }
                    break;
                }

                case Mode.SweepTell:
                    vx = -facing * 0.4f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Sweep;
                        timer = 0.35f;
                        anim.Play("lift");
                        vx = facing * 4f;
                        var spot = Center + new Vector2(facing * 1.6f, 0.6f);
                        HitPlayerInBox(spot, new Vector2(3.2f, 2.8f), Mathf.RoundToInt(damage * 1.1f), 11f);
                        ArenaFX.Slash(spot, facing, 3.4f, 40f, new Color(1f, 0.8f, 0.55f, 0.9f));
                        ArenaFX.Spray(spot + Vector2.down, Vector2.up, Dust, 6, 5f, 0.4f, 0.1f);
                        ArenaFX.Shake(0.25f);
                    }
                    break;

                case Mode.Sweep:
                    vx = Mathf.MoveTowards(vx, 0f, 20f * dt);
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.5f; anim.Play("walk"); }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f) { mode = Mode.Walk; timer = Random.Range(0.5f, 1.1f); }
                    break;
            }
        }

        void ChooseAttack(float dist, bool sameLevel)
        {
            float roll = Random.value;
            Mode next;
            if (!sameLevel) next = Mode.ChargeTell;
            else if (dist < 3f) next = roll < 0.45f ? Mode.GrabTell : roll < 0.8f ? Mode.SweepTell : Mode.ChargeTell;
            else next = roll < 0.65f ? Mode.ChargeTell : Mode.GrabTell;
            if (next == lastAttack) next = next == Mode.ChargeTell ? Mode.SweepTell : Mode.ChargeTell;
            lastAttack = next;
            mode = next;
            vx = 0f;
            timer = next == Mode.ChargeTell ? DelayedTell(chargeTell, 0.4f) : next == Mode.GrabTell ? DelayedTell(grabTell, 0.5f) : DelayedTell(sweepTell, 0.4f);
            anim.Play("tell");
            BeginTell(timer);
        }

        protected override void OnStagger()
        {
            EndTell();
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            if (stunTimer > 0f) { mode = Mode.Recover; timer = 0.4f; anim.Play("walk"); }
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
