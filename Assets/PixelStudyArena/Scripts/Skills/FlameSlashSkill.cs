using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Flame Slash (Z). The knight drops into a low guard with the sword down by his heel; fire catches
    /// at the tip and climbs the blade. Then he whips it up in a rising slash and the fire pours out
    /// along the arc (FireArc): it burns everything the arc covers and spills burning blobs onto the
    /// floor ahead. The moment the blade passes in front there's an anime impact frame.
    /// While he does this the rest of the world runs at half speed (he doesn't), and hits don't
    /// interrupt him (hyper armor), so you don't get swarmed mid-move.
    /// </summary>
    public class FlameSlashSkill : PlayerSkill
    {
        [Header("Flame Slash")]
        public FireArc arcPrefab;
        public int damage = 60;
        public int pourDamage = 14;
        public float burnDps = 12f;
        public float burnSeconds = 3.5f;
        [Tooltip("How fast everything else moves while the knight does this (he keeps full speed).")]
        [Range(0.1f, 1f)] public float worldSpeed = 0.5f;
        [Tooltip("How long the blade takes to catch fire in the low guard.")]
        public float igniteTime = 0.45f;
        public float followThrough = 0.3f;
        [Tooltip("Frame of the 'flameRise' animation where the blade passes in front and the fire erupts.")]
        public int eruptFrame = 1;
        [Tooltip("Small upright flames that lick along the burning blade.")]
        public Sprite[] flameSprites;

        [Header("Blade in the low guard (units from the feet, facing right)")]
        public Vector2 bladeHilt = new Vector2(-1.06f, 1.0f);
        public Vector2 bladeTip = new Vector2(-1.4f, 0.68f);

        // where the blade tip is in each frame of the rising swing (facing right)
        static readonly Vector2[] SwingTips = { new Vector2(1.46f, 0.56f), new Vector2(1.61f, 0.98f), new Vector2(-0.07f, 2.16f), new Vector2(-0.54f, 2.32f) };
        static readonly Vector2[] SwingGrips = { new Vector2(0.56f, 0.86f), new Vector2(0.8f, 0.99f), new Vector2(0.17f, 1.4f), new Vector2(0.12f, 1.7f) };

        static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        static readonly Color FireHot = new Color(1f, 0.85f, 0.35f);
        static readonly Color WhiteHot = new Color(1f, 0.97f, 0.85f);

        enum Phase { Lower, Swing, Follow }
        Phase phase;
        float t, particleTimer, flameTimer, glowTimer;
        bool erupted;

        public override bool SuperArmor => true;

        void Reset()
        {
            skillId = SkillCatalog.FlameSlash;
            displayName = "Flame Slash";
            key = KeyCode.Z;
            gamepadButton = KeyCode.JoystickButton3;
            cooldown = 7f;
        }

        Vector2 World(Vector2 local) => player.visual.TransformPoint(new Vector3(local.x, local.y, 0f));

        public override bool TryActivate()
        {
            StartCooldown();
            phase = Phase.Lower;
            t = 0f;
            erupted = false;
            particleTimer = flameTimer = glowTimer = 0f;
            player.anim.Play("flameLower", true);
            player.Squash(1.12f, 0.86f); // drops low
            player.flash.Hold(Fire, 0.12f, 22f);
            Sfx.Play("flame_ignite", 0.8f);
            ArenaFX.BeginFocus(worldSpeed);
            // the tip scrapes the stone and catches
            var tip = World(bladeTip);
            ArenaFX.Spray(tip, new Vector2(-player.Facing, 0.6f), WhiteHot, 6, 5f, 0.5f, 0.08f);
            ArenaFX.Glow(tip, new Color(1f, 0.5f, 0.15f, 0.5f), 1.2f, 0.2f);
            return true;
        }

        public override bool Tick(float dt)
        {
            t += dt;
            switch (phase)
            {
                case Phase.Lower:
                {
                    float lit = Mathf.Clamp01(t / igniteTime);
                    // fire climbs from the tip up to the hilt
                    BurnLine(World(bladeTip), World(Vector2.Lerp(bladeTip, bladeHilt, lit)), lit, dt, true);
                    if (t >= igniteTime) Ablaze();
                    break;
                }
                case Phase.Swing:
                {
                    int f = Mathf.Clamp(player.anim.Frame, 0, SwingTips.Length - 1);
                    BurnLine(World(SwingGrips[f]), World(SwingTips[f]), 1f, dt, false);
                    if (!erupted && player.anim.Frame >= eruptFrame) Erupt();
                    if (player.anim.IsDone) { phase = Phase.Follow; t = 0f; }
                    break;
                }
                case Phase.Follow:
                {
                    // the fire on the raised blade gutters out
                    float fade = 1f - Mathf.Clamp01(t / followThrough);
                    if (Random.value < 0.6f * fade)
                        ArenaFX.Spray(World(SwingTips[3]), Vector2.up, Random.value < 0.5f ? Fire : FireHot, 1, 2.5f, 0.4f, 0.12f);
                    if (t >= followThrough)
                    {
                        Finish();
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>Flames along the lit part of the blade.</summary>
        void BurnLine(Vector2 from, Vector2 to, float strength, float dt, bool igniting)
        {
            if ((particleTimer -= dt) <= 0f)
            {
                particleTimer = 0.014f;
                for (int i = 0; i < 3; i++)
                {
                    var p = Vector2.Lerp(from, to, Random.value);
                    ArenaFX.Spray(p, new Vector2(Random.Range(-0.25f, 0.25f), 1f), Random.value < 0.45f ? FireHot : Fire, 1, Random.Range(2.5f, 4.5f), 0.3f, Random.Range(0.12f, 0.2f));
                }
                // a white-hot core right on the blade
                ArenaFX.Burst(Vector2.Lerp(from, to, Random.value), WhiteHot, 1, 0.6f, 0.08f, 0.1f, 0.5f);
                if (igniting) ArenaFX.Burst(to, WhiteHot, 2, 2f, 0.09f, 0.18f, 1.5f); // the climbing ignition front
            }
            if (flameSprites != null && flameSprites.Length > 0 && (flameTimer -= dt) <= 0f)
            {
                flameTimer = 0.022f;
                for (int i = 0; i < 2; i++)
                {
                    var p = Vector2.Lerp(from, to, Random.value);
                    var sprite = flameSprites[Random.Range(0, flameSprites.Length)];
                    float s = Random.Range(1.3f, 2f) * (0.55f + 0.45f * strength);
                    ArenaFX.PlaySprite(sprite, p + Vector2.down * 0.15f, Color.white, 0.18f, false, new Vector3(s, s, 1f), new Vector3(s * 0.45f, s * 1.25f, 1f),
                        Random.Range(-12f, 12f), 0f, Random.value < 0.5f, 26);
                }
            }
            if ((glowTimer -= dt) <= 0f)
            {
                glowTimer = 0.06f;
                ArenaFX.Glow((from + to) * 0.5f, new Color(1f, 0.45f, 0.12f, 0.35f + 0.35f * strength), 1.8f + 2.2f * strength, 0.12f);
            }
        }

        void Ablaze()
        {
            phase = Phase.Swing;
            t = 0f;
            var mid = World(Vector2.Lerp(bladeTip, bladeHilt, 0.5f));
            ArenaFX.Ring(mid, new Color(1f, 0.7f, 0.3f, 0.9f), 0.3f, 2.2f, 0.18f);
            ArenaFX.Burst(mid, FireHot, 12, 5f, 0.1f, 0.3f, 2f);
            player.flash.Flash(FireHot, 0.12f);
            player.anim.Play("flameRise", true);
            player.Squash(0.84f, 1.18f); // springs up into the swing
        }

        void Erupt()
        {
            erupted = true;
            int facing = player.Facing;
            var origin = (Vector2)player.transform.position + new Vector2(0.1f * facing, 1f);
            if (arcPrefab != null)
            {
                var arc = Instantiate(arcPrefab, origin, Quaternion.identity);
                arc.Launch(facing, Scaled(damage), Scaled(pourDamage), burnDps * player.AttackMultiplier, burnSeconds);
            }
            var front = origin + new Vector2(1.4f * facing, 0f);
            Sfx.Play("flame_burst", 1f, 1f, 0.03f);
            ImpactFrame.Play(front, ImpactFrame.Style.Fire);
            ArenaFX.HitStop(0.13f);
            ArenaFX.Shake(0.7f);
            CameraFollow2D.Zoom(0.88f, 0.3f);
            ArenaFX.Burst(front, FireHot, 22, 8f, 0.13f, 0.45f, 2f);
            ArenaFX.Glow(front, new Color(1f, 0.55f, 0.15f, 0.8f), 5f, 0.35f);
            player.flash.ClearHold();
            player.flash.Flash(WhiteHot, 0.15f);
            player.Squash(1.22f, 0.84f);
            player.Lunge(4f, 0.1f);
        }

        void Finish()
        {
            ArenaFX.EndFocus(0.2f);
            player.flash.ClearHold();
        }

        public override void Cancel() => Finish();
    }
}
