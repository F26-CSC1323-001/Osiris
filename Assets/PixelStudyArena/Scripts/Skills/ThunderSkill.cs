using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Thunderstrike (V, unlockable): the knight raises the sword and the sky darkens; lightning crawls
    /// up the blade. A bolt hits the sword, then lightning chains down onto up to five of the nearest
    /// bugs one after another (each bolt jumps from the last victim to the next), dealing heavy damage,
    /// scorching the floor and leaving survivors stunned and crackling.
    /// </summary>
    public class ThunderSkill : PlayerSkill
    {
        [Header("Thunderstrike")]
        public Sprite[] boltFrames;
        public Sprite scorchSprite;
        public int damage = 40;
        public int maxTargets = 5;
        public float range = 14f;
        public float stunSeconds = 1.6f;
        public float windup = 0.35f;
        public float strikeGap = 0.1f;

        static readonly Color Spark = new Color(0.6f, 0.9f, 1f);
        static readonly Color SparkHot = new Color(0.95f, 1f, 1f);

        readonly List<BugEnemy> targets = new List<BugEnemy>();
        float timer, sparkTimer;
        int next;
        bool fired;
        Vector2 lastStrike;

        // the tip of the sword while it's pointed at the sky ("thunderCall")
        Vector2 SwordTip => (Vector2)player.transform.position + new Vector2(-0.07f * player.Facing, 2.16f);

        public override bool TryActivate()
        {
            StartCooldown();
            fired = false;
            timer = windup;
            player.anim.Play("thunderCall", true);
            player.flash.Hold(Spark, 0.25f, 40f);
            player.Squash(0.9f, 1.1f);
            ArenaFX.Darken(0.45f, windup + strikeGap * maxTargets + 0.3f); // the storm rolls in
            return true;
        }

        public override bool Tick(float dt)
        {
            timer -= dt;
            if (!fired)
            {
                sparkTimer -= dt;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = 0.04f;
                    var tip = SwordTip;
                    ArenaFX.Burst(tip, Random.value < 0.5f ? Spark : SparkHot, 2, 3f, 0.07f, 0.25f, 0f);
                    // electricity crawling up into the sky
                    ArenaFX.Zap(tip, tip + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.2f, 2.6f)), Spark, 0.06f);
                }
                if (timer <= 0f) Fire();
                return false;
            }

            if (next < targets.Count && timer <= 0f)
            {
                timer = strikeGap;
                var bug = targets[next++];
                if (bug != null && !bug.IsDead) Strike(bug.Center, bug, next == targets.Count);
            }
            return next >= targets.Count && timer <= -0.2f;
        }

        void Fire()
        {
            fired = true;
            player.flash.ClearHold();
            player.flash.Flash(SparkHot, 0.15f);
            targets.Clear();
            next = 0;
            timer = 0f;

            // the first bolt hits the raised sword
            var tip = SwordTip;
            Bolt(tip, 0.7f);
            ArenaFX.Glow(tip, new Color(0.7f, 0.9f, 1f, 0.9f), 3f, 0.25f);
            ArenaFX.Burst(tip, SparkHot, 12, 7f, 0.07f, 0.3f, 0f);
            ArenaFX.ScreenFlash(new Color(0.85f, 0.95f, 1f, 0.35f), 0.08f);
            lastStrike = tip;
            player.anim.Play("clapStrike", true); // brings the sword down as the bolts fall
            player.Squash(1.15f, 0.88f);

            var director = ArenaDirector.Instance;
            if (director != null)
            {
                var from = player.Center;
                targets.AddRange(director.Alive
                    .Where(b => b != null && !b.IsDead && Vector2.Distance(b.Center, from) <= range)
                    .OrderBy(b => Vector2.Distance(b.Center, from))
                    .Take(maxTargets));
            }
            // nothing in range: one bolt in front so the button still does something
            if (targets.Count == 0) Strike((Vector2)player.transform.position + new Vector2(3f * player.Facing, 0.2f), null, true);
        }

        /// <summary>A bolt from the top of the screen down to a point.</summary>
        void Bolt(Vector2 point, float width)
        {
            if (boltFrames == null || boltFrames.Length == 0) return;
            var cam = Camera.main;
            float top = cam != null ? cam.transform.position.y + cam.orthographicSize + 1f : point.y + 10f;
            float height = Mathf.Max(3f, (top - point.y) / 5f); // bolt sprites are 5 units tall at scale 1
            var bolt = boltFrames[Random.Range(0, boltFrames.Length)];
            bool flip = Random.value < 0.5f;
            ArenaFX.PlaySprite(bolt, point, new Color(0.6f, 0.85f, 1f, 1f), 0.3f, true, new Vector3(2.4f * width, height, 1f), new Vector3(1f * width, height, 1f), 0f, 0f, flip, 29);
            ArenaFX.PlaySprite(bolt, point, Color.white, 0.14f, false, new Vector3(1.1f * width, height, 1f), new Vector3(0.6f * width, height, 1f), 0f, 0f, flip, 30);
        }

        void Strike(Vector2 point, BugEnemy bug, bool last)
        {
            Bolt(point, last ? 1.3f : 1f);
            // the lightning jumps from the last thing it hit
            ArenaFX.Zap(lastStrike, point, Spark, 0.16f);
            ArenaFX.Zap(lastStrike, point, SparkHot, 0.1f);
            lastStrike = point;
            for (int i = 0; i < 3; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                ArenaFX.Zap(point, point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.8f, 1.6f), Spark, 0.1f);
            }

            // scorch the floor under it
            var floor = Physics2D.Raycast(point + Vector2.up * 0.5f, Vector2.down, 6f, ArenaLayers.GroundMask);
            if (floor)
            {
                if (scorchSprite != null)
                    ArenaFX.PlaySprite(scorchSprite, floor.point + Vector2.up * 0.04f, Color.white, 1.4f, false, new Vector3(0.6f, 0.8f, 1f), new Vector3(0.75f, 0.8f, 1f), 0f, 0f, false, 23);
                ArenaFX.FlatRing(floor.point + Vector2.up * 0.08f, new Color(0.6f, 0.9f, 1f, 0.8f), 0.3f, 2.4f, 0.22f);
                ArenaFX.Spray(floor.point, Vector2.up, SparkHot, 6, 7f, 0.5f, 0.07f);
            }

            ArenaFX.Glow(point, new Color(0.6f, 0.85f, 1f, 0.8f), 4.5f, 0.3f);
            ArenaFX.Ring(point, Spark, 0.3f, 2.6f, 0.25f);
            ArenaFX.Burst(point, SparkHot, 16, 9f, 0.08f, 0.35f, 1f);
            ArenaFX.ScreenFlash(new Color(0.8f, 0.92f, 1f, last ? 0.28f : 0.18f), 0.07f);
            ArenaFX.Shake(last ? 0.45f : 0.25f);

            if (bug == null) return;
            bug.TakeHit(Scaled(damage), point + Vector2.up, 2f, false, true);
            if (!bug.IsDead) bug.ApplyShock(stunSeconds);
            ArenaFX.HitStop(last ? 0.08f : 0.04f);
        }

        public override void Cancel()
        {
            player.flash.ClearHold();
            targets.Clear();
        }
    }
}
