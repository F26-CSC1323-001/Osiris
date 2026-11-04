using System.Collections.Generic;
using System.Linq;
using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Heavenfall (G, the ultimate). Needs a full gauge, which fills as you fight: damage you deal,
    /// parries, and a little from damage you take (it carries over between floors). Invincible the
    /// whole way through, and the world almost stops while it plays out:
    ///   Charge   — the knight raises his blade to the sky. Light spirals into it, rings close in, the
    ///              floor cracks with light and a beam climbs into the clouds. Impact frame as the
    ///              power surges, and again as it peaks.
    ///   Rain     — spears of light fall on every bug on screen (impact frames on the 3rd and 6th).
    ///   The Sword— he brings the blade down and a colossal sword of light falls from the sky onto the
    ///              biggest bug (impact frame). Pillars of light burst out along the floor both ways.
    ///   Blast    — the sword shatters: a last impact frame and a blast that hits everything on screen.
    /// Bosses can't dodge or parry any of it.
    /// </summary>
    public class UltimateSkill : PlayerSkill
    {
        /// <summary>Gauge points for a parry (damage dealt counts 1 point per damage).</summary>
        public const float ParryCharge = 45f;
        /// <summary>Gauge points per damage the knight takes.</summary>
        public const float HurtCharge = 0.8f;

        [Header("Heavenfall")]
        [Tooltip("A colossal sword of light pointing down (pivot at the tip).")]
        public Sprite bladeSprite;
        [Tooltip("A spear of light pointing down (pivot at the tip).")]
        public Sprite spearSprite;
        [Tooltip("A beam of light (pivot at the bottom, 4 units tall at scale 1).")]
        public Sprite pillarSprite;
        [Tooltip("The sigil that opens in the sky.")]
        public Sprite sigilSprite;
        [Tooltip("Gauge points to fill it (see ParryCharge / HurtCharge).")]
        public float chargeNeeded = 900f;

        public int spearDamage = 32;
        public int spears = 8;
        public int bladeDamage = 180;
        public int pillarDamage = 25;
        public int blastDamage = 95;
        [Tooltip("How far from the knight it finds bugs (about one screen).")]
        public float reach = 14f;
        [Range(0.05f, 1f)] public float worldSpeed = 0.12f;
        public float chargeTime = 1.5f;
        public float spearGap = 0.11f;

        public static UltimateSkill Instance { get; private set; }

        static readonly Color Gold = new Color(1f, 0.84f, 0.38f);
        static readonly Color Holy = new Color(1f, 0.97f, 0.82f);
        static readonly Color Beam = new Color(1f, 0.82f, 0.4f, 0.7f);

        enum Phase { Charge, Rain, Fall, Pillars, Blast, Recover }
        Phase phase;
        float t, fxTimer;
        int pulses, spearsDone, pillarStep, facing;
        bool surged, active, bound;
        Vector2 bladePoint, skyPoint;
        readonly List<BugEnemy> targets = new List<BugEnemy>();
        readonly HashSet<BugEnemy> pillarHit = new HashSet<BugEnemy>();
        SpriteRenderer blade, bladeGlow;

        public float Charge01 => GameSession.Run != null ? Mathf.Clamp01(GameSession.Run.ultCharge) : 0f;
        public override bool Ready => Charge01 >= 1f;
        public override bool Invulnerable => true;

        Vector2 Tip => player.Center + new Vector2(0.15f * player.Facing, 1.35f);
        Vector2 Feet => player.transform.position;

        /// <summary>Adds to the gauge (bugs call it when they take damage). Ignored while Heavenfall is playing.</summary>
        public static void Feed(float points)
        {
            var u = Instance;
            var run = GameSession.Run;
            if (u == null || u.active || run == null || points <= 0f || run.ultCharge >= 1f) return;
            run.ultCharge = Mathf.Min(1f, run.ultCharge + points / Mathf.Max(1f, u.chargeNeeded));
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (active) Finish();
        }

        protected override void Update()
        {
            base.Update();
            var hud = GameUIController.Instance != null ? GameUIController.Instance.hud : null;
            if (hud == null) return;
            if (!bound)
            {
                bound = true;
                hud.SetUltimate(icon, KeyLabel, displayName);
            }
            hud.SetUltimateCharge(Charge01);
        }

        // ------------------------------------------------------------------ start

        public override bool TryActivate()
        {
            if (!Ready) return false;
            GameSession.Run.ultCharge = 0f;
            active = true;
            phase = Phase.Charge;
            t = fxTimer = 0f;
            pulses = spearsDone = pillarStep = 0;
            surged = false;
            facing = player.Facing;
            targets.Clear();
            pillarHit.Clear();
            player.skillOwnsVelocity = true;
            player.Body.linearVelocity = Vector2.zero;
            player.anim.Play("thunderCall", true);
            player.Squash(0.88f, 1.14f);
            player.flash.Hold(Gold, 0.35f, 30f);
            ArenaFX.BeginFocus(worldSpeed, 0.78f);
            CameraFollow2D.Zoom(0.84f, chargeTime + 0.4f);
            ArenaFX.DamageNumber(player.Center + Vector2.up * 2.4f, "HEAVENFALL", Gold, 1.7f);
            ArenaFX.Glow(Tip, new Color(1f, 0.85f, 0.4f, 0.6f), 3f, chargeTime);
            ArenaFX.Shake(0.2f);
            return true;
        }

        public override bool Tick(float dt)
        {
            t += dt;
            player.Body.linearVelocity = Vector2.zero;
            switch (phase)
            {
                case Phase.Charge: UpdateCharge(dt); break;
                case Phase.Rain: UpdateRain(); break;
                case Phase.Fall: UpdateFall(); break;
                case Phase.Pillars: UpdatePillars(); break;
                case Phase.Blast:
                    if (t >= 0.12f) Blast();
                    break;
                case Phase.Recover:
                    if (t >= 0.5f)
                    {
                        Finish();
                        return true;
                    }
                    break;
            }
            return false;
        }

        // ------------------------------------------------------------------ 1. charge

        void UpdateCharge(float dt)
        {
            float k = Mathf.Clamp01(t / chargeTime);
            var tip = Tip;

            // light spiralling into the blade, faster and faster
            if ((fxTimer -= dt) <= 0f)
            {
                fxTimer = Mathf.Lerp(0.035f, 0.012f, k);
                float a = Random.Range(0f, Mathf.PI * 2f);
                var from = tip + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 5.5f);
                var toward = (tip - from).normalized;
                var swirl = new Vector2(-toward.y, toward.x) * 0.6f;
                ArenaFX.Spray(from, toward + swirl, Random.value < 0.4f ? Color.white : Gold, 1, Mathf.Lerp(7f, 13f, k), 0.05f, 0.09f);
                // a beam climbing from the blade into the sky
                if (pillarSprite != null)
                    ArenaFX.PlaySprite(pillarSprite, tip, new Color(Beam.r, Beam.g, Beam.b, 0.35f + 0.4f * k), 0.09f, true,
                        new Vector3(0.25f + 0.55f * k, 0.4f + 3.6f * k, 1f), new Vector3(0.2f + 0.45f * k, 0.5f + 3.8f * k, 1f), 0f, 0f, false, 26);
            }

            // pulses: rings close in on the blade and the floor cracks with light
            if (t >= pulses * (chargeTime / 4f) && pulses < 4)
            {
                pulses++;
                ArenaFX.Ring(tip, new Color(1f, 0.85f, 0.4f, 0.9f), 6f - pulses * 0.5f, 0.3f, chargeTime / 4f);
                ArenaFX.FlatRing(Feet + Vector2.up * 0.08f, new Color(1f, 0.85f, 0.4f, 0.8f), 0.5f, 4f + pulses * 1.6f, 0.4f);
                ArenaFX.Burst(Feet + Vector2.up * 0.1f, Gold, 6 + pulses * 3, 5f, 0.09f, 0.4f, 2f);
                for (int i = -1; i <= 1; i += 2)
                    ArenaFX.Spray(Feet + new Vector2(i * 0.4f, 0.05f), new Vector2(i, 0.15f), Holy, 3 + pulses, 9f, 0.15f, 0.08f);
                ArenaFX.Shake(0.12f + pulses * 0.08f);
                player.Squash(0.92f, 1.1f);
            }

            // the power surges halfway: impact frame
            if (!surged && t >= chargeTime * 0.55f)
            {
                surged = true;
                ImpactFrame.Play(tip, ImpactFrame.Style.Gold);
                ArenaFX.HitStop(0.07f);
                ArenaFX.Ring(tip, new Color(1f, 0.95f, 0.75f, 0.95f), 0.4f, 7f, 0.3f);
                ArenaFX.Burst(tip, Holy, 24, 9f, 0.1f, 0.4f, 0.5f);
                ArenaFX.Shake(0.45f);
                player.flash.Flash(Color.white, 0.12f);
            }

            if (t >= chargeTime) BeginRain();
        }

        // ------------------------------------------------------------------ 2. rain of spears

        void BeginRain()
        {
            phase = Phase.Rain;
            t = 0f;
            var tip = Tip;
            // the peak: the sky tears open
            ImpactFrame.Play(tip, ImpactFrame.Style.Gold);
            ArenaFX.HitStop(0.09f);
            ArenaFX.After(0.1f, () => ArenaFX.ScreenFlash(new Color(1f, 0.9f, 0.6f, 0.4f), 0.15f));
            ArenaFX.Shake(0.6f);
            ArenaFX.Burst(tip, Holy, 30, 11f, 0.11f, 0.45f, 0.5f);
            if (pillarSprite != null)
                ArenaFX.PlaySprite(pillarSprite, tip, new Color(1f, 0.95f, 0.8f, 0.9f), 0.5f, true, new Vector3(1.4f, 5f, 1f), new Vector3(0.1f, 6f, 1f), 0f, 0f, false, 27);

            targets.Clear();
            var director = ArenaDirector.Instance;
            var origin = player.Center;
            if (director != null)
                targets.AddRange(director.Alive.Where(b => b != null && b.CanBeTargeted &&
                                                            Mathf.Abs(b.Center.x - origin.x) < reach && Mathf.Abs(b.Center.y - origin.y) < 9f)
                                               .OrderBy(b => Vector2.Distance(b.Center, origin)));
            // the sky sigil opens over the fight
            float cx = targets.Count > 0 ? targets.Average(b => b.Center.x) : origin.x + facing * 4f;
            skyPoint = new Vector2(Mathf.Lerp(origin.x, cx, 0.6f), origin.y + 5f);
            if (sigilSprite != null)
            {
                // two rune circles turning against each other
                ArenaFX.PlaySprite(sigilSprite, skyPoint, new Color(1f, 0.85f, 0.4f, 0.8f), 2.6f, true, Vector3.one * 1.2f, Vector3.one * 2.3f, 0f, 45f, false, 20);
                ArenaFX.PlaySprite(sigilSprite, skyPoint, new Color(1f, 0.97f, 0.85f, 0.5f), 2.6f, true, Vector3.one * 0.8f, Vector3.one * 1.6f, 30f, -70f, false, 20);
            }
            ArenaFX.Glow(skyPoint, new Color(1f, 0.85f, 0.45f, 0.5f), 7f, 2.5f);
        }

        void UpdateRain()
        {
            if (spearsDone < spears && t >= spearsDone * spearGap)
            {
                Spear(spearsDone);
                spearsDone++;
            }
            if (spearsDone >= spears && t >= spears * spearGap + 0.15f) BeginFall();
        }

        void Spear(int index)
        {
            Vector2 point;
            var live = targets.Where(b => b != null && b.CanBeTargeted).ToList();
            if (live.Count > 0)
            {
                var bug = live[index % live.Count];
                point = bug.Center + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.3f, 0.2f));
            }
            else
                point = GroundBelow(Feet + new Vector2(facing * (2f + index * 1.4f), 1f)) + Vector2.up * 0.2f;

            // a streak of light from the sigil, then the spear buried in its target
            if (pillarSprite != null)
                ArenaFX.PlaySprite(pillarSprite, point, new Color(1f, 0.9f, 0.6f, 0.8f), 0.2f, true, new Vector3(0.45f, 3f, 1f), new Vector3(0.05f, 3.2f, 1f), Random.Range(-8f, 8f), 0f, false, 28);
            if (spearSprite != null)
                ArenaFX.PlaySprite(spearSprite, point, Color.white, 0.32f, false, new Vector3(1.1f, 1.7f, 1f), new Vector3(0.9f, 1f, 1f), Random.Range(-6f, 6f), 0f, false, 33);
            ArenaFX.Burst(point, Gold, 12, 7f, 0.1f, 0.35f, 1f);
            ArenaFX.Burst(point, Color.white, 6, 4f, 0.07f, 0.2f, 0f);
            ArenaFX.Ring(point, new Color(1f, 0.9f, 0.55f, 0.9f), 0.3f, 2.2f, 0.2f);
            ArenaFX.Glow(point, new Color(1f, 0.85f, 0.45f, 0.45f), 2f, 0.25f);
            ArenaFX.Shake(0.22f);

            foreach (var bug in EnemiesInCircle(point, 1.5f)) Smite(bug, spearDamage, 20f, 3f);

            if (index == 2 || index == 5)
            {
                ImpactFrame.Play(point, index == 2 ? ImpactFrame.Style.Gold : ImpactFrame.Style.Steel);
                ArenaFX.HitStop(0.06f);
                ArenaFX.Shake(0.45f);
            }
            else ArenaFX.HitStop(0.025f);
        }

        // ------------------------------------------------------------------ 3. the sword from the sky

        void BeginFall()
        {
            phase = Phase.Fall;
            t = 0f;
            player.anim.Play("clapStrike", true);
            player.Squash(1.15f, 0.86f);

            // onto the biggest bug (the boss), else the nearest cluster, else ahead
            var live = targets.Where(b => b != null && b.CanBeTargeted).ToList();
            var main = live.OrderByDescending(b => b.maxHealth).ThenBy(b => Vector2.Distance(b.Center, player.Center)).FirstOrDefault();
            var at = main != null ? (Vector2)main.transform.position : Feet + new Vector2(facing * 4.5f, 0f);
            bladePoint = GroundBelow(at + Vector2.up * 1f);

            if (bladeSprite == null) return;
            var go = new GameObject("HeavenfallSword");
            blade = go.AddComponent<SpriteRenderer>();
            blade.sprite = bladeSprite;
            blade.sortingOrder = 34;
            go.transform.localScale = Vector3.one * 2.4f;
            go.transform.position = bladePoint + Vector2.up * 16f;
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localScale = new Vector3(1.25f, 1.05f, 1f);
            bladeGlow = glowGo.AddComponent<SpriteRenderer>();
            bladeGlow.sprite = bladeSprite;
            bladeGlow.sortingOrder = 33;
            if (ArenaFX.Instance != null) bladeGlow.sharedMaterial = ArenaFX.Instance.additiveMaterial;
            bladeGlow.color = new Color(1f, 0.8f, 0.35f, 0.7f);
            // where it will land
            ArenaFX.FlatRing(bladePoint + Vector2.up * 0.08f, new Color(1f, 0.85f, 0.4f, 0.9f), 5f, 0.6f, 0.2f);
        }

        void UpdateFall()
        {
            const float fallTime = 0.17f;
            float k = Mathf.Clamp01(t / fallTime);
            if (blade != null)
            {
                blade.transform.position = bladePoint + Vector2.up * Mathf.Lerp(16f, -0.7f, k * k);
                ArenaFX.Afterimage(blade, new Color(1f, 0.85f, 0.45f, 0.45f), 0.18f);
            }
            if (k >= 1f) Impact();
        }

        void Impact()
        {
            phase = Phase.Pillars;
            t = 0f;
            pillarStep = 0;
            var p = bladePoint;
            ImpactFrame.Play(p + Vector2.up * 1.5f, ImpactFrame.Style.Gold);
            ArenaFX.HitStop(0.18f);
            ArenaFX.After(0.14f, () => ArenaFX.ScreenFlash(new Color(1f, 1f, 0.9f, 0.55f), 0.2f));
            ArenaFX.Shake(1f);
            CameraFollow2D.Zoom(0.8f, 0.35f);
            ArenaFX.FlatRing(p + Vector2.up * 0.1f, new Color(1f, 0.9f, 0.6f, 0.95f), 0.5f, 16f, 0.45f);
            ArenaFX.Ring(p + Vector2.up * 1.5f, new Color(1f, 0.85f, 0.4f, 0.9f), 0.5f, 10f, 0.4f);
            ArenaFX.Glow(p + Vector2.up * 1.5f, new Color(1f, 0.85f, 0.45f, 0.8f), 9f, 0.6f);
            for (int i = 0; i < 3; i++) ArenaFX.Burst(p + Vector2.up * (0.3f + i * 0.8f), i == 1 ? Color.white : Gold, 26, 12f, 0.13f, 0.6f, 2f);
            ArenaFX.Spray(p + Vector2.up * 0.1f, new Vector2(-1f, 0.4f), new Color(0.7f, 0.65f, 0.6f), 14, 10f, 0.4f, 0.14f);
            ArenaFX.Spray(p + Vector2.up * 0.1f, new Vector2(1f, 0.4f), new Color(0.7f, 0.65f, 0.6f), 14, 10f, 0.4f, 0.14f);
            ArenaFX.DamageNumber(p + Vector2.up * 3.2f, "JUDGEMENT", Gold, 1.4f);

            foreach (var bug in EnemiesInBox(p + Vector2.up * 2.5f, new Vector2(9f, 7f))) Smite(bug, bladeDamage, 120f, 10f);
            player.flash.Flash(Color.white, 0.15f);
        }

        // ------------------------------------------------------------------ 4. pillars, then the blast

        void UpdatePillars()
        {
            // the embedded sword burns brighter and brighter
            if (bladeGlow != null) bladeGlow.color = new Color(1f, 0.85f, 0.4f, 0.55f + 0.45f * Mathf.PingPong(t * 8f, 1f));

            const float gap = 0.07f;
            const int steps = 6;
            if (pillarStep < steps && t >= pillarStep * gap)
            {
                pillarStep++;
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = GroundBelow(bladePoint + new Vector2(side * pillarStep * 2f, 2f));
                    if (pillarSprite != null)
                    {
                        ArenaFX.PlaySprite(pillarSprite, at, new Color(1f, 0.88f, 0.5f, 0.9f), 0.45f, true, new Vector3(1.1f, 0.3f, 1f), new Vector3(0.6f, 2.4f, 1f), 0f, 0f, false, 28);
                        ArenaFX.PlaySprite(pillarSprite, at, Color.white, 0.25f, true, new Vector3(0.45f, 0.4f, 1f), new Vector3(0.15f, 2.6f, 1f), 0f, 0f, false, 29);
                    }
                    ArenaFX.Burst(at + Vector2.up * 0.2f, Gold, 8, 7f, 0.1f, 0.4f, 2f);
                    foreach (var bug in EnemiesInBox(at + Vector2.up * 3f, new Vector2(2.4f, 6f)))
                        if (pillarHit.Add(bug)) Smite(bug, pillarDamage, 20f, 4f);
                }
                ArenaFX.Shake(0.2f);
            }
            if (pillarStep >= steps && t >= steps * gap + 0.25f)
            {
                phase = Phase.Blast;
                t = 0f;
            }
        }

        void Blast()
        {
            phase = Phase.Recover;
            t = 0f;
            var p = bladePoint + Vector2.up * 1.8f;
            // the sword shatters
            ImpactFrame.Play(p, ImpactFrame.Style.Steel);
            ArenaFX.HitStop(0.15f);
            ArenaFX.After(0.12f, () => ArenaFX.ScreenFlash(new Color(1f, 1f, 1f, 0.7f), 0.25f));
            ArenaFX.Shake(1f);
            ArenaFX.Ring(p, new Color(1f, 0.95f, 0.8f, 0.95f), 0.5f, 18f, 0.5f);
            ArenaFX.Ring(p, new Color(1f, 0.8f, 0.35f, 0.8f), 0.5f, 12f, 0.6f);
            ArenaFX.Glow(p, new Color(1f, 0.9f, 0.6f, 0.9f), 14f, 0.5f);
            for (int i = 0; i < 4; i++) ArenaFX.Burst(p + Random.insideUnitCircle * 1.2f, i % 2 == 0 ? Color.white : Gold, 30, 14f, 0.12f, 0.8f, 1.5f);
            if (blade != null) Destroy(blade.gameObject);
            blade = bladeGlow = null;

            var director = ArenaDirector.Instance;
            var origin = player.Center;
            if (director != null)
                foreach (var bug in director.Alive.Where(b => b != null && b.CanBeTargeted &&
                                                              Mathf.Abs(b.Center.x - origin.x) < reach + 2f && Mathf.Abs(b.Center.y - origin.y) < 10f).ToList())
                    Smite(bug, blastDamage, 80f, 12f);

            ArenaFX.EndFocus(0.35f);
            player.flash.ClearHold();
            player.flash.Flash(Color.white, 0.2f);
            player.anim.Play("idle");
            player.Squash(0.95f, 1.05f);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A hit no boss can dodge or parry.</summary>
        void Smite(BugEnemy bug, int amount, float poise, float knockback)
        {
            if (bug == null || !bug.CanBeTargeted) return;
            BugEnemy.Unavoidable = true;
            try { bug.TakeHit(Scaled(amount), player.transform.position, knockback, true, true, poise); }
            finally { BugEnemy.Unavoidable = false; }
        }

        static Vector2 GroundBelow(Vector2 p)
        {
            var hit = Physics2D.Raycast(p, Vector2.down, 14f, ArenaLayers.GroundMask);
            return hit ? hit.point : p;
        }

        void Finish()
        {
            active = false;
            if (blade != null) Destroy(blade.gameObject);
            blade = bladeGlow = null;
            ArenaFX.EndFocus(0.3f);
            if (player != null)
            {
                player.flash.ClearHold();
                player.skillOwnsVelocity = false;
            }
        }

        public override void Cancel() => Finish();
    }
}
