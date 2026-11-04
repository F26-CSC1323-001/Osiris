using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Boss for floor 40, fought in her Web (a chasm crossed by silk bridges). A giant spider:
    ///   Venom Bite   — up close: rears back, then two quick bites.
    ///   Web Snare    — spits a line of silk. If it sticks she reels you in for a poisoned bite: roll to
    ///                  cut free (or block it).
    ///   Ceiling Drop — climbs a thread up out of sight and crawls across the ceiling. A shadow and
    ///                  falling silk mark where she'll land, then she drops. She's open after landing.
    ///   Cocoon Spit  — lobs sticky globs. They leave webs on the floor that slow you; get hit by two
    ///                  in a row and you're wrapped in a cocoon: mash to break out before she arrives.
    ///   Enraged      — cuts the web bridges (the arena shrinks), spiderlings hatch from the egg sacs,
    ///                  and she sometimes drops twice.
    /// Super armor: hits don't interrupt her, but parries and stance breaks do.
    /// </summary>
    public class SilkWidow : BossBug
    {
        [Header("Silk")]
        [Tooltip("A strand of silk, 1 unit long at scale 1 (pivot at the left end).")]
        public Sprite silkSprite;
        public Sprite globSprite;
        public WebPatch patchPrefab;
        public BugEnemy spiderlingPrefab;

        [Header("Movement")]
        public float walkSpeed = 3.4f;

        [Header("Venom Bite")]
        public float biteTell = 0.45f;

        [Header("Web Snare")]
        public float snareTell = 0.55f;
        public float snareSpeed = 19f;
        public float reelSpeed = 9f;

        [Header("Cocoon Spit")]
        public float spitTell = 0.5f;
        public int globs = 3;
        public float wrapWindow = 4f;

        [Header("Ceiling Drop")]
        public float climbSpeed = 16f;
        public float crawlSpeed = 11f;
        public float dropTell = 0.6f;
        public float dropSpeed = 34f;
        public float splayTime = 1.1f;

        [Header("Brood")]
        public float broodCooldown = 13f;

        enum Mode
        {
            Intro, Stalk, BiteTell, Bite, SnareTell, Snaring, SpitTell, Climb, Crawl, DropTell, Drop, Splayed,
            CocoonRush, CutBridges, Recover,
        }
        Mode mode = Mode.Intro, lastAttack;
        float timer, vx, ceilingY, groundY, wrapTimer, broodTimer, strandTimer;
        int bites, wrapHits, dropsLeft;
        bool onCeiling;
        Vector2 flyVelocity;
        SpriteRenderer thread;
        BoxCollider2D box;
        Vector2 boxOffset;

        static readonly Color Silk = new Color(0.95f, 0.95f, 1f);
        static readonly Color Venom = new Color(0.65f, 1f, 0.4f);
        static readonly Color Danger = new Color(1f, 0.35f, 0.3f, 0.9f);
        static readonly Color Dust = new Color(0.55f, 0.5f, 0.6f);

        float Pace => enraged ? 1.2f : 1f;
        bool Flying => rb != null && rb.bodyType == RigidbodyType2D.Kinematic;
        Vector2 Mouth => (Vector2)transform.position + new Vector2(facing * 1.6f, 0.9f);
        Vector2 Spinnerets => (Vector2)transform.position + new Vector2(-facing * 1.5f, 1.2f);

        protected override void Awake()
        {
            base.Awake();
            box = GetComponent<BoxCollider2D>();
            if (box != null) boxOffset = box.offset;
        }

        protected override void Start()
        {
            base.Start();
            snare = new Snare(this, silkSprite);
            var go = new GameObject("WidowThread");
            thread = go.AddComponent<SpriteRenderer>();
            thread.sprite = silkSprite;
            thread.sortingOrder = 9;
            thread.enabled = false;
            timer = 1.4f;
            anim.Play("tell");
            ArenaFX.Shake(0.5f);
            ArenaFX.Ring(Center, new Color(0.8f, 0.3f, 0.35f, 0.9f), 1f, 8f, 0.5f);
            for (int i = 0; i < 12; i++)
                ArenaFX.Spray(Center + Vector2.up, Random.insideUnitCircle, Silk, 2, 6f, 0.5f, 0.08f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (thread != null) Destroy(thread.gameObject);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            wrapTimer -= dt;
            broodTimer -= dt;
            if (wrapTimer <= 0f) wrapHits = 0;
            TickWaves(dt);

            switch (mode)
            {
                case Mode.Intro:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f) { mode = Mode.Stalk; timer = 0.6f; }
                    break;

                case Mode.Stalk:
                {
                    if (!PlayerAlive) { vx = 0f; anim.Play("walk"); break; }
                    FacePlayer();
                    float dist = Mathf.Abs(ToPlayer.x);
                    vx = WallAhead(1.8f) || dist < 2.2f ? 0f : facing * walkSpeed * Pace;
                    anim.Play("walk");
                    if (enraged && broodTimer <= 0f) Brood();
                    if (timer <= 0f) ChooseAttack(dist, Mathf.Abs(ToPlayer.y) < 2f);
                    break;
                }

                case Mode.BiteTell:
                    vx = -facing * 0.6f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Bite;
                        bites = enraged ? 3 : 2;
                        timer = 0f;
                    }
                    break;

                case Mode.Bite:
                    vx = Mathf.MoveTowards(vx, 0f, 40f * dt);
                    if (timer <= 0f)
                    {
                        if (bites <= 0) { mode = Mode.Recover; timer = 0.6f / Pace; anim.Play("walk"); break; }
                        bites--;
                        FacePlayer();
                        anim.Play("bite", true);
                        vx = facing * 8f;
                        var spot = (Vector2)transform.position + new Vector2(facing * 2f, 0.9f);
                        var r = HitPlayerInBox(spot, new Vector2(2f, 1.6f), damage, 7f);
                        if (r == HitResult.Damaged) player.ApplyPoison(3, 2.5f);
                        ArenaFX.Burst(spot, Venom, 5, 4f, 0.08f, 0.25f, 0.5f);
                        ArenaFX.Shake(0.15f);
                        timer = 0.3f;
                    }
                    break;

                case Mode.SnareTell:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.6f) ArenaFX.Burst(Mouth, Silk, 1, 2f, 0.06f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Snaring;
                        anim.Play("spit", true);
                        snare.Fire(Mouth, PlayerAlive ? player.Center : Mouth + new Vector2(facing * 5f, 0f), snareSpeed, reelSpeed);
                        timer = 4f;
                    }
                    break;

                case Mode.Snaring:
                {
                    vx = 0f;
                    var s = snare.Tick(Mouth, dt);
                    if (s == SnareState.Caught)
                    {
                        // reeled all the way in: the bite
                        player.Release(Vector2.zero);
                        anim.Play("bite", true);
                        var r = player.TakeHit(Mathf.RoundToInt(damage * 1.5f), Center, 10f);
                        if (r == HitResult.Damaged) player.ApplyPoison(4, 3f);
                        ArenaFX.Burst(Mouth, Venom, 12, 6f, 0.1f, 0.3f, 0.5f);
                        ArenaFX.HitStop(0.1f);
                        ArenaFX.Shake(0.4f);
                        snare.Cancel();
                        mode = Mode.Recover;
                        timer = 0.7f;
                    }
                    else if (s == SnareState.Missed || s == SnareState.Escaped || timer <= 0f)
                    {
                        snare.Cancel();
                        mode = Mode.Recover;
                        timer = s == SnareState.Escaped ? 0.9f : 0.5f; // a little opening after you cut free
                        anim.Play("walk");
                    }
                    break;
                }

                case Mode.SpitTell:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.5f) ArenaFX.Burst(Spinnerets, Silk, 1, 2f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        Spit();
                        mode = Mode.Recover;
                        timer = 0.6f / Pace;
                    }
                    break;

                case Mode.Climb:
                {
                    // up a thread to the ceiling
                    flyVelocity = new Vector2(0f, climbSpeed * Pace);
                    DrawThread(new Vector2(transform.position.x, ceilingY), (Vector2)transform.position + Vector2.up * 1.3f);
                    if (transform.position.y + 1.4f >= ceilingY - 0.1f)
                    {
                        // flip over and hang from the ceiling
                        onCeiling = true;
                        SetUpsideDown(true);
                        transform.position = new Vector3(transform.position.x, ceilingY, 0f);
                        rb.position = transform.position;
                        thread.enabled = false;
                        mode = Mode.Crawl;
                        timer = 1.4f;
                    }
                    break;
                }

                case Mode.Crawl:
                {
                    // scuttles along the ceiling to above the knight
                    float tx = PlayerAlive ? player.transform.position.x : transform.position.x;
                    flyVelocity = new Vector2(Mathf.Clamp((tx - transform.position.x) * 5f, -crawlSpeed, crawlSpeed) * Pace, 0f);
                    anim.Play("walk");
                    SilkDrip();
                    if (Mathf.Abs(tx - transform.position.x) < 0.4f || timer <= 0f)
                    {
                        mode = Mode.DropTell;
                        timer = Tell(dropTell);
                        groundY = LandingY(transform.position.x);
                        WarnSpot(new Vector2(transform.position.x, groundY), 1.6f, timer, Danger);
                        BeginTell(timer);
                    }
                    break;
                }

                case Mode.DropTell:
                {
                    // hangs there, tracking you a little
                    float tx = PlayerAlive ? player.transform.position.x : transform.position.x;
                    flyVelocity = new Vector2(Mathf.Clamp((tx - transform.position.x) * 2f, -3f, 3f), 0f);
                    groundY = LandingY(transform.position.x);
                    SilkDrip();
                    if (Random.value < 0.5f) ArenaFX.Burst(new Vector2(transform.position.x, groundY + 0.1f), Danger, 1, 2f, 0.08f, 0.25f, 0.5f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        // let go: right herself and fall
                        SetUpsideDown(false);
                        transform.position = new Vector3(transform.position.x, ceilingY - 2.2f, 0f);
                        rb.position = transform.position;
                        onCeiling = false;
                        flyVelocity = new Vector2(0f, -dropSpeed);
                        mode = Mode.Drop;
                        anim.Play("splay");
                        strandTimer = 0f;
                    }
                    break;
                }

                case Mode.Drop:
                    ArenaFX.Afterimage(body, new Color(0.8f, 0.4f, 0.5f, 0.35f), 0.15f);
                    DrawThread(new Vector2(transform.position.x, ceilingY), (Vector2)transform.position + Vector2.up * 1.3f);
                    if (transform.position.y <= groundY + 0.05f)
                    {
                        transform.position = new Vector3(transform.position.x, groundY, 0f);
                        rb.position = transform.position;
                        Land();
                        Slam();
                    }
                    break;

                case Mode.Splayed:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        if (dropsLeft > 0 && PlayerAlive) { dropsLeft--; StartClimb(); break; }
                        mode = Mode.Recover;
                        timer = 0.3f;
                        anim.Play("walk");
                    }
                    break;

                case Mode.CocoonRush:
                {
                    // the knight is wrapped up: she rushes in for the bite
                    if (!PlayerAlive || !player.IsHeldBy(this)) { mode = Mode.Recover; timer = 0.6f; break; }
                    FacePlayer();
                    vx = facing * walkSpeed * 2.4f;
                    anim.Play("walk");
                    if (Mathf.Abs(ToPlayer.x) < 2.1f)
                    {
                        player.Release(Vector2.zero);
                        anim.Play("bite", true);
                        var r = player.TakeHit(Mathf.RoundToInt(damage * 1.6f), Center, 11f);
                        if (r == HitResult.Damaged) player.ApplyPoison(4, 3f);
                        ArenaFX.Burst(Mouth, Venom, 14, 6f, 0.1f, 0.3f, 0.5f);
                        ArenaFX.Shake(0.45f);
                        ArenaFX.HitStop(0.1f);
                        mode = Mode.Recover;
                        timer = 0.7f;
                    }
                    else if (timer <= 0f) { player.Release(Vector2.zero); mode = Mode.Recover; timer = 0.5f; }
                    break;
                }

                case Mode.CutBridges:
                    vx = 0f;
                    if (timer <= 0f)
                    {
                        EndTell();
                        CutBridges();
                        Brood();
                        mode = Mode.Recover;
                        timer = 0.8f;
                    }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f)
                    {
                        mode = Mode.Stalk;
                        timer = Random.Range(0.4f, 0.9f) / Pace;
                    }
                    break;
            }
        }

        // ---------- choosing ----------

        void ChooseAttack(float dist, bool sameLevel)
        {
            float roll = Random.value;
            Mode next;
            if (!sameLevel) next = roll < 0.55f ? Mode.Climb : Mode.SnareTell;
            else if (dist < 3.2f) next = roll < 0.45f ? Mode.BiteTell : roll < 0.65f ? Mode.SpitTell : roll < 0.85f ? Mode.Climb : Mode.SnareTell;
            else next = roll < 0.4f ? Mode.SnareTell : roll < 0.7f ? Mode.SpitTell : Mode.Climb;
            if (next == lastAttack) next = next == Mode.Climb ? Mode.SnareTell : Mode.Climb;
            lastAttack = next;
            vx = 0f;
            switch (next)
            {
                case Mode.BiteTell:
                    mode = next;
                    timer = DelayedTell(biteTell, 0.5f);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.SnareTell:
                    mode = next;
                    timer = Tell(snareTell);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.SpitTell:
                    mode = next;
                    timer = Tell(spitTell);
                    anim.Play("spit");
                    BeginTell(timer);
                    break;
                default:
                    dropsLeft = enraged && Random.value < 0.5f ? 1 : 0;
                    StartClimb();
                    break;
            }
        }

        // ---------- ceiling drop ----------

        void StartClimb()
        {
            var hit = Physics2D.Raycast(Center, Vector2.up, 40f, ArenaLayers.SolidMask);
            ceilingY = hit ? hit.point.y : Bounds.yMax + 2f;
            mode = Mode.Climb;
            vx = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            flyVelocity = Vector2.zero;
            anim.Play("walk");
            ArenaFX.Burst(transform.position, Dust, 12, 5f, 0.12f, 0.4f, 1f);
        }

        /// <summary>Where she lands: the floor at the knight's level (she drops straight through the bridges above him).</summary>
        float LandingY(float x)
        {
            float from = ceilingY - 2.5f;
            if (PlayerAlive) from = Mathf.Min(from, player.transform.position.y + 0.6f);
            return GroundBelow(new Vector2(x, from)).y;
        }

        void SetUpsideDown(bool flip)
        {
            if (visual != null) visual.localScale = new Vector3(visual.localScale.x, flip ? -1f : 1f, 1f);
            if (box != null) box.offset = flip ? new Vector2(boxOffset.x, -boxOffset.y) : boxOffset;
        }

        void Land()
        {
            flyVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            thread.enabled = false;
            SetUpsideDown(false);
            onCeiling = false;
        }

        void Slam()
        {
            var feet = (Vector2)transform.position;
            mode = Mode.Splayed;
            timer = splayTime;
            vx = 0f;
            anim.Play("splay");
            ArenaFX.Shake(0.8f);
            ArenaFX.HitStop(0.08f);
            ArenaFX.Burst(feet + Vector2.up * 0.3f, Dust, 28, 9f, 0.14f, 0.6f, 3f);
            ArenaFX.FlatRing(feet + Vector2.up * 0.1f, new Color(0.9f, 0.5f, 0.55f, 0.9f), 0.5f, 6f, 0.3f);
            CameraFollow2D.Zoom(0.94f, 0.2f);
            HitPlayerInBox(feet + Vector2.up * 0.9f, new Vector2(3.8f, 2f), Mathf.RoundToInt(damage * 1.4f), 12f);
            Shockwaves(feet, 11f * Pace, 6f, Silk, Mathf.RoundToInt(damage * 0.6f));
        }

        void DrawThread(Vector2 top, Vector2 bottom)
        {
            if (thread == null) return;
            thread.enabled = true;
            var d = bottom - top;
            thread.transform.position = top;
            thread.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            thread.transform.localScale = new Vector3(d.magnitude, 1f, 1f);
        }

        /// <summary>Silk drifting down from where she hangs (so you can track her off-screen).</summary>
        void SilkDrip()
        {
            if ((strandTimer -= Time.deltaTime) > 0f) return;
            strandTimer = 0.05f;
            var p = new Vector2(transform.position.x + Random.Range(-1.2f, 1.2f), transform.position.y - 1.5f);
            ArenaFX.Spray(p, Vector2.down, Silk, 1, 2f, 0.2f, 0.07f);
        }

        // ---------- cocoon spit ----------

        void Spit()
        {
            if (!PlayerAlive) return;
            anim.Play("spit", true);
            int count = globs + (enraged ? 2 : 0);
            var from = Spinnerets + Vector2.up * 0.4f;
            for (int i = 0; i < count; i++)
            {
                // one aimed right at the knight, the rest spread around
                float offset = i == 0 ? 0f : (i % 2 == 0 ? 1 : -1) * (1.4f + (i - 1) / 2 * 1.6f);
                var target = (Vector2)player.transform.position + new Vector2(offset, 0.6f);
                float flight = 0.75f + i * 0.08f;
                const float g = 22f;
                var v = new Vector2((target.x - from.x) / flight, (target.y - from.y + 0.5f * g * flight * flight) / flight);
                var shot = Shoot(globSprite, from, v, 0, g, Color.white, new Color(1f, 1f, 1f, 0.2f), 1.1f, 0.4f, false, 300f, 3f);
                if (shot == null) continue;
                shot.OnTouchPlayer = p => { GlobHit(p, shot.Position); return true; };
                shot.OnLand = at => LeaveWeb(at);
            }
            ArenaFX.Burst(from, Silk, 10, 5f, 0.08f, 0.3f, 1f);
        }

        void GlobHit(ArenaPlayer p, Vector2 at)
        {
            ArenaFX.Burst(p.Center, Silk, 10, 4f, 0.09f, 0.35f, 0.5f);
            if (wrapHits >= 1 && wrapTimer > 0f)
            {
                // a second glob while still sticky: wrapped up in a cocoon. Mash to break out before she gets there.
                if (!p.TryGrab(this, at, 7, false, true, true)) return;
                wrapHits = 0;
                if (!Flying && (mode == Mode.Stalk || mode == Mode.Recover || mode == Mode.SpitTell))
                {
                    EndTell();
                    mode = Mode.CocoonRush;
                    timer = 3.5f;
                }
                return;
            }
            if (p.TakeHit(Mathf.RoundToInt(damage * 0.4f), at, 3f) != HitResult.Damaged) return;
            wrapHits = 1;
            wrapTimer = wrapWindow;
            ArenaFX.DamageNumber(p.Center + Vector2.up * 1.2f, "STICKY", Silk, 0.6f);
        }

        void LeaveWeb(Vector2 at)
        {
            if (patchPrefab == null) return;
            var floor = GroundBelow(at + Vector2.up * 0.3f, 2f);
            Instantiate(patchPrefab, floor + Vector2.up * 0.02f, Quaternion.identity);
            ArenaFX.Burst(floor + Vector2.up * 0.2f, Silk, 6, 3f, 0.08f, 0.3f, 1f);
        }

        // ---------- brood & bridges ----------

        void Brood()
        {
            broodTimer = broodCooldown;
            var director = ArenaDirector.Instance;
            if (director == null || spiderlingPrefab == null) return;
            if (director.Alive.Count > 4) return;
            var spots = new List<Vector2>();
            if (director.bossProps != null)
                foreach (var t in director.bossProps) if (t != null) spots.Add(t.position);
            if (spots.Count == 0) spots.Add(Center + new Vector2(-2f, 0f));
            // the sacs farthest from the knight hatch
            if (PlayerAlive) spots.Sort((a, b) => Vector2.Distance(b, player.Center).CompareTo(Vector2.Distance(a, player.Center)));
            for (int i = 0; i < Mathf.Min(2, spots.Count); i++)
            {
                ArenaFX.Burst(spots[i] + Vector2.up * 0.4f, Silk, 14, 5f, 0.1f, 0.4f, 1.5f);
                director.SpawnBug(spiderlingPrefab, spots[i] + Vector2.up * 0.1f);
            }
        }

        void CutBridges()
        {
            var bridges = new List<WebBridge>(WebBridge.All);
            // keeps the lowest ones; cuts about half of the rest
            bridges.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            int cut = Mathf.CeilToInt(bridges.Count * 0.5f);
            for (int i = 0; i < cut && i < bridges.Count; i++)
                bridges[i].Cut();
            ArenaFX.Shake(0.6f);
            ArenaFX.DamageNumber(Center + Vector2.up * 2.2f, "THE WEB FALLS", Danger, 0.9f);
        }

        protected override void OnEnrage()
        {
            base.OnEnrage();
            if (Flying || mode == Mode.Snaring || mode == Mode.CocoonRush) return;
            // screeches, then cuts the bridges
            mode = Mode.CutBridges;
            timer = 0.8f;
            vx = 0f;
            anim.Play("tell");
            BeginTell(timer);
            ArenaFX.Darken(0.5f, 1.2f);
        }

        // ---------- reactions ----------

        protected override void OnStagger()
        {
            EndTell();
            snare?.Cancel();
            if (PlayerAlive && player.IsHeldBy(this)) player.Release(Vector2.zero);
            if (Flying)
            {
                // knocked off the ceiling
                transform.position = new Vector3(transform.position.x, onCeiling ? ceilingY - 2.2f : transform.position.y, 0f);
                rb.position = transform.position;
                Land();
                anim.Play("splay");
            }
            if (stunTimer > 0f) { mode = Mode.Recover; dropsLeft = 0; }
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
            anim.Play("walk");
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
