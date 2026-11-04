using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Riposte (F, starting skill): a quick parry stance that works from any side. If anything hits
    /// you while it's up, the hit is cancelled, every bug nearby is staggered and open to a critical,
    /// the ones close by take a counter-slash, acid is knocked back, and most of the cooldown is refunded.
    /// </summary>
    public class RiposteSkill : PlayerSkill
    {
        [Header("Riposte")]
        [Tooltip("Rune circle shown as the golden ward behind the knight.")]
        public Sprite wardSprite;
        public float window = 0.45f;
        public float recovery = 0.25f;
        public float stunRadius = 4.5f;
        public float stunSeconds = 2.5f;
        public int counterDamage = 20;
        public float counterRadius = 2.6f;
        public float successCooldown = 1.5f;

        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        enum Phase { Stance, Counter, Recover }
        Phase phase;
        float timer, shimmerTimer;

        public override bool Invulnerable => phase == Phase.Counter;

        public override bool TryActivate()
        {
            StartCooldown();
            phase = Phase.Stance;
            timer = window;
            player.anim.Play("defend", true);
            player.flash.Hold(Gold, 0.25f, 40f);
            // a ring that closes in shows how long the parry window lasts
            ArenaFX.Ring(player.Center, new Color(1f, 0.85f, 0.35f, 0.8f), 3f, 0.8f, window);
            // a golden ward spins up behind the knight
            if (wardSprite != null)
            {
                ArenaFX.PlaySprite(wardSprite, player.Center, new Color(1f, 0.82f, 0.35f, 0.85f), window, true, Vector3.one * 0.6f, Vector3.one * 1.25f, 0f, 140f, false, 19, player.transform);
                ArenaFX.PlaySprite(wardSprite, player.Center, new Color(1f, 0.95f, 0.7f, 0.45f), window, false, Vector3.one * 0.55f, Vector3.one * 1.15f, 30f, -100f, false, 19, player.transform);
            }
            ArenaFX.Glow(player.Center, new Color(1f, 0.8f, 0.35f, 0.4f), 3.5f, window);
            return true;
        }

        public override bool TryParry(Vector2 source)
        {
            if (phase != Phase.Stance) return false;
            Counter(source);
            return true;
        }

        public override bool Tick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Stance:
                    if (player.anim.Current == "defend" && player.anim.IsDone) player.anim.Play("protect");
                    // gold motes drifting up off the ward
                    if ((shimmerTimer -= dt) <= 0f)
                    {
                        shimmerTimer = 0.04f;
                        var p = player.Center + Random.insideUnitCircle * 1.3f;
                        ArenaFX.Spray(p, Vector2.up, Random.value < 0.5f ? Gold : Color.white, 1, 1.5f, 0.3f, 0.07f);
                    }
                    if (timer <= 0f)
                    {
                        phase = Phase.Recover;
                        timer = recovery;
                        player.flash.ClearHold();
                    }
                    return false;
                default:
                    return timer <= 0f;
            }
        }

        void Counter(Vector2 source)
        {
            phase = Phase.Counter;
            timer = 0.35f;
            player.flash.ClearHold();
            player.flash.Flash(Gold, 0.25f);
            player.SetFacing(source.x >= player.transform.position.x ? 1 : -1);
            player.anim.Play("riposte", true);
            player.Squash(1.15f, 0.9f);

            var c = player.Center;
            int f = player.Facing;
            ArenaFX.Burst(c, Gold, 24, 9f, 0.12f, 0.5f, 1f);
            // an X of two gold cuts in front of the knight
            var smears = player.smearSprites;
            if (smears != null && smears.Length > 1)
            {
                var pivot = (Vector2)player.transform.position + new Vector2(0.15f * f, 1.05f);
                ArenaFX.PlaySprite(smears[0], pivot, new Color(1f, 0.92f, 0.6f, 1f), 0.2f, false, Vector3.one * 1.2f, Vector3.one * 1.4f, 0f, -160f * f, f < 0, 27);
                ArenaFX.PlaySprite(smears[1], pivot, new Color(1f, 0.92f, 0.6f, 1f), 0.2f, false, Vector3.one * 1.2f, Vector3.one * 1.4f, 0f, 160f * f, f < 0, 27);
                ArenaFX.PlaySprite(smears[0], pivot, new Color(1f, 0.7f, 0.25f, 0.5f), 0.28f, true, Vector3.one * 1.3f, Vector3.one * 1.6f, 0f, -160f * f, f < 0, 26);
                ArenaFX.PlaySprite(smears[1], pivot, new Color(1f, 0.7f, 0.25f, 0.5f), 0.28f, true, Vector3.one * 1.3f, Vector3.one * 1.6f, 0f, 160f * f, f < 0, 26);
            }
            else ArenaFX.Slash(c + new Vector2(0.9f * f, 0f), f, 2.6f, 0f, new Color(1f, 0.9f, 0.55f, 0.95f));
            if (player.starSprite != null)
                ArenaFX.PlaySprite(player.starSprite, c + new Vector2(0.9f * f, 0f), new Color(1f, 0.9f, 0.6f, 1f), 0.25f, true, Vector3.one * 0.6f, Vector3.one * 2.6f, 0f, 120f);
            // the ward bursts outward to the edge of the stun (the circle's outer ring is ~1.4 units at scale 1)
            if (wardSprite != null)
                ArenaFX.PlaySprite(wardSprite, c, new Color(1f, 0.85f, 0.4f, 0.85f), 0.4f, true, Vector3.one * 1.1f, Vector3.one * (stunRadius / 1.4f), 0f, 300f, false, 26);
            else ArenaFX.Ring(c, Gold, 0.5f, stunRadius * 2f, 0.35f);
            ArenaFX.DamageNumber(c + Vector2.up * 1.3f, "RIPOSTE!", Gold, 1.1f);
            ImpactFrame.Play(c + new Vector2(0.8f * f, 0f), ImpactFrame.Style.Gold);
            ArenaFX.HitStop(0.16f);
            ArenaFX.Shake(0.5f);

            foreach (var bug in EnemiesInCircle(c, stunRadius))
            {
                bug.Break(stunSeconds); // parried: open to a critical (press attack next to them)
                if (Vector2.Distance(bug.Center, c) <= counterRadius)
                    bug.TakeHit(Scaled(counterDamage), player.transform.position, 5f, true, true);
            }

            CooldownLeft = Mathf.Min(CooldownLeft, successCooldown);
        }

        public override void Cancel() => player.flash.ClearHold();
    }
}
