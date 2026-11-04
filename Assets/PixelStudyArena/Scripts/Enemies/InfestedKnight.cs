using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Final boss, phase 1 (floor 50, the Throne Room): a knight like you, rotted through by the swarm.
    /// He fights with your own moves, corrupted violet:
    ///   Sword Combo   — your three slashes; sometimes he holds the third a beat to catch an early roll.
    ///   Heavy Stab    — draws back (dark light gathers on the blade) and lunges across the floor.
    ///   Flame Slash   — lowers the blade, it catches violet fire, and he swings it up: a burning arc
    ///                   pours fire across the floor.
    ///   Thousand Cuts — the world darkens, he vanishes and blinks around you, leaving cut marks in the
    ///                   air. Then he sheathes... and they all land. Get out of them, or roll on the beat.
    ///   Thunderclap   — a ring closes on the floor under you, then a black bolt and a shockwave.
    ///   Roll &amp; parry — like you, he rolls out of the way of your swings and sometimes parries them.
    /// When his health runs out the parasite inside tears out of the armor and the throne room crumbles:
    /// phase 2 (Parasite).
    /// </summary>
    public class InfestedKnight : BossBug
    {
        [Header("Moves")]
        public Sprite[] smearSprites;
        public Sprite thrustSprite;
        public Sprite starSprite;
        public Sprite cutSprite;
        public Sprite[] boltFrames;
        public FireArc arcPrefab;
        public BugEnemy parasitePrefab;
        public float runSpeed = 5.5f;
        public float preferredRange = 2.3f;

        [Header("Evade")]
        [Range(0f, 1f)] public float rollChance = 0.14f;
        [Range(0f, 1f)] public float parryChance = 0.09f;
        public float evadeCooldown = 2.8f;

        enum Mode
        {
            Intro, Stalk, Combo, Roll, Guard, Riposte, StabCharge, Stab, FlameLower, FlameRise,
            CutsDraw, CutsBlink, CutsHold, ClapCall, Recover, Transform,
        }
        Mode mode = Mode.Intro, lastAttack;
        float timer, evadeTimer, vx, rollTime, chargeFx;
        int swing, swingsLeft, blinks, rollDir;
        bool swingHit, swingShown, delayedThird, erupted, transforming;
        Vector2 markCenter, clapPoint;

        struct Swing
        {
            public string clip;
            public int activeFrom, activeTo;
            public Vector2 box, size, smearOffset;
            public int smear;
            public float smearScale, spin;
            public bool heavy;
        }

        static readonly Swing[] Combo =
        {
            new Swing { clip = "slash1", activeFrom = 2, activeTo = 3, box = new Vector2(1.3f, 1.3f), size = new Vector2(2.6f, 2.1f), smear = 0, smearOffset = new Vector2(0.2f, 1.3f), smearScale = 1.55f, spin = -140f },
            new Swing { clip = "slash2", activeFrom = 2, activeTo = 3, box = new Vector2(1.2f, 1.9f), size = new Vector2(2.6f, 3.3f), smear = 1, smearOffset = new Vector2(0.15f, 1.35f), smearScale = 1.55f, spin = 150f },
            new Swing { clip = "slash3", activeFrom = 4, activeTo = 5, box = new Vector2(1.7f, 1.3f), size = new Vector2(3.4f, 2.8f), smear = 2, smearOffset = new Vector2(0.25f, 1.4f), smearScale = 1.4f, spin = -160f, heavy = true },
        };

        static readonly Color Violet = new Color(0.72f, 0.35f, 1f);
        static readonly Color VioletHot = new Color(0.95f, 0.75f, 1f);
        static readonly Color Rot = new Color(0.45f, 0.1f, 0.35f);
        static readonly Color Dust = new Color(0.6f, 0.58f, 0.62f);

        float Pace => enraged ? 1.2f : 1f;
        float Scale => visual != null ? Mathf.Abs(visual.localScale.y) : 1f;
        bool Rolling => mode == Mode.Roll;
        protected override bool FinalForm => false;
        protected override bool Untouchable => transforming || Rolling || mode == Mode.CutsBlink;

        Vector2 Local(Vector2 p) => visual != null ? (Vector2)visual.TransformPoint(p) : (Vector2)transform.position + p;

        protected override void Start()
        {
            base.Start();
            timer = 1.6f;
            anim.Play("idle");
            ArenaFX.Shake(0.4f);
            ArenaFX.Ring(Center, new Color(0.7f, 0.35f, 1f, 0.9f), 1f, 8f, 0.5f);
            ArenaFX.Darken(0.4f, 1.4f);
        }

        protected override void Think(float dt)
        {
            if (transforming) return;
            timer -= dt;
            evadeTimer -= dt;
            TickWaves(dt);
            Corruption(dt);

            switch (mode)
            {
                case Mode.Intro:
                    vx = 0f;
                    FacePlayer();
                    if (timer < 0.8f && anim.Current != "heavyCharge") { anim.Play("heavyCharge"); ArenaFX.Burst(Center, Violet, 16, 5f, 0.1f, 0.5f, 1f); }
                    if (timer <= 0f) { mode = Mode.Stalk; timer = 0.5f; }
                    break;

                case Mode.Stalk:
                {
                    if (!PlayerAlive) { vx = 0f; anim.Play("idle"); break; }
                    FacePlayer();
                    float dist = Mathf.Abs(ToPlayer.x);
                    if (dist > preferredRange + 0.6f && !WallAhead(1f)) vx = facing * runSpeed * Pace;
                    else if (dist < preferredRange - 1f) vx = -facing * runSpeed * 0.4f;
                    else vx = 0f;
                    anim.Play(Mathf.Abs(vx) > 0.5f ? "run" : "idle");
                    if (timer <= 0f) ChooseAttack(dist, Mathf.Abs(ToPlayer.y) < 2f);
                    break;
                }

                case Mode.Combo: UpdateCombo(dt); break;

                case Mode.Roll:
                {
                    rollTime += dt;
                    float t = Mathf.Clamp01(rollTime / 0.4f);
                    vx = rollDir * 12f * (t < 0.7f ? 1f : Mathf.Lerp(1f, 0.3f, (t - 0.7f) / 0.3f));
                    float angle = -rollDir * facing * 360f * (1f - (1f - t) * (1f - t));
                    var rot = Quaternion.Euler(0f, 0f, angle * facing);
                    var pivot = new Vector3(0f, 0.65f, 0f);
                    if (visual != null) { visual.localRotation = rot; visual.localPosition = (pivot - rot * pivot) * Scale; }
                    ArenaFX.Afterimage(body, new Color(0.7f, 0.35f, 1f, 0.35f), 0.15f);
                    if (rollTime >= 0.42f) EndRoll();
                    break;
                }

                case Mode.Guard:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        // riposte: an overhead cleave
                        mode = Mode.Riposte;
                        swing = 2;
                        StartSwing(Combo[2]);
                        swingsLeft = 0;
                    }
                    break;

                case Mode.Riposte:
                    UpdateSwing(Combo[2], Mathf.RoundToInt(damage * 1.4f));
                    if (anim.IsDone) { mode = Mode.Recover; timer = 0.5f; }
                    break;

                case Mode.StabCharge:
                {
                    vx = 0f;
                    FacePlayer();
                    // dark light gathers on the blade
                    if ((chargeFx -= dt) <= 0f)
                    {
                        chargeFx = 0.03f;
                        var grip = Local(new Vector2(0.35f, 0.95f));
                        var from = grip + Random.insideUnitCircle.normalized * Random.Range(1f, 1.8f);
                        ArenaFX.Spray(from, grip - from, Random.value < 0.5f ? Violet : VioletHot, 1, 6f, 0.05f, 0.08f);
                    }
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Stab;
                        timer = 0.5f;
                        swingHit = false;
                        anim.Play("heavyStab", true);
                        vx = facing * 17f;
                        if (thrustSprite != null)
                            ArenaFX.PlaySprite(thrustSprite, Local(new Vector2(0.45f, 0.98f)), Violet, 0.2f, true, new Vector3(0.6f, 1.1f, 1f), new Vector3(1.6f, 0.7f, 1f), 0f, 0f, facing < 0, 27);
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.5f), Dust, 6, 4f, 0.4f, 0.12f);
                    }
                    break;
                }

                case Mode.Stab:
                    ArenaFX.Afterimage(body, new Color(0.7f, 0.35f, 1f, 0.35f), 0.15f);
                    if (timer > 0.3f && !swingHit)
                    {
                        var r = HitPlayerInBox((Vector2)transform.position + new Vector2(facing * 2.2f, 1.25f), new Vector2(3.8f, 1.4f), Mathf.RoundToInt(damage * 1.3f), 11f);
                        if (r != HitResult.Ignored) swingHit = true;
                    }
                    if (timer <= 0.3f) vx = Mathf.MoveTowards(vx, 0f, 60f * dt);
                    if (timer <= 0f) { mode = Mode.Recover; timer = 0.55f; anim.Play("idle"); }
                    break;

                case Mode.FlameLower:
                {
                    vx = 0f;
                    float lit = 1f - Mathf.Clamp01(timer / Tell(0.6f));
                    var tip = Local(new Vector2(-1.4f, 0.68f));
                    var hilt = Local(new Vector2(-1.06f, 1f));
                    BurnBlade(tip, Vector2.Lerp(tip, hilt, lit));
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.FlameRise;
                        erupted = false;
                        anim.Play("flameRise", true);
                        timer = 1f;
                    }
                    break;
                }

                case Mode.FlameRise:
                    vx = 0f;
                    if (!erupted && anim.Frame >= 1) Erupt();
                    if (anim.IsDone || timer <= 0f) { mode = Mode.Recover; timer = 0.6f; anim.Play("idle"); }
                    break;

                case Mode.CutsDraw:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.6f) ArenaFX.Burst(Local(new Vector2(0.35f, 0.95f)), Violet, 1, 3f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        // vanishes
                        markCenter = PlayerAlive ? player.Center : Center;
                        mode = Mode.CutsBlink;
                        blinks = 5;
                        timer = 0f;
                        rb.bodyType = RigidbodyType2D.Kinematic;
                        rb.linearVelocity = Vector2.zero;
                        ArenaFX.Burst(Center, Violet, 14, 6f, 0.1f, 0.3f, 0.5f);
                        body.enabled = false;
                    }
                    break;

                case Mode.CutsBlink:
                    if (timer <= 0f)
                    {
                        timer = 0.08f;
                        if (blinks-- > 0) Blink();
                        else Reappear();
                    }
                    break;

                case Mode.CutsHold:
                    vx = 0f;
                    if (timer <= 0.22f && !swingShown)
                    {
                        // the glint as he sheathes: this is the beat to roll on
                        swingShown = true;
                        var tip = Local(new Vector2(0.9f, 0.4f));
                        if (starSprite != null) ArenaFX.PlaySprite(starSprite, tip, VioletHot, 0.3f, true, Vector3.one * 0.4f, Vector3.one * 1.6f, 0f, 220f);
                        ArenaFX.Burst(tip, Color.white, 5, 2f, 0.06f, 0.2f, 0f);
                    }
                    if (timer <= 0f) ReleaseCuts();
                    break;

                case Mode.ClapCall:
                    vx = 0f;
                    if (Random.value < 0.5f)
                    {
                        var tip = Local(new Vector2(-0.07f, 2.16f));
                        ArenaFX.Zap(tip, tip + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.2f, 2.6f)), Violet, 0.06f);
                    }
                    if (timer <= 0f) Thunderclap();
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f) { mode = Mode.Stalk; timer = Random.Range(0.3f, 0.7f) / Pace; }
                    break;
            }
        }

        // ---------- choosing ----------

        void ChooseAttack(float dist, bool sameLevel)
        {
            float roll = Random.value;
            Mode next;
            if (!sameLevel) next = roll < 0.5f ? Mode.ClapCall : Mode.CutsDraw;
            else if (dist < 3.4f) next = roll < 0.45f ? Mode.Combo : roll < 0.6f ? Mode.FlameLower : roll < 0.75f ? Mode.CutsDraw : roll < 0.88f ? Mode.ClapCall : Mode.StabCharge;
            else next = roll < 0.3f ? Mode.StabCharge : roll < 0.55f ? Mode.FlameLower : roll < 0.78f ? Mode.ClapCall : Mode.CutsDraw;
            if (next == lastAttack) next = dist < 3.4f ? Mode.Combo : Mode.StabCharge;
            if (next == lastAttack) next = Mode.ClapCall;
            lastAttack = next;
            vx = 0f;
            switch (next)
            {
                case Mode.Combo:
                    mode = Mode.Combo;
                    swing = 0;
                    swingsLeft = enraged ? 3 : Random.value < 0.6f ? 3 : 2;
                    delayedThird = Random.value < 0.4f;
                    BeginTell(0.25f);
                    timer = Tell(0.25f);
                    anim.Play("idle");
                    break;
                case Mode.StabCharge:
                    mode = next;
                    timer = DelayedTell(0.7f, 0.5f);
                    anim.Play("heavyCharge", true);
                    BeginTell(timer);
                    flash.Hold(Violet, 0.3f, 24f);
                    break;
                case Mode.FlameLower:
                    mode = next;
                    timer = Tell(0.6f);
                    anim.Play("flameLower", true);
                    BeginTell(timer);
                    break;
                case Mode.CutsDraw:
                    mode = next;
                    timer = Tell(0.6f);
                    anim.Play("heavyCharge", true);
                    BeginTell(timer);
                    ArenaFX.Darken(0.6f, 1.8f);
                    if (PlayerAlive) ArenaFX.Ring(player.Center, new Color(0.75f, 0.4f, 1f, 0.9f), 4f, 0.4f, timer);
                    break;
                default:
                    mode = Mode.ClapCall;
                    timer = Tell(0.8f);
                    anim.Play("thunderCall", true);
                    BeginTell(timer);
                    clapPoint = PlayerAlive ? GroundBelow(player.transform.position + Vector3.up * 0.5f) : (Vector2)transform.position;
                    WarnSpot(clapPoint, 1.4f, timer, new Color(0.75f, 0.4f, 1f, 0.95f));
                    ArenaFX.Darken(0.5f, 1.1f);
                    break;
            }
        }

        // ---------- the sword combo ----------

        void UpdateCombo(float dt)
        {
            if (timer > 0f) return;
            if (anim.Current != Combo[swing].clip || anim.IsDone)
            {
                if (anim.Current == Combo[swing].clip) swing++;
                if (swing >= swingsLeft) { mode = Mode.Recover; timer = 0.5f / Pace; anim.Play("idle"); return; }
                if (swing == 2 && delayedThird && anim.Current != "heavyCharge")
                {
                    // holds the cleave up a beat
                    anim.Play("heavyCharge", true);
                    timer = 0.35f;
                    BeginTell(timer);
                    return;
                }
                EndTell();
                StartSwing(Combo[swing]);
                return;
            }
            UpdateSwing(Combo[swing], swing == 2 ? Mathf.RoundToInt(damage * 1.25f) : damage);
        }

        void StartSwing(Swing s)
        {
            FacePlayer();
            anim.Play(s.clip, true);
            swingHit = swingShown = false;
            vx = facing * (s.heavy ? 7f : 4f);
        }

        void UpdateSwing(Swing s, int amount)
        {
            vx = Mathf.MoveTowards(vx, 0f, 30f * Time.deltaTime);
            int f = anim.Frame;
            if (f < s.activeFrom || f > s.activeTo) return;
            if (!swingShown)
            {
                swingShown = true;
                if (smearSprites != null && s.smear < smearSprites.Length)
                {
                    var pivot = (Vector2)transform.position + new Vector2(s.smearOffset.x * facing, s.smearOffset.y);
                    var start = Vector3.one * s.smearScale * 0.94f;
                    var end = Vector3.one * s.smearScale * 1.08f;
                    ArenaFX.PlaySprite(smearSprites[s.smear], pivot, new Color(0.85f, 0.6f, 1f, 1f), 0.16f, false, start, end, 0f, s.spin * facing, facing < 0, 27);
                    ArenaFX.PlaySprite(smearSprites[s.smear], pivot, new Color(0.6f, 0.15f, 0.8f, 0.55f), 0.22f, true, start * 1.05f, end * 1.12f, 0f, s.spin * facing, facing < 0, 26);
                }
                if (s.heavy) ArenaFX.Shake(0.25f);
            }
            if (swingHit) return;
            var r = HitPlayerInBox((Vector2)transform.position + new Vector2(s.box.x * facing, s.box.y), s.size, amount, s.heavy ? 11f : 7f);
            if (r != HitResult.Ignored) swingHit = true;
        }

        // ---------- roll & parry ----------

        void StartRoll(int dir)
        {
            mode = Mode.Roll;
            rollDir = dir;
            rollTime = 0f;
            anim.Play("roll", true);
            ArenaFX.Spray(transform.position, new Vector2(-dir, 0.4f), Dust, 6, 4f, 0.35f, 0.12f);
        }

        void EndRoll()
        {
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
            mode = Mode.Recover;
            timer = 0.25f;
            anim.Play("idle");
        }

        public override void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            if (Untouchable || IsDead) return;
            if (TryEvade(from)) return;
            base.TakeHit(amount, from, knockback, heavy, crit, poise);
        }

        bool TryEvade(Vector2 from)
        {
            if (Unavoidable || evadeTimer > 0f || stunTimer > 0f || (mode != Mode.Stalk && mode != Mode.Recover)) return false;
            bool melee = PlayerAlive && Vector2.Distance(player.Center, Center) < 4f && Vector2.Distance(from, player.transform.position) < 1.6f;
            if (melee && Random.value < parryChance)
            {
                evadeTimer = evadeCooldown;
                FacePlayer();
                mode = Mode.Guard;
                timer = 0.3f;
                vx = 0f;
                anim.Play("defend", true);
                var spark = Center + new Vector2(facing * 0.9f, 0.4f);
                flash.Flash(Color.white, 0.15f);
                ArenaFX.Ring(spark, new Color(0.85f, 0.6f, 1f, 0.95f), 0.4f, 3.2f, 0.25f);
                ArenaFX.Burst(spark, VioletHot, 18, 8f, 0.1f, 0.4f, 1f);
                ArenaFX.DamageNumber(Center + Vector2.up * 1.9f, "PARRIED!", new Color(0.9f, 0.7f, 1f), 1.05f);
                ArenaFX.HitStop(0.1f);
                ArenaFX.Shake(0.3f);
                player.Recoil(Center, 0.35f);
                return true;
            }
            if (Random.value < rollChance)
            {
                evadeTimer = evadeCooldown;
                // away from the blow, or (sometimes) straight through you
                int away = Center.x >= from.x ? 1 : -1;
                if (Random.value < 0.35f || Physics2D.Raycast(Center, new Vector2(away, 0f), 3f, ArenaLayers.SolidMask)) away = -away;
                StartRoll(away);
                return true;
            }
            return false;
        }

        // ---------- flame slash ----------

        void BurnBlade(Vector2 from, Vector2 to)
        {
            for (int i = 0; i < 2; i++)
            {
                var p = Vector2.Lerp(from, to, Random.value);
                ArenaFX.Spray(p, new Vector2(Random.Range(-0.25f, 0.25f), 1f), Random.value < 0.45f ? VioletHot : Violet, 1, Random.Range(2.5f, 4.5f), 0.3f, Random.Range(0.12f, 0.2f));
            }
            if (Random.value < 0.3f) ArenaFX.Glow((from + to) * 0.5f, new Color(0.6f, 0.2f, 1f, 0.45f), 2.6f, 0.12f);
        }

        void Erupt()
        {
            erupted = true;
            var origin = (Vector2)transform.position + new Vector2(0.15f * facing, 1.25f);
            if (arcPrefab != null)
            {
                var arc = Instantiate(arcPrefab, origin, Quaternion.identity);
                arc.transform.localScale = Vector3.one * 1.15f;
                arc.LaunchHostile(facing, Mathf.RoundToInt(damage * 1.5f), Mathf.RoundToInt(damage * 0.5f), new Color(0.65f, 0.3f, 1f), VioletHot, Rot);
            }
            var front = origin + new Vector2(1.6f * facing, 0f);
            ImpactFrame.Play(front, ImpactFrame.Style.Void);
            ArenaFX.HitStop(0.1f);
            ArenaFX.Shake(0.6f);
            ArenaFX.Burst(front, Violet, 22, 8f, 0.13f, 0.45f, 2f);
        }

        // ---------- thousand cuts (his version) ----------

        void Blink()
        {
            // flickers to a spot around the marks, leaving an afterimage and a cut hanging in the air
            float a = Random.Range(0f, Mathf.PI * 2f);
            var spot = markCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(1.6f, 2.6f);
            var from = (Vector2)transform.position;
            transform.position = spot + Vector2.down * 1.2f;
            rb.position = transform.position;
            facing = markCenter.x >= spot.x ? 1 : -1;
            if (visual != null) visual.localScale = new Vector3(facing * Scale, Scale, 1f);
            anim.Play(Combo[blinks % 3].clip, true);
            ArenaFX.Afterimage(body, new Color(0.7f, 0.35f, 1f, 0.7f), 0.5f);
            ArenaFX.Burst(spot, Violet, 4, 3f, 0.07f, 0.2f, 0f);
            if (cutSprite == null) return;
            float angle = Random.Range(-70f, 70f);
            float stay = 0.08f * (blinks + 1) + 0.6f;
            ArenaFX.PlaySprite(cutSprite, markCenter + Random.insideUnitCircle * 0.3f, new Color(0.85f, 0.55f, 1f, 1f), stay, false, new Vector3(0.2f, 0.5f, 1f), new Vector3(0.75f, 0.45f, 1f), angle, 0f, false, 31);
            ArenaFX.PlaySprite(cutSprite, markCenter, new Color(0.6f, 0.2f, 0.9f, 0.6f), stay, true, new Vector3(0.2f, 1.4f, 1f), new Vector3(0.75f, 1.1f, 1f), angle, 0f, false, 30);
        }

        void Reappear()
        {
            // stands behind the knight, back turned, blade lowered
            float side = PlayerAlive ? -player.Facing : -facing;
            var spot = GroundBelow(new Vector2(markCenter.x + side * 2.6f, markCenter.y + 0.5f));
            if (Physics2D.OverlapBox(spot + Vector2.up * 1.2f, new Vector2(1f, 2f), 0f, ArenaLayers.SolidMask)) spot = GroundBelow(new Vector2(markCenter.x - side * 2.6f, markCenter.y + 0.5f));
            transform.position = spot;
            rb.position = spot;
            rb.bodyType = RigidbodyType2D.Dynamic;
            body.enabled = true;
            facing = spot.x > markCenter.x ? 1 : -1; // facing away
            anim.Play("cutsRest", true);
            mode = Mode.CutsHold;
            timer = 0.75f;
            swingShown = false;
        }

        void ReleaseCuts()
        {
            ImpactFrame.Play(markCenter, ImpactFrame.Style.Void);
            ArenaFX.HitStop(0.12f);
            ArenaFX.Shake(0.7f);
            if (cutSprite != null)
                for (int i = 0; i < 4; i++)
                    ArenaFX.PlaySprite(cutSprite, markCenter + Random.insideUnitCircle * 0.4f, Color.white, 0.25f, true, new Vector3(0.8f, 2.4f, 1f), new Vector3(0.95f, 0.1f, 1f), Random.Range(-70f, 70f), 0f, false, 32);
            if (PlayerAlive && Vector2.Distance(player.Center, markCenter) < 2.1f)
                player.TakeHit(Mathf.RoundToInt(damage * 1.8f), markCenter + new Vector2(-player.Facing, 0f), 9f);
            mode = Mode.Recover;
            timer = 0.8f; // his opening
        }

        // ---------- thunderclap ----------

        void Thunderclap()
        {
            EndTell();
            anim.Play("clapStrike", true);
            if (boltFrames != null && boltFrames.Length > 0)
            {
                var bolt = boltFrames[Random.Range(0, boltFrames.Length)];
                ArenaFX.PlaySprite(bolt, clapPoint, new Color(0.6f, 0.25f, 1f, 1f), 0.4f, true, new Vector3(4.5f, 4.5f, 1f), new Vector3(2.5f, 4.5f, 1f), 0f, 0f, false, 29);
                ArenaFX.PlaySprite(bolt, clapPoint, new Color(0.15f, 0.02f, 0.2f, 1f), 0.25f, false, new Vector3(2.2f, 4.5f, 1f), new Vector3(1.2f, 4.5f, 1f), 0f, 0f, true, 30);
            }
            ImpactFrame.Play(clapPoint + Vector2.up * 1.5f, ImpactFrame.Style.Void);
            ArenaFX.FlatRing(clapPoint + Vector2.up * 0.08f, new Color(0.75f, 0.4f, 1f, 0.9f), 0.4f, 6f, 0.35f);
            ArenaFX.Burst(clapPoint + Vector2.up * 0.3f, VioletHot, 26, 10f, 0.09f, 0.45f, 2f);
            ArenaFX.HitStop(0.12f);
            ArenaFX.Shake(0.8f);
            HitPlayerInBox(clapPoint + Vector2.up * 1.2f, new Vector2(3.6f, 2.8f), Mathf.RoundToInt(damage * 1.5f), 12f);
            Shockwaves(clapPoint, 11f * Pace, 7f, Violet, Mathf.RoundToInt(damage * 0.6f));
            mode = Mode.Recover;
            timer = 0.6f;
        }

        // ---------- corruption ----------

        float rotTimer;

        /// <summary>The swarm seething out of the armor.</summary>
        void Corruption(float dt)
        {
            if ((rotTimer -= dt) > 0f) return;
            rotTimer = 0.07f;
            var p = Center + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.8f, 1f));
            ArenaFX.Spray(p, Vector2.up, Random.value < 0.5f ? Rot : Violet, 1, 1.2f, 0.5f, 0.08f);
        }

        // ---------- phase 2 ----------

        protected override void OnEnrage()
        {
            base.OnEnrage();
            ArenaFX.Ring(Center, new Color(0.7f, 0.35f, 1f, 0.9f), 1f, 9f, 0.5f);
            ArenaFX.DamageNumber(Center + Vector2.up * 2.3f, "THE ROT SPREADS", Violet, 0.9f);
        }

        protected override void Die()
        {
            if (transforming) return;
            StartCoroutine(TransformRoutine());
        }

        System.Collections.IEnumerator TransformRoutine()
        {
            transforming = true;
            mode = Mode.Transform;
            EndTell();
            vx = 0f;
            body.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            var hud = GameUIController.Instance != null ? GameUIController.Instance.hud : null;
            if (hud != null) hud.SetBoss(true, 0f);
            anim.Play("hurt", true);
            ArenaFX.SlowMo(0.35f, 1.4f);
            ArenaFX.Darken(0.7f, 3f);
            flash.Hold(Violet, 0.5f, 30f);

            // he staggers, the armor cracks, something moves inside
            for (int i = 0; i < 8; i++)
            {
                ArenaFX.Burst(Center + Random.insideUnitCircle * 0.6f, i % 2 == 0 ? Violet : Rot, 10, 6f, 0.12f, 0.5f, 1f);
                ArenaFX.Shake(0.25f + i * 0.05f);
                yield return new WaitForSecondsRealtime(0.18f);
            }
            anim.Play("dead", true);
            ThroneRoom.CrumbleNow();
            yield return new WaitForSecondsRealtime(0.5f);

            // it tears out of him
            ImpactFrame.Play(Center, ImpactFrame.Style.Void);
            ArenaFX.HitStop(0.15f);
            ArenaFX.Shake(1f);
            ArenaFX.Ring(Center, new Color(0.75f, 0.4f, 1f, 0.95f), 0.5f, 10f, 0.5f);
            for (int i = 0; i < 4; i++) ArenaFX.Burst(Center + Random.insideUnitCircle, i % 2 == 0 ? Violet : bloodColor, 24, 10f, 0.16f, 0.8f, 3f);
            var director = ArenaDirector.Instance;
            if (director != null && parasitePrefab != null) director.SpawnBug(parasitePrefab, transform.position);
            flash.ClearHold();
            base.Die(); // (FinalForm is false: no "ENEMY FELLED" yet)
        }

        protected override void OnStagger()
        {
            EndTell();
            if (Rolling) EndRoll();
            if (mode == Mode.CutsBlink || mode == Mode.CutsHold) { body.enabled = true; rb.bodyType = RigidbodyType2D.Dynamic; }
            if (stunTimer > 0f) { mode = Mode.Recover; anim.Play("hurt"); }
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
            anim.Play("idle");
        }

        void FixedUpdate()
        {
            if (IsDead || transforming) { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
            if (rb.bodyType == RigidbodyType2D.Kinematic) { rb.linearVelocity = Vector2.zero; return; }
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
