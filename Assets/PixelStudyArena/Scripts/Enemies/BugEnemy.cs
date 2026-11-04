using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Shared bug behaviour: health, hit flash, knockback, stagger, stun, damage numbers, coin drops
    /// and the death burst. Subclasses implement the AI in Think().
    ///
    /// Stance (Elden Ring style): every hit also deals stance damage (heavy hits, jump attacks and
    /// charged stabs deal a lot). When it fills up the bug's stance breaks: it reels, open to a
    /// critical hit (press attack next to it). Parries open bugs up the same way. Stance recovers if
    /// the bug isn't hit for a few seconds.
    /// </summary>
    public abstract class BugEnemy : MonoBehaviour
    {
        [Header("Stats")]
        public int maxHealth = 30;
        public int damage = 12;
        public Vector2Int coinDrop = new Vector2Int(2, 4);
        [Range(0f, 1f)] public float heartDropChance = 0.06f;
        [Tooltip("Never flinches from hits (bosses). Stance breaks still stagger it.")]
        public bool superArmor;

        [Header("Stance")]
        [Tooltip("Stance damage it can take before it breaks and is open to a critical hit.")]
        public float stance = 24f;
        public float stanceRecoverDelay = 3f;
        public float stanceBreakTime = 2.2f;

        [Header("Parts")]
        public Transform visual;
        public SpriteRenderer body;
        public PixelAnimator anim;
        public SpriteFlash flash;
        public Color bloodColor = new Color(0.55f, 0.85f, 0.3f);
        [Tooltip("Sprite color (the Infested Knight is a darkened knight).")]
        public Color tint = Color.white;
        public float centerHeight = 0.5f;
        public float exclaimHeight = 1.4f;

        public int Health { get; protected set; }
        public bool IsDead { get; private set; }
        public Vector2 Center => (Vector2)transform.position + Vector2.up * centerHeight;
        public event Action<BugEnemy> Died;

        protected Rigidbody2D rb;
        protected ArenaPlayer player;
        protected int facing = 1;
        protected float stunTimer, staggerTimer;
        protected float telegraphScale = 1f;

        float burnTimer, burnDps, burnTick, burnFxTimer;
        static readonly Color BurnColor = new Color(1f, 0.55f, 0.15f);

        public bool IsBurning => burnTimer > 0f;

        /// <summary>Set while the knight's ultimate lands its hits: bosses can't dodge or parry them.</summary>
        public static bool Unavoidable;

        /// <summary>Dazed by a parry, Riposte, lightning, a stance break... (the knight's heavy stab crits on these).</summary>
        public bool IsStunned => stunTimer > 0f;

        /// <summary>Stance broken or parried: the knight can finish it with a critical hit right now.</summary>
        public bool CanCritical => critOpen && stunTimer > 0f && !IsDead;

        /// <summary>0..1, how close the stance is to breaking.</summary>
        public float Stance01 => stance <= 0f ? 0f : Mathf.Clamp01(stanceDamage / stance);

        /// <summary>While true, light hits don't interrupt this bug (it's mid-attack). Heavy hits still do.</summary>
        protected virtual bool HyperArmor => false;

        /// <summary>While true, nothing can hurt, stun or slow this bug (the Reaper Mantis mid-vanish).</summary>
        protected virtual bool Untouchable => false;

        /// <summary>Alive and actually there to be hit (skills skip bugs that aren't).</summary>
        public bool CanBeTargeted => !IsDead && !Untouchable;

        float stanceDamage, stanceTimer, glintTimer;
        bool critOpen;
        static readonly Color CritGold = new Color(1f, 0.85f, 0.35f);

        static readonly Color WindUpColor = new Color(1f, 0.45f, 0.15f);
        protected static readonly List<Collider2D> Scratch = new List<Collider2D>();

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * ArenaDirector.HealthScale));
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * ArenaDirector.DamageScale));
            telegraphScale = ArenaDirector.TelegraphScale;
            Health = maxHealth;
        }

        protected virtual void Start()
        {
            player = ArenaPlayer.Instance;
            flash.Flash(new Color(0.8f, 0.5f, 1f), 0.35f);
        }

        void Update()
        {
            if (IsDead || Time.timeScale < 0.01f) return;
            if (player == null) player = ArenaPlayer.Instance;
            float dt = Time.deltaTime;

            UpdateBurn(dt);
            if (IsDead) return;
            UpdateSlow(dt);
            UpdateStance(dt);
            UpdateShock(dt);
            dt *= SpeedScale; // slowed bugs think, wind up and recover slower too

            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                if (critOpen) CritGlint(dt);
                if (stunTimer <= 0f)
                {
                    if (critOpen) { critOpen = false; flash.ClearHold(); }
                    OnStunEnd();
                }
                WhileStunned(dt);
                return;
            }
            if (staggerTimer > 0f)
            {
                staggerTimer -= dt;
                WhileStaggered(dt);
                return;
            }
            Think(dt);
            if (visual != null) visual.localScale = new Vector3(facing * Mathf.Abs(visual.localScale.x), visual.localScale.y, 1f);
        }

        protected abstract void Think(float dt);

        protected virtual void WhileStunned(float dt)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 20f * dt), rb.linearVelocity.y);
        }

        protected virtual void WhileStaggered(float dt) { }

        protected virtual void OnStunEnd() { }

        /// <summary>Called when a hit interrupts this bug (not for super-armored bosses).</summary>
        protected virtual void OnStagger() { }

        /// <summary>
        /// A hit from the knight or a skill. poise is the stance damage; leave it at -1 to work it out
        /// from the damage (heavy hits break stance faster).
        /// </summary>
        public virtual void TakeHit(int amount, Vector2 from, float knockback, bool heavy, bool crit, float poise = -1f)
        {
            if (IsDead || Untouchable) return;
            if (stunTimer > 0f) amount = Mathf.RoundToInt(amount * 1.5f);
            Health -= amount;
            UltimateSkill.Feed(amount);
            Sfx.Play(crit ? "crit" : "hit", crit ? 0.9f : 0.6f, heavy ? 0.85f : 1f);

            int dir = Center.x >= from.x ? 1 : -1;
            flash.Flash(Color.white, 0.12f);
            ArenaFX.DamageNumber(Center + Vector2.up * 0.5f, crit ? amount + "!" : amount.ToString(),
                crit ? new Color(1f, 0.85f, 0.3f) : Color.white, crit ? 1.25f : 1f);
            ArenaFX.Spray(Center, new Vector2(dir, 0.35f), bloodColor, crit ? 12 : 7, 7f, 0.5f, 0.13f);

            bool flinch = !superArmor && !(HyperArmor && !heavy);
            if (flinch && stunTimer <= 0f)
            {
                staggerTimer = heavy ? 0.4f : 0.22f;
                if (rb != null)
                {
                    float up = rb.bodyType == RigidbodyType2D.Dynamic ? (heavy ? 5f : 2.5f) : 0f;
                    rb.linearVelocity = new Vector2(dir * knockback, up);
                }
                OnStagger();
            }
            else if (!flinch && !superArmor)
            {
                // hyper armor: it shrugs the hit off and keeps attacking
                ArenaFX.Burst(Center, new Color(1f, 0.6f, 0.3f), 4, 4f, 0.08f, 0.2f, 0.5f);
            }

            OnDamaged();
            if (Health <= 0) { Die(); return; }
            AddStance(poise >= 0f ? poise : amount * (heavy ? 0.9f : 0.5f));
        }

        // ---------- stance ----------

        void AddStance(float amount)
        {
            if (amount <= 0f || stance <= 0f || stunTimer > 0f) return;
            stanceDamage += amount;
            stanceTimer = stanceRecoverDelay;
            if (stanceDamage >= stance) BreakStance();
        }

        void UpdateStance(float dt)
        {
            if (stanceDamage <= 0f) return;
            if ((stanceTimer -= dt) > 0f) return;
            stanceDamage = Mathf.MoveTowards(stanceDamage, 0f, stance * 0.4f * dt);
        }

        void BreakStance()
        {
            stanceDamage = 0f;
            ArenaFX.DamageNumber(Center + Vector2.up * 1.2f, "STANCE BROKEN", CritGold, 0.65f);
            ArenaFX.Ring(Center, new Color(1f, 0.85f, 0.4f, 0.8f), 0.3f, 1.7f, 0.22f);
            ArenaFX.Burst(Center, CritGold, 14, 6f, 0.1f, 0.4f, 1.5f);
            ArenaFX.Shake(0.25f);
            Break(stanceBreakTime);
        }

        /// <summary>Staggers the bug and leaves it open to a critical hit (stance break, parry).</summary>
        public void Break(float seconds)
        {
            if (IsDead || Untouchable) return;
            bool already = stunTimer > 0f;
            stunTimer = Mathf.Max(stunTimer, seconds);
            critOpen = true;
            glintTimer = 0f;
            stanceDamage = 0f;
            flash.Flash(Color.white, 0.2f);
            flash.Hold(CritGold, 0.35f, 9f);
            if (!already) OnStagger();
        }

        /// <summary>The knight's critical lands: holds the bug in place for the finisher. One critical per opening.</summary>
        public void HoldForCritical(float seconds)
        {
            critOpen = false;
            flash.ClearHold();
            stunTimer = Mathf.Max(stunTimer, seconds);
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>Gold glint over a bug that's open to a critical.</summary>
        void CritGlint(float dt)
        {
            if ((glintTimer -= dt) > 0f) return;
            glintTimer = 0.45f;
            var top = Center + Vector2.up * (exclaimHeight - centerHeight + 0.1f);
            if (ArenaPlayer.Instance != null && ArenaPlayer.Instance.starSprite != null)
                ArenaFX.PlaySprite(ArenaPlayer.Instance.starSprite, top, new Color(1f, 0.9f, 0.5f, 0.9f), 0.35f, true, Vector3.one * 0.25f, Vector3.one * 0.7f, 0f, 160f);
            ArenaFX.Burst(top, CritGold, 2, 1.5f, 0.07f, 0.3f, 0.5f);
        }

        /// <summary>A tell that sometimes holds a little longer, so panic rolls get punished (Elden Ring's delayed attacks).</summary>
        protected float DelayedTell(float seconds, float maxExtra = 0.6f)
        {
            float extra = UnityEngine.Random.value < 0.35f ? UnityEngine.Random.Range(0.25f, 1f) * maxExtra : UnityEngine.Random.Range(0f, 0.15f);
            return seconds * telegraphScale * (1f + extra);
        }

        /// <summary>Called after this bug loses health (hits and burn ticks).</summary>
        protected virtual void OnDamaged() { }

        /// <summary>Sets the bug on fire: damage every half second for a few seconds.</summary>
        public void ApplyBurn(float damagePerSecond, float seconds)
        {
            if (IsDead) return;
            burnDps = Mathf.Max(burnDps, damagePerSecond);
            burnTimer = Mathf.Max(burnTimer, seconds);
            if (burnTick <= 0f) burnTick = 0.5f;
        }

        void UpdateBurn(float dt)
        {
            if (burnTimer <= 0f) return;
            burnTimer -= dt;
            burnTick -= dt;
            burnFxTimer -= dt;
            if (burnFxTimer <= 0f)
            {
                burnFxTimer = 0.06f;
                var p = Center + UnityEngine.Random.insideUnitCircle * 0.35f;
                ArenaFX.Spray(p, Vector2.up, UnityEngine.Random.value < 0.5f ? BurnColor : new Color(1f, 0.85f, 0.35f), 1, 2.5f, 0.4f, 0.12f);
            }
            if (burnTick <= 0f)
            {
                burnTick = 0.5f;
                TakeChipDamage(Mathf.Max(1, Mathf.RoundToInt(burnDps * 0.5f)), BurnColor);
            }
            if (burnTimer <= 0f) burnDps = 0f;
        }

        /// <summary>Light damage that doesn't interrupt the bug (burning, fire patches).</summary>
        public void TakeChipDamage(int amount, Color color)
        {
            if (IsDead || Untouchable) return;
            Health -= amount;
            UltimateSkill.Feed(amount);
            flash.Flash(color, 0.08f);
            ArenaFX.DamageNumber(Center + Vector2.up * 0.4f, amount.ToString(), color, 0.7f);
            OnDamaged();
            if (Health <= 0) Die();
        }

        // ---------- slow ----------

        public const float SlowFactor = 0.45f;
        static readonly Color SlowTint = new Color(0.62f, 0.8f, 1f);
        float slowTimer, slowFxTimer;

        public bool IsSlowed => slowTimer > 0f;

        /// <summary>1 normally, SlowFactor while slowed. Movement code multiplies speeds by this.</summary>
        protected float SpeedScale => IsSlowed ? SlowFactor : 1f;

        /// <summary>Slows movement, attacks and animation for a while.</summary>
        public void ApplySlow(float seconds)
        {
            if (IsDead) return;
            slowTimer = Mathf.Max(slowTimer, seconds);
        }

        void UpdateSlow(float dt)
        {
            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                slowFxTimer -= dt;
                if (slowFxTimer <= 0f)
                {
                    slowFxTimer = 0.15f;
                    ArenaFX.Burst(Center + UnityEngine.Random.insideUnitCircle * 0.4f, SlowTint, 1, 1.5f, 0.06f, 0.25f, 0f);
                }
            }
            if (body != null) body.color = IsSlowed ? SlowTint * tint : tint;
            if (anim != null) anim.speed = SpeedScale;
        }

        /// <summary>Parried: the bug is dazed and takes extra damage for a moment.</summary>
        public virtual void Stun(float seconds)
        {
            if (IsDead || Untouchable) return;
            bool already = stunTimer > 0f;
            stunTimer = Mathf.Max(stunTimer, seconds);
            if (already) return;
            flash.ClearHold();
            flash.Flash(new Color(1f, 0.9f, 0.4f), 0.3f);
            ArenaFX.DamageNumber(Center + Vector2.up * 0.9f, "STUNNED", new Color(1f, 0.9f, 0.4f), 0.75f);
            OnStagger();
        }

        /// <summary>Freezes the bug in place for a moment, no fuss (Thousand Cuts holds its victims).</summary>
        public void Hold(float seconds)
        {
            if (IsDead || Untouchable) return;
            bool already = stunTimer > 0f;
            stunTimer = Mathf.Max(stunTimer, seconds);
            EndTell();
            if (!already) OnStagger();
        }

        // ---------- electrified (lightning skills) ----------

        float shockTimer, shockFxTimer;
        static readonly Color ShockBlue = new Color(0.6f, 0.9f, 1f);

        public bool IsShocked => shockTimer > 0f;

        /// <summary>Stunned by lightning: electricity crackles over the bug the whole time so you can see it.</summary>
        public void ApplyShock(float seconds)
        {
            if (IsDead || Untouchable) return;
            Stun(seconds);
            stunTimer = Mathf.Max(stunTimer, seconds);
            shockTimer = Mathf.Max(shockTimer, seconds);
        }

        void UpdateShock(float dt)
        {
            if (shockTimer <= 0f) return;
            shockTimer -= dt;
            if (stunTimer <= 0f) { shockTimer = 0f; return; }
            if ((shockFxTimer -= dt) > 0f) return;
            shockFxTimer = UnityEngine.Random.Range(0.06f, 0.14f);
            float size = Mathf.Max(0.5f, centerHeight);
            var a = Center + UnityEngine.Random.insideUnitCircle * size;
            var b = Center + UnityEngine.Random.insideUnitCircle * size;
            ArenaFX.Zap(a, b, ShockBlue, 0.07f);
            ArenaFX.Burst(a, Color.white, 1, 2f, 0.06f, 0.15f, 0f);
            flash.Flash(ShockBlue, 0.06f);
        }

        /// <summary>Testing helper (F9): dies on the spot. Skips dodges and parries.</summary>
        public void ForceKill()
        {
            if (IsDead) return;
            Health = 0;
            OnDamaged();
            Die();
        }

        protected virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            flash.ClearHold();
            Sfx.Play("bug_death", 0.6f);
            ArenaFX.Burst(Center, bloodColor, 26, 8f, 0.16f, 0.7f, 2.5f);
            ArenaFX.Burst(Center, Color.white, 8, 5f, 0.1f, 0.3f, 1f);
            ArenaFX.Ring(Center, new Color(bloodColor.r, bloodColor.g, bloodColor.b, 0.8f), 0.3f, 2.5f, 0.25f);
            ArenaFX.Shake(0.15f);

            var director = ArenaDirector.Instance;
            if (director != null)
            {
                director.DropCoins(Center, UnityEngine.Random.Range(coinDrop.x, coinDrop.y + 1));
                if (UnityEngine.Random.value < heartDropChance) director.DropHeart(Center);
            }
            Died?.Invoke(this);
            PlayDeathClip();
            Destroy(gameObject);
        }

        /// <summary>Bugs drawn with their own death animation (the group's monster sprites) leave it playing where they fell.</summary>
        void PlayDeathClip()
        {
            if (anim == null || visual == null || !anim.Has("death")) return;
            var corpse = new GameObject(name + " (dead)");
            corpse.SetActive(false); // its animator wakes up with the clip already set
            corpse.transform.SetPositionAndRotation(visual.position, visual.rotation);
            corpse.transform.localScale = visual.lossyScale;
            var sr = corpse.AddComponent<SpriteRenderer>();
            sr.sortingOrder = body.sortingOrder;
            sr.color = body.color;
            var deathAnim = corpse.AddComponent<PixelAnimator>();
            deathAnim.target = sr;
            deathAnim.clips = Array.FindAll(anim.clips, c => c.name == "death");
            corpse.SetActive(true);
            deathAnim.Play("death");
            Destroy(corpse, anim.Duration("death") + 0.35f);
        }

        // ---------- helpers for subclasses ----------

        protected void FacePlayer()
        {
            if (player != null) facing = player.transform.position.x >= transform.position.x ? 1 : -1;
        }

        protected Vector2 ToPlayer => player != null ? player.Center - Center : Vector2.zero;

        protected bool PlayerAlive => player != null && !player.IsDead;

        /// <summary>Wind-up tell: "!" icon plus an orange pulse until the attack starts.</summary>
        protected void BeginTell(float seconds)
        {
            Sfx.Play("tell", 0.3f);
            ArenaFX.Exclaim(transform, exclaimHeight, seconds);
            flash.Hold(WindUpColor, 0.65f);
        }

        protected void EndTell() => flash.ClearHold();

        /// <summary>Hurts the player if they are inside the box. Returns the result (to react to parries).</summary>
        protected HitResult HitPlayerInBox(Vector2 center, Vector2 size, int amount, float knockback)
        {
            if (!PlayerAlive) return HitResult.Ignored;
            Scratch.Clear();
            var filter = new ContactFilter2D();
            filter.SetLayerMask(ArenaLayers.PlayerMask);
            filter.useTriggers = true;
            Physics2D.OverlapBox(center, size, 0f, filter, Scratch);
            if (Scratch.Count == 0) return HitResult.Ignored;
            var result = player.TakeHit(amount, Center, knockback);
            if (result == HitResult.Parried) Break(1.8f); // open to a riposte
            return result;
        }

        protected bool GroundAhead(float distance)
        {
            var origin = (Vector2)transform.position + new Vector2(facing * distance, 0.3f);
            return Physics2D.Raycast(origin, Vector2.down, 0.8f, ArenaLayers.GroundMask);
        }

        protected bool WallAhead(float distance)
        {
            return Physics2D.Raycast(Center, new Vector2(facing, 0f), distance, ArenaLayers.SolidMask);
        }
    }
}
