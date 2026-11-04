using System.Collections.Generic;
using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Shared boss bits: the health bar at the top of the screen, the big slow-motion death, and the
    /// tools the boss moves are built from (warning lines and floor marks, shockwaves, projectiles,
    /// the enrage at half health). Which boss shows up on which floor is decided by
    /// ArenaDirector.bossPrefabs / GameSession.BossNames; each fights in its own arena.
    /// </summary>
    public abstract class BossBug : BugEnemy
    {
        [Header("Boss effects")]
        [Tooltip("Thin line used to show where an attack will cut through.")]
        public Sprite lineSprite;
        public BossShot shotPrefab;
        [Tooltip("Rocks shaken loose from the ceiling (Rockfall).")]
        public Sprite debrisSprite;
        [Tooltip("Name on the health bar. Empty: this floor's boss name.")]
        public string barName;
        [Tooltip("Mockup 2's simple version: a few readable moves with longer wind-ups, beatable with the sword " +
                 "and a jump (no roll or shield needed). It never enrages, so none of the enraged extras either.")]
        public bool simple;

        protected bool enraged;

        /// <summary>False for a boss's first phase: dying hands over to the next form without the "ENEMY FELLED" moment.</summary>
        protected virtual bool FinalForm => true;

        protected Rect Bounds => ArenaDirector.Instance != null ? ArenaDirector.Instance.flyBounds : new Rect(-100f, -100f, 200f, 200f);

        protected override void Start()
        {
            base.Start();
            if (simple) telegraphScale *= 1.3f; // longer wind-ups
            Sfx.Play("roar", 1f, 1f, 0f);
            UpdateBar();
        }

        protected override void OnDamaged()
        {
            UpdateBar();
            if (!enraged && !simple && !IsDead && Health < maxHealth * 0.5f)
            {
                enraged = true;
                OnEnrage();
            }
        }

        /// <summary>Below half health. Bosses roar, speed up and unlock their nastiest moves.</summary>
        protected virtual void OnEnrage()
        {
            flash.Flash(new Color(1f, 0.4f, 0.3f), 0.4f);
            ArenaFX.Ring(Center, new Color(1f, 0.45f, 0.3f, 0.9f), 1f, 7f, 0.45f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.8f, "ENRAGED!", new Color(1f, 0.5f, 0.35f), 1.1f);
            ArenaFX.Shake(0.6f);
            CameraFollow2D.Zoom(0.92f, 0.4f);
        }

        protected override void Die()
        {
            if (snare != null) snare.Cancel();
            if (FinalForm)
            {
                Sfx.Play("roar", 1f, 0.75f, 0f); // a last, lower cry
                var hud = GameUIController.Instance != null ? GameUIController.Instance.hud : null;
                if (hud != null)
                {
                    hud.SetBoss(false, 0f);
                    hud.ShowBanner("ENEMY FELLED", new Color(1f, 0.84f, 0.45f), 3.2f);
                }
                ArenaFX.SlowMo(0.25f, 0.9f);
                ArenaFX.Shake(0.9f);
                for (int i = 0; i < 4; i++)
                    ArenaFX.Burst(Center + Random.insideUnitCircle, bloodColor, 20, 10f, 0.2f, 0.9f, 3f);
            }
            base.Die();
        }

        protected void UpdateBar()
        {
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null) ui.hud.SetBoss(!IsDead, Mathf.Max(0, Health) / (float)maxHealth, false, barName);
        }

        protected virtual void OnDestroy()
        {
            if (snare != null) snare.Destroy();
        }

        // ---------- falling rocks ----------

        class Rock { public float x, delay; }
        readonly List<Rock> rocks = new List<Rock>();
        static readonly Color RockWarn = new Color(1f, 0.35f, 0.3f, 0.9f);

        /// <summary>Shakes a rock loose above x: a mark on the floor shows where, then it falls.</summary>
        protected void Rockfall(float x, float delay)
        {
            var b = Bounds;
            x = Mathf.Clamp(x, b.xMin + 0.5f, b.xMax - 0.5f);
            rocks.Add(new Rock { x = x, delay = delay });
            WarnSpot(GroundBelow(new Vector2(x, b.yMax)), 0.7f, delay + 0.45f, RockWarn);
        }

        /// <summary>Call every Think so queued rocks fall.</summary>
        protected void TickRocks(float dt)
        {
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                var r = rocks[i];
                if ((r.delay -= dt) > 0f) continue;
                rocks.RemoveAt(i);
                var top = CeilingAbove(new Vector2(r.x, Bounds.yMin + 1f));
                var from = new Vector2(r.x, Mathf.Min(top.y - 0.6f, Bounds.yMax + 2f));
                Shoot(debrisSprite, from, Vector2.down * 3f, Mathf.RoundToInt(damage * 0.8f), 34f, Color.white, new Color(0.6f, 0.55f, 0.6f, 0.35f),
                    1.1f, 0.45f, false, Random.Range(-200f, 200f));
                ArenaFX.Spray(from, Vector2.down, Dust, 4, 3f, 0.6f, 0.1f);
            }
        }

        // ---------- silk snare (Silk Widow, Parasite) ----------

        protected enum SnareState { Idle, Flying, Reeling, Missed, Escaped, Caught }

        /// <summary>
        /// A strand of silk shot at the knight. If it sticks, the knight is reeled in toward the mouth
        /// (roll to cut free); reaching the mouth = Caught (bite!).
        /// </summary>
        protected class Snare
        {
            readonly BossBug owner;
            readonly Sprite silk;
            SpriteRenderer strand;
            BossShot shot;
            public SnareState State { get; private set; } = SnareState.Idle;
            float reelSpeed;
            Vector2 reelFeet; // where the knight is being dragged to (kept here so the pull doesn't depend on the frame rate)

            public Snare(BossBug owner, Sprite silk)
            {
                this.owner = owner;
                this.silk = silk;
            }

            public void Fire(Vector2 mouth, Vector2 target, float speed, float reel)
            {
                Cancel();
                reelSpeed = reel;
                var p = ArenaPlayer.Instance;
                shot = owner.Shoot(silk, mouth, (target - mouth).normalized * speed, 0, 0f, Color.white, new Color(1f, 1f, 1f, 0.25f), 1.2f, 0.4f, true, 0f, 1.4f);
                if (shot == null) { State = SnareState.Missed; return; }
                shot.OnTouchPlayer = player =>
                {
                    if (player.TryGrab(owner, shot.Position, 1, true))
                    {
                        State = SnareState.Reeling;
                        reelFeet = player.transform.position;
                        ArenaFX.Burst(player.Center, Color.white, 10, 4f, 0.08f, 0.3f, 0.5f);
                    }
                    return true;
                };
                State = SnareState.Flying;
                if (strand == null)
                {
                    var go = new GameObject("SnareStrand");
                    strand = go.AddComponent<SpriteRenderer>();
                    strand.sprite = silk;
                    strand.sortingOrder = 26;
                }
                strand.enabled = true;
            }

            /// <summary>Call every frame while snaring. Returns what's happening.</summary>
            public SnareState Tick(Vector2 mouth, float dt)
            {
                var p = ArenaPlayer.Instance;
                switch (State)
                {
                    case SnareState.Flying:
                        if (shot == null) { State = SnareState.Missed; Snap(mouth); break; }
                        Draw(mouth, shot.Position);
                        break;
                    case SnareState.Reeling:
                        if (p == null || !p.IsHeldBy(owner)) { State = SnareState.Escaped; Snap(mouth); break; }
                        var to = new Vector2(mouth.x, owner.transform.position.y + 0.05f);
                        reelFeet = Vector2.MoveTowards(reelFeet, to, reelSpeed * dt);
                        p.HoldAt(reelFeet);
                        Draw(mouth, p.Center);
                        if (Mathf.Abs(reelFeet.x - mouth.x) < 1.3f) { State = SnareState.Caught; Hide(); }
                        break;
                }
                return State;
            }

            void Draw(Vector2 a, Vector2 b)
            {
                if (strand == null) return;
                var d = b - a;
                strand.transform.position = a;
                strand.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                strand.transform.localScale = new Vector3(d.magnitude, 1f, 1f); // the silk sprite is 1 unit long
            }

            void Snap(Vector2 mouth)
            {
                if (strand != null && strand.enabled)
                    ArenaFX.Burst(strand.transform.position + strand.transform.right * strand.transform.localScale.x * 0.5f, Color.white, 6, 3f, 0.07f, 0.3f, 0.5f);
                Hide();
            }

            void Hide()
            {
                if (strand != null) strand.enabled = false;
            }

            public void Cancel()
            {
                if (shot != null) Object.Destroy(shot.gameObject);
                shot = null;
                Hide();
                var p = ArenaPlayer.Instance;
                if (p != null && p.IsHeldBy(owner)) p.Release(Vector2.zero);
                State = SnareState.Idle;
            }

            public void Destroy()
            {
                Cancel();
                if (strand != null) Object.Destroy(strand.gameObject);
            }
        }

        protected Snare snare;

        /// <summary>Feet are on something solid (ground or a platform).</summary>
        protected bool OnGround(float probe = 0.15f) =>
            Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.1f, Vector2.down, 0.1f + probe, ArenaLayers.GroundMask);

        /// <summary>Tell time, shorter once enraged and on Hard.</summary>
        protected float Tell(float seconds) => seconds * telegraphScale * (enraged ? 0.8f : 1f);

        // ---------- telegraphs ----------

        /// <summary>A glowing line where an attack is about to cut through (lasts `seconds`, real time).</summary>
        protected void WarnLine(Vector2 from, Vector2 to, float seconds, Color color, float width = 0.5f)
        {
            if (lineSprite == null) return;
            var d = to - from;
            float length = d.magnitude;
            if (length < 0.05f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var mid = (from + to) * 0.5f;
            // the line sprite is 5 units long at scale 1
            ArenaFX.PlaySprite(lineSprite, mid, color, seconds, true, new Vector3(length / 5f, width * 0.4f, 1f), new Vector3(length / 5f, width, 1f), angle, 0f, false, 33);
            ArenaFX.PlaySprite(lineSprite, mid, new Color(1f, 1f, 1f, 0.6f), seconds, false, new Vector3(length / 5f, width * 0.15f, 1f), new Vector3(length / 5f, width * 0.3f, 1f), angle, 0f, false, 34);
        }

        /// <summary>A closing ring and glow on the floor where something is about to land.</summary>
        protected static void WarnSpot(Vector2 at, float radius, float seconds, Color color)
        {
            ArenaFX.FlatRing(at + Vector2.up * 0.06f, color, radius * 3.2f, radius * 0.6f, seconds, 0.35f);
            ArenaFX.Glow(at + Vector2.up * 0.25f, new Color(color.r, color.g, color.b, 0.45f), radius * 2.4f, seconds);
        }

        /// <summary>The floor (or platform) straight below a point.</summary>
        protected static Vector2 GroundBelow(Vector2 p, float distance = 30f)
        {
            var hit = Physics2D.Raycast(p + Vector2.up * 0.3f, Vector2.down, distance, ArenaLayers.GroundMask);
            return hit ? hit.point : p;
        }

        /// <summary>The ceiling above a point (or the top of the flight area).</summary>
        protected Vector2 CeilingAbove(Vector2 p)
        {
            var hit = Physics2D.Raycast(p + Vector2.up * 0.5f, Vector2.up, 30f, ArenaLayers.SolidMask);
            return hit ? hit.point : new Vector2(p.x, Bounds.yMax + 1f);
        }

        // ---------- projectiles ----------

        protected BossShot Shoot(Sprite sprite, Vector2 from, Vector2 velocity, int amount, float gravity, Color tint, Color trail,
            float scale = 1f, float radius = 0.4f, bool face = true, float spin = 0f, float life = 4f)
        {
            if (shotPrefab == null || sprite == null) return null;
            var shot = Instantiate(shotPrefab, from, Quaternion.identity);
            shot.Launch(sprite, velocity, amount, gravity, life, tint, trail, scale, radius, face, spin);
            return shot;
        }

        // ---------- shockwaves that run along the floor ----------

        class Shockwave
        {
            public float x, y, traveled, range, speed;
            public int dir, damage;
            public bool hit;
            public Color color;
        }

        readonly List<Shockwave> waves = new List<Shockwave>();
        static readonly Color Dust = new Color(0.62f, 0.58f, 0.52f);

        /// <summary>Two shockwaves running out along the floor from a slam (jump over them).</summary>
        protected void Shockwaves(Vector2 at, float speed, float range, Color color, int amount)
        {
            waves.Add(new Shockwave { x = at.x + 0.8f, y = at.y, dir = 1, speed = speed, range = range, color = color, damage = amount });
            waves.Add(new Shockwave { x = at.x - 0.8f, y = at.y, dir = -1, speed = speed, range = range, color = color, damage = amount });
        }

        /// <summary>Call every Think so shockwaves keep moving.</summary>
        protected void TickWaves(float dt)
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                float step = w.speed * dt;
                w.x += w.dir * step;
                w.traveled += step;
                var p = new Vector2(w.x, w.y);
                bool wall = Physics2D.OverlapPoint(p + new Vector2(w.dir * 0.3f, 0.5f), ArenaLayers.SolidMask);
                bool floor = Physics2D.Raycast(p + Vector2.up * 0.4f, Vector2.down, 0.8f, ArenaLayers.GroundMask);
                if (w.traveled > w.range || wall || !floor) { waves.RemoveAt(i); continue; }

                ArenaFX.Spray(p + Vector2.up * 0.1f, new Vector2(w.dir * 0.3f, 1f), Dust, 2, 6f, 0.35f, 0.13f);
                if (Random.value < 0.6f) ArenaFX.Burst(p + Vector2.up * 0.2f, w.color, 1, 2f, 0.09f, 0.25f, 2f);
                if (!w.hit && HitPlayerInBox(p + Vector2.up * 0.45f, new Vector2(0.9f, 0.9f), w.damage, 7f) != HitResult.Ignored)
                    w.hit = true;
            }
        }
    }
}
