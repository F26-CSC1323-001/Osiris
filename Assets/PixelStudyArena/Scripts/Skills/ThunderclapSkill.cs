using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Thunderclap (E, unlockable): the sky darkens and a target ring marks the ground, then a giant bolt
    /// slams down (impact frame, screen flash, scorched crater) and an electric shockwave rolls outward.
    /// Everything it passes is damaged and stunned long enough to walk up and land a charged heavy stab
    /// (the stab does extra damage to stunned bugs); stunned bugs crackle with electricity so you can
    /// see who's open. (Thunderstrike on V is the multi-target version.)
    /// </summary>
    public class ThunderclapSkill : PlayerSkill
    {
        [Header("Thunderclap")]
        public Sprite[] boltFrames;
        public Sprite ringSprite;
        public Sprite[] zapFrames;
        public Sprite scorchSprite;
        public Sprite crackSprite;
        public float windup = 0.45f;
        public float range = 9f;
        public int damage = 45;
        public float maxRadius = 6f;
        public float waveTime = 0.5f;
        public float knockback = 6f;
        [Tooltip("Long enough to close the distance, charge and land a heavy stab.")]
        public float stunSeconds = 3f;
        [Tooltip("Bosses shake it off sooner.")]
        public float bossStunSeconds = 1.75f;

        static readonly Color Spark = new Color(0.6f, 0.9f, 1f);
        static readonly Color SparkHot = new Color(0.95f, 1f, 1f);

        enum Phase { Windup, Wave }
        Phase phase;
        float timer, sparkTimer, zapTimer;
        Vector2 point;
        readonly HashSet<BugEnemy> struck = new HashSet<BugEnemy>();

        // calling down the storm: hits still hurt, but they don't stop the bolt
        public override bool SuperArmor => true;

        public override bool TryActivate()
        {
            StartCooldown();
            phase = Phase.Windup;
            timer = windup;
            struck.Clear();
            point = PickPoint();
            player.anim.Play("thunderCall", true);
            player.flash.Hold(Spark, 0.3f, 40f);
            player.Squash(0.9f, 1.1f);
            // target marker that closes in until the bolt lands
            ArenaFX.Ring(point + Vector2.up * 0.1f, new Color(0.6f, 0.9f, 1f, 0.9f), 3.2f, 0.4f, windup);
            ArenaFX.FlatRing(point + Vector2.up * 0.06f, new Color(0.6f, 0.9f, 1f, 0.7f), 4f, 0.5f, windup);
            ArenaFX.Glow(point, new Color(0.5f, 0.8f, 1f, 0.35f), 3f, windup);
            ArenaFX.Darken(0.55f, windup + 0.35f); // the storm gathers
            return true;
        }

        Vector2 PickPoint()
        {
            int f = player.Facing;
            var from = (Vector2)player.transform.position;
            var director = ArenaDirector.Instance;
            if (director != null)
            {
                var bug = director.Alive
                    .Where(b => b != null && !b.IsDead && (b.transform.position.x - from.x) * f > -1f && Vector2.Distance(b.transform.position, from) <= range)
                    .OrderBy(b => Vector2.Distance(b.transform.position, from))
                    .FirstOrDefault();
                if (bug != null) return GroundBelow(bug.transform.position);
            }
            return GroundBelow(from + new Vector2(4f * f, 0f));
        }

        static Vector2 GroundBelow(Vector2 p)
        {
            var hit = Physics2D.Raycast(p + Vector2.up * 1f, Vector2.down, 12f, ArenaLayers.GroundMask);
            return hit ? hit.point : p;
        }

        public override bool Tick(float dt)
        {
            if (phase == Phase.Windup)
            {
                timer -= dt;
                sparkTimer -= dt;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = 0.03f;
                    ArenaFX.Burst(player.Center + new Vector2(0.2f * player.Facing, 1.3f), Random.value < 0.5f ? Spark : SparkHot, 2, 3f, 0.07f, 0.25f, 0f);
                    ArenaFX.Burst(point + Vector2.up * 0.2f, Spark, 1, 2f, 0.06f, 0.2f, 1f);
                    // lightning flickering in the clouds above the target
                    if (Random.value < 0.35f)
                    {
                        var sky = point + new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(5f, 7f));
                        ArenaFX.Zap(zapFrames, sky, sky + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, -0.4f)), Spark, 0.07f);
                    }
                }
                if (timer <= 0f) Strike();
                return false;
            }

            timer += dt;
            float t = Mathf.Clamp01(timer / waveTime);
            float radius = maxRadius * (1f - (1f - t) * (1f - t));

            foreach (var bug in EnemiesInCircle(point + Vector2.up * 0.5f, radius))
            {
                if (!struck.Add(bug)) continue;
                bug.TakeHit(Scaled(damage), point, knockback, true, true);
                if (!bug.IsDead) bug.ApplyShock(bug is BossBug ? bossStunSeconds : stunSeconds);
                ArenaFX.Zap(zapFrames, point + Vector2.up * 0.5f, bug.Center, Spark);
            }

            // electricity crawling along the shockwave
            zapTimer -= dt;
            if (zapTimer <= 0f && t < 1f)
            {
                zapTimer = 0.025f;
                for (int i = 0; i < 2; i++)
                {
                    float a = Random.Range(0f, Mathf.PI);
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var p = point + Vector2.up * 0.3f + dir * radius;
                    var tangent = new Vector2(-dir.y, dir.x);
                    ArenaFX.Zap(zapFrames, p - tangent * 0.5f, p + tangent * 0.5f + dir * Random.Range(-0.3f, 0.3f), Spark, 0.08f);
                }
                ArenaFX.Spray(point + new Vector2(radius, 0.1f), new Vector2(1f, 0.6f), new Color(0.7f, 0.7f, 0.72f), 1, 4f, 0.4f, 0.1f);
                ArenaFX.Spray(point + new Vector2(-radius, 0.1f), new Vector2(-1f, 0.6f), new Color(0.7f, 0.7f, 0.72f), 1, 4f, 0.4f, 0.1f);
            }

            return timer >= waveTime + 0.15f;
        }

        void Strike()
        {
            phase = Phase.Wave;
            timer = 0f;
            player.flash.ClearHold();
            player.anim.Play("clapStrike", true);
            player.Squash(1.22f, 0.82f);

            if (boltFrames != null && boltFrames.Length > 0)
            {
                var a = boltFrames[0];
                var b = boltFrames[Mathf.Min(1, boltFrames.Length - 1)];
                var c = boltFrames[boltFrames.Length - 1];
                ArenaFX.PlaySprite(a, point, new Color(0.55f, 0.85f, 1f, 1f), 0.4f, true, new Vector3(4.5f, 4.5f, 1f), new Vector3(2.5f, 4.5f, 1f), 0f, 0f, false, 29);
                ArenaFX.PlaySprite(b, point, Color.white, 0.25f, false, new Vector3(2.2f, 4.5f, 1f), new Vector3(1.2f, 4.5f, 1f), 0f, 0f, true, 30);
                ArenaFX.PlaySprite(c, point + new Vector2(-0.7f, 0f), new Color(0.7f, 0.9f, 1f, 0.8f), 0.2f, true, new Vector3(1.2f, 3.5f, 1f), new Vector3(0.6f, 3.5f, 1f), 0f, 0f, false, 29);
                ArenaFX.PlaySprite(c, point + new Vector2(0.7f, 0f), new Color(0.7f, 0.9f, 1f, 0.8f), 0.2f, true, new Vector3(1.2f, 3.5f, 1f), new Vector3(0.6f, 3.5f, 1f), 0f, 0f, true, 29);
            }
            if (ringSprite != null)
            {
                float end = maxRadius / 1.75f; // ring sprite radius is ~1.75 units at scale 1
                ArenaFX.PlaySprite(ringSprite, point + Vector2.up * 0.3f, Color.white, waveTime + 0.15f, false, Vector3.one * 0.2f, Vector3.one * end, 0f, 90f);
                ArenaFX.PlaySprite(ringSprite, point + Vector2.up * 0.3f, new Color(0.4f, 0.75f, 1f, 0.8f), waveTime + 0.25f, true, Vector3.one * 0.3f, Vector3.one * end * 1.1f, 20f, -60f);
            }

            // a scorched crater cracked into the floor
            if (scorchSprite != null)
                ArenaFX.PlaySprite(scorchSprite, point + Vector2.up * 0.04f, Color.white, 2.2f, false, new Vector3(1.2f, 1.2f, 1f), new Vector3(1.5f, 1.2f, 1f), 0f, 0f, false, 23);
            if (crackSprite != null)
                ArenaFX.PlaySprite(crackSprite, point, Color.white, 1.6f, false, new Vector3(1.2f, 1.1f, 1f), new Vector3(1.5f, 1.3f, 1f), 0f, 0f, false, 23);

            ImpactFrame.Play(point + Vector2.up * 1.5f, ImpactFrame.Style.Lightning);
            // the white-out comes right after the impact frame (during it, it would wash the silhouettes out)
            var at = point;
            ArenaFX.After(0.12f, () =>
            {
                ArenaFX.ScreenFlash(new Color(0.8f, 0.95f, 1f, 0.6f), 0.16f);
                ArenaFX.Glow(at + Vector2.up, new Color(0.5f, 0.85f, 1f, 0.9f), 9f, 0.5f);
            });
            ArenaFX.Burst(point + Vector2.up * 0.3f, SparkHot, 30, 10f, 0.09f, 0.45f, 2f);
            ArenaFX.Burst(point + Vector2.up * 0.2f, new Color(0.7f, 0.7f, 0.72f), 20, 7f, 0.14f, 0.6f, 3f);
            ArenaFX.Shake(0.9f);
            ArenaFX.HitStop(0.14f);
            CameraFollow2D.Zoom(0.9f, 0.2f);
        }

        public override void Cancel() => player.flash.ClearHold();
    }
}
