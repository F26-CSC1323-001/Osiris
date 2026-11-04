using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Boss for floor 30, fought in the Moonlit Shrine. A duelist with fast footwork that strings its
    /// moves together: after an attack it often flows straight into another (once in phase 1, up to
    /// three times once enraged). It hops back when you crowd it and blinks behind you when you keep away.
    ///   Triple Slash      — raises both scythes, then three fast slashes that step forward. Enraged, a
    ///                       fourth rising cut comes a beat late (don't roll early).
    ///   Dash Slash        — crouches, then darts across the floor with one long cut.
    ///   Crescent Wave     — a blade of wind skimming along the floor (jump it); enraged, a second one
    ///                       follows at head height.
    ///   Blink Strike      — a mark appears behind you, then it's there, mid-slash.
    ///   Leap Slam         — flies up, hovers over you, then slams down. Shockwaves along the floor, and
    ///                       its scythes get stuck for a moment.
    ///   Reaping Dance     — it vanishes and the shrine goes dark. Red lines flash across you and it cuts
    ///                       along each one, then reappears for a final X cut.
    ///   Moonlit Execution — its signature. It crosses its scythes under a huge moon (red flash: perilous),
    ///                       then lunges across the floor. Roll through it, jump it, or parry it (shield
    ///                       or Riposte): blocking doesn't work. If it catches you it blinks around you,
    ///                       cutting, and finishes with a giant X. Whiffed, it's wide open.
    /// It also reads your attacks: a chance to dodge a hit (and often punish right after) or to parry a
    /// sword swing (knocks you back and counter-slashes). Below half health it gets faster and meaner.
    /// </summary>
    public class ReaperMantis : BossBug
    {
        [Tooltip("Scythe slash: the swing effect, and the crescent wave it throws.")]
        public Sprite slashSprite;

        [Header("Footwork")]
        public float walkSpeed = 4.4f;
        public float preferredRange = 2.4f;
        public float hopSpeed = 12f;

        [Header("Triple Slash")]
        public float tripleTell = 0.42f;
        public float slashGap = 0.2f;
        public Vector2 slashBox = new Vector2(2.9f, 2.6f);
        public Vector2 slashOffset = new Vector2(1.6f, 1.4f);

        [Header("Dash Slash")]
        public float dashTell = 0.36f;
        public float dashSpeed = 21f;
        public float dashTime = 0.4f;

        [Header("Leap Slam")]
        public float leapTell = 0.36f;
        public float hoverTime = 0.45f;
        public float slamSpeed = 34f;
        public float stuckTime = 0.6f;
        public float shockSpeed = 13f;
        public float shockRange = 8f;

        [Header("Crescent Wave")]
        public float waveTell = 0.4f;
        public float waveSpeed = 15f;

        [Header("Blink Strike")]
        public float blinkTell = 0.38f;

        [Header("Reaping Dance")]
        public int danceCuts = 4;
        public float cutWarn = 0.45f;
        public float cutGap = 0.34f;
        public float danceCooldown = 20f;

        [Header("Moonlit Execution")]
        [Tooltip("The huge crescent moon that rises behind it.")]
        public Sprite moonSprite;
        public float execTell = 0.75f;
        public float execSpeed = 30f;
        public float execCooldown = 14f;
        public int execCuts = 6;
        [Tooltip("Damage of each cut while it holds you, as a fraction of its damage.")]
        public float execCutDamage = 0.16f;
        [Tooltip("The final X cut, as a fraction of its damage.")]
        public float execFinishDamage = 0.9f;
        [Tooltip("How long it's open after a whiffed lunge.")]
        public float execWhiff = 0.95f;

        [Header("Combos")]
        [Range(0f, 1f)] public float chainChance = 0.45f;
        [Range(0f, 1f)] public float enragedChainChance = 0.75f;

        [Header("Dodge & Parry")]
        [Range(0f, 1f)] public float dodgeChance = 0.15f;
        [Range(0f, 1f)] public float parryChance = 0.1f;
        public float evadeCooldown = 2.6f;
        [Range(0f, 1f)] public float punishAfterDodge = 0.55f;

        enum Mode
        {
            Intro, Stalk, Hop, TripleTell, Triple, DashTell, Dash, LeapCrouch, LeapRise, LeapHover, LeapSlam, Stuck,
            WaveTell, BlinkTell, BlinkStrike, DanceIn, Dance, DanceFinish, Recover, Dodge, Guard, CounterTell, Counter,
            ExecTell, ExecDash, ExecWhiff, ExecCuts, ExecPause, ExecFinish,
        }
        Mode mode = Mode.Intro, lastAttack;
        float timer, evadeTimer, hopTimer, danceTimer, execTimer = 7f, vx, riseTarget, landY, secondWave = -1f, danceT, fxTimer;
        int strike, strikes, chain, execCut;
        bool hitThisMove, struck;
        Vector2 flyVelocity, blinkDest, finishSpot, execFrom, execHold;

        class DanceCut
        {
            public float warnAt, strikeAt;
            public bool warned, done;
            public Vector2 from, to;
        }
        readonly List<DanceCut> cuts = new List<DanceCut>();
        DanceCut finishA, finishB;

        static readonly Color Blade = new Color(0.85f, 1f, 0.8f);
        static readonly Color Jade = new Color(0.45f, 0.9f, 0.55f);
        static readonly Color Moon = new Color(0.82f, 1f, 0.88f);
        static readonly Color Dust = new Color(0.62f, 0.58f, 0.52f);
        static readonly Color Danger = new Color(1f, 0.25f, 0.25f, 0.95f);

        // where it blinks to around the knight during the execution (offsets from his center)
        static readonly Vector2[] ExecSpots =
        {
            new Vector2(2.1f, -0.1f), new Vector2(-2.1f, 0.5f), new Vector2(1.5f, 1.8f),
            new Vector2(-1.8f, -0.2f), new Vector2(0.3f, 2.5f), new Vector2(2.2f, 0.8f),
        };

        /// <summary>Easy 0.6x, Normal 1x, Hard 1.4x the dodge and parry chances.</summary>
        static float EvadeScale => GameSession.Run == null ? 1f : GameSession.Run.difficulty switch
        {
            Difficulty.Easy => 0.6f,
            Difficulty.Hard => 1.4f,
            _ => 1f,
        };

        bool Flying => rb != null && rb.bodyType == RigidbodyType2D.Kinematic;
        bool Vanished => mode == Mode.DanceIn || mode == Mode.Dance;
        bool Executing => mode == Mode.ExecCuts || mode == Mode.ExecPause || mode == Mode.ExecFinish;
        float Pace => enraged ? 1.25f : 1f;
        Vector2 Feet => transform.position;

        protected override void Start()
        {
            base.Start();
            timer = 1.1f;
            danceTimer = 16f;
            if (simple)
            {
                // one move at a time, and it doesn't read your swings (no dodging or parrying them)
                chainChance = enragedChainChance = 0f;
                dodgeChance = parryChance = 0f;
            }
            anim.Play("raise");
            ArenaFX.Shake(0.5f);
            ArenaFX.Ring(Center, new Color(0.5f, 1f, 0.6f, 0.9f), 1f, 8f, 0.5f);
            PlaySlash(Center + Vector2.up, 20f, 1.5f);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            evadeTimer -= dt;
            hopTimer -= dt;
            danceTimer -= dt;
            execTimer -= dt;
            TickWaves(dt);
            if (secondWave > 0f && (secondWave -= dt) <= 0f) ThrowWave(1.8f);

            switch (mode)
            {
                case Mode.Intro:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f) { mode = Mode.Stalk; timer = 0.5f; }
                    break;

                case Mode.Stalk:
                {
                    if (!PlayerAlive) { vx = 0f; anim.Play("idle"); break; }
                    chain = 0;
                    FacePlayer();
                    float dist = Mathf.Abs(player.transform.position.x - transform.position.x);
                    bool sameLevel = Mathf.Abs(player.transform.position.y - transform.position.y) < 1.6f;
                    // crowded: hop back out of reach
                    if (dist < 1.3f && hopTimer <= 0f && sameLevel && Random.value < 0.6f) { Hop(); break; }
                    if (dist > preferredRange + 0.5f && !WallAhead(1.2f)) vx = facing * walkSpeed * Pace;
                    else if (dist < preferredRange - 1.2f && !Physics2D.Raycast(Center, new Vector2(-facing, 0f), 1.4f, ArenaLayers.SolidMask)) vx = -facing * walkSpeed * 0.6f;
                    else vx = 0f;
                    anim.Play(Mathf.Abs(vx) > 0.1f ? "walk" : "idle");
                    if (timer <= 0f) ChooseAttack(dist, sameLevel);
                    break;
                }

                case Mode.Hop:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.35f), 0.18f);
                    if (timer <= 0f) { mode = Mode.Stalk; timer = Mathf.Min(timer, 0.15f); }
                    break;

                case Mode.TripleTell:
                    vx = -facing * 0.6f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Triple;
                        strike = 0;
                        strikes = enraged ? 4 : 3;
                        timer = 0f;
                    }
                    break;

                case Mode.Triple:
                    vx = Mathf.MoveTowards(vx, 0f, 40f * dt);
                    if (timer <= 0f)
                    {
                        if (strike >= strikes)
                        {
                            anim.Play("idle");
                            AfterMove(0.6f);
                            break;
                        }
                        Strike(strike);
                        strike++;
                        timer = slashGap / Pace;
                        if (strike == 3 && strikes == 4)
                        {
                            // the fourth cut comes a beat late: scythe raised, a glint, then it rips upward
                            timer = 0.42f;
                            anim.Play("raise");
                            BeginTell(timer);
                            ArenaFX.Burst(Center + new Vector2(facing * 0.6f, 1.6f), Color.white, 6, 3f, 0.07f, 0.2f, 0f);
                        }
                    }
                    break;

                case Mode.DashTell:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.3f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.3f), Dust, 1, 3f, 0.4f, 0.1f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Dash;
                        timer = dashTime;
                        hitThisMove = false;
                        anim.Play("slashDown", true);
                        vx = facing * dashSpeed;
                        PlaySlash(Center + new Vector2(facing * 1.2f, 0.1f), -10f, 1.5f);
                    }
                    break;

                case Mode.Dash:
                    vx = facing * dashSpeed;
                    ArenaFX.Afterimage(body, new Color(0.5f, 1f, 0.6f, 0.35f), 0.2f);
                    if (!hitThisMove)
                    {
                        var r = HitPlayerInBox(Center + new Vector2(facing * 1.3f, 0f), new Vector2(2.4f, 2.4f), damage, 10f);
                        if (r != HitResult.Ignored) hitThisMove = true;
                        if (r == HitResult.Parried) break;
                    }
                    if (timer <= 0f || WallAhead(1.3f))
                    {
                        vx = facing * 4f;
                        anim.Play("slashUp", true);
                        PlaySlash(Center + new Vector2(facing * 1.4f, 0.4f), 30f, 1.2f);
                        if (enraged && Random.value < 0.5f) secondWave = 0.15f; // flicks a blade of wind back the way it came
                        AfterMove(0.5f);
                    }
                    break;

                case Mode.WaveTell:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + new Vector2(-facing * 0.6f, 1f), Blade, 1, 2f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        anim.Play("slashUp", true);
                        PlaySlash(Center + new Vector2(facing * 1.3f, 0.2f), 25f, 1.4f);
                        ThrowWave(0.55f);
                        if (enraged) secondWave = 0.25f;
                        AfterMove(0.45f);
                    }
                    break;

                case Mode.BlinkTell:
                    vx = 0f;
                    if (Random.value < 0.6f) ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.4f), 0.2f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        var from = Center;
                        ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.6f), 0.4f);
                        transform.position = blinkDest;
                        rb.position = blinkDest;
                        rb.linearVelocity = Vector2.zero;
                        FacePlayer();
                        WarnLine(from, Center, 0.18f, new Color(0.6f, 1f, 0.7f, 0.9f), 0.6f);
                        ArenaFX.Burst(Center, Jade, 14, 6f, 0.1f, 0.3f, 0.5f);
                        mode = Mode.BlinkStrike;
                        timer = 0.12f;
                        struck = false;
                        anim.Play("raise");
                    }
                    break;

                case Mode.BlinkStrike:
                    vx = 0f;
                    if (!struck && timer <= 0f)
                    {
                        struck = true;
                        anim.Play("slashDown", true);
                        PlaySlash(Center + new Vector2(facing * 1.4f, 0.3f), -25f, 1.6f);
                        HitPlayerInBox(Feet + new Vector2(facing * 1.6f, 1.3f), new Vector2(3.2f, 2.6f), Mathf.RoundToInt(damage * 1.1f), 10f);
                        ArenaFX.Shake(0.3f);
                        timer = 0.4f;
                    }
                    else if (struck && timer <= 0f) AfterMove(0.3f);
                    break;

                case Mode.LeapCrouch:
                    vx = 0f;
                    if (Random.value < 0.4f) ArenaFX.Burst(Center + new Vector2(-facing * 0.6f, 0.8f), new Color(0.75f, 1f, 0.8f), 1, 2f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.LeapRise;
                        rb.bodyType = RigidbodyType2D.Kinematic;
                        rb.linearVelocity = Vector2.zero;
                        var bounds = Bounds;
                        float want = Mathf.Max((PlayerAlive ? player.transform.position.y : transform.position.y) + 5f, transform.position.y + 4f);
                        riseTarget = Mathf.Min(want, bounds.yMax - 3.4f);
                        timer = 0.7f;
                        anim.Play("fly");
                        ArenaFX.Burst(transform.position, Dust, 18, 6f, 0.13f, 0.5f, 1f);
                        ArenaFX.Shake(0.2f);
                    }
                    break;

                case Mode.LeapRise:
                {
                    float toY = riseTarget - transform.position.y;
                    float tx = PlayerAlive ? player.transform.position.x : transform.position.x;
                    flyVelocity = new Vector2(Mathf.Clamp((tx - transform.position.x) * 3f, -9f, 9f), Mathf.Clamp(toY * 8f, -4f, 22f));
                    ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.25f), 0.15f);
                    if (toY < 0.15f || timer <= 0f)
                    {
                        mode = Mode.LeapHover;
                        timer = Tell(hoverTime);
                        BeginTell(timer);
                    }
                    break;
                }

                case Mode.LeapHover:
                {
                    FacePlayer();
                    float tx = PlayerAlive ? player.transform.position.x : transform.position.x;
                    float bob = Mathf.Sin(Time.time * 18f) * 1.2f;
                    flyVelocity = new Vector2(Mathf.Clamp((tx - transform.position.x) * 6f, -14f, 14f), bob);
                    landY = LandingY();
                    if (Random.value < 0.5f)
                        ArenaFX.Burst(new Vector2(transform.position.x, landY + 0.05f), new Color(1f, 0.35f, 0.3f), 1, 2.5f, 0.09f, 0.25f, 0.5f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.LeapSlam;
                        landY = LandingY();
                        flyVelocity = new Vector2(0f, -slamSpeed);
                        anim.Play("slam", true);
                    }
                    break;
                }

                case Mode.LeapSlam:
                    ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.35f), 0.15f);
                    if (transform.position.y <= landY + 0.05f)
                    {
                        transform.position = new Vector3(transform.position.x, landY, 0f);
                        rb.position = transform.position;
                        Land();
                        Impact();
                    }
                    break;

                case Mode.Stuck:
                    vx = 0f;
                    if (Random.value < 0.12f)
                        ArenaFX.Spray((Vector2)transform.position + new Vector2(facing * 0.9f, 0.1f), Vector2.up, Dust, 1, 3f, 0.6f, 0.1f);
                    if (timer <= 0f)
                    {
                        ArenaFX.Burst((Vector2)transform.position + new Vector2(facing * 0.9f, 0.2f), Dust, 14, 5f, 0.12f, 0.5f, 2f);
                        // tears its scythes free with a sweep when enraged
                        if (enraged && PlayerAlive && Mathf.Abs(ToPlayer.x) < 3.2f)
                        {
                            FacePlayer();
                            anim.Play("slashUp", true);
                            PlaySlash(Center + new Vector2(facing * 1.3f, 0.2f), 35f, 1.5f);
                            HitPlayerInBox(Feet + new Vector2(facing * 1.5f, 1.2f), new Vector2(3.4f, 2.6f), damage, 9f);
                        }
                        else anim.Play("raise");
                        mode = Mode.Recover;
                        timer = 0.35f;
                    }
                    break;

                case Mode.DanceIn:
                    flyVelocity = Vector2.MoveTowards(flyVelocity, Vector2.up * 20f, 120f * dt);
                    ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.4f), 0.2f);
                    if (timer <= 0f)
                    {
                        flyVelocity = Vector2.zero;
                        body.enabled = false;
                        mode = Mode.Dance;
                        danceT = 0f;
                    }
                    break;

                case Mode.Dance:
                    UpdateDance(dt);
                    break;

                case Mode.DanceFinish:
                    vx = 0f;
                    if (!struck && timer <= 0f)
                    {
                        struck = true;
                        ExecuteCut(finishA, true);
                        ExecuteCut(finishB, true);
                        transform.position = finishSpot; // the cuts streaked its afterimages around; it stands where it reappeared
                        rb.position = finishSpot;
                        FacePlayer();
                        ImpactFrame.Play(player != null ? player.Center : Center, ImpactFrame.Style.Jade);
                        ArenaFX.HitStop(0.12f);
                        ArenaFX.Shake(0.7f);
                        timer = 0.8f; // its big opening
                        anim.Play("slashDown", true);
                    }
                    else if (struck && timer <= 0f) { mode = Mode.Recover; timer = 0.3f; anim.Play("idle"); }
                    break;

                case Mode.Dodge:
                    vx = Mathf.MoveTowards(vx, 0f, 40f * dt);
                    ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.4f), 0.2f);
                    if (timer <= 0f)
                    {
                        // punishes whoever just swung at it
                        if (PlayerAlive && Random.value < punishAfterDodge * (enraged ? 1.3f : 1f)) { chain = 1; FollowUp(); break; }
                        mode = Mode.Stalk;
                        timer = Random.Range(0.2f, 0.5f);
                    }
                    break;

                case Mode.Guard:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        mode = Mode.CounterTell;
                        timer = Tell(0.3f);
                        BeginTell(timer);
                        anim.Play("raise");
                    }
                    break;

                case Mode.CounterTell:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Counter;
                        timer = 0.5f;
                        hitThisMove = false;
                        anim.Play("slashDown", true);
                        vx = facing * 15f; // lunges after the knight it just knocked back
                        PlaySlash(Center + new Vector2(facing * 1.5f, 0.2f), -20f, 1.7f);
                        ArenaFX.Shake(0.3f);
                    }
                    break;

                case Mode.Counter:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (!hitThisMove && timer > 0.28f)
                    {
                        ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.3f), 0.15f);
                        if (HitPlayerInBox(Center + new Vector2(facing * 1.8f, 0f), new Vector2(3.4f, 2.8f), Mathf.RoundToInt(damage * 1.25f), 12f) != HitResult.Ignored)
                            hitThisMove = true;
                    }
                    if (timer <= 0f) { anim.Play("idle"); AfterMove(0.4f); }
                    break;

                case Mode.ExecTell: UpdateExecTell(dt); break;
                case Mode.ExecDash: UpdateExecDash(); break;

                case Mode.ExecWhiff:
                    vx = Mathf.MoveTowards(vx, 0f, 25f * dt);
                    if (Random.value < 0.3f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.3f), Dust, 1, 3f, 0.4f, 0.1f);
                    if (timer <= 0f) { Spotlight(false); mode = Mode.Recover; timer = 0.2f; anim.Play("idle"); }
                    break;

                case Mode.ExecCuts: UpdateExecCuts(); break;

                case Mode.ExecPause:
                    // back turned behind the knight, scythes lowered... then it all lands
                    if (PlayerAlive && player.IsHeldBy(this)) player.HoldAt(execHold);
                    else { EndExecution(); break; }
                    if (timer <= 0f) ExecFinish();
                    break;

                case Mode.ExecFinish:
                    vx = 0f;
                    if (timer <= 0f) { Spotlight(false); mode = Mode.Recover; timer = 0.2f; anim.Play("idle"); }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f)
                    {
                        mode = Mode.Stalk;
                        timer = Random.Range(0.25f, 0.6f) * (enraged ? 0.6f : 1f);
                    }
                    break;
            }
        }

        // ---------- choosing ----------

        void ChooseAttack(float dist, bool sameLevel)
        {
            Mode next;
            float roll = Random.value;
            bool canDance = danceTimer <= 0f && lineSprite != null;
            bool canExec = execTimer <= 0f && sameLevel && dist < 11f;
            if (simple)
            {
                // Triple Slash, Dash Slash and Crescent Wave (jump them), Leap Slam (its scythes get stuck);
                // no Blink Strike, Reaping Dance or Moonlit Execution
                if (!sameLevel) next = Mode.LeapCrouch;
                else if (dist < 4.2f) next = roll < 0.5f ? Mode.TripleTell : roll < 0.75f ? Mode.WaveTell : Mode.LeapCrouch;
                else next = roll < 0.4f ? Mode.DashTell : roll < 0.75f ? Mode.WaveTell : Mode.LeapCrouch;
                if (next == lastAttack) next = next == Mode.LeapCrouch ? Mode.WaveTell : Mode.LeapCrouch;
            }
            else if (canExec && roll < 0.4f) next = Mode.ExecTell;
            else if (canDance && roll < 0.65f) next = Mode.DanceIn;
            else
            {
                roll = Random.value;
                if (!sameLevel) next = roll < 0.5f ? Mode.LeapCrouch : Mode.BlinkTell;
                else if (dist < 4.2f) next = roll < 0.4f ? Mode.TripleTell : roll < 0.6f ? Mode.BlinkTell : roll < 0.8f ? Mode.WaveTell : Mode.LeapCrouch;
                else if (dist < 13f) next = roll < 0.35f ? Mode.DashTell : roll < 0.65f ? Mode.WaveTell : roll < 0.85f ? Mode.BlinkTell : Mode.LeapCrouch;
                else next = roll < 0.5f ? Mode.LeapCrouch : Mode.WaveTell;
                if (next == lastAttack) next = dist < 4.2f ? Mode.TripleTell : Mode.WaveTell;
                if (next == lastAttack) next = Mode.LeapCrouch;
            }
            Begin(next, 1f);
        }

        /// <summary>Starts a move. tellScale shortens the wind-up when it flows out of another move.</summary>
        void Begin(Mode next, float tellScale)
        {
            lastAttack = next;
            vx = 0f;
            switch (next)
            {
                case Mode.TripleTell:
                    mode = next;
                    timer = Tell(tripleTell) * tellScale * (Random.value < 0.35f ? Random.Range(1.3f, 1.7f) : 1f); // sometimes a delayed swing
                    anim.Play("raise");
                    BeginTell(timer);
                    break;
                case Mode.DashTell:
                    mode = next;
                    timer = Tell(dashTell) * tellScale;
                    anim.Play("crouch");
                    BeginTell(timer);
                    break;
                case Mode.WaveTell:
                    mode = next;
                    timer = Tell(waveTell) * tellScale;
                    anim.Play("raise");
                    BeginTell(timer);
                    break;
                case Mode.BlinkTell:
                    StartBlink(tellScale);
                    break;
                case Mode.DanceIn:
                    StartDance();
                    break;
                case Mode.ExecTell:
                    StartExecution();
                    break;
                default:
                    StartLeap(tellScale);
                    break;
            }
        }

        /// <summary>A move just ended: often flows straight into another one, otherwise a short recovery.</summary>
        void AfterMove(float recover)
        {
            int maxChain = enraged ? 3 : 1;
            float chance = enraged ? enragedChainChance : chainChance;
            if (PlayerAlive && chain < maxChain && Random.value < chance)
            {
                chain++;
                FollowUp();
                return;
            }
            mode = Mode.Recover;
            timer = recover * (enraged ? 0.7f : 1f);
        }

        void FollowUp()
        {
            FacePlayer();
            float dist = Mathf.Abs(player.transform.position.x - transform.position.x);
            bool sameLevel = Mathf.Abs(player.transform.position.y - transform.position.y) < 1.6f;
            float roll = Random.value;
            Mode next;
            if (enraged && execTimer <= 0f && sameLevel && roll < 0.25f) next = Mode.ExecTell;
            else if (!sameLevel) next = roll < 0.6f ? Mode.BlinkTell : Mode.LeapCrouch;
            else if (dist < 3.5f) next = roll < 0.4f ? Mode.BlinkTell : roll < 0.75f ? Mode.TripleTell : Mode.WaveTell;
            else next = roll < 0.45f ? Mode.DashTell : roll < 0.8f ? Mode.BlinkTell : Mode.LeapCrouch;
            if (next == lastAttack) next = next == Mode.BlinkTell ? Mode.DashTell : Mode.BlinkTell;
            // flowing out of the last move: a quicker wind-up, with a flash so it still reads
            flash.Flash(new Color(0.7f, 1f, 0.75f), 0.12f);
            ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.5f), 0.25f);
            Begin(next, 0.7f);
        }

        void Hop()
        {
            hopTimer = 2.2f;
            mode = Mode.Hop;
            timer = 0.3f;
            int away = -facing;
            if (Physics2D.Raycast(Center, new Vector2(away, 0f), 2.4f, ArenaLayers.SolidMask)) away = facing; // cornered: hop over instead
            vx = away * hopSpeed;
            rb.linearVelocity = new Vector2(vx, away == -facing ? 6f : 12f);
            anim.Play("dodge", true);
            ArenaFX.Spray(transform.position, new Vector2(-away, 0.4f), Dust, 6, 4f, 0.4f, 0.12f);
        }

        void StartLeap(float tellScale = 1f)
        {
            mode = Mode.LeapCrouch;
            lastAttack = Mode.LeapCrouch;
            timer = Tell(leapTell) * tellScale;
            vx = 0f;
            anim.Play("crouch");
            BeginTell(timer);
        }

        // ---------- moves ----------

        void Strike(int index)
        {
            FacePlayer();
            bool rising = index == 3;
            bool down = index % 2 == 0;
            anim.Play(down ? "slashDown" : "slashUp", true);
            vx = facing * (rising ? 9f : 7f);
            float angle = index == 0 ? -28f : index == 1 ? 32f : index == 2 ? -8f : 60f;
            float size = rising ? 1.75f : index == 2 ? 1.45f : 1.15f;
            PlaySlash(Center + new Vector2(facing * 1.3f, rising ? 0.6f : 0.3f), angle, size);
            var hit = (Vector2)transform.position + new Vector2(facing * slashOffset.x, slashOffset.y + (rising ? 0.5f : 0f));
            var box = rising ? new Vector2(slashBox.x * 1.1f, slashBox.y * 1.4f) : index == 2 ? slashBox * 1.15f : slashBox;
            int amount = rising ? Mathf.RoundToInt(damage * 1.3f) : index == 2 ? Mathf.RoundToInt(damage * 1.2f) : damage;
            HitPlayerInBox(hit, box, amount, rising ? 13f : index == 2 ? 10f : 6f);
            ArenaFX.Spray(hit, new Vector2(facing, rising ? 1f : 0.2f), Blade, rising ? 8 : 4, 6f, 0.4f, 0.07f);
            ArenaFX.Shake(rising ? 0.35f : index == 2 ? 0.25f : 0.12f);
        }

        /// <summary>A blade of wind thrown along the floor (height above the feet).</summary>
        void ThrowWave(float height)
        {
            var from = Feet + new Vector2(facing * 1.4f, height);
            Shoot(slashSprite, from, new Vector2(facing * waveSpeed, 0f), Mathf.RoundToInt(damage * 0.8f), 0f,
                new Color(0.75f, 1f, 0.8f, 1f), new Color(0.45f, 1f, 0.6f, 0.45f), 1.3f, 0.55f, true, 0f, 3f);
            ArenaFX.Burst(from, Blade, 8, 5f, 0.08f, 0.25f, 0f);
            ArenaFX.Shake(0.15f);
        }

        void StartBlink(float tellScale = 1f)
        {
            if (!PlayerAlive) { StartLeap(); return; }
            // behind the knight (or in front if that's a wall)
            float px = player.transform.position.x;
            var dest = new Vector2(px - player.Facing * 1.8f, player.transform.position.y + 0.5f);
            if (Physics2D.OverlapBox(dest + Vector2.up * 1f, new Vector2(1.2f, 1.8f), 0f, ArenaLayers.SolidMask))
                dest.x = px + player.Facing * 1.8f;
            var b = Bounds;
            dest.x = Mathf.Clamp(dest.x, b.xMin + 0.5f, b.xMax - 0.5f);
            blinkDest = GroundBelow(dest);
            mode = Mode.BlinkTell;
            lastAttack = Mode.BlinkTell;
            timer = Tell(blinkTell) * tellScale;
            vx = 0f;
            anim.Play("crouch");
            BeginTell(timer);
            WarnSpot(blinkDest, 0.9f, timer + 0.1f, new Color(0.5f, 1f, 0.6f, 0.9f));
        }

        void PlaySlash(Vector2 at, float angle, float size)
        {
            if (slashSprite == null) { ArenaFX.Slash(at, facing, size * 2f, angle, Blade); return; }
            ArenaFX.PlaySprite(slashSprite, at, Color.white, 0.16f, true, new Vector3(size * 0.85f, size * 0.85f, 1f), new Vector3(size * 1.15f, size * 1.1f, 1f),
                angle * facing, 0f, facing < 0, 27);
        }

        /// <summary>The floor the knight is standing on, under the mantis.</summary>
        float LandingY()
        {
            float fromY = PlayerAlive ? player.transform.position.y + 0.6f : transform.position.y;
            var origin = new Vector2(transform.position.x, Mathf.Min(fromY, transform.position.y));
            var hit = Physics2D.Raycast(origin, Vector2.down, 30f, ArenaLayers.GroundMask);
            if (hit && hit.distance > 0.01f) return hit.point.y;
            hit = Physics2D.Raycast(new Vector2(transform.position.x, transform.position.y), Vector2.down, 30f, ArenaLayers.GroundMask);
            return hit ? hit.point.y : transform.position.y - 5f;
        }

        void Land()
        {
            flyVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
        }

        void Impact()
        {
            mode = Mode.Stuck;
            timer = stuckTime;
            vx = 0f;
            anim.Play("slam", true);
            var feet = Feet;
            ArenaFX.Shake(0.8f);
            ArenaFX.HitStop(0.08f);
            ArenaFX.Burst(feet + new Vector2(facing * 0.9f, 0.2f), Dust, 30, 9f, 0.15f, 0.7f, 3f);
            ArenaFX.FlatRing(feet + Vector2.up * 0.1f, new Color(0.6f, 1f, 0.7f, 0.8f), 0.5f, 6f, 0.3f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.8f, "STUCK!", Jade, 0.85f);
            HitPlayerInBox(feet + new Vector2(facing * 1.1f, 1f), new Vector2(3.8f, 2.2f), Mathf.RoundToInt(damage * 1.3f), 12f);
            Shockwaves(feet, shockSpeed * Pace, shockRange, Jade, Mathf.RoundToInt(damage * 0.7f));
        }

        // ---------- Moonlit Execution ----------

        void StartExecution()
        {
            execTimer = execCooldown * (enraged ? 0.7f : 1f);
            mode = Mode.ExecTell;
            lastAttack = Mode.ExecTell;
            timer = Tell(execTell);
            vx = 0f;
            fxTimer = 0f;
            FacePlayer();
            anim.Play("guard", true); // scythes crossed
            BeginTell(timer);
            flash.Hold(new Color(1f, 0.3f, 0.3f), 0.35f, 26f);
            // the world goes dark and a huge moon rises behind it; a red flash says this one can't be blocked
            Spotlight(true);
            var moon = Center + new Vector2(-facing * 0.5f, 1.2f);
            ArenaFX.Darken(0.75f, timer + 0.6f);
            if (moonSprite != null)
                ArenaFX.PlaySprite(moonSprite, moon, new Color(1f, 1f, 1f, 0.95f), timer + 0.25f, false, new Vector3(1.6f, 1.6f, 1f), new Vector3(2.1f, 2.1f, 1f), 0f, 0f, facing < 0, 16);
            ArenaFX.Glow(moon, new Color(Moon.r, Moon.g, Moon.b, 0.5f), 9f, timer + 0.2f);
            ArenaFX.Ring(moon, new Color(0.85f, 1f, 0.9f, 0.85f), 7f, 3.2f, timer);
            ArenaFX.Ring(Center, Danger, 0.4f, 4.5f, 0.3f);
            ArenaFX.DamageNumber(Center + Vector2.up * 2.4f, "MOONLIT EXECUTION", new Color(1f, 0.35f, 0.35f), 1f);
            ArenaFX.Shake(0.3f);
        }

        int baseOrder = -1;

        /// <summary>During the execution it's drawn above the darkness (in front of its moon).</summary>
        void Spotlight(bool on)
        {
            if (body == null) return;
            if (baseOrder < 0) baseOrder = body.sortingOrder;
            body.sortingOrder = on ? 17 : baseOrder;
        }

        void UpdateExecTell(float dt)
        {
            vx = 0f;
            FacePlayer();
            // moonlight pouring into the scythes
            if ((fxTimer -= dt) <= 0f)
            {
                fxTimer = 0.03f;
                var tip = Center + new Vector2(facing * 0.6f, 0.9f);
                var from = tip + Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.6f);
                ArenaFX.Spray(from, tip - from, Random.value < 0.5f ? Color.white : Moon, 1, 8f, 0.05f, 0.08f);
            }
            if (timer > 0f) return;
            EndTell();
            mode = Mode.ExecDash;
            timer = 0.34f;
            hitThisMove = false;
            execFrom = Center;
            anim.Play("slashDown", true);
            vx = facing * execSpeed;
            PlaySlash(Center + new Vector2(facing * 1.2f, 0.2f), -5f, 1.9f);
            ArenaFX.Burst(transform.position, Dust, 16, 7f, 0.12f, 0.4f, 1f);
            ArenaFX.Shake(0.35f);
        }

        void UpdateExecDash()
        {
            vx = facing * execSpeed;
            ArenaFX.Afterimage(body, new Color(0.7f, 1f, 0.78f, 0.5f), 0.25f);
            if (!hitThisMove && PlayerAlive &&
                Physics2D.OverlapBox(Center + new Vector2(facing * 0.8f, -0.1f), new Vector2(2.2f, 2.1f), 0f, ArenaLayers.PlayerMask))
            {
                if (player.TryParryGrab(Center)) { ExecParried(); return; }
                // rolling through it (i-frames) just lets it pass
                if (player.TryGrab(this, Center, 0)) { hitThisMove = true; BeginExecCuts(); return; }
            }
            if (timer <= 0f || WallAhead(1.2f))
            {
                // whiffed: it skids to a stop, wide open
                WarnLine(execFrom, Center, 0.3f, new Color(0.7f, 1f, 0.78f, 0.9f), 0.5f);
                mode = Mode.ExecWhiff;
                timer = execWhiff;
                vx = facing * 7f;
                anim.Play("slam", true);
                ArenaFX.Burst(transform.position, Dust, 14, 6f, 0.12f, 0.45f, 1f);
            }
        }

        void ExecParried()
        {
            WarnLine(execFrom, Center, 0.25f, new Color(1f, 0.95f, 0.6f, 0.9f), 0.5f);
            vx = -facing * 6f;
            var spark = Center + new Vector2(facing * 0.9f, 0.3f);
            ImpactFrame.Play(spark, ImpactFrame.Style.Steel);
            ArenaFX.HitStop(0.15f);
            ArenaFX.Shake(0.6f);
            ArenaFX.Burst(spark, new Color(1f, 0.95f, 0.6f), 24, 9f, 0.1f, 0.4f, 1f);
            ArenaFX.DamageNumber(Center + Vector2.up * 2f, "DEFLECTED!", new Color(1f, 0.9f, 0.45f), 1.1f);
            Spotlight(false);
            mode = Mode.Recover;
            timer = 0.3f;
            Break(2.6f); // staggered, open to a critical
        }

        void BeginExecCuts()
        {
            mode = Mode.ExecCuts;
            execCut = 0;
            timer = 0.16f;
            vx = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            flyVelocity = Vector2.zero;
            execHold = (Vector2)player.transform.position + Vector2.up * 0.35f; // lifted off his feet
            player.HoldAt(execHold);
            WarnLine(execFrom, Center, 0.25f, new Color(0.7f, 1f, 0.78f, 0.9f), 0.6f);
            ArenaFX.Darken(0.88f, 2.4f);
            ImpactFrame.Play(player.Center, ImpactFrame.Style.Jade);
            ArenaFX.HitStop(0.1f);
            ArenaFX.Shake(0.5f);
            ArenaFX.Burst(player.Center, Moon, 14, 7f, 0.1f, 0.3f, 0.5f);
        }

        void UpdateExecCuts()
        {
            vx = 0f;
            flyVelocity = Vector2.zero;
            if (!PlayerAlive || !player.IsHeldBy(this)) { EndExecution(); return; }
            player.HoldAt(execHold);
            if (timer > 0f) return;
            if (execCut >= execCuts) { BeginExecPause(); return; }
            ExecSlash(execCut++);
            timer = 0.15f;
        }

        void ExecSlash(int index)
        {
            var target = player.Center;
            var from = Center;
            ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.55f), 0.3f);
            var offset = ExecSpots[index % ExecSpots.Length];
            if (Random.value < 0.5f) offset.x = -offset.x;
            var spot = target + offset;
            transform.position = spot + Vector2.down * centerHeight;
            rb.position = transform.position;
            facing = target.x >= spot.x ? 1 : -1;
            anim.Play(index % 2 == 0 ? "slashDown" : "slashUp", true);
            WarnLine(from, Center, 0.14f, new Color(0.7f, 1f, 0.78f, 0.8f), 0.45f);

            // a cut straight through the knight
            float angle = Random.Range(-60f, 60f);
            if (lineSprite != null)
            {
                ArenaFX.PlaySprite(lineSprite, target, Color.white, 0.18f, true, new Vector3(0.75f, 2.2f, 1f), new Vector3(0.85f, 0.1f, 1f), angle, 0f, false, 34);
                ArenaFX.PlaySprite(lineSprite, target, new Color(0.5f, 1f, 0.6f, 0.8f), 0.26f, true, new Vector3(0.75f, 3.6f, 1f), new Vector3(0.8f, 0.2f, 1f), angle, 0f, false, 33);
            }
            PlaySlash(target, angle * 0.5f, 1.4f);
            float a = angle * Mathf.Deg2Rad;
            var normal = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            ArenaFX.Spray(target, normal, new Color(0.85f, 0.1f, 0.12f), 5, 7f, 0.4f, 0.1f);
            ArenaFX.Spray(target, -normal, new Color(0.85f, 0.1f, 0.12f), 5, 7f, 0.4f, 0.1f);
            player.TakePoison(Mathf.Max(1, Mathf.RoundToInt(damage * execCutDamage)));
            ArenaFX.Shake(0.25f);
            if (index == execCuts / 2)
            {
                ImpactFrame.Play(target, ImpactFrame.Style.Jade);
                ArenaFX.HitStop(0.07f);
            }
            else ArenaFX.HitStop(0.035f);
        }

        void BeginExecPause()
        {
            // reappears behind him on the floor, back turned, scythes lowered
            var target = player.Center;
            int side = Random.value < 0.5f ? -1 : 1;
            var ground = GroundBelow(new Vector2(target.x + side * 2.6f, target.y));
            if (Physics2D.OverlapBox(ground + Vector2.up * 1.2f, new Vector2(1.2f, 2f), 0f, ArenaLayers.SolidMask))
                ground = GroundBelow(new Vector2(target.x - side * 2.6f, target.y));
            ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.6f), 0.35f);
            transform.position = ground;
            rb.position = ground;
            Land();
            facing = ground.x > target.x ? 1 : -1; // facing away
            anim.Play("slam", true);
            mode = Mode.ExecPause;
            timer = 0.5f;
            // the final X hangs over him
            float r = 3f;
            WarnLine(target + new Vector2(-r, -r * 0.8f), target + new Vector2(r, r * 0.8f), timer + 0.05f, Danger, 0.8f);
            WarnLine(target + new Vector2(-r, r * 0.8f), target + new Vector2(r, -r * 0.8f), timer + 0.05f, Danger, 0.8f);
            ArenaFX.Burst(Center + new Vector2(-facing * 0.8f, 0.4f), Color.white, 5, 2f, 0.06f, 0.2f, 0f);
        }

        void ExecFinish()
        {
            mode = Mode.ExecFinish;
            timer = 0.9f; // its opening
            var target = player.Center;
            ImpactFrame.Play(target, ImpactFrame.Style.Jade);
            ArenaFX.HitStop(0.16f);
            ArenaFX.After(0.12f, () => ArenaFX.ScreenFlash(new Color(0.85f, 1f, 0.9f, 0.45f), 0.18f));
            ArenaFX.Shake(0.9f);
            if (lineSprite != null)
                foreach (var angle in new[] { 38f, -38f })
                {
                    ArenaFX.PlaySprite(lineSprite, target, Color.white, 0.3f, true, new Vector3(1.4f, 3.2f, 1f), new Vector3(1.6f, 0.1f, 1f), angle, 0f, false, 34);
                    ArenaFX.PlaySprite(lineSprite, target, new Color(0.5f, 1f, 0.6f, 0.85f), 0.4f, true, new Vector3(1.4f, 5f, 1f), new Vector3(1.5f, 0.2f, 1f), angle, 0f, false, 33);
                }
            ArenaFX.Burst(target, new Color(0.85f, 0.1f, 0.12f), 22, 9f, 0.12f, 0.5f, 1f);
            player.Release(Vector2.zero);
            player.TakeHit(Mathf.RoundToInt(damage * execFinishDamage), Center, 14f);
            flash.ClearHold();
        }

        /// <summary>The knight got loose mid-execution (or died): back to the floor.</summary>
        void EndExecution()
        {
            if (Flying)
            {
                var ground = GroundBelow((Vector2)transform.position + Vector2.up * 0.5f);
                transform.position = ground;
                rb.position = ground;
                Land();
            }
            flash.ClearHold();
            Spotlight(false);
            mode = Mode.Recover;
            timer = 0.5f;
            anim.Play("idle");
        }

        // ---------- Reaping Dance ----------

        void StartDance()
        {
            danceTimer = danceCooldown * (enraged ? 0.6f : 1f);
            mode = Mode.DanceIn;
            timer = 0.3f;
            vx = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            anim.Play("fly");
            flash.Flash(Color.white, 0.2f);
            ArenaFX.Burst(Center, Jade, 18, 7f, 0.12f, 0.4f, 1f);
            ArenaFX.DamageNumber(Center + Vector2.up * 2f, "REAPING DANCE", Danger, 0.9f);
            int count = enraged ? danceCuts + 2 + (Random.value < 0.5f ? 1 : 0) : danceCuts;
            float gap = enraged ? cutGap * 0.82f : cutGap;
            ArenaFX.Darken(0.6f, 0.3f + count * gap + cutWarn + 1.4f);
            cuts.Clear();
            for (int i = 0; i < count; i++)
                cuts.Add(new DanceCut { warnAt = 0.15f + i * gap, strikeAt = 0.15f + i * gap + cutWarn });
        }

        void UpdateDance(float dt)
        {
            danceT += dt;
            flyVelocity = Vector2.zero;
            bool allDone = true;
            foreach (var cut in cuts)
            {
                if (!cut.warned && danceT >= cut.warnAt)
                {
                    // a line straight through where the knight is right now
                    cut.warned = true;
                    var through = PlayerAlive ? player.Center : Center;
                    float angle = (Random.value < 0.5f ? Random.Range(-25f, 25f) : Random.Range(35f, 65f) * (Random.value < 0.5f ? 1f : -1f)) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    if (Random.value < 0.5f) dir = -dir;
                    cut.from = through - dir * 5f;
                    cut.to = through + dir * 5f;
                    WarnLine(cut.from, cut.to, cutWarn + 0.05f, Danger, 0.7f);
                }
                if (cut.warned && !cut.done && danceT >= cut.strikeAt) ExecuteCut(cut, false);
                if (!cut.done) allDone = false;
            }
            if (allDone && danceT >= cuts[cuts.Count - 1].strikeAt + 0.3f) FinishDance();
        }

        /// <summary>Flashes along a cut line and hurts the knight if he's still on it.</summary>
        void ExecuteCut(DanceCut cut, bool heavy)
        {
            cut.done = true;
            // afterimages streaking along the line
            for (int i = 0; i <= 5; i++)
            {
                transform.position = Vector2.Lerp(cut.from, cut.to, i / 5f) + Vector2.down * centerHeight;
                ArenaFX.Afterimage(body, new Color(0.6f, 1f, 0.7f, 0.45f), 0.25f);
            }
            var d = cut.to - cut.from;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var mid = (cut.from + cut.to) * 0.5f;
            if (lineSprite != null)
            {
                ArenaFX.PlaySprite(lineSprite, mid, Color.white, 0.22f, true, new Vector3(d.magnitude / 5f, heavy ? 3f : 2f, 1f), new Vector3(d.magnitude / 5f * 1.1f, 0.1f, 1f), angle, 0f, false, 34);
                ArenaFX.PlaySprite(lineSprite, mid, new Color(0.5f, 1f, 0.6f, 0.8f), 0.3f, true, new Vector3(d.magnitude / 5f, heavy ? 5f : 3.5f, 1f), new Vector3(d.magnitude / 5f, 0.2f, 1f), angle, 0f, false, 33);
            }
            ArenaFX.Shake(heavy ? 0.4f : 0.22f);

            if (!PlayerAlive) return;
            // closest point on the cut to the knight
            var p = player.Center;
            float t = Mathf.Clamp01(Vector2.Dot(p - cut.from, d) / Mathf.Max(0.01f, d.sqrMagnitude));
            var closest = cut.from + d * t;
            if (Vector2.Distance(closest, p) < 0.75f)
                HitPlayerInBox(closest, new Vector2(1.4f, 1.4f), Mathf.RoundToInt(damage * (heavy ? 1.4f : 0.9f)), heavy ? 12f : 7f);
        }

        void FinishDance()
        {
            // it reappears beside the knight for a final X cut
            body.enabled = true;
            Land();
            var b = Bounds;
            float px = PlayerAlive ? player.transform.position.x : transform.position.x;
            float x = px + (px > b.center.x ? -3f : 3f);
            finishSpot = GroundBelow(new Vector2(x, (PlayerAlive ? player.transform.position.y : transform.position.y) + 1f));
            transform.position = finishSpot;
            rb.position = finishSpot;
            FacePlayer();
            var through = PlayerAlive ? player.Center : Center;
            float a = 40f * Mathf.Deg2Rad;
            var d1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var d2 = new Vector2(Mathf.Cos(-a), Mathf.Sin(-a));
            finishA = new DanceCut { from = through - d1 * 3.5f, to = through + d1 * 3.5f };
            finishB = new DanceCut { from = through - d2 * 3.5f, to = through + d2 * 3.5f };
            mode = Mode.DanceFinish;
            timer = Tell(0.5f);
            struck = false;
            WarnLine(finishA.from, finishA.to, timer + 0.05f, Danger, 0.9f);
            WarnLine(finishB.from, finishB.to, timer + 0.05f, Danger, 0.9f);
            anim.Play("raise");
            BeginTell(timer);
            ArenaFX.Burst(Center, Jade, 16, 6f, 0.1f, 0.3f, 0.5f);
        }

        // ---------- dodge & parry ----------

        // nowhere to be hit (or stunned) while it dances, or while it's cutting the knight apart
        protected override bool Untouchable => Vanished || Executing;

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            if (Untouchable) return;
            if (TryEvade(from)) return;
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
        }

        bool TryEvade(Vector2 from)
        {
            if (Unavoidable || IsDead || evadeTimer > 0f || stunTimer > 0f || Flying) return false;
            if (mode != Mode.Stalk && mode != Mode.Recover && mode != Mode.Intro && mode != Mode.Hop) return false;
            if (!OnGround()) return false;

            float scale = EvadeScale * (enraged ? 1.2f : 1f);
            // a parry needs the knight's own blade: close by, and the hit coming from the knight
            bool melee = PlayerAlive && Vector2.Distance(player.Center, Center) < 3.8f &&
                         Vector2.Distance(from, player.transform.position) < 1.6f;
            if (melee && Random.value < parryChance * scale) { Parry(); return true; }
            if (Random.value < dodgeChance * scale) { Dodge(from); return true; }
            return false;
        }

        void Dodge(Vector2 from)
        {
            evadeTimer = evadeCooldown;
            int away = Center.x >= from.x ? 1 : -1;
            if (Physics2D.Raycast(Center, new Vector2(away, 0f), 2.6f, ArenaLayers.SolidMask)) away = -away;
            mode = Mode.Dodge;
            timer = 0.3f;
            vx = away * 13f;
            rb.linearVelocity = new Vector2(vx, 6f);
            anim.Play("dodge", true);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.6f, "DODGE", new Color(0.7f, 1f, 0.75f), 0.95f);
            ArenaFX.Spray(transform.position, new Vector2(-away, 0.4f), Dust, 6, 4f, 0.4f, 0.12f);
        }

        void Parry()
        {
            evadeTimer = evadeCooldown;
            FacePlayer();
            mode = Mode.Guard;
            timer = 0.25f;
            vx = 0f;
            anim.Play("guard", true);
            var spark = Center + new Vector2(facing * 0.9f, 0.5f);
            flash.Flash(Color.white, 0.15f);
            ArenaFX.Ring(spark, new Color(0.8f, 1f, 0.7f, 0.95f), 0.4f, 3.2f, 0.25f);
            ArenaFX.Burst(spark, new Color(1f, 0.95f, 0.6f), 18, 8f, 0.1f, 0.4f, 1f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.7f, "PARRIED!", new Color(1f, 0.9f, 0.45f), 1.05f);
            ArenaFX.HitStop(0.1f);
            ArenaFX.Shake(0.3f);
            if (PlayerAlive) player.Recoil(Center, 0.3f);
        }

        // ---------- reactions ----------

        protected override void OnEnrage()
        {
            base.OnEnrage();
            ArenaFX.Ring(Center, new Color(0.5f, 1f, 0.6f, 0.9f), 1f, 9f, 0.5f);
            PlaySlash(Center + Vector2.up * 0.5f, 0f, 2f);
            danceTimer = Mathf.Min(danceTimer, 1.5f); // the dance comes soon after
            execTimer = Mathf.Min(execTimer, 5f);
        }

        protected override void OnStagger()
        {
            EndTell();
            if (Vanished) return;
            Spotlight(false);
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            if (Flying)
            {
                // knocked out of the air
                Land();
                anim.Play("dodge");
            }
            body.enabled = true;
            if (stunTimer > 0f) mode = Mode.Recover;
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.3f;
            anim.Play("idle");
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            if (Flying) { rb.linearVelocity = flyVelocity * SpeedScale; return; }
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
