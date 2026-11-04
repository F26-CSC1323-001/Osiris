using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Overhead Slash (Q, starting skill): the knight raises the sword (a glint on the blade), then brings
    /// it down over his head in a wide arc that covers the space above and in front of him, so it catches
    /// wasps as well as ground bugs. The blade cracks the floor and a ripple runs along the ground.
    /// </summary>
    public class OverheadSlashSkill : PlayerSkill
    {
        [Header("Overhead Slash")]
        public Sprite arcSprite;
        public Sprite crackSprite;
        public int damage = 26;
        [Tooltip("Hit area relative to the knight's feet (x is flipped with facing).")]
        public Vector2 hitOffset = new Vector2(1.1f, 1.7f);
        public Vector2 hitSize = new Vector2(3.6f, 3.8f);
        [Tooltip("Frame of the 'overhead' animation where the sword is highest (it glints).")]
        public int raiseFrame = 2;
        [Tooltip("Frame of the 'overhead' animation where the arc comes down.")]
        public int releaseFrame = 4;

        static readonly Color Edge = new Color(0.55f, 0.9f, 1f);
        static readonly Color Dust = new Color(0.75f, 0.78f, 0.82f);
        static readonly Color Stone = new Color(0.45f, 0.5f, 0.58f);

        bool glinted, released;
        int ripple;
        float rippleTimer;
        Vector2 rippleFrom;

        public override bool TryActivate()
        {
            glinted = released = false;
            ripple = -1;
            StartCooldown();
            player.anim.Play("overhead", true);
            player.Squash(0.9f, 1.1f); // rises up into the swing
            return true;
        }

        public override bool Tick(float dt)
        {
            if (!glinted && player.anim.Frame >= raiseFrame) Glint();
            if (!released && player.anim.Frame >= releaseFrame) Release();

            // a ripple of dust and light running along the floor after the blade hits
            if (ripple >= 0 && ripple < 4 && (rippleTimer -= dt) <= 0f)
            {
                rippleTimer = 0.045f;
                ripple++;
                var p = rippleFrom + new Vector2(player.Facing * 0.85f * ripple, 0.05f);
                ArenaFX.Spray(p, Vector2.up, Dust, 3, 4.5f - ripple * 0.6f, 0.35f, 0.12f);
                ArenaFX.Burst(p + Vector2.up * 0.1f, Edge, 2, 2.5f, 0.07f, 0.2f, 1f);
                ArenaFX.FlatRing(p + Vector2.up * 0.05f, new Color(0.6f, 0.9f, 1f, 0.6f), 0.2f, 0.9f, 0.18f);
            }
            return player.anim.IsDone && (ripple < 0 || ripple >= 4);
        }

        void Glint()
        {
            glinted = true;
            int f = player.Facing;
            var tip = (Vector2)player.transform.position + new Vector2(-0.35f * f, 2.45f);
            if (player.starSprite != null)
                ArenaFX.PlaySprite(player.starSprite, tip, Color.white, 0.18f, true, Vector3.one * 0.3f, Vector3.one * 1.2f, 0f, 220f);
            ArenaFX.Burst(tip, Edge, 4, 2f, 0.06f, 0.18f, 0f);
            player.flash.Flash(new Color(0.75f, 0.95f, 1f), 0.08f);
            player.Squash(0.88f, 1.14f);
        }

        void Release()
        {
            released = true;
            player.Squash(1.2f, 0.84f);
            int f = player.Facing;
            var pivot = (Vector2)player.transform.position + new Vector2(0.15f * f, 0.95f);

            // the arc: a bright pass, a soft glow and a faint trailing echo, sweeping a little as they fade
            ArenaFX.PlaySprite(arcSprite, pivot, Color.white, 0.2f, false, new Vector3(0.95f, 0.95f, 1f), new Vector3(1.25f, 1.2f, 1f), f > 0 ? 8f : -8f, f > 0 ? -60f : 60f, f < 0, 27);
            ArenaFX.PlaySprite(arcSprite, pivot, new Color(0.45f, 0.8f, 1f, 0.7f), 0.28f, true, new Vector3(1.05f, 1.05f, 1f), new Vector3(1.4f, 1.35f, 1f), f > 0 ? 8f : -8f, f > 0 ? -60f : 60f, f < 0, 28);
            ArenaFX.PlaySprite(arcSprite, pivot, new Color(0.35f, 0.6f, 1f, 0.35f), 0.38f, true, new Vector3(1.2f, 1.2f, 1f), new Vector3(1.6f, 1.5f, 1f), f > 0 ? 25f : -25f, f > 0 ? -40f : 40f, f < 0, 26);
            ArenaFX.Afterimage(player.body, new Color(0.5f, 0.85f, 1f, 0.5f), 0.25f);

            // the blade bites into the floor
            var ground = (Vector2)player.transform.position + new Vector2(1.9f * f, 0f);
            var floor = Physics2D.Raycast(ground + Vector2.up * 0.8f, Vector2.down, 1.6f, ArenaLayers.GroundMask);
            if (floor)
            {
                ground = floor.point;
                if (crackSprite != null)
                    ArenaFX.PlaySprite(crackSprite, ground, Color.white, 0.9f, false, new Vector3(0.7f, 0.7f, 1f), new Vector3(1.05f, 1f, 1f), 0f, 0f, f < 0, 23);
                ArenaFX.FlatRing(ground + Vector2.up * 0.08f, new Color(0.7f, 0.92f, 1f, 0.85f), 0.4f, 3f, 0.25f);
                ArenaFX.Spray(ground, new Vector2(f, 1f), Dust, 8, 6f, 0.45f, 0.13f);
                ArenaFX.Spray(ground, new Vector2(-f, 0.9f), Dust, 5, 4.5f, 0.45f, 0.11f);
                ArenaFX.Burst(ground + Vector2.up * 0.15f, Stone, 6, 6f, 0.11f, 0.5f, 4f);
                ArenaFX.Spray(ground + Vector2.up * 0.1f, Vector2.up, Edge, 8, 8f, 0.25f, 0.07f);
                ripple = 0;
                rippleTimer = 0.03f;
                rippleFrom = ground;
            }

            bool hit = false;
            var center = (Vector2)player.transform.position + new Vector2(hitOffset.x * f, hitOffset.y);
            foreach (var bug in EnemiesInBox(center, hitSize))
            {
                bug.TakeHit(Scaled(damage), player.Center, 7f, true, false, 14f);
                ArenaFX.Spray(bug.Center, new Vector2(f, -0.4f), Edge, 6, 7f, 0.4f, 0.09f);
                hit = true;
            }

            player.Lunge(4f, 0.08f);
            ArenaFX.HitStop(hit ? 0.08f : 0.03f);
            ArenaFX.Shake(hit ? 0.35f : 0.2f);
        }
    }
}
