using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Boss for floor 20, fought in her Hive (a tall arena of floating combs). She never sits still:
    /// between attacks she darts around the knight. She picks from:
    ///   Dive-bomb     — rears back and spears down at you; enraged she chains up to three dives. If
    ///                   she hits the floor her stinger gets stuck for a moment (free hits!).
    ///   Royal Lance   — flies to the edge of the hive at your height, a golden line shows her path,
    ///                   then she lances straight across the arena (jump or roll through it). Enraged,
    ///                   twice.
    ///   Barrage       — a fan of stingers aimed at you (two fans when enraged). Parry them back.
    ///   Poison        — sweeps across dropping poison sacs that leave lingering clouds.
    ///   Stinger Storm — enraged: rises to the top of the hive and spins, spraying a spiral of stingers.
    ///   Swarm         — enraged: calls in wasps.
    /// Super armor: hits don't interrupt her, but parries and stance breaks do.
    /// </summary>
    public class HornetQueen : BossBug
    {
        public Stinger stingerPrefab;
        public AcidBlob sacPrefab;
        public BugEnemy summonPrefab;

        [Header("Flight")]
        public float flySpeed = 9f;
        public float hoverHeight = 3.3f;
        public float dartSpeed = 24f;

        [Header("Dive")]
        public float diveTell = 0.6f;
        public float diveSpeed = 20f;
        public float diveTime = 0.85f;
        public float stuckTime = 1.1f;

        [Header("Royal Lance")]
        public float lanceTell = 0.8f;
        public float lanceSpeed = 30f;

        [Header("Barrage")]
        public float barrageTell = 0.55f;
        public int stingers = 5;
        public float stingerSpeed = 12f;
        public float spreadDegrees = 48f;

        [Header("Stinger Storm")]
        public float stormTime = 1.6f;
        public float stormRate = 0.08f;
        public float stormSpeed = 7.5f;

        [Header("Poison")]
        public int sacs = 4;
        public float sweepSpeed = 10f;

        enum Mode { Intro, Hover, Dart, DiveTell, Dive, Stuck, Rise, LanceMove, LanceTell, Lance, BarrageTell, SacMove, SacSweep, StormMove, StormTell, Storm, SwarmTell, Recover }
        Mode mode, lastAttack;
        float timer, swarmCooldown, stormCooldown, sacTimer, bobSeed, dartTimer, spinAngle, stormTimer, stormFire;
        int side = 1, volleysLeft, sacsLeft, sweepDir, divesLeft, lancesLeft, lanceDir;
        bool hitThisMove;
        Vector2 velocity, diveDir, moveTarget;

        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        static readonly Color GoldLine = new Color(1f, 0.75f, 0.25f, 0.9f);
        static readonly Color Poison = new Color(0.65f, 1f, 0.35f);

        protected override void Start()
        {
            base.Start();
            bobSeed = Random.value * 10f;
            mode = Mode.Intro;
            timer = 1.2f;
            anim.Play("fly");
            ArenaFX.Shake(0.5f);
            ArenaFX.Ring(Center, new Color(1f, 0.8f, 0.3f, 0.9f), 1f, 8f, 0.5f);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            swarmCooldown -= dt;
            stormCooldown -= dt;
            dartTimer -= dt;
            var pos = (Vector2)transform.position;

            switch (mode)
            {
                case Mode.Intro:
                    velocity = Vector2.MoveTowards(velocity, Vector2.up * 1.5f, 10f * dt);
                    FacePlayer();
                    if (timer <= 0f) { mode = Mode.Hover; timer = 0.8f; }
                    break;

                case Mode.Hover:
                    anim.Play("fly");
                    Steer(HoverPoint(), flySpeed, dt);
                    FacePlayer();
                    if (timer <= 0f && PlayerAlive) ChooseAttack();
                    else if (dartTimer <= 0f && PlayerAlive && timer > 0.35f) StartDart();
                    break;

                case Mode.Dart:
                    // a quick zip to a new spot around the knight
                    ArenaFX.Afterimage(body, new Color(1f, 0.8f, 0.35f, 0.35f), 0.15f);
                    FacePlayer();
                    if (Vector2.Distance(pos, moveTarget) < 0.8f || timer <= 0f) { mode = Mode.Hover; velocity *= 0.3f; }
                    break;

                case Mode.DiveTell:
                    FacePlayer();
                    // rear back and up before the dive
                    velocity = Vector2.MoveTowards(velocity, (-ToPlayer.normalized + Vector2.up * 0.6f) * 2.2f, 20f * dt);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Dive;
                        timer = diveTime;
                        hitThisMove = false;
                        diveDir = PlayerAlive ? (player.Center + Vector2.down * 0.3f - Center).normalized : Vector2.down;
                        velocity = diveDir * diveSpeed;
                        anim.Play("dive");
                        PointAlong(diveDir);
                        ArenaFX.Burst(Center, Gold, 10, 5f, 0.1f, 0.3f, 0f);
                    }
                    break;

                case Mode.Dive:
                    ArenaFX.Afterimage(body, new Color(1f, 0.75f, 0.3f, 0.35f), 0.18f);
                    if (!hitThisMove)
                    {
                        var r = HitPlayerInBox(Center + diveDir * 0.8f, new Vector2(1.6f, 1.6f), damage, 9f);
                        if (r != HitResult.Ignored) hitThisMove = true;
                        if (r == HitResult.Parried) break;
                    }
                    if (Physics2D.OverlapCircle(Center + diveDir * 1.2f, 0.35f, ArenaLayers.SolidMask))
                    {
                        // stinger buried in the floor
                        velocity = Vector2.zero;
                        mode = Mode.Stuck;
                        timer = stuckTime;
                        divesLeft = 0;
                        anim.Play("stuck");
                        ArenaFX.Shake(0.45f);
                        ArenaFX.Burst(Center + diveDir * 1.2f, new Color(0.62f, 0.58f, 0.52f), 22, 7f, 0.14f, 0.6f, 2.5f);
                        ArenaFX.FlatRing(Center + diveDir * 1.2f, new Color(1f, 0.8f, 0.35f, 0.8f), 0.4f, 3.5f, 0.25f);
                        ArenaFX.DamageNumber(Center + Vector2.up * 1.4f, "STUCK!", Gold, 0.85f);
                    }
                    else if (timer <= 0f)
                    {
                        if (--divesLeft > 0 && PlayerAlive)
                        {
                            // pull up and go again
                            mode = Mode.DiveTell;
                            timer = Tell(0.32f);
                            BeginTell(timer);
                            PointAlong(Vector2.zero);
                            anim.Play("tell");
                        }
                        else { mode = Mode.Rise; timer = 0.5f; }
                    }
                    break;

                case Mode.Stuck:
                    velocity = Vector2.zero;
                    if (Random.value < 0.15f)
                        ArenaFX.Spray(Center + diveDir * 1.2f, Vector2.up, new Color(0.62f, 0.58f, 0.52f), 1, 3f, 0.6f, 0.1f);
                    if (timer <= 0f)
                    {
                        ArenaFX.Burst(Center + diveDir * 1.2f, new Color(0.62f, 0.58f, 0.52f), 12, 5f, 0.12f, 0.5f, 2f);
                        mode = Mode.Rise;
                        timer = 0.5f;
                        anim.Play("fly");
                    }
                    break;

                case Mode.Rise:
                    PointAlong(Vector2.zero);
                    anim.Play("fly");
                    Steer(HoverPoint(), flySpeed * 1.3f, dt);
                    if (timer <= 0f) { mode = Mode.Hover; timer = Random.Range(0.7f, 1.3f) * (enraged ? 0.75f : 1f); }
                    break;

                case Mode.LanceMove:
                    anim.Play("fly");
                    Steer(moveTarget, flySpeed * 2.2f, dt);
                    facing = lanceDir;
                    if (Vector2.Distance(pos, moveTarget) < 0.7f || timer <= 0f)
                    {
                        mode = Mode.LanceTell;
                        timer = lancesLeft > 1 || !enraged ? Tell(lanceTell) : Tell(lanceTell * 0.65f);
                        var b = Bounds;
                        WarnLine(new Vector2(b.xMin, Center.y), new Vector2(b.xMax, Center.y), timer, GoldLine, 0.9f);
                        BeginTell(timer);
                        anim.Play("tell");
                    }
                    break;

                case Mode.LanceTell:
                    facing = lanceDir;
                    velocity = Vector2.MoveTowards(velocity, new Vector2(-lanceDir * 1.5f, 0f), 12f * dt);
                    // light gathering on the stinger
                    if (Random.value < 0.7f)
                    {
                        var tip = Center + new Vector2(lanceDir * 1.1f, -0.3f);
                        var from = tip + Random.insideUnitCircle.normalized * 1.3f;
                        ArenaFX.Spray(from, tip - from, Gold, 1, 6f, 0.05f, 0.08f);
                    }
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Lance;
                        hitThisMove = false;
                        velocity = new Vector2(lanceDir * lanceSpeed, 0f);
                        anim.Play("dive");
                        PointAlong(new Vector2(lanceDir, 0f));
                        ArenaFX.Burst(Center, Gold, 16, 7f, 0.1f, 0.3f, 0f);
                        ArenaFX.Shake(0.3f);
                    }
                    break;

                case Mode.Lance:
                {
                    ArenaFX.Afterimage(body, new Color(1f, 0.8f, 0.3f, 0.5f), 0.25f);
                    ArenaFX.Spray(Center, new Vector2(-lanceDir, 0f), Gold, 2, 4f, 0.3f, 0.1f);
                    if (!hitThisMove)
                    {
                        var r = HitPlayerInBox(Center + new Vector2(lanceDir * 0.8f, 0f), new Vector2(2.2f, 1.3f), Mathf.RoundToInt(damage * 1.15f), 12f);
                        if (r != HitResult.Ignored) hitThisMove = true;
                        if (r == HitResult.Parried) break;
                    }
                    var b = Bounds;
                    bool atEdge = lanceDir > 0 ? pos.x >= b.xMax - 1.2f : pos.x <= b.xMin + 1.2f;
                    if (atEdge)
                    {
                        velocity = Vector2.zero;
                        PointAlong(Vector2.zero);
                        ArenaFX.Shake(0.25f);
                        if (--lancesLeft > 0 && PlayerAlive) StartLance(-lanceDir);
                        else { mode = Mode.Recover; timer = 0.6f; anim.Play("fly"); }
                    }
                    break;
                }

                case Mode.BarrageTell:
                    FacePlayer();
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, 16f * dt);
                    if (Random.value < 0.4f) ArenaFX.Burst(StingerTip, Gold, 1, 1.5f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        FireBarrage();
                        if (--volleysLeft > 0) { timer = Tell(0.35f); BeginTell(timer); }
                        else { mode = Mode.Recover; timer = 0.6f; anim.Play("fly"); }
                    }
                    break;

                case Mode.SacMove:
                    anim.Play("fly");
                    Steer(moveTarget, flySpeed * 1.6f, dt);
                    FacePlayer();
                    if (Vector2.Distance(pos, moveTarget) < 0.6f || timer <= 0f)
                    {
                        mode = Mode.SacSweep;
                        sacsLeft = enraged ? sacs + 2 : sacs;
                        sacTimer = 0.15f;
                        timer = 2.6f;
                        facing = sweepDir;
                        ArenaFX.Exclaim(transform, exclaimHeight, 0.4f);
                    }
                    break;

                case Mode.SacSweep:
                {
                    facing = sweepDir;
                    velocity = Vector2.MoveTowards(velocity, new Vector2(sweepDir * sweepSpeed, 0f), 30f * dt);
                    sacTimer -= dt;
                    if (sacTimer <= 0f && sacsLeft > 0)
                    {
                        sacTimer = 0.3f;
                        sacsLeft--;
                        DropSac();
                    }
                    bool blocked = Physics2D.Raycast(Center, new Vector2(sweepDir, 0f), 1.8f, ArenaLayers.SolidMask);
                    if ((sacsLeft == 0 && sacTimer <= 0f) || blocked || timer <= 0f)
                    {
                        mode = Mode.Recover;
                        timer = 0.5f;
                    }
                    break;
                }

                case Mode.StormMove:
                    anim.Play("fly");
                    Steer(moveTarget, flySpeed * 1.8f, dt);
                    if (Vector2.Distance(pos, moveTarget) < 0.7f || timer <= 0f)
                    {
                        mode = Mode.StormTell;
                        timer = Tell(0.65f);
                        BeginTell(timer);
                        ArenaFX.Ring(Center, new Color(1f, 0.8f, 0.3f, 0.9f), 6f, 0.5f, timer);
                        ArenaFX.Glow(Center, new Color(1f, 0.75f, 0.3f, 0.5f), 5f, timer);
                    }
                    break;

                case Mode.StormTell:
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, 16f * dt);
                    spinAngle += 400f * dt;
                    Spin();
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Storm;
                        stormTimer = stormTime;
                        stormFire = 0f;
                        ArenaFX.Shake(0.35f);
                    }
                    break;

                case Mode.Storm:
                    velocity = Vector2.zero;
                    stormTimer -= dt;
                    spinAngle += 900f * dt;
                    Spin();
                    if ((stormFire -= dt) <= 0f)
                    {
                        stormFire = stormRate;
                        // three arms of stingers sweeping round
                        float baseAngle = (stormTime - stormTimer) * 260f;
                        for (int arm = 0; arm < 3; arm++)
                        {
                            float a = (baseAngle + arm * 120f) * Mathf.Deg2Rad;
                            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            if (stingerPrefab == null) break;
                            var s = Instantiate(stingerPrefab, Center + dir * 0.9f, Quaternion.identity);
                            s.Launch(dir, stormSpeed, Mathf.RoundToInt(damage * 0.5f));
                        }
                        ArenaFX.Burst(Center, Gold, 2, 3f, 0.07f, 0.2f, 0f);
                    }
                    if (stormTimer <= 0f)
                    {
                        if (visual != null) visual.localRotation = Quaternion.identity;
                        mode = Mode.Recover;
                        timer = 0.9f;
                        anim.Play("fly");
                    }
                    break;

                case Mode.SwarmTell:
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, 16f * dt);
                    if (timer <= 0f)
                    {
                        EndTell();
                        Swarm();
                        mode = Mode.Recover;
                        timer = 0.7f;
                        anim.Play("fly");
                    }
                    break;

                case Mode.Recover:
                    anim.Play("fly");
                    PointAlong(Vector2.zero);
                    Steer(HoverPoint(), flySpeed * 0.8f, dt);
                    if (timer <= 0f) { mode = Mode.Hover; timer = Random.Range(0.6f, 1.2f) * (enraged ? 0.7f : 1f); dartTimer = Random.Range(0.2f, 0.6f); }
                    break;
            }

            pos += velocity * dt;
            var bounds = Bounds;
            // diving (and pulling back out) she may dip all the way to the floor to bury her stinger
            bool low = mode == Mode.Dive || mode == Mode.Stuck || mode == Mode.Rise;
            pos.x = Mathf.Clamp(pos.x, bounds.xMin + 0.8f, bounds.xMax - 0.8f);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin + (low ? -0.9f : 0.6f), bounds.yMax - 1f);
            transform.position = pos;
        }

        Vector2 HoverPoint()
        {
            if (!PlayerAlive) return (Vector2)transform.position + Vector2.up;
            // drift from one side of the knight to the other every few seconds
            if (Mathf.Repeat(Time.time + bobSeed, 3.5f) < Time.deltaTime) side = -side;
            return player.Center + new Vector2(side * 2.8f, hoverHeight + Mathf.Sin(Time.time * 2.4f + bobSeed) * 0.6f);
        }

        void Steer(Vector2 target, float speed, float dt)
        {
            var desired = Vector2.ClampMagnitude((target - (Vector2)transform.position) * 2.4f, speed);
            velocity = Vector2.MoveTowards(velocity, desired, 22f * dt);
        }

        void StartDart()
        {
            var b = Bounds;
            side = Random.value < 0.5f ? -1 : 1;
            moveTarget = player.Center + new Vector2(side * Random.Range(2.5f, 5f), Random.Range(2.2f, 5.5f));
            moveTarget.x = Mathf.Clamp(moveTarget.x, b.xMin + 1.5f, b.xMax - 1.5f);
            moveTarget.y = Mathf.Clamp(moveTarget.y, b.yMin + 1.5f, b.yMax - 1.5f);
            velocity = (moveTarget - (Vector2)transform.position).normalized * dartSpeed;
            mode = Mode.Dart;
            timer = 0.3f;
            dartTimer = Random.Range(1f, 2f) * (enraged ? 0.7f : 1f);
            ArenaFX.Burst(Center, Gold, 4, 3f, 0.08f, 0.2f, 0f);
        }

        /// <summary>Tilts the body along a direction (zero = level again).</summary>
        void PointAlong(Vector2 dir)
        {
            if (visual == null) return;
            if (dir == Vector2.zero) { visual.localRotation = Quaternion.identity; return; }
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - (facing < 0 ? 180f : 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void Spin()
        {
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
        }

        Vector2 StingerTip => Center + new Vector2(facing * 0.5f, -0.95f);

        void ChooseAttack()
        {
            bool canSwarm = enraged && swarmCooldown <= 0f && summonPrefab != null;
            bool canStorm = enraged && stormCooldown <= 0f && stingerPrefab != null;
            float roll = Random.value;
            Mode next;
            if (simple)
            {
                // Dive-bomb (her stinger gets stuck: free hits), Royal Lance (jump it) and Barrage; no poison sacs
                if (roll < 0.5f && lastAttack != Mode.DiveTell) next = Mode.DiveTell;
                else if (roll < 0.75f && lastAttack != Mode.LanceMove) next = Mode.LanceMove;
                else if (lastAttack != Mode.BarrageTell) next = Mode.BarrageTell;
                else next = Mode.DiveTell;
            }
            else if (canStorm && roll < 0.3f) next = Mode.StormMove;
            else if (canSwarm && roll < 0.42f) next = Mode.SwarmTell;
            else if (roll < 0.62f && lastAttack != Mode.DiveTell) next = Mode.DiveTell;
            else if (roll < 0.8f && lastAttack != Mode.LanceMove) next = Mode.LanceMove;
            else if (roll < 0.92f && lastAttack != Mode.BarrageTell) next = Mode.BarrageTell;
            else if (lastAttack != Mode.SacMove) next = Mode.SacMove;
            else next = Mode.DiveTell;
            lastAttack = next;

            switch (next)
            {
                case Mode.DiveTell:
                    mode = next;
                    divesLeft = enraged ? 3 : !simple && Random.value < 0.4f ? 2 : 1;
                    timer = DelayedTell(diveTell, 0.5f) * (enraged ? 0.8f : 1f);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.LanceMove:
                    lancesLeft = enraged ? 2 : 1;
                    StartLance(player.transform.position.x > Bounds.center.x ? 1 : -1);
                    break;
                case Mode.BarrageTell:
                    mode = next;
                    volleysLeft = enraged ? 2 : 1;
                    timer = Tell(barrageTell);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.SacMove:
                {
                    mode = next;
                    sweepDir = player.transform.position.x > transform.position.x ? 1 : -1;
                    var bounds = Bounds;
                    float startX = Mathf.Clamp(player.transform.position.x - sweepDir * 6.5f, bounds.xMin + 1.5f, bounds.xMax - 1.5f);
                    moveTarget = new Vector2(startX, Mathf.Min(player.Center.y + 4.6f, bounds.yMax - 1.5f));
                    timer = 2.2f;
                    break;
                }
                case Mode.StormMove:
                {
                    mode = next;
                    stormCooldown = 12f;
                    var b = Bounds;
                    moveTarget = new Vector2(Mathf.Clamp(player.transform.position.x, b.xMin + 6f, b.xMax - 6f), Mathf.Min(player.Center.y + 6f, b.yMax - 2f));
                    timer = 1.6f;
                    break;
                }
                default:
                    mode = Mode.SwarmTell;
                    timer = Tell(0.8f);
                    anim.Play("tell");
                    BeginTell(timer);
                    swarmCooldown = 14f;
                    break;
            }
        }

        /// <summary>Flies to the edge of the hive on the knight's level, ready to lance across.</summary>
        void StartLance(int towardSide)
        {
            var b = Bounds;
            // start on the far side and lance toward the knight
            lanceDir = towardSide;
            float x = lanceDir > 0 ? b.xMin + 1.5f : b.xMax - 1.5f;
            float y = PlayerAlive ? Mathf.Clamp(player.Center.y + 0.2f, b.yMin + 0.8f, b.yMax - 1.5f) : transform.position.y;
            moveTarget = new Vector2(x, y);
            mode = Mode.LanceMove;
            timer = 1.4f;
            PointAlong(Vector2.zero);
        }

        void FireBarrage()
        {
            if (stingerPrefab == null || !PlayerAlive) return;
            int count = enraged ? stingers + 2 : stingers;
            float spread = enraged ? spreadDegrees * 1.35f : spreadDegrees;
            var tip = StingerTip;
            var aim = (player.Center - tip).normalized;
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + (count == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(count - 1)));
                var s = Instantiate(stingerPrefab, tip, Quaternion.identity);
                s.Launch(new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)), stingerSpeed, Mathf.RoundToInt(damage * 0.6f));
            }
            velocity = -aim * 3f; // recoil
            ArenaFX.Burst(tip, Gold, 12, 6f, 0.1f, 0.3f, 0f);
            ArenaFX.Shake(0.15f);
        }

        void DropSac()
        {
            if (sacPrefab == null) return;
            var from = Center + Vector2.down * 0.8f;
            var sac = Instantiate(sacPrefab, from, Quaternion.identity);
            var ground = Physics2D.Raycast(from, Vector2.down, 30f, ArenaLayers.GroundMask);
            var target = ground ? ground.point + new Vector2(sweepDir * 1.2f, 0f) : from + new Vector2(sweepDir * 1.2f, -6f);
            sac.Launch(target, 0.55f, Mathf.RoundToInt(damage * 0.5f));
            ArenaFX.Burst(from, Poison, 4, 2f, 0.08f, 0.3f, 0f);
        }

        void Swarm()
        {
            var director = ArenaDirector.Instance;
            if (director == null) return;
            director.SpawnBug(summonPrefab, Center + new Vector2(-2.5f, 0.5f));
            director.SpawnBug(summonPrefab, Center + new Vector2(2.5f, 0.5f));
            ArenaFX.Ring(Center, new Color(1f, 0.8f, 0.3f, 0.8f), 0.5f, 6f, 0.4f);
            ArenaFX.Shake(0.3f);
        }

        protected override void WhileStunned(float dt)
        {
            // dazed: sinks a little and wobbles
            PointAlong(Vector2.zero);
            velocity = Vector2.MoveTowards(velocity, Vector2.down * 1.2f, 10f * dt);
            var pos = (Vector2)transform.position + velocity * dt;
            if (Physics2D.OverlapCircle(pos + Vector2.down * 1.1f, 0.3f, ArenaLayers.GroundMask)) pos.y = transform.position.y;
            transform.position = pos;
        }

        protected override void OnStagger()
        {
            EndTell();
            PointAlong(Vector2.zero);
            if (stunTimer > 0f) { mode = Mode.Recover; anim.Play("stuck"); divesLeft = 0; lancesLeft = 0; }
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Rise;
            timer = 0.5f;
            anim.Play("fly");
        }

        void FixedUpdate()
        {
            // Movement is driven by the transform; the kinematic body only carries the collider.
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }
}
