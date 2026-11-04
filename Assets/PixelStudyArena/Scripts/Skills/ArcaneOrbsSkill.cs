using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Arcane Orbs (C, unlockable): a rune circle opens on the floor and another spins up behind the
    /// knight; five purple orbs pour out of it and circle him, then each one homes in on a different
    /// nearby bug and bursts on it. The orbs keep going after the cast, so you're free to act.
    /// </summary>
    public class ArcaneOrbsSkill : PlayerSkill
    {
        [Header("Arcane Orbs")]
        public ArcaneOrb orbPrefab;
        public Sprite circleSprite;
        public int count = 5;
        public int damage = 38;
        public float castTime = 0.45f;

        static readonly Color Purple = new Color(0.78f, 0.45f, 1f);
        static readonly Color PurpleLight = new Color(0.95f, 0.8f, 1f);
        float timer, sparkTimer;

        public override bool TryActivate()
        {
            StartCooldown();
            timer = castTime;
            player.anim.Play("cast", true);
            player.flash.Hold(Purple, 0.3f, 25f);
            player.Squash(0.88f, 1.12f);
            var center = player.Center;
            var feet = (Vector2)player.transform.position;
            if (circleSprite != null)
            {
                // a rune circle on the floor (seen at an angle) and one spinning upright behind the knight
                ArenaFX.PlaySprite(circleSprite, feet + Vector2.up * 0.06f, new Color(0.8f, 0.5f, 1f, 0.95f), castTime + 0.4f, true, new Vector3(0.6f, 0.18f, 1f), new Vector3(1.9f, 0.55f, 1f), 0f, 0f, false, 19);
                ArenaFX.PlaySprite(circleSprite, center, new Color(0.7f, 0.4f, 1f, 0.75f), castTime + 0.3f, true, Vector3.one * 0.5f, Vector3.one * 1.45f, 0f, 150f, false, 19);
                ArenaFX.PlaySprite(circleSprite, center, new Color(1f, 0.9f, 1f, 0.55f), castTime + 0.2f, false, Vector3.one * 0.45f, Vector3.one * 1.3f, 30f, -110f, false, 19);
            }
            ArenaFX.Ring(center, new Color(0.8f, 0.5f, 1f, 0.8f), 0.4f, 2.4f, 0.3f);
            ArenaFX.Glow(center, new Color(0.7f, 0.35f, 1f, 0.6f), 3.5f, 0.45f);
            if (orbPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 360f / count;
                    var a = angle * Mathf.Deg2Rad;
                    var orb = Instantiate(orbPrefab, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.4f, Quaternion.identity);
                    // they peel off one after another
                    orb.Init(angle, 0.45f + i * 0.12f, Scaled(damage));
                }
            }
            return true;
        }

        public override bool Tick(float dt)
        {
            timer -= dt;
            // motes rising off the floor circle
            if ((sparkTimer -= dt) <= 0f)
            {
                sparkTimer = 0.03f;
                var p = (Vector2)player.transform.position + new Vector2(Random.Range(-1.6f, 1.6f), 0.05f);
                ArenaFX.Spray(p, Vector2.up, Random.value < 0.5f ? Purple : PurpleLight, 1, 3f, 0.15f, 0.08f);
            }
            if (timer > 0f) return false;
            player.flash.ClearHold();
            return true;
        }

        public override void Cancel() => player.flash.ClearHold();
    }
}
