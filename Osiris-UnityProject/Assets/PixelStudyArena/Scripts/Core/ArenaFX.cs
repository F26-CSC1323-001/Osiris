using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Game-feel helpers: hit stop, screen shake, particle bursts, damage numbers, afterimages,
    /// slash arcs and "!" warning icons. One per arena scene; everything is called through statics.
    /// </summary>
    public class ArenaFX : MonoBehaviour
    {
        public static ArenaFX Instance { get; private set; }

        [Header("Assets (set by the builder)")]
        public Material particleMaterial;
        public Material additiveMaterial;
        public TMP_FontAsset numberFont;
        public Material numberMaterial;
        public Sprite exclaimSprite;
        public Sprite slashSprite;
        public Sprite ringSprite;
        public Sprite glowSprite;
        public Sprite fireSlashSprite;

        const float HitStopScale = 0.04f;

        class Number
        {
            public TextMeshPro text;
            public Vector3 velocity;
            public float age, life, scale;
        }

        class Ghost
        {
            public SpriteRenderer renderer;
            public float age, life;
            public Color color;
        }

        class Effect
        {
            public SpriteRenderer renderer;
            public float age, life;
            public Vector3 startScale, endScale;
            public Color color;
            public Transform follow;
            public Vector3 followOffset;
            public float spin;
        }

        ParticleSystem bursts;
        readonly List<Number> numbers = new List<Number>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Effect> effects = new List<Effect>();
        Coroutine hitStop;

        void Awake()
        {
            Instance = this;
            bursts = CreateBurstSystem();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
        }

        ParticleSystem CreateBurstSystem()
        {
            var go = new GameObject("Bursts");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = ArenaLayers.GroundMask;
            collision.bounce = 0.35f;
            collision.dampen = 0.4f;
            collision.lifetimeLoss = 0.1f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = particleMaterial;
            renderer.sortingOrder = 30;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ statics

        public static void Shake(float trauma) => CameraFollow2D.Shake(trauma);

        /// <summary>Freezes the action for a split second so hits feel heavy.</summary>
        public static void HitStop(float seconds)
        {
            if (Instance == null || Time.timeScale < 0.5f) return; // paused by a menu, or already stopped
            if (Instance.hitStop != null) Instance.StopCoroutine(Instance.hitStop);
            Instance.hitStop = Instance.StartCoroutine(Instance.HitStopRoutine(seconds));
        }

        IEnumerator HitStopRoutine(float seconds)
        {
            Time.timeScale = HitStopScale;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
            hitStop = null;
        }

        /// <summary>Slow motion (boss kill). Gives way to menus that pause the game.</summary>
        public static void SlowMo(float scale, float seconds)
        {
            if (Instance == null || Time.timeScale < 0.5f) return;
            Instance.StartCoroutine(Instance.SlowMoRoutine(scale, seconds));
        }

        IEnumerator SlowMoRoutine(float scale, float seconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = 1f;
        }

        public static void Burst(Vector2 position, Color color, int count, float speed = 6f, float size = 0.14f, float life = 0.55f, float upward = 2f)
        {
            if (Instance == null) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle;
                ep.position = position + Random.insideUnitCircle * 0.15f;
                ep.velocity = dir * speed * Random.Range(0.4f, 1f) + Vector2.up * upward;
                ep.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
                ep.startSize = size * Random.Range(0.6f, 1.3f);
                ep.startLifetime = life * Random.Range(0.6f, 1.3f);
                ep.applyShapeToPosition = false;
                Instance.bursts.Emit(ep, 1);
            }
        }

        /// <summary>Directional spray (blood toward the hit direction, dust off the feet).</summary>
        public static void Spray(Vector2 position, Vector2 direction, Color color, int count, float speed = 7f, float spread = 0.6f, float size = 0.12f)
        {
            if (Instance == null) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var dir = (direction.normalized + Random.insideUnitCircle * spread).normalized;
                ep.position = position;
                ep.velocity = dir * speed * Random.Range(0.5f, 1.1f);
                ep.startColor = color;
                ep.startSize = size * Random.Range(0.6f, 1.4f);
                ep.startLifetime = Random.Range(0.25f, 0.6f);
                Instance.bursts.Emit(ep, 1);
            }
        }

        public static void DamageNumber(Vector2 position, string text, Color color, float scale = 1f)
        {
            if (Instance == null) return;
            var n = Instance.GetNumber();
            n.text.text = text;
            n.text.color = color;
            n.text.transform.position = position + new Vector2(Random.Range(-0.25f, 0.25f), 0.2f);
            n.scale = scale;
            n.text.transform.localScale = Vector3.one * scale;
            n.velocity = new Vector3(Random.Range(-1f, 1f), 4.5f, 0f);
            n.age = 0f;
            n.life = 0.8f;
            n.text.gameObject.SetActive(true);
        }

        public static void Afterimage(SpriteRenderer source, Color color, float life = 0.22f)
        {
            if (Instance == null || source == null || source.sprite == null) return;
            var g = Instance.GetGhost();
            g.renderer.sprite = source.sprite;
            g.renderer.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            g.renderer.transform.localScale = source.transform.lossyScale;
            g.renderer.flipX = source.flipX;
            g.renderer.sortingOrder = source.sortingOrder - 1;
            g.color = color;
            g.renderer.color = color;
            g.age = 0f;
            g.life = life;
            g.renderer.gameObject.SetActive(true);
        }

        /// <summary>Sword arc in front of the player.</summary>
        public static void Slash(Vector2 position, int facing, float size, float angle, Color color)
        {
            if (Instance == null) return;
            var e = Instance.Spawn(Instance.slashSprite, position, color, 0.14f, true);
            e.renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle * facing);
            e.renderer.flipX = facing < 0;
            e.startScale = new Vector3(size * 0.8f, size * 0.8f, 1f);
            e.endScale = new Vector3(size * 1.15f, size * 1.05f, 1f);
            e.renderer.sortingOrder = 26;
        }

        /// <summary>
        /// General-purpose effect sprite for skills: grows from startScale to endScale and fades out.
        /// Optionally follows a transform and spins.
        /// </summary>
        public static void PlaySprite(Sprite sprite, Vector2 position, Color color, float life, bool additive,
            Vector3 startScale, Vector3 endScale, float rotation = 0f, float spin = 0f, bool flipX = false,
            int order = 27, Transform follow = null)
        {
            if (Instance == null || sprite == null) return;
            var e = Instance.Spawn(sprite, position, color, life, additive);
            e.startScale = startScale;
            e.endScale = endScale;
            e.renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            e.renderer.flipX = flipX;
            e.renderer.sortingOrder = order;
            e.spin = spin;
            if (follow != null)
            {
                e.follow = follow;
                e.followOffset = (Vector3)position - follow.position;
            }
        }

        /// <summary>A small lightning arc stretched between two points (sprites are 2 units long at scale 1).</summary>
        public static void Zap(Sprite[] frames, Vector2 from, Vector2 to, Color color, float life = 0.12f)
        {
            if (Instance == null || frames == null || frames.Length == 0) return;
            var d = to - from;
            float length = d.magnitude;
            if (length < 0.05f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var sprite = frames[Random.Range(0, frames.Length)];
            PlaySprite(sprite, from, color, life, true, new Vector3(length / 2f, 1.2f, 1f), new Vector3(length / 2f, 0.6f, 1f), angle, 0f, false, 31);
            PlaySprite(sprite, from, Color.white, life * 0.6f, false, new Vector3(length / 2f, 0.7f, 1f), new Vector3(length / 2f, 0.4f, 1f), angle, 0f, false, 32);
        }

        /// <summary>Brief full-screen flash (Thunderclap).</summary>
        public static void ScreenFlash(Color color, float life)
        {
            var cam = Camera.main;
            if (Instance == null || cam == null || Instance.glowSprite == null) return;
            var e = Instance.Spawn(Instance.glowSprite, cam.transform.position, color, life, true);
            e.startScale = e.endScale = Vector3.one * 90f;
            e.renderer.sortingOrder = 60;
            e.follow = cam.transform;
            e.followOffset = new Vector3(0f, 0f, 10f);
        }

        /// <summary>Big burning crescent for Flame Slash.</summary>
        public static void FireSlash(Vector2 position, int facing, float size)
        {
            if (Instance == null) return;
            var e = Instance.Spawn(Instance.fireSlashSprite != null ? Instance.fireSlashSprite : Instance.slashSprite, position, Color.white, 0.22f, false);
            e.renderer.flipX = facing < 0;
            e.startScale = new Vector3(size * 0.75f, size * 0.85f, 1f);
            e.endScale = new Vector3(size * 1.2f, size * 1.1f, 1f);
            e.renderer.sortingOrder = 27;
            var glow = Instance.Spawn(Instance.fireSlashSprite != null ? Instance.fireSlashSprite : Instance.slashSprite, position, new Color(1f, 0.6f, 0.2f, 0.8f), 0.3f, true);
            glow.renderer.flipX = facing < 0;
            glow.startScale = new Vector3(size * 0.9f, size, 1f);
            glow.endScale = new Vector3(size * 1.4f, size * 1.3f, 1f);
            glow.renderer.sortingOrder = 28;
        }

        /// <summary>Expanding glowing ring (spawn portals, parries, landings).</summary>
        public static void Ring(Vector2 position, Color color, float from, float to, float life)
        {
            if (Instance == null) return;
            var e = Instance.Spawn(Instance.ringSprite, position, color, life, true);
            e.startScale = Vector3.one * from;
            e.endScale = Vector3.one * to;
            e.renderer.sortingOrder = 27;
        }

        public static void Glow(Vector2 position, Color color, float size, float life)
        {
            if (Instance == null) return;
            var e = Instance.Spawn(Instance.glowSprite, position, color, life, true);
            e.startScale = Vector3.one * size;
            e.endScale = Vector3.one * size * 1.3f;
            e.renderer.sortingOrder = 24;
        }

        /// <summary>Red "!" over an enemy that is about to attack (Dead Cells-style tell).</summary>
        public static void Exclaim(Transform target, float height, float life)
        {
            if (Instance == null) return;
            var e = Instance.Spawn(Instance.exclaimSprite, target.position + Vector3.up * height, Color.white, life, false);
            e.follow = target;
            e.followOffset = Vector3.up * height;
            e.startScale = Vector3.one * 1.4f;
            e.endScale = Vector3.one;
            e.renderer.sortingOrder = 40;
        }

        // ------------------------------------------------------------------ pools

        Number GetNumber()
        {
            foreach (var existing in numbers) if (!existing.text.gameObject.activeSelf) return existing;
            var go = new GameObject("DamageNumber");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = numberFont;
            if (numberMaterial != null) t.fontSharedMaterial = numberMaterial;
            t.fontSize = 6f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.sortingOrder = 45;
            t.rectTransform.sizeDelta = new Vector2(4f, 1f);
            var n = new Number { text = t };
            numbers.Add(n);
            return n;
        }

        Ghost GetGhost()
        {
            foreach (var g in ghosts) if (!g.renderer.gameObject.activeSelf) return g;
            var go = new GameObject("Afterimage");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.material = additiveMaterial;
            var ghost = new Ghost { renderer = r };
            ghosts.Add(ghost);
            return ghost;
        }

        Effect Spawn(Sprite sprite, Vector2 position, Color color, float life, bool additive)
        {
            Effect e = null;
            foreach (var x in effects) if (!x.renderer.gameObject.activeSelf) { e = x; break; }
            if (e == null)
            {
                var go = new GameObject("Effect");
                go.transform.SetParent(transform, false);
                e = new Effect { renderer = go.AddComponent<SpriteRenderer>() };
                effects.Add(e);
            }
            e.renderer.sprite = sprite;
            e.renderer.material = additive ? additiveMaterial : particleMaterial;
            e.renderer.flipX = false;
            e.renderer.transform.SetPositionAndRotation(position, Quaternion.identity);
            e.renderer.color = color;
            e.color = color;
            e.age = 0f;
            e.life = life;
            e.follow = null;
            e.spin = 0f;
            e.startScale = e.endScale = Vector3.one;
            e.renderer.gameObject.SetActive(true);
            return e;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            foreach (var n in numbers)
            {
                if (!n.text.gameObject.activeSelf) continue;
                n.age += dt;
                n.velocity.y -= 12f * dt;
                n.text.transform.position += n.velocity * dt;
                float t = n.age / n.life;
                var c = n.text.color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                n.text.color = c;
                float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
                n.text.transform.localScale = Vector3.one * pop * n.scale;
                if (n.age >= n.life) n.text.gameObject.SetActive(false);
            }

            foreach (var g in ghosts)
            {
                if (!g.renderer.gameObject.activeSelf) continue;
                g.age += dt;
                var c = g.color;
                c.a *= 1f - g.age / g.life;
                g.renderer.color = c;
                if (g.age >= g.life) g.renderer.gameObject.SetActive(false);
            }

            foreach (var e in effects)
            {
                if (!e.renderer.gameObject.activeSelf) continue;
                e.age += dt;
                float t = Mathf.Clamp01(e.age / e.life);
                float ease = 1f - (1f - t) * (1f - t);
                e.renderer.transform.localScale = Vector3.Lerp(e.startScale, e.endScale, ease);
                var c = e.color;
                c.a *= 1f - t * t;
                e.renderer.color = c;
                if (e.follow != null) e.renderer.transform.position = e.follow.position + e.followOffset;
                if (e.spin != 0f) e.renderer.transform.Rotate(0f, 0f, e.spin * dt);
                if (e.age >= e.life) e.renderer.gameObject.SetActive(false);
            }
        }
    }
}
