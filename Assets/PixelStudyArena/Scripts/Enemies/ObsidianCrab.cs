using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Boss for floor 30, fought on the Obsidian Shore, drawn with the group's crab sprites
    /// (Assets/Sprites/Boss). A heavy crab that fights on the floor with three readable moves:
    ///   Claw Snap    — up close: raises its claw, then snaps it shut in front of it. Step back.
    ///   Scuttle Rush — digs in, then rushes across the shore. Jump over it, or get up on a ledge.
    ///                  Slamming into a wall dazes it: open to a critical!
    ///   Shell Slam   — rears up and slams down: shockwaves run along the floor (jump them) and
    ///                  obsidian shards fall on the marked spots.
    /// Super armor: hits don't interrupt it, but stance breaks do.
    /// </summary>
    public class ObsidianCrab : BossBug
    {
        [Header("Movement")]
        public float walkSpeed = 2.4f;

        [Header("Claw Snap")]
        public float snapTell = 0.6f;
        public float snapRange = 3.4f;
        public Vector2 snapBox = new Vector2(2.8f, 2.2f);

        [Header("Scuttle Rush")]
        public float rushTell = 0.8f;
        public float rushSpeed = 13f;
        public float wallDaze = 2f;

        [Header("Shell Slam")]
        public float slamTell = 0.75f;
        public float shockSpeed = 10f;
        public float shockRange = 10f;
        public int shards = 4;

        enum Mode { Intro, Walk, SnapTell, Snap, RushTell, Rush, SlamTell, Recover }
        Mode mode, lastAttack;
        float timer, vx;
        bool hitThisMove, struck;

        static readonly Color Ember = new Color(1f, 0.5f, 0.25f);
        static readonly Color Dust = new Color(0.55f, 0.48f, 0.45f);

        protected override void Start()
        {
            base.Start();
            mode = Mode.Intro;
            timer = 1.3f;
            anim.Play("tell", true);
            ArenaFX.Shake(0.5f);
            ArenaFX.Ring(Center, new Color(1f, 0.5f, 0.25f, 0.9f), 1f, 8f, 0.5f);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            TickWaves(dt);
            TickRocks(dt);

            switch (mode)
            {
                case Mode.Intro:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f) { mode = Mode.Walk; timer = 0.8f; }
                    break;

                case Mode.Walk:
                    FacePlayer();
                    vx = WallAhead(2f) || Mathf.Abs(ToPlayer.x) < 1.8f ? 0f : facing * walkSpeed;
                    anim.Play(Mathf.Abs(vx) > 0.1f ? "walk" : "idle");
                    if (timer <= 0f && PlayerAlive) ChooseAttack();
                    break;

                case Mode.SnapTell:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Snap;
                        timer = anim.Duration("attack");
                        struck = false;
                        anim.Play("attack", true);
                    }
                    break;

                case Mode.Snap:
                    vx = 0f;
                    if (!struck)
                    {
                        // the claw shuts on the first frame of the snap
                        struck = true;
                        var claw = Center + new Vector2(facing * 1.7f, 0.2f);
                        HitPlayerInBox(claw, snapBox, damage, 9f);
                        ArenaFX.Burst(claw, Ember, 10, 6f, 0.12f, 0.35f, 1f);
                        ArenaFX.Shake(0.3f);
                    }
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.6f; }
                    break;

                case Mode.RushTell:
                    vx = -facing * 0.8f; // digs in, backing up
                    if (Random.value < 0.5f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), Dust, 1, 4f, 0.4f, 0.12f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Rush;
                        timer = 3f;
                        hitThisMove = false;
                        anim.Play("walk");
                        anim.speed = 2.5f; // legs going flat out
                        ArenaFX.Shake(0.25f);
                    }
                    break;

                case Mode.Rush:
                    vx = facing * rushSpeed;
                    ArenaFX.Afterimage(body, new Color(1f, 0.45f, 0.25f, 0.3f), 0.15f);
                    if (Random.value < 0.6f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.5f), Dust, 2, 4f, 0.4f, 0.14f);
                    if (!hitThisMove && HitPlayerInBox(Center + new Vector2(facing * 0.6f, 0f), new Vector2(3f, 2f), damage, 12f) != HitResult.Ignored)
                        hitThisMove = true;
                    if (WallAhead(1.9f))
                    {
                        // smashed into the wall: dazed, open to a critical
                        anim.speed = 1f;
                        vx = 0f;
                        var wallPoint = Center + new Vector2(facing * 1.6f, 0f);
                        ArenaFX.Shake(0.7f);
                        ArenaFX.Burst(wallPoint, new Color(0.35f, 0.3f, 0.35f), 26, 9f, 0.18f, 0.8f, 3f);
                        Rockfall(transform.position.x + facing * Random.Range(-3f, 1f), 0.25f);
                        Break(wallDaze);
                    }
                    else if (timer <= 0f)
                    {
                        anim.speed = 1f;
                        mode = Mode.Recover;
                        timer = 0.5f;
                    }
                    break;

                case Mode.SlamTell:
                    vx = 0f;
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + Vector2.down * 0.6f, Ember, 1, 2f, 0.08f, 0.25f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        Slam();
                    }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (anim.Current != "attack" || anim.IsDone) anim.Play("idle");
                    if (timer <= 0f)
                    {
                        mode = Mode.Walk;
                        timer = Random.Range(0.6f, 1.2f);
                    }
                    break;
            }
        }

        void ChooseAttack()
        {
            float dist = Mathf.Abs(ToPlayer.x);
            bool sameLevel = Mathf.Abs(ToPlayer.y) < 2.5f;
            float roll = Random.value;
            Mode next;
            if (sameLevel && dist < snapRange) next = roll < 0.6f ? Mode.SnapTell : Mode.SlamTell;
            else if (sameLevel && dist > 6f) next = roll < 0.6f ? Mode.RushTell : Mode.SlamTell;
            else next = roll < 0.5f ? Mode.SlamTell : Mode.RushTell;
            if (next == lastAttack && next != Mode.SnapTell) next = next == Mode.RushTell ? Mode.SlamTell : Mode.RushTell;
            lastAttack = next;

            FacePlayer();
            vx = 0f;
            mode = next;
            timer = next == Mode.SnapTell ? DelayedTell(snapTell, 0.3f) : Tell(next == Mode.RushTell ? rushTell : slamTell);
            anim.Play("tell", true);
            BeginTell(timer);
        }

        // ---------- Shell Slam ----------

        void Slam()
        {
            var feet = (Vector2)transform.position;
            anim.Play("attack", true);
            ArenaFX.Shake(0.8f);
            ArenaFX.HitStop(0.07f);
            ArenaFX.Burst(feet + Vector2.up * 0.3f, Dust, 30, 9f, 0.16f, 0.7f, 3f);
            ArenaFX.FlatRing(feet + Vector2.up * 0.1f, new Color(1f, 0.5f, 0.25f, 0.9f), 0.5f, 7f, 0.35f);
            CameraFollow2D.Zoom(0.95f, 0.2f);
            HitPlayerInBox(feet + new Vector2(facing * 1.2f, 0.8f), new Vector2(3.4f, 1.8f), damage, 11f);
            Shockwaves(feet, shockSpeed, shockRange, Ember, Mathf.RoundToInt(damage * 0.7f));
            // shards of obsidian shaken loose around the knight (marked on the floor first)
            float around = PlayerAlive ? player.transform.position.x : feet.x;
            for (int i = 0; i < shards; i++) Rockfall(around + Random.Range(-7f, 7f), 0.45f + i * 0.15f);
            mode = Mode.Recover;
            timer = 1f;
        }

        // ---------- reactions ----------

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
        }

        protected override void OnStagger()
        {
            EndTell();
            anim.speed = 1f;
            if (stunTimer > 0f) mode = Mode.Recover;
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
