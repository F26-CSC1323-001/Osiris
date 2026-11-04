using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Thousand Cuts (X, unlockable): the knight drops into a draw stance and the world almost stops.
    /// He vanishes and blinks through every bug ahead (up to 8), leaving glowing cut marks on each one,
    /// and comes to rest past them with his back turned. A heartbeat later everything he passed is cut
    /// at once: an impact frame, then a huge hit on every marked bug. Invincible the whole time.
    /// </summary>
    public class ThousandCutsSkill : PlayerSkill
    {
        [Header("Thousand Cuts")]
        [Tooltip("Thin cut line (5 units long at scale 1, pivot in the middle).")]
        public Sprite cutSprite;
        [Tooltip("Streak left between blinks (4 units long at scale 1, pivot at the left end).")]
        public Sprite streakSprite;
        public int damage = 64;
        public int maxTargets = 8;
        [Tooltip("How far ahead (and how high) it reaches.")]
        public float reach = 11f;
        public float reachUp = 5f;
        [Tooltip("How fast the world moves while the knight does this (he keeps full speed).")]
        [Range(0.05f, 1f)] public float worldSpeed = 0.15f;
        public float drawTime = 0.4f;
        public float blinkTime = 0.075f;
        public float holdTime = 0.42f;
        public float recoverTime = 0.3f;

        static readonly Color Steel = new Color(0.88f, 0.96f, 1f);
        static readonly Color Edge = new Color(0.55f, 0.85f, 1f);
        static readonly Color Ghost = new Color(0.5f, 0.82f, 1f, 0.65f);
        static readonly string[] SlashClips = { "slash1", "slash2", "slash3" };

        struct Cut
        {
            public Vector2 at;
            public float angle, length;
            public Color blood;
        }

        enum Phase { Draw, Blink, Hold, Recover }
        Phase phase;
        float t, fxTimer;
        int next, facing;
        readonly List<Vector2> stops = new List<Vector2>();
        readonly List<BugEnemy> marked = new List<BugEnemy>();
        readonly List<Cut> cuts = new List<Cut>();

        // he's everywhere at once: nothing can touch him
        public override bool Invulnerable => true;

        Vector2 Grip => (Vector2)player.transform.position + new Vector2(0.35f * player.Facing, 0.95f);

        public override bool TryActivate()
        {
            StartCooldown();
            phase = Phase.Draw;
            t = 0f;
            next = 0;
            fxTimer = 0f;
            facing = player.Facing;
            stops.Clear();
            marked.Clear();
            cuts.Clear();
            player.skillOwnsVelocity = true;
            player.Body.linearVelocity = Vector2.zero;
            player.anim.Play("heavyCharge", true); // blade drawn back, ready to cut
            player.Squash(1.14f, 0.85f);
            player.flash.Hold(Edge, 0.25f, 30f);
            ArenaFX.BeginFocus(worldSpeed, 0.8f);
            ArenaFX.Ring(player.Center, new Color(0.7f, 0.9f, 1f, 0.9f), 4f, 0.3f, drawTime);
            CameraFollow2D.Zoom(0.9f, drawTime);
            return true;
        }

        public override bool Tick(float dt)
        {
            t += dt;
            if (phase != Phase.Recover) player.Body.linearVelocity = Vector2.zero;
            switch (phase)
            {
                case Phase.Draw:
                    // light gathers on the blade
                    if ((fxTimer -= dt) <= 0f)
                    {
                        fxTimer = 0.025f;
                        var grip = Grip;
                        var from = grip + Random.insideUnitCircle.normalized * Random.Range(0.9f, 1.7f);
                        ArenaFX.Spray(from, grip - from, Random.value < 0.5f ? Color.white : Edge, 1, 7f, 0.05f, 0.07f);
                    }
                    if (t >= drawTime) BeginBlinks();
                    break;
                case Phase.Blink:
                    if (t >= blinkTime)
                    {
                        t = 0f;
                        if (next < stops.Count) BlinkTo(next++);
                        else BeginHold();
                    }
                    break;
                case Phase.Hold:
                    if (t >= holdTime) Release();
                    break;
                case Phase.Recover:
                    if (t >= recoverTime)
                    {
                        Finish();
                        return true;
                    }
                    break;
            }
            return false;
        }

        void BeginBlinks()
        {
            phase = Phase.Blink;
            t = blinkTime; // first blink right away
            player.flash.ClearHold();
            player.flash.Flash(Color.white, 0.1f);
            var origin = (Vector2)player.transform.position;

            var area = EnemiesInBox(origin + new Vector2(facing * reach * 0.5f, reachUp * 0.5f - 0.5f), new Vector2(reach + 1.5f, reachUp + 1f));
            foreach (var bug in area.Where(b => (b.Center.x - origin.x) * facing > -0.6f)
                         .OrderBy(b => (b.Center.x - origin.x) * facing).Take(maxTargets))
            {
                marked.Add(bug);
                bug.Hold(0.6f); // frozen mid-motion until the cut lands
                stops.Add(StandBeside(bug));
            }
            if (marked.Count == 0)
            {
                // nothing to cut: still flash forward through the air
                stops.Add(Clear(GroundBelow(origin + new Vector2(facing * 3.5f, 0.5f)), origin));
            }
            // come to rest past the last one, back turned
            var last = stops.Count > 0 ? stops[stops.Count - 1] : origin;
            stops.Add(Clear(GroundBelow(last + new Vector2(facing * 1.8f, 0.5f)), last));
        }

        /// <summary>Feet position right past a bug, level with it (in the air for wasps).</summary>
        Vector2 StandBeside(BugEnemy bug)
        {
            var col = bug.GetComponent<Collider2D>();
            float half = col != null ? col.bounds.extents.x : 0.5f;
            var feet = new Vector2(bug.Center.x + facing * (half + 0.75f), bug.Center.y - 0.95f);
            var ground = Physics2D.Raycast(feet + Vector2.up * 1.2f, Vector2.down, 1.6f, ArenaLayers.GroundMask);
            if (ground && ground.point.y > feet.y - 0.4f) feet.y = ground.point.y;
            if (Blocked(feet)) feet.x = bug.Center.x - facing * (half + 0.75f);
            return feet;
        }

        static bool Blocked(Vector2 feet) =>
            Physics2D.OverlapBox(feet + Vector2.up * 1f, new Vector2(0.6f, 1.5f), 0f, ArenaLayers.SolidMask) != null;

        /// <summary>Pulls a spot back toward where we came from until the knight fits there.</summary>
        static Vector2 Clear(Vector2 feet, Vector2 from)
        {
            for (int i = 0; i < 8 && Blocked(feet); i++) feet = Vector2.Lerp(feet, from, 0.35f);
            return feet;
        }

        static Vector2 GroundBelow(Vector2 p)
        {
            var hit = Physics2D.Raycast(p + Vector2.up * 0.5f, Vector2.down, 10f, ArenaLayers.GroundMask);
            return hit ? hit.point : p;
        }

        void BlinkTo(int index)
        {
            var from = player.Center;
            ArenaFX.Afterimage(player.body, Ghost, 0.45f);
            player.Teleport(stops[index]);
            var to = player.Center;
            ArenaFX.Streak(streakSprite, from, to, Color.white, 0.22f, 0.45f);
            ArenaFX.Streak(streakSprite, from, to, Edge, 0.4f, 1.2f);
            ArenaFX.Burst(from, Edge, 4, 3f, 0.07f, 0.2f, 0f);

            if (index >= marked.Count)
            {
                player.SetFacing(facing); // the last stop: past them all, back turned
                return;
            }
            // he appears past the bug, turned back on it, mid-slash
            var bug = marked[index];
            int back = -facing;
            player.SetFacing(back);
            player.anim.Play(SlashClips[Random.Range(0, SlashClips.Length)], true);
            player.Squash(1.15f, 0.9f);
            var smears = player.smearSprites;
            if (smears != null && smears.Length > 0)
            {
                var smear = smears[Random.Range(0, smears.Length)];
                var pivot = to + new Vector2(0.15f * back, 0.1f);
                float spin = Random.value < 0.5f ? -200f : 200f;
                ArenaFX.PlaySprite(smear, pivot, Color.white, 0.14f, false, Vector3.one * 1.1f, Vector3.one * 1.25f, Random.Range(-30f, 30f), spin * back, back < 0, 27);
                ArenaFX.PlaySprite(smear, pivot, new Color(0.5f, 0.8f, 1f, 0.5f), 0.2f, true, Vector3.one * 1.15f, Vector3.one * 1.35f, Random.Range(-30f, 30f), spin * back, back < 0, 26);
            }

            // cut marks across the bug that hang there until everything is cut at once
            float size = 1.4f + Mathf.Max(bug.centerHeight, 0.5f) * 1.4f;
            int count = Random.Range(2, 4);
            float stay = (stops.Count - index) * blinkTime + holdTime + 0.05f;
            for (int i = 0; i < count; i++)
            {
                var cut = new Cut
                {
                    at = bug.Center + Random.insideUnitCircle * 0.2f,
                    angle = (i == 0 ? Random.Range(-35f, -10f) : i == 1 ? Random.Range(15f, 50f) : Random.Range(70f, 110f)) * facing,
                    length = size * Random.Range(0.9f, 1.25f),
                    blood = bug.bloodColor,
                };
                cuts.Add(cut);
                ArenaFX.PlaySprite(cutSprite, cut.at, Color.white, stay, false, new Vector3(cut.length / 5f * 0.3f, 0.5f, 1f), new Vector3(cut.length / 5f, 0.45f, 1f), cut.angle, 0f, false, 31);
                ArenaFX.PlaySprite(cutSprite, cut.at, new Color(0.5f, 0.85f, 1f, 0.7f), stay, true, new Vector3(cut.length / 5f * 0.3f, 1.4f, 1f), new Vector3(cut.length / 5f, 1.1f, 1f), cut.angle, 0f, false, 30);
            }
            bug.flash.Flash(Color.white, 0.12f);
            ArenaFX.Burst(bug.Center, Steel, 6, 5f, 0.07f, 0.2f, 0.3f);
            ArenaFX.Shake(0.15f);
        }

        void BeginHold()
        {
            phase = Phase.Hold;
            t = 0f;
            // at rest past them, blade lowered, back turned... wait for it
            player.anim.Play("cutsRest", true);
            player.Squash(0.92f, 1.06f);
            var tip = (Vector2)player.transform.position + new Vector2(0.9f * facing, 0.35f);
            if (player.starSprite != null)
                ArenaFX.PlaySprite(player.starSprite, tip, Color.white, 0.3f, true, Vector3.one * 0.3f, Vector3.one * 1.3f, 0f, 200f);
            ArenaFX.Burst(tip, Color.white, 4, 2f, 0.06f, 0.2f, 0f);
        }

        void Release()
        {
            phase = Phase.Recover;
            t = 0f;
            var focus = marked.Count > 0
                ? marked.Where(b => b != null).Select(b => b.Center).Aggregate(Vector2.zero, (a, b) => a + b) / Mathf.Max(1, marked.Count(b => b != null))
                : player.Center + new Vector2(-2f * facing, 0f);
            ImpactFrame.Play(focus, ImpactFrame.Style.Steel);
            ArenaFX.HitStop(0.16f);
            ArenaFX.Shake(0.9f);
            ArenaFX.After(0.1f, () => ArenaFX.ScreenFlash(new Color(1f, 1f, 1f, 0.35f), 0.12f));
            CameraFollow2D.Zoom(0.86f, 0.25f);

            // every cut splits open
            foreach (var cut in cuts)
            {
                ArenaFX.PlaySprite(cutSprite, cut.at, Color.white, 0.28f, true, new Vector3(cut.length / 5f, 2.6f, 1f), new Vector3(cut.length / 5f * 1.2f, 0.1f, 1f), cut.angle, 0f, false, 32);
                float a = cut.angle * Mathf.Deg2Rad;
                var normal = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                ArenaFX.Spray(cut.at, normal, cut.blood, 6, 8f, 0.3f, 0.13f);
                ArenaFX.Spray(cut.at, -normal, cut.blood, 6, 8f, 0.3f, 0.13f);
            }
            foreach (var bug in marked)
            {
                if (bug == null || bug.IsDead) continue;
                bug.TakeHit(Scaled(damage), player.transform.position, 6f, true, true, 60f);
                ArenaFX.Burst(bug.Center, Color.white, 10, 7f, 0.08f, 0.3f, 0.5f);
            }
            ArenaFX.EndFocus(0.25f);
            player.flash.Flash(Color.white, 0.2f);
            player.Squash(1.1f, 0.92f);
        }

        void Finish()
        {
            ArenaFX.EndFocus(0.2f);
            player.flash.ClearHold();
            player.skillOwnsVelocity = false;
        }

        public override void Cancel() => Finish();
    }
}
