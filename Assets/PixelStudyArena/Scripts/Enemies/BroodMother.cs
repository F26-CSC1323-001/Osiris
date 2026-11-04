using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Boss for floor 10, fought in her Nest. A huge beetle that throws her weight around:
    ///   Charge       â€” rears up, then rams across the arena. Ramming a wall staggers her: open to a
    ///                  critical! Enraged, she bounces off the first wall and charges straight back.
    ///   Seismic Leap â€” crouches and leaps at you. The landing crushes whatever is under her, sends
    ///                  shockwaves along the floor (jump them) and shakes rocks loose from the ceiling
    ///                  (watch the marks on the floor). Enraged, she sometimes leaps twice.
    ///   Acid Volley  â€” lobs a spread of acid at you.
    ///   Acid Rain    â€” sprays acid high into the air; it rains down on marked spots across the nest.
    ///   Brood        â€” the egg nests around the arena pulse and hatch wasps and ticks.
    /// Super armor: hits don't interrupt her, but parries and stance breaks do.
    /// </summary>
    public class BroodMother : BossBug
    {
        public AcidBlob blobPrefab;
        public BugEnemy summonPrefab;
        public BugEnemy hatchlingPrefab;

        [Header("Movement")]
        public float walkSpeed = 2.8f;

        [Header("Charge")]
        public float chargeSpeed = 15f;
        public float chargeTell = 0.7f;
        public float wallDaze = 1.5f;

        [Header("Seismic Leap")]
        public float leapTell = 0.55f;
        public float leapTime = 0.85f;
        public int rocks = 6;
        public float shockSpeed = 12f;
        public float shockRange = 9f;

        [Header("Acid")]
        public float volleyTell = 0.6f;
        public float rainTell = 0.75f;
        public int rainBlobs = 7;

        [Header("Brood")]
        public float broodTell = 0.8f;
        public float broodCooldown = 12f;

        enum Mode { Intro, Walk, ChargeTell, Charge, VolleyTell, LeapCrouch, Leap, RainTell, BroodTell, Recover }
        Mode mode, lastAttack;
        float timer, broodTimer, vx, airTime;
        bool hitThisCharge, roared;
        int ricochets, leapsLeft;

        static readonly Color Purple = new Color(0.8f, 0.45f, 1f);
        static readonly Color Acid = new Color(0.6f, 1f, 0.35f);
        static readonly Color Danger = new Color(1f, 0.35f, 0.3f, 0.9f);
        static readonly Color Dust = new Color(0.6f, 0.58f, 0.55f);

        float Pace => enraged ? 1.2f : 1f;

        protected override void Start()
        {
            base.Start();
            mode = Mode.Intro;
            timer = 1.4f;
            anim.Play("tell");
            ArenaFX.Shake(0.5f);
            ArenaFX.Ring(Center, new Color(0.8f, 0.4f, 1f, 0.9f), 1f, 8f, 0.5f);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            broodTimer -= dt;
            TickWaves(dt);
            TickRocks(dt);

            switch (mode)
            {
                case Mode.Intro:
                    vx = 0f;
                    FacePlayer();
                    if (!roared && timer < 0.7f)
                    {
                        // the roar shakes dust and a few rocks from the ceiling
                        roared = true;
                        ArenaFX.Shake(0.8f);
                        ArenaFX.Ring(Center, new Color(0.8f, 0.45f, 1f, 0.8f), 1f, 10f, 0.6f);
                        for (int i = 0; i < 3; i++) Rockfall(transform.position.x + Random.Range(-9f, 9f), 0.3f + i * 0.25f);
                    }
                    if (timer <= 0f) { mode = Mode.Walk; timer = 0.8f; anim.Play("walk"); }
                    break;

                case Mode.Walk:
                    FacePlayer();
                    anim.Play("walk");
                    vx = WallAhead(1.7f) || Mathf.Abs(ToPlayer.x) < 1.6f ? 0f : facing * walkSpeed * Pace;
                    if (timer <= 0f && PlayerAlive) ChooseAttack();
                    break;

                case Mode.ChargeTell:
                    vx = -facing * 0.8f;
                    if (Random.value < 0.4f) ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), Dust, 1, 4f, 0.4f, 0.12f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Charge;
                        timer = 2.4f;
                        hitThisCharge = false;
                        ricochets = enraged ? 1 : 0;
                        anim.Play("charge");
                        ArenaFX.Shake(0.25f);
                    }
                    break;

                case Mode.Charge:
                    vx = facing * chargeSpeed * Pace;
                    ArenaFX.Afterimage(body, new Color(0.8f, 0.45f, 1f, 0.3f), 0.15f);
                    if (Random.value < 0.6f)
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.5f), Dust, 2, 4f, 0.4f, 0.14f);
                    if (!hitThisCharge)
                    {
                        var r = HitPlayerInBox(Center + new Vector2(facing * 0.8f, 0f), new Vector2(2.6f, 1.6f), damage, 12f);
                        if (r != HitResult.Ignored) hitThisCharge = true;
                        if (r == HitResult.Parried) break;
                    }
                    if (WallAhead(1.6f))
                    {
                        var wallPoint = Center + new Vector2(facing * 1.4f, 0f);
                        ArenaFX.Shake(0.7f);
                        ArenaFX.Burst(wallPoint, new Color(0.6f, 0.6f, 0.65f), 26, 9f, 0.18f, 0.8f, 3f);
                        Rockfall(transform.position.x + facing * Random.Range(-3f, 1f), 0.25f);
                        if (ricochets > 0)
                        {
                            // bounces off and comes straight back
                            ricochets--;
                            facing = -facing;
                            hitThisCharge = false;
                            timer = 2.4f;
                            ArenaFX.HitStop(0.06f);
                            ArenaFX.DamageNumber(Center + Vector2.up * 1.6f, "!!", new Color(1f, 0.5f, 0.4f), 1.1f);
                        }
                        else
                        {
                            // dazed by the impact: open to a critical
                            vx = 0f;
                            Break(wallDaze);
                        }
                    }
                    else if (timer <= 0f)
                    {
                        mode = Mode.Recover;
                        timer = 0.5f;
                    }
                    break;

                case Mode.LeapCrouch:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + Vector2.down * 0.5f, Purple, 1, 2f, 0.08f, 0.25f, 0f);
                    if (timer <= 0f) Launch();
                    break;

                case Mode.Leap:
                {
                    airTime += dt;
                    ArenaFX.Afterimage(body, new Color(0.75f, 0.45f, 1f, 0.35f), 0.2f);
                    // tilt nose-up while rising, nose-down coming in
                    float vy = rb.linearVelocity.y;
                    if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(vy * 1.4f, -25f, 25f) * facing);
                    if (airTime > 0.2f && vy <= 0.1f && OnGround(0.25f)) Slam();
                    else if (airTime > leapTime + 1.2f) Slam();
                    break;
                }

                case Mode.VolleyTell:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        Volley();
                        mode = Mode.Recover;
                        timer = 0.8f / Pace;
                    }
                    break;

                case Mode.RainTell:
                    vx = 0f;
                    // acid boils up out of her back
                    if (Random.value < 0.6f) ArenaFX.Spray(Center + Vector2.up * 0.8f, Vector2.up, Acid, 1, 5f, 0.4f, 0.14f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        AcidRain();
                        mode = Mode.Recover;
                        timer = 1f / Pace;
                    }
                    break;

                case Mode.BroodTell:
                    vx = 0f;
                    foreach (var spot in EggSpots())
                        if (Random.value < 0.3f) ArenaFX.Burst(spot + Vector2.up * 0.4f, Acid, 1, 2f, 0.08f, 0.3f, 1f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        Brood();
                        mode = Mode.Recover;
                        timer = 0.8f;
                    }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    anim.Play("walk");
                    if (timer <= 0f)
                    {
                        if (leapsLeft > 0 && PlayerAlive) { StartLeap(); break; }
                        mode = Mode.Walk;
                        timer = Random.Range(0.6f, 1.2f) / Pace;
                    }
                    break;
            }
        }

        void ChooseAttack()
        {
            float dist = Mathf.Abs(ToPlayer.x);
            bool sameLevel = Mathf.Abs(ToPlayer.y) < 2.5f;
            bool canBrood = broodTimer <= 0f && Health < maxHealth * 0.8f && (ArenaDirector.Instance == null || ArenaDirector.Instance.Alive.Count < 4);
            float roll = Random.value;
            Mode next;
            if (simple)
            {
                // Charge (she dazes herself on the wall), Seismic Leap (jump the shockwaves) and Acid Volley
                if (!sameLevel || dist > 8f) next = roll < 0.6f ? Mode.LeapCrouch : Mode.VolleyTell;
                else next = roll < 0.4f ? Mode.ChargeTell : roll < 0.7f ? Mode.LeapCrouch : Mode.VolleyTell;
                if (next == lastAttack) next = next == Mode.ChargeTell ? Mode.VolleyTell : Mode.ChargeTell;
            }
            else if (canBrood && roll < 0.25f) next = Mode.BroodTell;
            else if (!sameLevel) next = roll < 0.55f ? Mode.LeapCrouch : roll < 0.8f ? Mode.RainTell : Mode.VolleyTell;
            else if (dist > 8f) next = roll < 0.45f ? Mode.ChargeTell : roll < 0.75f ? Mode.LeapCrouch : Mode.RainTell;
            else next = roll < 0.35f ? Mode.ChargeTell : roll < 0.6f ? Mode.LeapCrouch : roll < 0.8f ? Mode.VolleyTell : Mode.RainTell;
            if (next == lastAttack && next != Mode.LeapCrouch) next = sameLevel ? Mode.LeapCrouch : Mode.VolleyTell;
            lastAttack = next;

            switch (next)
            {
                case Mode.ChargeTell:
                    mode = next;
                    timer = DelayedTell(chargeTell, 0.4f) * (enraged ? 0.8f : 1f);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.LeapCrouch:
                    leapsLeft = enraged && Random.value < 0.45f ? 1 : 0;
                    StartLeap();
                    break;
                case Mode.VolleyTell:
                    mode = next;
                    timer = Tell(volleyTell);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.RainTell:
                    mode = next;
                    timer = Tell(rainTell);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                default:
                    mode = Mode.BroodTell;
                    timer = Tell(broodTell);
                    anim.Play("tell");
                    BeginTell(timer);
                    broodTimer = broodCooldown;
                    break;
            }
        }

        // ---------- Seismic Leap ----------

        void StartLeap()
        {
            if (leapsLeft > 0 && mode == Mode.Recover) leapsLeft--;
            mode = Mode.LeapCrouch;
            timer = Tell(leapTell);
            vx = 0f;
            anim.Play("tell");
            BeginTell(timer);
        }

        void Launch()
        {
            EndTell();
            var b = Bounds;
            float targetX = PlayerAlive ? Mathf.Clamp(player.transform.position.x, b.xMin + 2f, b.xMax - 2f) : transform.position.x;
            float t = leapTime / Pace;
            float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
            vx = (targetX - transform.position.x) / t;
            rb.linearVelocity = new Vector2(vx, 0.5f * g * t);
            FacePlayer();
            mode = Mode.Leap;
            airTime = 0f;
            anim.Play("charge");
            WarnSpot(GroundBelow(new Vector2(targetX, transform.position.y + 1f)), 1.6f, t, Danger);
            ArenaFX.Burst(transform.position, Dust, 20, 7f, 0.14f, 0.5f, 1f);
            ArenaFX.Shake(0.25f);
        }

        void Slam()
        {
            vx = 0f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (visual != null) visual.localRotation = Quaternion.identity;
            var feet = (Vector2)transform.position;
            anim.Play("walk");
            ArenaFX.Shake(0.9f);
            ArenaFX.HitStop(0.08f);
            ArenaFX.Burst(feet + Vector2.up * 0.3f, Dust, 34, 10f, 0.16f, 0.7f, 3f);
            ArenaFX.FlatRing(feet + Vector2.up * 0.1f, new Color(0.8f, 0.5f, 1f, 0.9f), 0.5f, 7f, 0.35f);
            ArenaFX.Ring(feet + Vector2.up * 0.5f, new Color(0.8f, 0.5f, 1f, 0.7f), 0.5f, 4.5f, 0.3f);
            CameraFollow2D.Zoom(0.94f, 0.2f);
            // crushed under her
            HitPlayerInBox(feet + new Vector2(0f, 0.8f), new Vector2(3.6f, 1.8f), Mathf.RoundToInt(damage * 1.3f), 13f);
            Shockwaves(feet, shockSpeed * Pace, shockRange, Purple, Mathf.RoundToInt(damage * 0.7f));
            // the whole nest shakes and rocks come down around you
            float around = PlayerAlive ? player.transform.position.x : feet.x;
            int count = rocks + (enraged ? 3 : 0);
            for (int i = 0; i < count; i++)
                Rockfall(around + Random.Range(-9f, 9f), 0.35f + i * 0.13f);
            Rockfall(around, 0.5f); // one right on top of you
            mode = Mode.Recover;
            timer = (enraged ? 0.55f : 0.9f);
        }

        // ---------- acid ----------

        void Volley()
        {
            if (blobPrefab == null || !PlayerAlive) return;
            int count = enraged ? 5 : 3;
            var mouth = Center + new Vector2(facing * 1.3f, 0.5f);
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * 2.2f;
                var blob = Instantiate(blobPrefab, mouth, Quaternion.identity);
                blob.Launch((Vector2)player.transform.position + new Vector2(offset, 0.5f), 1f + i * 0.08f, Mathf.RoundToInt(damage * 0.7f));
            }
            ArenaFX.Burst(mouth, bloodColor, 14, 5f, 0.14f, 0.4f, 1f);
            ArenaFX.Shake(0.2f);
        }

        void AcidRain()
        {
            if (blobPrefab == null || !PlayerAlive) return;
            var b = Bounds;
            var from = Center + Vector2.up * 1f;
            int count = rainBlobs + (enraged ? 3 : 0);
            for (int i = 0; i < count; i++)
            {
                // one right on the knight, the rest spread around (and across the whole nest)
                float x = i == 0 ? player.transform.position.x : i < count / 2 + 1 ? player.transform.position.x + Random.Range(-5f, 5f) : Random.Range(b.xMin + 1f, b.xMax - 1f);
                x = Mathf.Clamp(x, b.xMin + 0.5f, b.xMax - 0.5f);
                var target = GroundBelow(new Vector2(x, b.yMax));
                float flight = Random.Range(1.05f, 1.55f);
                var blob = Instantiate(blobPrefab, from, Quaternion.identity);
                blob.Launch(target + Vector2.up * 0.3f, flight, Mathf.RoundToInt(damage * 0.65f));
                WarnSpot(target, 0.55f, flight, new Color(0.6f, 1f, 0.35f, 0.9f));
            }
            ArenaFX.Spray(from, Vector2.up, Acid, 20, 10f, 0.35f, 0.16f);
            ArenaFX.Glow(from, new Color(0.6f, 1f, 0.35f, 0.6f), 4f, 0.4f);
            ArenaFX.Shake(0.35f);
        }

        // ---------- brood ----------

        IEnumerable<Vector2> EggSpots()
        {
            var props = ArenaDirector.Instance != null ? ArenaDirector.Instance.bossProps : null;
            if (props != null && props.Length > 0)
            {
                foreach (var t in props) if (t != null) yield return t.position;
                yield break;
            }
            yield return Center + new Vector2(-2.5f, 2f);
            yield return Center + new Vector2(2.5f, 2f);
        }

        void Brood()
        {
            var director = ArenaDirector.Instance;
            if (director == null) return;
            var spots = new List<Vector2>(EggSpots());
            // hatch the nests furthest from the knight first, so the brood comes from all sides
            if (PlayerAlive) spots.Sort((a, b) => Vector2.Distance(b, player.Center).CompareTo(Vector2.Distance(a, player.Center)));
            int count = Mathf.Min(spots.Count, enraged ? 3 : 2);
            for (int i = 0; i < count; i++)
            {
                var spot = spots[i];
                ArenaFX.Burst(spot + Vector2.up * 0.4f, Acid, 18, 6f, 0.13f, 0.5f, 2f);
                ArenaFX.Ring(spot + Vector2.up * 0.4f, new Color(0.6f, 1f, 0.4f, 0.8f), 0.3f, 2.5f, 0.3f);
                var prefab = hatchlingPrefab != null && i % 2 == 1 ? hatchlingPrefab : summonPrefab;
                director.SpawnBug(prefab, spot + Vector2.up * (prefab == summonPrefab ? 1.2f : 0.1f));
            }
            ArenaFX.Shake(0.35f);
        }

        // ---------- reactions ----------

        protected override void OnEnrage()
        {
            base.OnEnrage();
            ArenaFX.Ring(Center, new Color(0.8f, 0.45f, 1f, 0.9f), 1f, 10f, 0.6f);
            for (int i = 0; i < 4; i++) Rockfall(transform.position.x + Random.Range(-10f, 10f), 0.2f + i * 0.2f);
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
        }

        protected override void OnStagger()
        {
            EndTell();
            if (visual != null) visual.localRotation = Quaternion.identity;
            if (stunTimer > 0f) { mode = Mode.Recover; leapsLeft = 0; }
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
