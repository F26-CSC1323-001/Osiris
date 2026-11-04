using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Flame Slash's fire: a blazing arc left in the air along the path of the rising swing. It burns
    /// everything it covers, then fire pours off it in blobs that splash down in front of the knight
    /// and keep burning on the floor for a moment. Runs on world time (slowed while Flame Slash slows
    /// the world, so the eruption plays out in slow motion).
    /// </summary>
    public class FireArc : MonoBehaviour
    {
        public SpriteRenderer arc;
        public SpriteRenderer arcGlow;
        [Tooltip("The first frames show the fire pouring out along the swing; the rest loop.")]
        public Sprite[] arcFrames;
        public int revealFrames = 3;
        public Sprite[] globFrames;
        public Sprite[] groundFireFrames;
        public Material additiveMaterial;

        [Header("Shape (matches the art; facing right, degrees counter-clockwise from forward)")]
        public float radius = 2.2f;
        public float fromAngle = -40f;
        public float toAngle = 118f;

        [Header("Timing")]
        public float life = 0.85f;
        public float hitWindow = 0.22f;

        [Header("Pouring fire")]
        public int globCount = 14;
        [Tooltip("Blobs of fire keep spilling off the arc for this long.")]
        public float pourTime = 0.45f;
        public float globGravity = 18f;
        public float patchTime = 1.8f;

        static readonly Color FireDefault = new Color(1f, 0.55f, 0.15f);
        static readonly Color FireHotDefault = new Color(1f, 0.85f, 0.35f);
        static readonly Color EmberDefault = new Color(1f, 0.35f, 0.1f);

        // the Infested Knight's version burns violet and hurts the knight instead of bugs
        bool hostile;
        Color tint = Color.white;
        Color Fire = FireDefault, FireHot = FireHotDefault, Ember = EmberDefault;
        bool playerHitByArc;

        class Glob
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float delay, age;
            public bool live, hitPlayer;
            public readonly HashSet<BugEnemy> hit = new HashSet<BugEnemy>();
        }

        class Patch
        {
            public SpriteRenderer sr;
            public Vector2 pos;
            public float age, tick, scale;
            public int frame;
        }

        readonly HashSet<BugEnemy> arcHit = new HashSet<BugEnemy>();
        readonly List<Glob> globs = new List<Glob>();
        readonly List<Patch> patches = new List<Patch>();
        ContactFilter2D enemyFilter;
        static readonly List<Collider2D> Hits = new List<Collider2D>();

        int dir = 1, damage, pourDamage, frame;
        float burnDps, burnSeconds, age, frameTimer, emberTimer;
        bool arcDone, launched;

        /// <summary>Where a local angle (facing right) points, in the world.</summary>
        Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * dir, Mathf.Sin(r));
        }

        Vector2 Origin => transform.position;

        /// <summary>A boss's arc (use a prefab with recolored sprites; color lightly tints them): burns the knight instead of the bugs.</summary>
        public void LaunchHostile(int direction, int arcDamage, int pourDamageAmount, Color color, Color hot, Color ember)
        {
            hostile = true;
            tint = color;
            Fire = color;
            FireHot = hot;
            Ember = ember;
            Launch(direction, arcDamage, pourDamageAmount, 0f, 0f);
        }

        public void Launch(int direction, int arcDamage, int pourDamageAmount, float dps, float burnTime)
        {
            dir = direction >= 0 ? 1 : -1;
            damage = arcDamage;
            pourDamage = pourDamageAmount;
            burnDps = dps;
            burnSeconds = burnTime;
            enemyFilter = new ContactFilter2D();
            enemyFilter.SetLayerMask(ArenaLayers.EnemyMask);
            enemyFilter.useTriggers = true;
            arc.flipX = dir < 0;
            if (arcGlow != null) arcGlow.flipX = dir < 0;
            if (arcFrames != null && arcFrames.Length > 0) arc.sprite = arcFrames[0];
            Apply(0f);

            // blobs of fire keep spilling off the arc for a moment and splash down ahead of the knight
            for (int i = 0; i < globCount; i++)
            {
                float k = globCount == 1 ? 0.5f : i / (float)(globCount - 1);
                float a = Mathf.Lerp(fromAngle + 15f, toAngle - 10f, Random.value);
                var outward = Dir(a);
                var g = new Glob
                {
                    pos = Origin + outward * radius * Random.Range(0.7f, 0.95f),
                    vel = outward * Random.Range(1.5f, 4f) + new Vector2(dir * Random.Range(2.5f, 7f), Random.Range(0.5f, 3.5f)),
                    delay = 0.05f + k * pourTime + Random.Range(-0.04f, 0.04f),
                };
                var go = new GameObject("FireGlob");
                go.transform.SetParent(transform, false);
                g.sr = go.AddComponent<SpriteRenderer>();
                g.sr.sortingOrder = 28;
                g.sr.color = hostile ? Color.Lerp(tint, Color.white, 0.7f) : Color.white;
                g.sr.enabled = false;
                if (globFrames != null && globFrames.Length > 0) g.sr.sprite = globFrames[i % globFrames.Length];
                globs.Add(g);
            }
            launched = true;
        }

        void Update()
        {
            if (!launched || Time.timeScale < 0.01f) return;
            float dt = Time.deltaTime;
            age += dt;

            if (!arcDone) UpdateArc(dt);
            UpdateGlobs(dt);
            UpdatePatches(dt);

            if (arcDone && globs.Count == 0 && patches.Count == 0) Destroy(gameObject);
        }

        // ---------- the arc ----------

        void UpdateArc(float dt)
        {
            float t = Mathf.Clamp01(age / life);
            Apply(t);

            if (arcFrames != null && arcFrames.Length > 1 && (frameTimer -= dt) <= 0f)
            {
                // pour out along the swing (fast), then flicker
                bool revealing = frame < revealFrames - 1;
                frameTimer = revealing ? 1f / 40f : 1f / 16f;
                int loop = Mathf.Max(1, arcFrames.Length - revealFrames);
                frame = revealing ? frame + 1 : revealFrames + (frame + 1 - revealFrames) % loop;
                arc.sprite = arcFrames[frame];
                if (arcGlow != null) arcGlow.sprite = arcFrames[frame];
            }

            // flames keep rising off the arc
            if ((emberTimer -= dt) <= 0f)
            {
                emberTimer = 0.02f;
                float a = Random.Range(fromAngle, toAngle);
                var p = Origin + Dir(a) * radius * Random.Range(0.7f, 1f) * transform.localScale.x;
                ArenaFX.Spray(p, new Vector2(Random.Range(-0.3f, 0.3f), 1f), Random.value < 0.5f ? Fire : FireHot, 2, 3.5f, 0.35f, 0.14f);
            }

            if (age <= hitWindow) HitArc();

            if (age >= life)
            {
                arcDone = true;
                arc.enabled = false;
                if (arcGlow != null) arcGlow.enabled = false;
                // the arc breaks apart into embers
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Lerp(fromAngle, toAngle, i / 8f);
                    ArenaFX.Burst(Origin + Dir(a) * radius * 0.85f, Random.value < 0.5f ? Fire : Ember, 4, 3f, 0.11f, 0.6f, 1.5f);
                }
            }
        }

        void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(0.9f, 1.1f, ease);
            transform.localScale = new Vector3(s, s, 1f);
            float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
            var c = hostile ? Color.Lerp(tint, Color.white, 0.7f) : Color.white;
            arc.color = new Color(c.r, c.g, c.b, alpha);
            if (arcGlow != null) arcGlow.color = hostile ? new Color(tint.r, tint.g, tint.b, 0.45f * alpha) : new Color(1f, 0.55f, 0.2f, 0.4f * alpha);
        }

        /// <summary>Inside the arc's sweep (or right at its heart).</summary>
        bool InArc(Vector2 point)
        {
            var d = point - Origin;
            if (d.magnitude > radius * transform.localScale.x + 0.9f) return false;
            float a = Mathf.Atan2(d.y, d.x * dir) * Mathf.Rad2Deg;
            return d.magnitude < 1f || (a >= fromAngle - 20f && a <= toAngle + 15f);
        }

        void HitArc()
        {
            if (hostile)
            {
                var p = ArenaPlayer.Instance;
                if (!playerHitByArc && p != null && !p.IsDead && InArc(p.Center))
                {
                    playerHitByArc = true;
                    if (p.TakeHit(damage, Origin - new Vector2(dir, 0f), 10f) == HitResult.Damaged)
                        ArenaFX.Burst(p.Center, Fire, 14, 7f, 0.13f, 0.45f, 1.5f);
                }
                return;
            }
            Hits.Clear();
            Physics2D.OverlapCircle(Origin, radius * transform.localScale.x + 0.7f, enemyFilter, Hits);
            foreach (var h in Hits)
            {
                var bug = h.GetComponentInParent<BugEnemy>();
                if (bug == null || bug.IsDead || arcHit.Contains(bug)) continue;
                var d = bug.Center - Origin;
                float a = Mathf.Atan2(d.y, d.x * dir) * Mathf.Rad2Deg;
                bool inside = d.magnitude < 1f || (a >= fromAngle - 20f && a <= toAngle + 15f);
                if (!inside) continue;
                arcHit.Add(bug);
                bug.TakeHit(damage, Origin - new Vector2(dir, 0f), 9f, true, true, 45f);
                if (!bug.IsDead) bug.ApplyBurn(burnDps, burnSeconds);
                ArenaFX.Burst(bug.Center, Fire, 14, 7f, 0.13f, 0.45f, 1.5f);
                ArenaFX.Burst(bug.Center, Color.white, 5, 5f, 0.08f, 0.2f, 0.5f);
            }
        }

        // ---------- pouring blobs ----------

        void UpdateGlobs(float dt)
        {
            for (int i = globs.Count - 1; i >= 0; i--)
            {
                var g = globs[i];
                if (!g.live)
                {
                    if ((g.delay -= dt) > 0f) continue;
                    g.live = true;
                    g.sr.enabled = g.sr.sprite != null;
                }
                g.age += dt;
                var prev = g.pos;
                g.vel.y -= globGravity * dt;
                g.pos += g.vel * dt;
                g.sr.transform.position = g.pos;
                g.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(g.vel.y, g.vel.x) * Mathf.Rad2Deg);
                if (globFrames != null && globFrames.Length > 1)
                    g.sr.sprite = globFrames[(int)(g.age * 14f + i) % globFrames.Length];

                // a trail of flame rising off it
                if (Random.value < 0.5f) ArenaFX.Spray(g.pos, Vector2.up, Random.value < 0.5f ? Fire : FireHot, 1, 1.2f, 0.6f, 0.11f);

                // burn what it passes through
                if (hostile)
                {
                    var pl = ArenaPlayer.Instance;
                    if (!g.hitPlayer && pl != null && !pl.IsDead && Vector2.Distance(pl.Center, g.pos) < 0.7f)
                    {
                        g.hitPlayer = true;
                        pl.TakeHit(pourDamage, g.pos, 4f);
                    }
                }
                else
                {
                    Hits.Clear();
                    Physics2D.OverlapCircle(g.pos, 0.35f, enemyFilter, Hits);
                    foreach (var h in Hits)
                    {
                        var bug = h.GetComponentInParent<BugEnemy>();
                        if (bug == null || bug.IsDead || !g.hit.Add(bug) || arcHit.Contains(bug) && g.age < 0.1f) continue;
                        bug.TakeHit(pourDamage, g.pos, 3f, false, false, 6f);
                        if (!bug.IsDead) bug.ApplyBurn(burnDps, burnSeconds);
                    }
                }

                // splash down on the floor (or a platform) and keep burning there
                var land = Physics2D.Linecast(prev, g.pos, ArenaLayers.GroundMask);
                bool landed = land.collider != null && land.normal.y > 0.5f;
                if (landed || g.age > 3f || (land.collider != null && land.normal.y <= 0.5f))
                {
                    if (landed)
                    {
                        AddPatch(land.point);
                        ArenaFX.Spray(land.point + Vector2.up * 0.05f, new Vector2(-0.6f, 1f), FireHot, 3, 4f, 0.4f, 0.11f);
                        ArenaFX.Spray(land.point + Vector2.up * 0.05f, new Vector2(0.6f, 1f), Fire, 3, 4f, 0.4f, 0.11f);
                    }
                    else ArenaFX.Burst(g.pos, Fire, 5, 3f, 0.1f, 0.4f, 1f);
                    Destroy(g.sr.gameObject);
                    globs.RemoveAt(i);
                }
            }
        }

        // ---------- burning patches on the floor ----------

        void AddPatch(Vector2 at)
        {
            // don't stack patches right on top of each other
            foreach (var other in patches)
                if (Mathf.Abs(other.pos.x - at.x) < 0.35f && Mathf.Abs(other.pos.y - at.y) < 0.3f) { other.age = Mathf.Min(other.age, 0.2f); return; }
            var go = new GameObject("GroundFire");
            go.transform.SetParent(transform, false);
            var p = new Patch { pos = at, scale = Random.Range(1.1f, 1.6f), frame = Random.Range(0, 4) };
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sortingOrder = 23;
            p.sr.color = hostile ? Color.Lerp(tint, Color.white, 0.7f) : Color.white;
            p.sr.sprite = groundFireFrames != null && groundFireFrames.Length > 0 ? groundFireFrames[p.frame % groundFireFrames.Length] : null;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * p.scale / Mathf.Max(0.01f, transform.localScale.x);
            patches.Add(p);
        }

        void UpdatePatches(float dt)
        {
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                var p = patches[i];
                p.age += dt;
                float t = p.age / patchTime;
                if (t >= 1f)
                {
                    ArenaFX.Burst(p.pos + Vector2.up * 0.2f, Ember, 3, 2f, 0.08f, 0.4f, 1.5f);
                    Destroy(p.sr.gameObject);
                    patches.RemoveAt(i);
                    continue;
                }
                // flicker, and die down at the end
                if (groundFireFrames != null && groundFireFrames.Length > 1)
                    p.sr.sprite = groundFireFrames[(int)(p.age * 12f + p.frame) % groundFireFrames.Length];
                float shrink = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                float grow = Mathf.Clamp01(p.age / 0.12f);
                p.sr.transform.position = p.pos;
                p.sr.transform.localScale = new Vector3(p.scale, p.scale * grow * Mathf.Max(0.15f, shrink), 1f) / Mathf.Max(0.01f, transform.localScale.x);
                if (Random.value < 0.25f) ArenaFX.Spray(p.pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f), Vector2.up, Random.value < 0.5f ? Fire : FireHot, 1, 2.5f, 0.3f, 0.1f);

                // anything standing in it burns
                if ((p.tick -= dt) > 0f) continue;
                p.tick = 0.35f;
                if (hostile)
                {
                    var pl = ArenaPlayer.Instance;
                    if (pl != null && !pl.IsDead && Mathf.Abs(pl.transform.position.x - p.pos.x) < 0.45f * p.scale && Mathf.Abs(pl.transform.position.y - p.pos.y) < 0.6f)
                        pl.TakePoison(Mathf.Max(1, pourDamage / 3));
                    continue;
                }
                Hits.Clear();
                Physics2D.OverlapBox(p.pos + Vector2.up * 0.4f, new Vector2(0.9f * p.scale, 0.8f), 0f, enemyFilter, Hits);
                foreach (var h in Hits)
                {
                    var bug = h.GetComponentInParent<BugEnemy>();
                    if (bug == null || bug.IsDead) continue;
                    bug.ApplyBurn(burnDps, burnSeconds);
                    bug.TakeChipDamage(Mathf.Max(1, pourDamage / 4), Fire);
                }
            }
        }
    }
}
