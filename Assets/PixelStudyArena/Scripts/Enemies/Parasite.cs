using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Final boss, phase 2: the thing that was inside the Infested Knight, torn loose in the ruins of
    /// the throne room. A swollen horror that has taken something from every boss before it:
    ///   Crushing Leap — (the Brood Mother) leaps at you; the landing sends shockwaves and brings rocks
    ///                   down. Enraged, it leaps twice.
    ///   Stinger Storm — (the Hornet Queen) rises on its wings and sprays a spiral of stingers.
    ///   Scythe Waves  — (the Reaper Mantis) blades of wind skimming the floor; a high one follows
    ///                   when enraged.
    ///   Web Snare     — (the Silk Widow) a line of silk that reels you in for a bite. Roll to cut free.
    ///   Brood Burst   — spiderlings and ticks burst out of its back.
    /// Super armor: hits don't interrupt it, but parries and stance breaks do.
    /// </summary>
    public class Parasite : BossBug
    {
        public Stinger stingerPrefab;
        public Sprite scytheSprite;
        public Sprite silkSprite;
        public BugEnemy[] broodPrefabs;

        public float walkSpeed = 3f;
        public float leapTime = 0.8f;
        public float stormTime = 1.6f;

        enum Mode { Intro, Stalk, LeapCrouch, Leap, Rise, Storm, Descend, WaveTell, SnareTell, Snaring, BroodTell, Recover }
        Mode mode = Mode.Intro, lastAttack;
        float timer, vx, airTime, stormFire, stormAngle, broodTimer, hoverY;
        int leapsLeft, wavesLeft;
        bool roared;
        Vector2 flyVelocity;

        static readonly Color Violet = new Color(0.72f, 0.35f, 1f);
        static readonly Color Ichor = new Color(0.55f, 0.95f, 0.5f);
        static readonly Color Danger = new Color(1f, 0.3f, 0.45f, 0.9f);
        static readonly Color Dust = new Color(0.6f, 0.55f, 0.62f);

        float Pace => enraged ? 1.25f : 1f;
        bool Flying => rb != null && rb.bodyType == RigidbodyType2D.Kinematic;
        Vector2 Mouth => (Vector2)transform.position + new Vector2(facing * 2.3f, 2.2f);

        protected override void Start()
        {
            base.Start();
            snare = new Snare(this, silkSprite);
            mode = Mode.Intro;
            timer = 2.2f;
            anim.Play("tell");
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null)
            {
                ui.hud.SetObjective("DEFEAT THE PARASITE");
                ui.hud.ShowBanner("THE PARASITE", new Color(0.8f, 0.45f, 1f), 2.4f);
            }
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
                    if (!roared && timer < 1.6f)
                    {
                        roared = true;
                        ImpactFrame.Play(Center, ImpactFrame.Style.Void);
                        ArenaFX.Shake(1f);
                        ArenaFX.Ring(Center, new Color(0.75f, 0.4f, 1f, 0.9f), 1f, 12f, 0.6f);
                        CameraFollow2D.Zoom(0.9f, 0.5f);
                        for (int i = 0; i < 4; i++) Rockfall(transform.position.x + Random.Range(-10f, 10f), 0.3f + i * 0.25f);
                    }
                    if (timer <= 0f) { mode = Mode.Stalk; timer = 0.6f; }
                    break;

                case Mode.Stalk:
                {
                    if (!PlayerAlive) { vx = 0f; anim.Play("idle"); break; }
                    FacePlayer();
                    float dist = Mathf.Abs(ToPlayer.x);
                    vx = WallAhead(2.4f) || dist < 3f ? 0f : facing * walkSpeed * Pace;
                    anim.Play(Mathf.Abs(vx) > 0.1f ? "walk" : "idle");
                    if (timer <= 0f) ChooseAttack(dist, Mathf.Abs(ToPlayer.y) < 2.5f);
                    break;
                }

                case Mode.LeapCrouch:
                    vx = 0f;
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + Vector2.down * 0.8f, Violet, 1, 2f, 0.08f, 0.25f, 0f);
                    if (timer <= 0f) Launch();
                    break;

                case Mode.Leap:
                {
                    airTime += dt;
                    ArenaFX.Afterimage(body, new Color(0.7f, 0.35f, 1f, 0.3f), 0.2f);
                    float vy = rb.linearVelocity.y;
                    if (airTime > 0.2f && vy <= 0.1f && OnGround(0.3f)) Slam();
                    else if (airTime > leapTime + 1.4f) Slam();
                    break;
                }

                case Mode.Rise:
                    flyVelocity = new Vector2(0f, Mathf.Clamp((hoverY - transform.position.y) * 6f, 0f, 14f));
                    anim.Play("fly");
                    if (transform.position.y >= hoverY - 0.15f || timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Storm;
                        timer = stormTime * (enraged ? 1.25f : 1f);
                        stormFire = 0f;
                        flyVelocity = Vector2.zero;
                        ArenaFX.Ring(Center, new Color(1f, 0.8f, 0.35f, 0.9f), 6f, 0.5f, 0.4f);
                    }
                    break;

                case Mode.Storm:
                    flyVelocity = new Vector2(0f, Mathf.Sin(Time.time * 6f) * 0.6f);
                    anim.Play("fly");
                    stormAngle += 230f * dt;
                    if ((stormFire -= dt) <= 0f && stingerPrefab != null)
                    {
                        stormFire = 0.09f;
                        int arms = enraged ? 4 : 3;
                        for (int arm = 0; arm < arms; arm++)
                        {
                            float a = (stormAngle + arm * 360f / arms) * Mathf.Deg2Rad;
                            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            var s = Instantiate(stingerPrefab, Center + d * 1.4f, Quaternion.identity);
                            s.Launch(d, 7.5f, Mathf.RoundToInt(damage * 0.5f));
                        }
                    }
                    if (timer <= 0f)
                    {
                        mode = Mode.Descend;
                        rb.bodyType = RigidbodyType2D.Dynamic;
                        rb.linearVelocity = new Vector2(0f, -10f);
                        airTime = 0f;
                        anim.Play("slam");
                    }
                    break;

                case Mode.Descend:
                    airTime += dt;
                    if ((airTime > 0.1f && OnGround(0.3f)) || airTime > 2f)
                    {
                        var feet = (Vector2)transform.position;
                        ArenaFX.Shake(0.5f);
                        ArenaFX.Burst(feet + Vector2.up * 0.3f, Dust, 20, 7f, 0.13f, 0.5f, 2f);
                        HitPlayerInBox(feet + Vector2.up * 1f, new Vector2(4.4f, 2f), damage, 11f);
                        mode = Mode.Recover;
                        timer = 0.7f;
                        anim.Play("idle");
                    }
                    break;

                case Mode.WaveTell:
                    vx = 0f;
                    FacePlayer();
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + new Vector2(facing * 1.8f, 1.4f), Violet, 1, 2f, 0.07f, 0.2f, 0f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        anim.Play("slam", true);
                        ThrowWave(wavesLeft % 2 == 0 ? 0.55f : 1.9f);
                        if (--wavesLeft > 0) { timer = 0.3f; anim.Play("tell"); }
                        else { mode = Mode.Recover; timer = 0.6f; }
                    }
                    break;

                case Mode.SnareTell:
                    vx = 0f;
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Snaring;
                        anim.Play("spit", true);
                        snare.Fire(Mouth, PlayerAlive ? player.Center : Mouth + new Vector2(facing * 5f, 0f), 18f, 9f);
                        timer = 4f;
                    }
                    break;

                case Mode.Snaring:
                {
                    vx = 0f;
                    var s = snare.Tick(Mouth, dt);
                    if (s == SnareState.Caught)
                    {
                        player.Release(Vector2.zero);
                        anim.Play("spit", true);
                        player.TakeHit(Mathf.RoundToInt(damage * 1.6f), Center, 12f);
                        ArenaFX.Burst(Mouth, Ichor, 14, 6f, 0.1f, 0.3f, 0.5f);
                        ArenaFX.HitStop(0.1f);
                        ArenaFX.Shake(0.45f);
                        snare.Cancel();
                        mode = Mode.Recover;
                        timer = 0.7f;
                    }
                    else if (s == SnareState.Missed || s == SnareState.Escaped || timer <= 0f)
                    {
                        snare.Cancel();
                        mode = Mode.Recover;
                        timer = s == SnareState.Escaped ? 0.9f : 0.5f;
                        anim.Play("idle");
                    }
                    break;
                }

                case Mode.BroodTell:
                    vx = 0f;
                    if (Random.value < 0.5f) ArenaFX.Burst(Center + Vector2.up * 1.2f, Ichor, 1, 3f, 0.1f, 0.3f, 1f);
                    if (timer <= 0f)
                    {
                        EndTell();
                        Brood();
                        mode = Mode.Recover;
                        timer = 0.7f;
                    }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f)
                    {
                        if (leapsLeft > 0 && PlayerAlive) { leapsLeft--; StartLeap(); break; }
                        mode = Mode.Stalk;
                        timer = Random.Range(0.3f, 0.8f) / Pace;
                    }
                    break;
            }
        }

        void ChooseAttack(float dist, bool sameLevel)
        {
            float roll = Random.value;
            Mode next;
            bool canBrood = broodTimer <= 0f && (ArenaDirector.Instance == null || ArenaDirector.Instance.Alive.Count < 4);
            if (canBrood && roll < 0.18f) next = Mode.BroodTell;
            else if (!sameLevel) next = roll < 0.5f ? Mode.LeapCrouch : Mode.Rise;
            else if (dist < 4.5f) next = roll < 0.4f ? Mode.LeapCrouch : roll < 0.6f ? Mode.SnareTell : roll < 0.8f ? Mode.Rise : Mode.WaveTell;
            else next = roll < 0.35f ? Mode.WaveTell : roll < 0.6f ? Mode.SnareTell : roll < 0.8f ? Mode.LeapCrouch : Mode.Rise;
            if (next == lastAttack) next = next == Mode.LeapCrouch ? Mode.WaveTell : Mode.LeapCrouch;
            lastAttack = next;
            vx = 0f;
            switch (next)
            {
                case Mode.LeapCrouch:
                    leapsLeft = enraged && Random.value < 0.5f ? 1 : 0;
                    StartLeap();
                    break;
                case Mode.Rise:
                    mode = Mode.Rise;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.linearVelocity = Vector2.zero;
                    hoverY = Mathf.Min(transform.position.y + 4.5f, Bounds.yMax - 4f);
                    timer = 1.2f;
                    anim.Play("fly");
                    BeginTell(0.5f);
                    ArenaFX.Burst(transform.position, Dust, 18, 6f, 0.13f, 0.5f, 1f);
                    break;
                case Mode.WaveTell:
                    mode = next;
                    wavesLeft = enraged ? 2 : 1;
                    timer = Tell(0.55f);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
                case Mode.SnareTell:
                    mode = next;
                    timer = Tell(0.55f);
                    anim.Play("spit");
                    BeginTell(timer);
                    break;
                default:
                    mode = Mode.BroodTell;
                    broodTimer = 12f;
                    timer = Tell(0.7f);
                    anim.Play("tell");
                    BeginTell(timer);
                    break;
            }
        }

        // ---------- crushing leap ----------

        void StartLeap()
        {
            mode = Mode.LeapCrouch;
            timer = Tell(0.5f);
            vx = 0f;
            anim.Play("tell");
            BeginTell(timer);
        }

        void Launch()
        {
            EndTell();
            var b = Bounds;
            float targetX = PlayerAlive ? Mathf.Clamp(player.transform.position.x, b.xMin + 2.5f, b.xMax - 2.5f) : transform.position.x;
            float t = leapTime / Pace;
            float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
            vx = (targetX - transform.position.x) / t;
            rb.linearVelocity = new Vector2(vx, 0.5f * g * t);
            FacePlayer();
            mode = Mode.Leap;
            airTime = 0f;
            anim.Play("fly");
            WarnSpot(GroundBelow(new Vector2(targetX, transform.position.y + 1f)), 2f, t, Danger);
            ArenaFX.Burst(transform.position, Dust, 22, 7f, 0.14f, 0.5f, 1f);
            ArenaFX.Shake(0.3f);
        }

        void Slam()
        {
            vx = 0f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            var feet = (Vector2)transform.position;
            anim.Play("slam");
            ArenaFX.Shake(1f);
            ArenaFX.HitStop(0.09f);
            ArenaFX.Burst(feet + Vector2.up * 0.3f, Dust, 36, 11f, 0.16f, 0.7f, 3f);
            ArenaFX.FlatRing(feet + Vector2.up * 0.1f, new Color(0.75f, 0.4f, 1f, 0.9f), 0.6f, 8f, 0.35f);
            CameraFollow2D.Zoom(0.93f, 0.2f);
            HitPlayerInBox(feet + Vector2.up * 1f, new Vector2(4.6f, 2.2f), Mathf.RoundToInt(damage * 1.4f), 13f);
            Shockwaves(feet, 12f * Pace, 9f, Violet, Mathf.RoundToInt(damage * 0.7f));
            float around = PlayerAlive ? player.transform.position.x : feet.x;
            for (int i = 0; i < (enraged ? 7 : 5); i++) Rockfall(around + Random.Range(-9f, 9f), 0.35f + i * 0.14f);
            Rockfall(around, 0.55f);
            mode = Mode.Recover;
            timer = enraged ? 0.55f : 0.85f;
        }

        // ---------- scythe waves ----------

        void ThrowWave(float height)
        {
            var from = (Vector2)transform.position + new Vector2(facing * 2.4f, height);
            Shoot(scytheSprite, from, new Vector2(facing * 15f, 0f), Mathf.RoundToInt(damage * 0.8f), 0f,
                new Color(0.85f, 0.6f, 1f, 1f), new Color(0.6f, 0.25f, 0.9f, 0.45f), 1.5f, 0.6f, true, 0f, 3f);
            ArenaFX.Burst(from, Violet, 10, 5f, 0.08f, 0.25f, 0f);
            ArenaFX.Shake(0.2f);
        }

        // ---------- brood ----------

        void Brood()
        {
            var director = ArenaDirector.Instance;
            if (director == null || broodPrefabs == null || broodPrefabs.Length == 0) return;
            var back = Center + new Vector2(-facing * 1.2f, 1.6f);
            ArenaFX.Burst(back, Ichor, 24, 8f, 0.13f, 0.5f, 2f);
            ArenaFX.Ring(back, new Color(0.6f, 1f, 0.5f, 0.85f), 0.3f, 3f, 0.3f);
            ArenaFX.Shake(0.35f);
            for (int i = 0; i < (enraged ? 3 : 2); i++)
            {
                var prefab = broodPrefabs[i % broodPrefabs.Length];
                var spot = GroundBelow(new Vector2(transform.position.x + (i % 2 == 0 ? -3f : 3f) * (1 + i / 2), transform.position.y + 2f));
                director.SpawnBug(prefab, spot + Vector2.up * 0.1f);
            }
        }

        // ---------- reactions ----------

        protected override void OnEnrage()
        {
            base.OnEnrage();
            ArenaFX.Ring(Center, new Color(0.75f, 0.4f, 1f, 0.9f), 1f, 11f, 0.6f);
            for (int i = 0; i < 5; i++) Rockfall(transform.position.x + Random.Range(-12f, 12f), 0.2f + i * 0.18f);
        }

        protected override void OnStagger()
        {
            EndTell();
            snare?.Cancel();
            if (Flying)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                flyVelocity = Vector2.zero;
                anim.Play("slam");
            }
            if (stunTimer > 0f) { mode = Mode.Recover; leapsLeft = 0; }
        }

        protected override void OnStunEnd()
        {
            mode = Mode.Recover;
            timer = 0.4f;
            anim.Play("idle");
        }

        void FixedUpdate()
        {
            if (IsDead) return;
            if (Flying) { rb.linearVelocity = flyVelocity * SpeedScale; return; }
            if (stunTimer > 0f) { rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 1f), rb.linearVelocity.y); return; }
            if (mode == Mode.Leap || mode == Mode.Descend) return; // ballistic
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
