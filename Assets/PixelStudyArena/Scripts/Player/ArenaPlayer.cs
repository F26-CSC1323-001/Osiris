using System.Collections.Generic;
using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    public enum HitResult { Ignored, Damaged, Blocked, Parried }

    /// <summary>
    /// The knight in the arena, with Elden Ring-style combat in 2D:
    ///   Stamina  — swings, rolls, heavy stabs and blocked hits drain the green bar; it refills when you
    ///              stop. With none left you can't attack or roll.
    ///   Commitment — once a swing starts it finishes. You can chain the next hit, roll or use a skill
    ///              only after the blade has passed (inputs pressed earlier are remembered).
    ///   Roll     — invincible for the first part only; the end of the roll is vulnerable. Pressing roll
    ///              with no direction is a quick backstep. Attack at the end of a roll = rolling slash.
    ///   Light combo (3 slashes), heavy stab (hold attack; hyper armor while charging), jump attack.
    ///   Stance   — hits break enemy stance; a staggered (or parried) bug can be finished with a
    ///              critical: press attack next to it. Invincible while it plays.
    ///   Shield   — blocks cost stamina (guard break when it runs out). Raise it just before a hit to
    ///              parry. Attack right after blocking a hit = guard counter (big stance damage).
    ///   Crimson flask — R heals 40%; a few sips per floor; getting hit mid-drink wastes the sip.
    /// Health lives in GameSession so the HUD stays in sync.
    /// Mockup 2 switches most of this off (see Abilities): the knight walks, jumps once, swings the
    /// sword (M1: the 3-hit combo, the jump attack and criticals on staggered bugs) and has one skill,
    /// Flame Slash (Z).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class ArenaPlayer : MonoBehaviour
    {
        public static ArenaPlayer Instance { get; private set; }

        [Header("Parts")]
        public Transform visual;
        public SpriteRenderer body;
        public PixelAnimator anim;
        public SpriteFlash flash;

        [Header("Abilities (Mockup 2: walk, one jump and the sword)")]
        [Tooltip("Jump again in the air.")]
        public bool doubleJump;
        [Tooltip("Roll / backstep (Shift or L).")]
        public bool dodge;
        [Tooltip("Hold attack to charge a heavy stab. Off: every press is a slash right away.")]
        public bool heavyStab;
        [Tooltip("Shield: block, parry and guard counter (K or Right Click).")]
        public bool shield;
        [Tooltip("Crimson flask (R).")]
        public bool flask;
        [Tooltip("Swings cost stamina (the green bar). Off: swing as much as you like, and the bar is hidden.")]
        public bool useStamina;

        [Header("Run")]
        public float runSpeed = 8f;
        public float groundAccel = 80f;
        public float groundDecel = 110f;
        public float airAccel = 55f;

        [Header("Jump")]
        public float jumpHeight = 4.4f;
        public float doubleJumpHeight = 3.1f;
        public float gravityScale = 4.2f;
        public float fallMultiplier = 1.6f;
        public float lowJumpMultiplier = 2.4f;
        public float maxFallSpeed = 24f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.13f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float staminaRegen = 62f;
        [Tooltip("Pause before stamina starts refilling after you spend some.")]
        public float staminaRegenDelay = 0.45f;
        [Tooltip("Longer pause when you run it all the way down.")]
        public float exhaustedDelay = 1f;

        [Header("Roll / backstep")]
        public float dodgeDistance = 4.6f;
        public float dodgeDuration = 0.42f;
        [Tooltip("Seconds into the roll when you're invincible (start, end). The rest of the roll can be hit.")]
        public Vector2 rollInvuln = new Vector2(0.02f, 0.3f);
        public float rollStamina = 20f;
        public float backstepDistance = 2.3f;
        public float backstepDuration = 0.3f;
        public Vector2 backstepInvuln = new Vector2(0.02f, 0.18f);
        public float backstepStamina = 12f;

        [Header("Sword")]
        public int[] comboDamage = { 10, 12, 22 };
        public int airAttackDamage = 14;
        public int rollAttackDamage = 11;
        public float comboResetTime = 0.5f;
        [Range(0f, 1f)] public float critChance = 0.1f;
        [Tooltip("Smear trails for the 3 slashes: diagonal, rising, cleave.")]
        public Sprite[] smearSprites;

        [Header("Heavy stab (hold attack)")]
        [Tooltip("Hold the attack button this long to start charging a stab instead of slashing.")]
        public float heavyHoldTime = 0.2f;
        public float heavyFullCharge = 0.6f;
        public float heavyMaxHold = 1.6f;
        public int heavyDamage = 26;
        public float heavyStamina = 24f;
        [Tooltip("Extra damage when the stab hits a stunned bug (on top of the usual bonus for hitting stunned bugs).")]
        public float stunnedStabMultiplier = 2.2f;
        public Sprite thrustSprite;
        public Sprite starSprite;

        [Header("Critical hit (staggered / parried bugs)")]
        public int criticalDamage = 48;
        public float criticalRange = 2.3f;

        [Header("Shield")]
        public float parryWindow = 0.2f;
        [Range(0f, 1f)] public float blockDamageTaken = 0.1f;
        public float blockMoveSpeed = 2.2f;
        public float blockStaminaBase = 8f;
        public float blockStaminaPerDamage = 1.5f;
        public float guardCounterWindow = 0.7f;
        public int guardCounterDamage = 24;
        public float guardBreakTime = 0.9f;

        [Header("Crimson flask")]
        public int flaskCharges = 3;
        [Range(0f, 1f)] public float flaskHealPercent = 0.4f;
        public float flaskDrinkTime = 0.95f;
        [Tooltip("When the heal lands during the drink. Get hit before this and the sip is wasted.")]
        public float flaskHealAt = 0.55f;
        public Sprite flaskSprite;
        public Sprite flaskIcon;

        [Header("Hurt")]
        public float invulnerableTime = 0.8f;
        public float hurtTime = 0.38f;

        [Header("Grabs, webs and poison")]
        [Tooltip("Silk wrapped round the knight when the Silk Widow cocoons him.")]
        public Sprite cocoonSprite;
        [Range(0.1f, 1f)] public float webSlow = 0.42f;

        public int Facing { get; private set; } = 1;
        public bool IsDead => state == State.Dead;
        /// <summary>In the invincible part of a roll or backstep.</summary>
        public bool IsDodging => state == State.Dodge && Invulnerable;
        public bool IsGrounded => grounded;
        public Vector2 Center => (Vector2)transform.position + new Vector2(0f, 0.95f);
        /// <summary>Roughly where the blade is mid-swing (skills spawn effects here).</summary>
        public Vector2 SwordPoint => (Vector2)transform.position + new Vector2(1f * Facing, 1.05f);
        /// <summary>Damage multiplier from Sharper Blade upgrades.</summary>
        public float AttackMultiplier => 1f + (GameSession.Run?.attackBonusPercent ?? 0) / 100f;
        public Rigidbody2D Body => rb;
        public float MoveInput => moveInput;
        public float Stamina => stamina;
        public int FlasksLeft => flasksLeft;

        // How a running skill moves the knight. Reset every time a skill starts.
        [System.NonSerialized] public float skillMoveSpeed;      // > 0: steer left/right at this speed
        [System.NonSerialized] public bool skillHang = true;     // float in the air while the skill runs
        [System.NonSerialized] public bool skillOwnsVelocity;   // the skill sets the velocity itself

        public void SetFacing(int direction) => Facing = direction >= 0 ? 1 : -1;

        enum State { Normal, Attack, Dodge, Hurt, Block, Dead, Skill, Charge, Stab, Critical, Drink, Held }
        enum Swing { Combo, Air, Roll, Counter }
        enum Intent { None, Light, Heavy, Undecided }

        struct AttackDef
        {
            public string clip;
            public int activeFrom, activeTo;
            public Vector2 boxOffset, boxSize;
            public float lunge, knockback, hitStop, shake, slashAngle, slashSize;
            public bool heavy, thrust;
            // smear trail: which sprite, where its pivot sits (from the feet), how big, how fast it sweeps
            public int smear;
            public Vector2 smearOffset;
            public float smearScale, smearSpin, smearLife;
            public float poise;    // stance damage
            public int hyperFrom;  // from this frame on, hits don't interrupt the swing (-1: never)
            public float stamina;
        }

        // Three different slashes: a diagonal down-swing, a rising uppercut (reaches wasps), an overhead cleave.
        static readonly AttackDef[] Combo =
        {
            new AttackDef { clip = "slash1", activeFrom = 2, activeTo = 3, boxOffset = new Vector2(1.05f, 1.05f), boxSize = new Vector2(2.1f, 1.7f), lunge = 3.5f, knockback = 2.5f, hitStop = 0.05f, shake = 0.12f, slashAngle = -15f, slashSize = 2.3f,
                smear = 0, smearOffset = new Vector2(0.15f, 1.0f), smearScale = 1.2f, smearSpin = -140f, smearLife = 0.15f, poise = 10f, hyperFrom = -1, stamina = 13f },
            new AttackDef { clip = "slash2", activeFrom = 2, activeTo = 3, boxOffset = new Vector2(0.95f, 1.55f), boxSize = new Vector2(2.1f, 2.7f), lunge = 3f, knockback = 3f, hitStop = 0.055f, shake = 0.14f, slashAngle = 35f, slashSize = 2.4f,
                smear = 1, smearOffset = new Vector2(0.1f, 1.05f), smearScale = 1.2f, smearSpin = 150f, smearLife = 0.15f, poise = 12f, hyperFrom = -1, stamina = 15f },
            new AttackDef { clip = "slash3", activeFrom = 4, activeTo = 5, boxOffset = new Vector2(1.35f, 1f), boxSize = new Vector2(2.7f, 2.2f), lunge = 7f, knockback = 7f, hitStop = 0.1f, shake = 0.32f, slashAngle = 0f, slashSize = 3.1f, heavy = true,
                smear = 2, smearOffset = new Vector2(0.2f, 1.1f), smearScale = 1.1f, smearSpin = -160f, smearLife = 0.2f, poise = 20f, hyperFrom = 2, stamina = 20f },
        };

        // Jump attack: an overhead chop on the way down (heavy: big stance damage).
        static readonly AttackDef JumpAttack =
            new AttackDef { clip = "jumpAttack", activeFrom = 2, activeTo = 3, boxOffset = new Vector2(1.1f, 0.75f), boxSize = new Vector2(2.4f, 2.3f), lunge = 0f, knockback = 5f, hitStop = 0.08f, shake = 0.25f, slashAngle = -30f, slashSize = 2.6f, heavy = true,
                smear = 2, smearOffset = new Vector2(0.2f, 1.1f), smearScale = 1.0f, smearSpin = -170f, smearLife = 0.17f, poise = 24f, hyperFrom = -1, stamina = 14f };

        // Rolling attack: a quick thrust out of the end of a roll.
        static readonly AttackDef RollAttack =
            new AttackDef { clip = "rollSlash", activeFrom = 1, activeTo = 2, boxOffset = new Vector2(1.3f, 0.95f), boxSize = new Vector2(2.6f, 1.2f), lunge = 8f, knockback = 3f, hitStop = 0.05f, shake = 0.12f, slashAngle = 0f, slashSize = 2.2f, thrust = true,
                poise = 10f, hyperFrom = -1, stamina = 12f };

        // Guard counter: straight out of a blocked hit, a heavy cleave.
        static readonly AttackDef GuardCounter =
            new AttackDef { clip = "guardCounter", activeFrom = 2, activeTo = 3, boxOffset = new Vector2(1.35f, 1.1f), boxSize = new Vector2(2.8f, 2.4f), lunge = 8f, knockback = 9f, hitStop = 0.12f, shake = 0.4f, slashAngle = 0f, slashSize = 3.1f, heavy = true,
                smear = 2, smearOffset = new Vector2(0.2f, 1.1f), smearScale = 1.15f, smearSpin = -170f, smearLife = 0.2f, poise = 38f, hyperFrom = 0, stamina = 16f };

        static readonly Color ChargeColor = new Color(1f, 0.85f, 0.45f);
        static readonly Color StabColor = new Color(1f, 0.9f, 0.6f);
        static readonly Color CritColor = new Color(1f, 0.82f, 0.4f);

        static readonly Color DustColor = new Color(0.75f, 0.78f, 0.82f, 0.9f);
        static readonly Color HurtColor = new Color(1f, 0.25f, 0.25f);
        static readonly Color ParryColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color DodgeColor = new Color(0.45f, 0.85f, 1f, 0.55f);
        static readonly Color HealColor = new Color(1f, 0.38f, 0.32f);

        const float BufferTime = 0.35f;
        const float RollPivotHeight = 0.65f;

        Rigidbody2D rb;
        CapsuleCollider2D capsule;
        State state;
        Swing swing;
        bool grounded, wasGrounded, usedDoubleJump, airAttackUsed, airDashUsed, slashShown, dodgeAirborne, backstep;
        float coyoteTimer, jumpBufferTimer, invulnTimer, stateTimer, comboTimer, blockTimer, guardCounterTimer;
        float afterimageTimer, footstepTimer, landTimer, lungeTimer, lungeSpeed, dodgeSpeed, dodgeTime, lowestFallSpeed, ignoreTimer;
        int comboIndex, dodgeDir, swingDamageBase;
        float moveInput;
        float stamina, staminaDelay;
        float velScale = 1f;

        // input buffer: what was pressed and is still waiting for the knight to be free
        bool attackBuffered, attackReleased, dodgeBuffered, flaskBuffered;
        float attackAge, attackHoldTime, dodgeAge, flaskAge, skillAge;
        int skillBuffered = -1;

        // heavy stab
        bool chargeFull, chargeFxOn;
        float chargeTime, chargeFxTimer, stabTimer, stabCharge;

        // critical
        BugEnemy critTarget;
        float critTime, critStandX;
        bool critStabbed, critRipped;

        // flask
        int flasksLeft;
        float drinkTime;
        bool drinkHealed;
        SpriteRenderer flaskInHand;

        // held by a bug (web snare, pincers, cocoon)
        Object holder;
        Vector2 holdPoint;
        int escapeLeft;
        bool cocooned;
        float heldTime, escapeShake;
        SpriteRenderer cocoonWrap;

        // poison
        float poisonTimer, poisonTick;
        int poisonDamage;
        static readonly Color PoisonColor = new Color(0.55f, 1f, 0.35f);

        AttackDef currentAttack;
        Vector2 squash = Vector2.one;
        Vector3 spawnPoint;
        Collider2D ignoredPlatform;
        readonly HashSet<BugEnemy> hitThisSwing = new HashSet<BugEnemy>();
        readonly List<Collider2D> hits = new List<Collider2D>();
        ContactFilter2D enemyFilter, groundFilter, platformFilter;
        PlayerSkill[] skills = new PlayerSkill[0];
        PlayerSkill activeSkill;

        float DodgeMultiplier => 1f + (GameSession.Run?.dodgeBonusPercent ?? 0) / 100f;
        /// <summary>Run speed multiplier from Swift Boots upgrades.</summary>
        float SpeedMultiplier => 1f + (GameSession.Run?.speedBonusPercent ?? 0) / 100f;
        float JumpVelocity(float height) => Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * gravityScale * height);
        float DodgeDuration => backstep ? backstepDuration : dodgeDuration;
        bool CanDodge => stamina > 0.5f && (grounded || !airDashUsed);

        /// <summary>Velocity in the knight's own time (the body is sped up while a skill slows the world).</summary>
        Vector2 Vel
        {
            get => rb.linearVelocity / velScale;
            set => rb.linearVelocity = value * velScale;
        }

        /// <summary>Invincible right now: the early part of a roll, or a critical.</summary>
        bool Invulnerable
        {
            get
            {
                if (state == State.Critical) return true;
                if (state != State.Dodge) return false;
                if (dodgeAirborne) return true;
                var window = backstep ? backstepInvuln : rollInvuln;
                return dodgeTime >= window.x && dodgeTime <= window.y;
            }
        }

        /// <summary>Hits hurt but don't interrupt: charging/stabbing, the last slash once it's swinging, the guard counter.</summary>
        bool HyperArmor =>
            state == State.Charge || state == State.Stab ||
            (state == State.Attack && currentAttack.hyperFrom >= 0 && anim.Frame >= currentAttack.hyperFrom);

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            rb.gravityScale = gravityScale;
            enemyFilter = Filter(ArenaLayers.EnemyMask, true);
            groundFilter = Filter(ArenaLayers.GroundMask, false);
            platformFilter = Filter(ArenaLayers.PlatformMask, false);
            spawnPoint = transform.position;
            skills = GetComponents<PlayerSkill>();
            stamina = maxStamina;
            flasksLeft = flaskCharges;

            // the flask the knight drinks from (shown only while drinking)
            var flaskGo = new GameObject("FlaskInHand");
            flaskGo.transform.SetParent(visual, false);
            flaskGo.transform.localPosition = new Vector3(0.38f, 1.38f, 0f);
            flaskInHand = flaskGo.AddComponent<SpriteRenderer>();
            flaskInHand.sprite = flaskSprite;
            flaskInHand.sortingOrder = body.sortingOrder + 1;
            flaskInHand.enabled = false;

            // silk wrapped round the knight while cocooned
            var wrapGo = new GameObject("Cocoon");
            wrapGo.transform.SetParent(transform, false);
            wrapGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            cocoonWrap = wrapGo.AddComponent<SpriteRenderer>();
            cocoonWrap.sprite = cocoonSprite;
            cocoonWrap.sortingOrder = body.sortingOrder + 2;
            cocoonWrap.enabled = false;
        }

        static ContactFilter2D Filter(int mask, bool triggers)
        {
            var f = new ContactFilter2D();
            f.SetLayerMask(mask);
            f.useTriggers = triggers;
            return f;
        }

        void OnEnable() => GameSession.Changed += OnSessionChanged;

        void OnDisable() => GameSession.Changed -= OnSessionChanged;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            anim.Play("idle");
            BindSkillHud();
            var hud = Hud;
            if (hud != null)
            {
                if (hud.staminaBar != null) hud.staminaBar.gameObject.SetActive(useStamina);
                hud.SetStamina(1f, true);
            }
            RefreshFlaskHud();
        }

        static HUDController Hud => GameUIController.Instance != null ? GameUIController.Instance.hud : null;

        string boundUnlocks;

        /// <summary>Puts every skill on the HUD (locked ones dimmed). Re-runs when a skill unlocks.</summary>
        void BindSkillHud()
        {
            var hud = Hud;
            if (hud == null || hud.skillSlots == null) return;
            var unlocks = string.Concat(System.Array.ConvertAll(skills, s => s.Unlocked ? "1" : "0"));
            if (unlocks == boundUnlocks) return;
            boundUnlocks = unlocks;
            for (int i = 0; i < hud.skillSlots.Length; i++)
            {
                if (i < skills.Length) hud.SetSkillSlot(i, skills[i].icon, skills[i].KeyLabel, skills[i].displayName, !skills[i].Unlocked);
                else hud.SetSkillSlot(i, null, null, null);
            }
        }

        void RefreshFlaskHud()
        {
            var hud = Hud;
            if (hud != null) hud.SetFlask(flask ? flaskIcon : null, flasksLeft, "R"); // no icon: hidden
        }

        void OnSessionChanged()
        {
            var run = GameSession.Run;
            if (run != null && state == State.Dead && run.health > 0) Revive();
            BindSkillHud();
        }

        // =====================================================================
        // Update
        // =====================================================================

        void Update()
        {
            if (Time.timeScale < 0.01f) return; // a menu has paused the game

            SyncTimeScale();
            float dt = Time.deltaTime * velScale; // the knight's own time
            TickTimers(dt);
            CheckGround();
            ReadInput(dt);

            if (chargeFxOn && state != State.Charge)
            {
                chargeFxOn = false; // interrupted (hit, dodge...) or released into the stab
                flash.ClearHold();
            }

            switch (state)
            {
                case State.Normal: UpdateNormal(); break;
                case State.Attack: UpdateAttack(); break;
                case State.Charge: UpdateCharge(dt); break;
                case State.Stab: UpdateStab(dt); break;
                case State.Dodge: UpdateDodge(dt); break;
                case State.Block: UpdateBlock(dt); break;
                case State.Critical: UpdateCritical(dt); break;
                case State.Drink: UpdateDrink(dt); break;
                case State.Held: UpdateHeld(dt); break;
                case State.Skill:
                    if (activeSkill == null || activeSkill.Tick(dt)) EndSkill();
                    break;
                case State.Hurt:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) state = State.Normal;
                    break;
            }

            UpdatePoison(dt);
            UpdateStamina(dt);
            UpdateGravity();
            UpdateAnimation(dt);
            UpdateVisual(dt);
            UpdateHud();

            if (transform.position.y < -6f) FellOut();
        }

        /// <summary>
        /// While a skill slows the world (ArenaFX focus), the knight keeps full speed: his animation runs
        /// faster and his body moves faster to make up for the slower physics.
        /// </summary>
        void SyncTimeScale()
        {
            float k = ArenaFX.PlayerTimeScale;
            if (!Mathf.Approximately(k, velScale))
            {
                rb.linearVelocity *= k / velScale;
                velScale = k;
            }
            anim.speed = velScale;
        }

        void TickTimers(float dt)
        {
            coyoteTimer -= dt;
            jumpBufferTimer -= dt;
            invulnTimer -= dt;
            landTimer -= dt;
            lungeTimer -= dt;
            guardCounterTimer -= dt;
            if (state != State.Attack) comboTimer -= dt;
            if (ignoredPlatform != null && (ignoreTimer -= dt) <= 0f)
            {
                Physics2D.IgnoreCollision(capsule, ignoredPlatform, false);
                ignoredPlatform = null;
            }
        }

        void ReadInput(float dt)
        {
            if (state == State.Dead)
            {
                moveInput = 0f;
                attackBuffered = dodgeBuffered = flaskBuffered = false;
                skillBuffered = -1;
                return;
            }
            moveInput = ArenaInput.Move;
            if (ArenaInput.JumpDown) jumpBufferTimer = jumpBufferTime;

            // attack: a tap is a slash, holding it is a heavy stab; kept until the knight is free to act
            if (ArenaInput.AttackDown)
            {
                attackBuffered = true;
                attackReleased = false;
                attackAge = 0f;
                attackHoldTime = 0f;
            }
            else if (attackBuffered)
            {
                attackAge += dt;
                if (!attackReleased)
                {
                    if (ArenaInput.AttackHeld) attackHoldTime += dt;
                    else attackReleased = true;
                }
                if (attackReleased && attackAge > BufferTime) attackBuffered = false;
            }

            if (dodge && ArenaInput.DodgeDown) { dodgeBuffered = true; dodgeAge = 0f; }
            else if (dodgeBuffered && (dodgeAge += dt) > BufferTime) dodgeBuffered = false;

            if (flask && ArenaInput.FlaskDown) { flaskBuffered = true; flaskAge = 0f; }
            else if (flaskBuffered && (flaskAge += dt) > BufferTime) flaskBuffered = false;

            bool pressed = false;
            for (int i = 0; i < skills.Length; i++)
            {
                if (!skills[i].Pressed()) continue;
                skillBuffered = i;
                skillAge = 0f;
                pressed = true;
            }
            if (!pressed && skillBuffered >= 0 && (skillAge += dt) > BufferTime) skillBuffered = -1;
        }

        Intent AttackIntent()
        {
            if (!attackBuffered) return Intent.None;
            if (attackReleased || !heavyStab) return Intent.Light;
            if (attackHoldTime >= heavyHoldTime) return Intent.Heavy;
            return Intent.Undecided;
        }

        void ConsumeAttack() => attackBuffered = false;

        void CheckGround()
        {
            wasGrounded = grounded;
            var b = capsule.bounds;
            hits.Clear();
            Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y - 0.04f), new Vector2(b.size.x * 0.85f, 0.1f), 0f, groundFilter, hits);
            bool touching = false;
            foreach (var h in hits)
                if (h != ignoredPlatform) touching = true;

            float vy = Vel.y;
            grounded = touching && vy <= 0.1f;
            if (grounded)
            {
                coyoteTimer = coyoteTime;
                usedDoubleJump = false;
                airAttackUsed = false;
                airDashUsed = false;
                if (!wasGrounded) OnLand();
            }
            else
            {
                lowestFallSpeed = Mathf.Min(lowestFallSpeed, vy);
            }
        }

        void OnLand()
        {
            if (lowestFallSpeed < -11f)
            {
                Sfx.Play("land", 0.6f);
                squash = new Vector2(1.25f, 0.78f);
                landTimer = 0.1f;
                var feet = (Vector2)transform.position;
                ArenaFX.Spray(feet, Vector2.left + Vector2.up * 0.2f, DustColor, 4, 3.5f, 0.3f, 0.12f);
                ArenaFX.Spray(feet, Vector2.right + Vector2.up * 0.2f, DustColor, 4, 3.5f, 0.3f, 0.12f);
                if (lowestFallSpeed < -19f) ArenaFX.Shake(0.12f);
            }
            lowestFallSpeed = 0f;
        }

        // ---------- stamina ----------

        void UpdateStamina(float dt)
        {
            staminaDelay -= dt;
            bool busy = state == State.Attack || state == State.Charge || state == State.Stab || state == State.Dodge || state == State.Critical;
            if (staminaDelay > 0f || busy || stamina >= maxStamina) return;
            float rate = staminaRegen * (state == State.Block ? 0.35f : 1f);
            stamina = Mathf.Min(maxStamina, stamina + rate * dt);
        }

        /// <summary>Spends stamina if there's any left (an action is allowed as long as the bar isn't empty).</summary>
        bool SpendStamina(float cost)
        {
            if (!useStamina) return true;
            if (stamina <= 0.5f)
            {
                DenyStamina();
                return false;
            }
            Drain(cost);
            return true;
        }

        void Drain(float cost)
        {
            if (!useStamina) return;
            stamina =Mathf.Max(0f, stamina - cost);
            staminaDelay = stamina <= 0f ? exhaustedDelay : staminaRegenDelay;
        }

        void DenyStamina()
        {
            var hud = Hud;
            if (hud != null) hud.DenyStamina();
        }

        // =====================================================================
        // States
        // =====================================================================

        void UpdateNormal()
        {
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            if (TryStartBufferedSkill()) return;

            if (jumpBufferTimer > 0f)
            {
                if (ArenaInput.Down && grounded && TryDropThrough())
                {
                    jumpBufferTimer = 0f;
                }
                else if (coyoteTimer > 0f)
                {
                    Jump(jumpHeight, false);
                }
                else if (doubleJump && !usedDoubleJump)
                {
                    usedDoubleJump = true;
                    Jump(doubleJumpHeight, true);
                }
            }

            if (dodgeBuffered)
            {
                dodgeBuffered = false;
                if (TryDodge()) return;
            }
            if (flaskBuffered)
            {
                flaskBuffered = false;
                if (TryDrink()) return;
            }

            var intent = AttackIntent();
            if (intent != Intent.None)
            {
                if (TryCritical()) { ConsumeAttack(); return; }
                if (!grounded) { ConsumeAttack(); StartJumpAttack(); return; } // in the air it's always the jump attack
                if (guardCounterTimer > 0f) { ConsumeAttack(); StartSwing(Swing.Counter); return; }
                if (intent == Intent.Light) { ConsumeAttack(); StartComboAttack(); return; }
                if (intent == Intent.Heavy) { ConsumeAttack(); StartCharge(); return; }
                return; // still deciding between a tap and a hold
            }
            if (shield && ArenaInput.BlockHeld && grounded) StartBlock();
        }

        void Jump(float height, bool doubleJump)
        {
            Vel = new Vector2(Vel.x, JumpVelocity(height));
            Sfx.Play("jump", 0.45f, doubleJump ? 1.25f : 1f);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            grounded = false;
            squash = new Vector2(0.8f, 1.2f);
            var feet = (Vector2)transform.position;
            if (doubleJump)
            {
                ArenaFX.Ring(feet + Vector2.up * 0.1f, new Color(0.7f, 0.9f, 1f, 0.8f), 0.6f, 2.2f, 0.25f);
                ArenaFX.Burst(feet, DustColor, 6, 3f, 0.1f, 0.35f, 0f);
                anim.Play("jumpUp", true);
            }
            else
            {
                ArenaFX.Spray(feet, Vector2.down + Vector2.left * 0.6f, DustColor, 3, 3f, 0.3f, 0.1f);
                ArenaFX.Spray(feet, Vector2.down + Vector2.right * 0.6f, DustColor, 3, 3f, 0.3f, 0.1f);
            }
        }

        bool TryDropThrough()
        {
            var b = capsule.bounds;
            hits.Clear();
            Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y - 0.05f), new Vector2(b.size.x * 0.9f, 0.12f), 0f, platformFilter, hits);
            if (hits.Count == 0) return false;
            ignoredPlatform = hits[0];
            Physics2D.IgnoreCollision(capsule, ignoredPlatform, true);
            ignoreTimer = 0.35f;
            grounded = false;
            coyoteTimer = 0f;
            Vel = new Vector2(Vel.x, -4f);
            return true;
        }

        // ---------- sword ----------

        void StartComboAttack()
        {
            if (comboTimer <= 0f) comboIndex = 0;
            StartSwing(Swing.Combo);
        }

        void StartJumpAttack()
        {
            if (airAttackUsed) return;
            StartSwing(Swing.Air);
        }

        bool StartSwing(Swing kind)
        {
            AttackDef def;
            int damage;
            switch (kind)
            {
                case Swing.Air: def = JumpAttack; damage = airAttackDamage; break;
                case Swing.Roll: def = RollAttack; damage = rollAttackDamage; break;
                case Swing.Counter: def = GuardCounter; damage = guardCounterDamage; break;
                default: def = Combo[comboIndex]; damage = comboDamage[Mathf.Min(comboIndex, comboDamage.Length - 1)]; break;
            }
            if (!SpendStamina(def.stamina)) return false;

            swing = kind;
            currentAttack = def;
            swingDamageBase = damage;
            guardCounterTimer = 0f;
            if (kind == Swing.Air)
            {
                airAttackUsed = true;
                if (Vel.y < 0f) Vel = new Vector2(Vel.x, 1.5f); // a moment of hang so the chop connects
            }
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            state = State.Attack;
            slashShown = false;
            hitThisSwing.Clear();
            anim.Play(def.clip, true);
            // each slash of the combo whooshes a little differently; the cleave and the jump chop are heavier
            Sfx.Play("swing", 0.7f, kind == Swing.Combo ? (comboIndex == 1 ? 1.12f : comboIndex == 2 ? 0.82f : 1f) : 0.88f);
            lungeTimer = kind == Swing.Roll ? 0.14f : 0.12f;
            lungeSpeed = def.lunge;
            squash = new Vector2(0.92f, 1.07f); // gather for the swing
            if (kind == Swing.Counter)
            {
                flash.Flash(ParryColor, 0.15f);
                ArenaFX.Burst(Center + new Vector2(0.4f * Facing, 0f), ParryColor, 8, 4f, 0.08f, 0.25f, 0.5f);
            }
            return true;
        }

        void UpdateAttack()
        {
            var def = currentAttack;
            int f = anim.Frame;
            if (f >= def.activeFrom && f <= def.activeTo)
            {
                if (!slashShown)
                {
                    slashShown = true;
                    PlaySmear(def);
                    squash = new Vector2(1.12f, 0.92f); // the swing lands
                    if (def.heavy && grounded)
                    {
                        // a heavy blow hits the ground in front
                        var ground = (Vector2)transform.position + new Vector2(1.6f * Facing, 0.05f);
                        ArenaFX.Spray(ground, new Vector2(Facing, 1f), DustColor, 7, 5f, 0.5f, 0.13f);
                        ArenaFX.Spray(ground, new Vector2(-Facing, 0.8f), DustColor, 4, 3.5f, 0.5f, 0.11f);
                        ArenaFX.Ring(ground + Vector2.up * 0.1f, new Color(1f, 0.9f, 0.7f, 0.7f), 0.3f, 2.4f, 0.2f);
                    }
                }
                DoHitbox(def);
            }

            // Recovery: the blade has passed. Now (and only now) you can chain, roll or use a skill.
            bool recovery = f > def.activeTo || anim.IsDone;
            if (recovery)
            {
                if (dodgeBuffered && CanDodge)
                {
                    dodgeBuffered = false;
                    EndAttack();
                    TryDodge();
                    return;
                }
                if (TryStartBufferedSkill()) return; // StartSkill ends the swing if the skill goes off
                if (swing != Swing.Air)
                {
                    var intent = AttackIntent();
                    if (intent != Intent.None && FindCriticalTarget() != null)
                    {
                        ConsumeAttack();
                        EndAttack();
                        TryCritical();
                        return;
                    }
                    if (intent == Intent.Light && stamina > 0.5f)
                    {
                        ConsumeAttack();
                        EndAttack();
                        StartComboAttack();
                        return;
                    }
                    if (intent == Intent.Heavy && stamina > 0.5f)
                    {
                        ConsumeAttack();
                        EndAttack();
                        StartCharge();
                        return;
                    }
                }
            }
            if (anim.IsDone || (swing == Swing.Air && grounded && recovery)) EndAttack();
        }

        void PlaySmear(AttackDef def)
        {
            if (def.thrust)
            {
                if (thrustSprite != null)
                {
                    var from = (Vector2)transform.position + new Vector2(0.45f * Facing, 0.98f);
                    ArenaFX.PlaySprite(thrustSprite, from, Color.white, 0.14f, true, new Vector3(0.4f, 0.7f, 1f), new Vector3(1f, 0.5f, 1f), 0f, 0f, Facing < 0, 27);
                }
                return;
            }
            var sprite = smearSprites != null && def.smear < smearSprites.Length ? smearSprites[def.smear] : null;
            if (sprite == null)
            {
                var pos = (Vector2)transform.position + new Vector2(def.boxOffset.x * 0.9f * Facing, def.boxOffset.y);
                ArenaFX.Slash(pos, Facing, def.slashSize, def.slashAngle, def.heavy ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(0.85f, 0.95f, 1f, 0.9f));
                return;
            }
            var pivot = (Vector2)transform.position + new Vector2(def.smearOffset.x * Facing, def.smearOffset.y);
            var start = new Vector3(def.smearScale * 0.94f, def.smearScale * 0.94f, 1f);
            var end = new Vector3(def.smearScale * 1.08f, def.smearScale * 1.05f, 1f);
            ArenaFX.PlaySprite(sprite, pivot, Color.white, def.smearLife, false, start, end, 0f, def.smearSpin * Facing, Facing < 0, 27);
            ArenaFX.PlaySprite(sprite, pivot, def.heavy ? new Color(1f, 0.7f, 0.35f, 0.45f) : new Color(0.45f, 0.75f, 1f, 0.4f),
                def.smearLife * 1.3f, true, start * 1.04f, end * 1.1f, 0f, def.smearSpin * Facing, Facing < 0, 26);
        }

        void EndAttack()
        {
            if (state != State.Attack) return;
            state = State.Normal;
            switch (swing)
            {
                case Swing.Combo: comboIndex = (comboIndex + 1) % Combo.Length; comboTimer = comboResetTime; break;
                case Swing.Roll: comboIndex = 1; comboTimer = comboResetTime; break;   // flows into the second slash
                case Swing.Counter: comboIndex = 0; comboTimer = 0f; break;
            }
        }

        void DoHitbox(AttackDef def)
        {
            var center = (Vector2)transform.position + new Vector2(def.boxOffset.x * Facing, def.boxOffset.y);
            hits.Clear();
            Physics2D.OverlapBox(center, def.boxSize, 0f, enemyFilter, hits);
            bool landed = false;
            foreach (var c in hits)
            {
                var bug = c.GetComponentInParent<BugEnemy>();
                if (bug == null || bug.IsDead || !hitThisSwing.Add(bug)) continue;
                bool crit = Random.value < critChance;
                float dmg = swingDamageBase * AttackMultiplier * Random.Range(0.9f, 1.1f) * (crit ? 1.75f : 1f);
                bug.TakeHit(Mathf.Max(1, Mathf.RoundToInt(dmg)), transform.position, def.knockback, def.heavy, crit, def.poise);
                // spark where the blade connects
                var contact = Vector2.Lerp(bug.Center, center, 0.35f);
                ArenaFX.Ring(contact, new Color(1f, 1f, 1f, 0.85f), 0.2f, def.heavy ? 1.8f : 1.2f, 0.12f);
                ArenaFX.Burst(contact, new Color(0.9f, 0.97f, 1f), def.heavy ? 8 : 5, 6f, 0.08f, 0.25f, 0.5f);
                landed = true;
            }
            if (landed)
            {
                ArenaFX.HitStop(def.hitStop);
                ArenaFX.Shake(def.shake);
            }
        }

        // ---------- heavy stab (hold attack) ----------

        Vector2 Grip => (Vector2)transform.position + new Vector2(0.35f * Facing, 0.95f);

        void StartCharge()
        {
            if (stamina <= 0.5f) { DenyStamina(); return; }
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            state = State.Charge;
            chargeTime = 0f;
            chargeFull = false;
            chargeFxOn = true;
            chargeFxTimer = 0f;
            anim.Play("heavyCharge", true);
            flash.Hold(ChargeColor, 0.22f, 20f);
            squash = new Vector2(1.08f, 0.93f);
        }

        void UpdateCharge(float dt)
        {
            chargeTime += dt;
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            if (dodgeBuffered && CanDodge) { dodgeBuffered = false; TryDodge(); return; }

            // light gathers at the blade
            chargeFxTimer -= dt;
            if (chargeFxTimer <= 0f)
            {
                chargeFxTimer = chargeFull ? 0.03f : 0.05f;
                var grip = Grip;
                var from = grip + Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.4f);
                ArenaFX.Spray(from, grip - from, chargeFull ? Color.white : ChargeColor, 1, 5f, 0.05f, 0.07f);
            }
            if (!chargeFull && chargeTime >= heavyFullCharge)
            {
                chargeFull = true;
                flash.Flash(Color.white, 0.12f);
                flash.Hold(ChargeColor, 0.4f, 34f);
                ArenaFX.Ring(Grip, new Color(1f, 0.9f, 0.55f, 0.9f), 0.2f, 1.8f, 0.2f);
                ArenaFX.Burst(Grip, ChargeColor, 8, 4f, 0.08f, 0.3f, 0.5f);
            }

            if (!ArenaInput.AttackHeld || chargeTime >= heavyMaxHold) StartStab();
        }

        void StartStab()
        {
            stabCharge = Mathf.Clamp01(chargeTime / heavyFullCharge);
            chargeFxOn = false;
            flash.ClearHold();
            Drain(heavyStamina);
            state = State.Stab;
            stabTimer = 0f;
            hitThisSwing.Clear();
            comboIndex = 0;
            anim.Play("heavyStab", true);
            lungeSpeed = 12f + 5f * stabCharge;
            lungeTimer = 0.14f;
            squash = new Vector2(1.25f, 0.84f);
            if (thrustSprite != null)
            {
                var from = (Vector2)transform.position + new Vector2(0.45f * Facing, 0.98f);
                ArenaFX.PlaySprite(thrustSprite, from, Color.white, 0.18f, true, new Vector3(0.45f, 0.9f, 1f), new Vector3(1.25f + 0.3f * stabCharge, 0.6f, 1f), 0f, 0f, Facing < 0, 27);
            }
            ArenaFX.Spray(transform.position, new Vector2(-Facing, 0.5f), DustColor, 6, 4f, 0.4f, 0.12f);
        }

        void UpdateStab(float dt)
        {
            stabTimer += dt;
            if (lungeTimer > 0f && (afterimageTimer -= dt) <= 0f)
            {
                afterimageTimer = 0.03f;
                ArenaFX.Afterimage(body, new Color(1f, 0.85f, 0.5f, 0.45f), 0.18f);
            }
            if (stabTimer <= 0.17f) DoStabHitbox();
            if (stabTimer > 0.26f)
            {
                if (dodgeBuffered && CanDodge) { dodgeBuffered = false; TryDodge(); return; }
                if (TryStartBufferedSkill()) return;
            }
            if (stabTimer >= 0.5f) state = State.Normal;
        }

        void DoStabHitbox()
        {
            var center = (Vector2)transform.position + new Vector2(1.6f * Facing, 1f);
            hits.Clear();
            Physics2D.OverlapBox(center, new Vector2(3.2f, 1.1f), 0f, enemyFilter, hits);
            bool landed = false, pierced = false;
            foreach (var c in hits)
            {
                var bug = c.GetComponentInParent<BugEnemy>();
                if (bug == null || bug.IsDead || !hitThisSwing.Add(bug)) continue;
                bool stunned = bug.IsStunned;
                float dmg = heavyDamage * AttackMultiplier * (1f + 0.5f * stabCharge) * Random.Range(0.95f, 1.05f) * (stunned ? stunnedStabMultiplier : 1f);
                var point = bug.Center;
                bug.TakeHit(Mathf.Max(1, Mathf.RoundToInt(dmg)), transform.position, stunned ? 12f : 8f, true, stunned || stabCharge >= 1f, 26f + 22f * stabCharge);
                landed = true;
                if (stunned)
                {
                    // stab into a stunned bug: critical
                    pierced = true;
                    ArenaFX.DamageNumber(point + Vector2.up * 1.1f, "STAB!", StabColor, 1.2f);
                    if (starSprite != null)
                        ArenaFX.PlaySprite(starSprite, point, Color.white, 0.22f, true, Vector3.one * 0.6f, Vector3.one * 2.4f, 0f, 90f);
                    ArenaFX.Ring(point, new Color(1f, 0.85f, 0.55f, 0.95f), 0.3f, 3.2f, 0.25f);
                    ArenaFX.Spray(point, new Vector2(Facing, 0.15f), bug.bloodColor, 16, 10f, 0.25f, 0.13f);
                    ArenaFX.Burst(point, Color.white, 10, 7f, 0.08f, 0.3f, 0.5f);
                    flash.Flash(StabColor, 0.15f);
                }
                else
                {
                    ArenaFX.Ring(point, new Color(1f, 1f, 1f, 0.8f), 0.2f, 1.6f, 0.12f);
                    ArenaFX.Spray(point, new Vector2(Facing, 0.1f), StabColor, 6, 7f, 0.3f, 0.08f);
                }
            }
            if (landed)
            {
                ArenaFX.HitStop(pierced ? 0.17f : 0.07f);
                ArenaFX.Shake(pierced ? 0.6f : 0.3f);
            }
        }

        // ---------- critical hit ----------

        /// <summary>The nearest staggered or parried bug within reach (null if none).</summary>
        BugEnemy FindCriticalTarget()
        {
            if (!grounded) return null;
            BugEnemy best = null;
            float bestDist = float.MaxValue;
            hits.Clear();
            Physics2D.OverlapCircle(Center, criticalRange + 1.5f, enemyFilter, hits);
            foreach (var c in hits)
            {
                var bug = c.GetComponentInParent<BugEnemy>();
                if (bug == null || !bug.CanCritical) continue;
                var d = (Vector2)c.bounds.ClosestPoint(Center) - Center;
                if (Mathf.Abs(d.y) > 1.6f || Mathf.Abs(d.x) > criticalRange) continue;
                float dist = d.sqrMagnitude;
                if (dist < bestDist) { bestDist = dist; best = bug; }
            }
            return best;
        }

        bool TryCritical()
        {
            var target = FindCriticalTarget();
            if (target == null) return false;
            StartCritical(target);
            return true;
        }

        void StartCritical(BugEnemy target)
        {
            state = State.Critical;
            critTarget = target;
            critTime = 0f;
            critStabbed = critRipped = false;
            Facing = target.Center.x >= transform.position.x ? 1 : -1;
            // step in to just in front of the bug
            var col = target.GetComponent<Collider2D>();
            float halfWidth = col != null ? col.bounds.extents.x : 0.5f;
            critStandX = target.Center.x - Facing * (halfWidth + 0.75f);
            target.HoldForCritical(1.1f);
            anim.Play("critWind", true);
            squash = new Vector2(0.9f, 1.1f);
            flash.Flash(CritColor, 0.15f);
            CameraFollow2D.Zoom(0.88f, 0.75f);
            ArenaFX.Spray(transform.position, new Vector2(-Facing, 0.4f), DustColor, 6, 4f, 0.4f, 0.12f);
        }

        void UpdateCritical(float dt)
        {
            critTime += dt;
            if (critTime < 0.12f && (afterimageTimer -= dt) <= 0f)
            {
                afterimageTimer = 0.025f;
                ArenaFX.Afterimage(body, new Color(1f, 0.85f, 0.5f, 0.45f), 0.2f);
            }
            if (!critStabbed && critTime >= 0.11f) CriticalStab();
            if (!critRipped && critTime >= 0.46f) CriticalRip();
            if (critTime >= 0.8f)
            {
                state = State.Normal;
                critTarget = null;
                comboIndex = 0;
            }
        }

        void CriticalStab()
        {
            critStabbed = true;
            anim.Play("critStab", true);
            squash = new Vector2(1.28f, 0.82f);
            if (critTarget == null || critTarget.IsDead) return;
            var point = critTarget.Center;
            float dmg = criticalDamage * AttackMultiplier * Random.Range(0.95f, 1.05f);
            critTarget.TakeHit(Mathf.Max(1, Mathf.RoundToInt(dmg)), transform.position, 0f, true, true, 0f);
            if (critTarget != null && !critTarget.IsDead) critTarget.HoldForCritical(0.9f);

            ArenaFX.DamageNumber(point + Vector2.up * 1.3f, "CRITICAL", CritColor, 0.9f);
            if (starSprite != null)
                ArenaFX.PlaySprite(starSprite, point, Color.white, 0.28f, true, Vector3.one * 0.8f, Vector3.one * 3.2f, 0f, 120f);
            if (thrustSprite != null)
                ArenaFX.PlaySprite(thrustSprite, (Vector2)transform.position + new Vector2(0.45f * Facing, 0.98f), Color.white, 0.2f, true,
                    new Vector3(0.6f, 1f, 1f), new Vector3(1.4f, 0.6f, 1f), 0f, 0f, Facing < 0, 27);
            ArenaFX.Ring(point, new Color(1f, 0.85f, 0.55f, 0.95f), 0.4f, 4f, 0.3f);
            ArenaFX.Spray(point + new Vector2(0.3f * Facing, 0f), new Vector2(Facing, 0.25f), critTarget.bloodColor, 24, 11f, 0.3f, 0.15f);
            ArenaFX.Burst(point, Color.white, 12, 7f, 0.09f, 0.3f, 0.5f);
            flash.Flash(Color.white, 0.18f);
            ImpactFrame.Play(point, ImpactFrame.Style.Steel);
            ArenaFX.HitStop(0.2f);
            ArenaFX.Shake(0.65f);
            CameraFollow2D.Zoom(0.8f, 0.4f);
        }

        void CriticalRip()
        {
            critRipped = true;
            anim.Play("critRip", true);
            squash = new Vector2(0.88f, 1.14f);
            var sprite = smearSprites != null && smearSprites.Length > 1 ? smearSprites[1] : null;
            var pivot = (Vector2)transform.position + new Vector2(0.1f * Facing, 1.05f);
            if (sprite != null)
            {
                ArenaFX.PlaySprite(sprite, pivot, Color.white, 0.16f, false, Vector3.one * 1.15f, Vector3.one * 1.3f, 0f, 150f * Facing, Facing < 0, 27);
                ArenaFX.PlaySprite(sprite, pivot, new Color(1f, 0.6f, 0.4f, 0.45f), 0.2f, true, Vector3.one * 1.2f, Vector3.one * 1.4f, 0f, 150f * Facing, Facing < 0, 26);
            }
            if (critTarget != null && !critTarget.IsDead)
            {
                ArenaFX.Spray(critTarget.Center, new Vector2(Facing * 0.4f, 1f), critTarget.bloodColor, 14, 9f, 0.35f, 0.13f);
                var targetBody = critTarget.GetComponent<Rigidbody2D>();
                if (targetBody != null && targetBody.bodyType == RigidbodyType2D.Dynamic)
                    targetBody.linearVelocity = new Vector2(Facing * 7f, 5f); // thrown off the blade
            }
            ArenaFX.Shake(0.3f);
        }

        // ---------- roll / backstep ----------

        bool TryDodge()
        {
            if (!grounded && airDashUsed) return false;
            bool back = grounded && Mathf.Abs(moveInput) < 0.1f;
            if (!SpendStamina(back ? backstepStamina : rollStamina)) return false;

            if (state == State.Skill) CancelSkill();
            state = State.Dodge;
            backstep = back;
            dodgeTime = 0f;
            dodgeAirborne = !grounded;
            if (dodgeAirborne) airDashUsed = true;
            if (back)
            {
                dodgeDir = -Facing; // hop back, still facing the enemy
                dodgeSpeed = backstepDistance / (backstepDuration * 0.8875f);
                anim.Play("backstep", true);
                squash = new Vector2(1.1f, 0.9f);
            }
            else
            {
                dodgeDir = Mathf.Abs(moveInput) > 0.1f ? (moveInput > 0f ? 1 : -1) : Facing;
                Facing = dodgeDir;
                dodgeSpeed = dodgeDistance * DodgeMultiplier / (dodgeDuration * 0.8875f);
                anim.Play("roll", true);
            }
            afterimageTimer = 0f;
            ArenaFX.Spray(transform.position, new Vector2(-dodgeDir, 0.4f), DustColor, 6, 4f, 0.35f, 0.12f);
            return true;
        }

        /// <summary>Full speed for most of the roll, slowing into the recovery at the end.</summary>
        float DodgeSpeedNow()
        {
            float t = dodgeTime / DodgeDuration;
            return dodgeSpeed * (t < 0.7f ? 1f : Mathf.Lerp(1f, 0.25f, (t - 0.7f) / 0.3f));
        }

        void UpdateDodge(float dt)
        {
            dodgeTime += dt;
            // afterimages only while invincible, so you can see the window
            if (Invulnerable && (afterimageTimer -= dt) <= 0f)
            {
                afterimageTimer = 0.035f;
                ArenaFX.Afterimage(body, DodgeColor);
            }

            float dur = DodgeDuration;
            if (dodgeTime >= dur * 0.62f)
            {
                if (!backstep && grounded && AttackIntent() != Intent.None)
                {
                    ConsumeAttack();
                    EndDodge(false);
                    if (!TryCritical()) StartSwing(Swing.Roll);
                    return;
                }
                if (TryStartBufferedSkill()) return;
            }
            if (dodgeTime >= dur) EndDodge(true);
        }

        void EndDodge(bool landed)
        {
            state = State.Normal;
            Vel = new Vector2(dodgeDir * (backstep ? 1f : runSpeed * 0.6f), Vel.y);
            if (landed && grounded)
            {
                // come out of the roll onto your feet
                squash = new Vector2(1.15f, 0.86f);
                ArenaFX.Spray(transform.position, new Vector2(dodgeDir, 0.5f), DustColor, 4, 3f, 0.4f, 0.1f);
            }
        }

        // ---------- shield ----------

        void StartBlock()
        {
            state = State.Block;
            blockTimer = 0f;
            anim.Play("defend", true);
        }

        void UpdateBlock(float dt)
        {
            blockTimer += dt;
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            if (!ArenaInput.BlockHeld || !grounded)
            {
                state = State.Normal;
                return;
            }
            if (dodgeBuffered) { dodgeBuffered = false; if (TryDodge()) return; }
            var intent = AttackIntent();
            if (intent != Intent.None)
            {
                if (TryCritical()) { ConsumeAttack(); return; }
                if (guardCounterTimer > 0f) { ConsumeAttack(); StartSwing(Swing.Counter); return; }
                if (intent == Intent.Light) { ConsumeAttack(); StartComboAttack(); return; }
                if (intent == Intent.Heavy) { ConsumeAttack(); StartCharge(); return; }
            }
            if (TryStartBufferedSkill()) return;
            if (anim.Current == "defend" && anim.IsDone) anim.Play("protect");
        }

        // ---------- flask ----------

        bool TryDrink()
        {
            if (!grounded) return false;
            if (flasksLeft <= 0)
            {
                var hud = Hud;
                if (hud != null) hud.DenyFlask();
                ArenaFX.DamageNumber(Center + Vector2.up * 1.3f, "NO FLASKS", new Color(0.75f, 0.6f, 0.6f), 0.6f);
                return false;
            }
            flasksLeft--;
            state = State.Drink;
            drinkTime = 0f;
            drinkHealed = false;
            anim.Play("drink", true);
            squash = new Vector2(0.94f, 1.06f);
            var h = Hud;
            if (h != null) h.FlaskUsed();
            RefreshFlaskHud();
            return true;
        }

        void UpdateDrink(float dt)
        {
            drinkTime += dt;
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;

            // raise the flask, tip it back, lower it
            float raise = Mathf.SmoothStep(0f, 1f, drinkTime / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (drinkTime - flaskDrinkTime + 0.18f) / 0.18f));
            float tilt = Mathf.SmoothStep(0f, 1f, (drinkTime - 0.15f) / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, (drinkTime - flaskHealAt - 0.1f) / 0.2f));
            flaskInHand.enabled = flaskSprite != null;
            flaskInHand.transform.localPosition = new Vector3(Mathf.Lerp(0.55f, 0.3f, raise), Mathf.Lerp(0.95f, 1.5f, raise), 0f);
            flaskInHand.transform.localRotation = Quaternion.Euler(0f, 0f, 115f * tilt);
            if (Random.value < 0.25f)
                ArenaFX.Burst(flaskInHand.transform.position, HealColor, 1, 1.2f, 0.06f, 0.3f, 1.5f);

            if (!drinkHealed && drinkTime >= flaskHealAt) FlaskHeal();
            if (drinkTime >= flaskDrinkTime) EndDrink();
        }

        void FlaskHeal()
        {
            drinkHealed = true;
            var run = GameSession.Run;
            int amount = Mathf.CeilToInt((run != null ? run.maxHealth : 100) * flaskHealPercent);
            GameSession.Heal(amount);
            ArenaFX.DamageNumber(Center + Vector2.up * 0.9f, "+" + amount, new Color(0.55f, 1f, 0.55f), 1.1f);
            ArenaFX.Ring(Center, new Color(1f, 0.45f, 0.4f, 0.85f), 0.4f, 3f, 0.35f);
            ArenaFX.Glow(Center, new Color(1f, 0.35f, 0.3f, 0.55f), 3.5f, 0.45f);
            for (int i = 0; i < 3; i++)
                ArenaFX.Burst(Center + Random.insideUnitCircle * 0.5f, i == 0 ? new Color(1f, 0.85f, 0.45f) : HealColor, 6, 2.5f, 0.09f, 0.7f, 3f);
            flash.Flash(HealColor, 0.3f);
            squash = new Vector2(1.08f, 0.94f);
        }

        void EndDrink()
        {
            flaskInHand.enabled = false;
            if (state == State.Drink) state = State.Normal;
        }

        // ---------- skills ----------

        bool TryStartBufferedSkill()
        {
            if (skillBuffered < 0) return false;
            int index = skillBuffered;
            skillBuffered = -1;
            return StartSkill(index);
        }

        static void DenySkill(int index)
        {
            var hud = Hud;
            if (hud != null) hud.DenySkill(index);
        }

        /// <summary>Uses skill slot 0..6 (also handy for on-screen buttons or tests).</summary>
        public bool UseSkill(int index)
        {
            if (index < 0 || index >= skills.Length) return false;
            if (state == State.Dead || state == State.Hurt || state == State.Skill || state == State.Critical) return false;
            if (state == State.Attack) EndAttack();
            if (state == State.Drink) EndDrink();
            return StartSkill(index);
        }

        bool StartSkill(int index)
        {
            var skill = skills[index];
            if (!skill.Unlocked)
            {
                DenySkill(index);
                ArenaFX.DamageNumber(Center + Vector2.up * 1.3f, "LOCKED", new Color(0.65f, 0.6f, 0.75f), 0.6f);
                return false;
            }
            if (!skill.Ready)
            {
                DenySkill(index);
                return false;
            }
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            if (state == State.Attack) EndAttack();
            skillMoveSpeed = 0f;
            skillHang = true;
            skillOwnsVelocity = false;
            if (!skill.TryActivate()) return false;
            state = State.Skill;
            activeSkill = skill;
            lungeTimer = 0f;
            return true;
        }

        void EndSkill()
        {
            activeSkill = null;
            if (state == State.Skill) state = State.Normal;
        }

        void CancelSkill()
        {
            if (activeSkill != null) activeSkill.Cancel();
            activeSkill = null;
        }

        /// <summary>Moves the knight's feet straight to a point and stops him (Thousand Cuts blinks).</summary>
        public void Teleport(Vector2 feet)
        {
            transform.position = feet;
            rb.position = feet;
            rb.linearVelocity = Vector2.zero;
            lungeTimer = 0f;
        }

        /// <summary>Pushes the knight forward for a moment (used by attacks and skills).</summary>
        public void Lunge(float speed, float seconds)
        {
            lungeSpeed = speed;
            lungeTimer = seconds;
        }

        void UpdateHud()
        {
            var hud = Hud;
            if (hud == null) return;
            int slots = hud.skillSlots != null ? hud.skillSlots.Length : 0;
            for (int i = 0; i < skills.Length && i < slots; i++)
                hud.SetSkillCooldown(i, skills[i].Cooldown01, skills[i].CooldownLeft);
            if (useStamina) hud.SetStamina(stamina / Mathf.Max(1f, maxStamina));
        }

        // =====================================================================
        // Grabs (web snare, pincers, cocoon) and poison
        // =====================================================================

        /// <summary>
        /// A bug grabs the knight (web snare, scorpion pincers, a stag beetle's mandibles, the cocoon).
        /// Fails while rolling (i-frames), during a critical, just after being hit, or (if blockable) when
        /// the shield faces it. escapePresses: roll presses to break free (cocoon: any button); 0 = can't.
        /// </summary>
        public bool TryGrab(Object by, Vector2 from, int escapePresses, bool blockable = false, bool cocoon = false, bool evenRightAfterAHit = false)
        {
            if (state == State.Dead || state == State.Held || Invulnerable || invulnTimer > 0f && !evenRightAfterAHit) return false;
            if (state == State.Skill && activeSkill != null && activeSkill.Invulnerable) return false;
            int fromDir = from.x >= transform.position.x ? 1 : -1;
            if (blockable && state == State.Block && fromDir == Facing)
            {
                var shieldPos = Center + new Vector2(0.45f * Facing, 0f);
                ArenaFX.Spray(shieldPos, new Vector2(Facing, 0.5f), ParryColor, 8, 6f, 0.5f, 0.08f);
                Drain(blockStaminaBase);
                return false;
            }
            if (state == State.Skill) CancelSkill();
            if (state == State.Drink) EndDrink();
            if (state == State.Attack) EndAttack();
            state = State.Held;
            holder = by;
            holdPoint = transform.position;
            escapeLeft = escapePresses;
            cocooned = cocoon;
            heldTime = 0f;
            escapeShake = 0f;
            attackBuffered = dodgeBuffered = flaskBuffered = false;
            skillBuffered = -1;
            anim.Play("hurt", true);
            flash.Flash(Color.white, 0.12f);
            if (cocoon)
            {
                cocoonWrap.enabled = cocoonSprite != null;
                ArenaFX.Burst(Center, new Color(0.95f, 0.95f, 1f), 18, 5f, 0.1f, 0.4f, 0.5f);
                ArenaFX.DamageNumber(Center + Vector2.up * 1.4f, "MASH TO BREAK FREE", new Color(0.95f, 0.95f, 1f), 0.6f);
            }
            else if (escapePresses > 0)
                ArenaFX.DamageNumber(Center + Vector2.up * 1.4f, "ROLL TO BREAK FREE", new Color(1f, 0.9f, 0.6f), 0.55f);
            return true;
        }

        public bool IsHeldBy(Object by) => state == State.Held && holder == by;

        public bool IsHeld => state == State.Held;

        /// <summary>The grabbing bug moves the knight's feet here each frame.</summary>
        public void HoldAt(Vector2 feet) => holdPoint = feet;

        /// <summary>Lets go. velocity throws the knight.</summary>
        public void Release(Vector2 velocity)
        {
            if (state != State.Held) return;
            EndHold();
            state = State.Normal;
            Vel = velocity;
        }

        void EndHold()
        {
            holder = null;
            cocooned = false;
            cocoonWrap.enabled = false;
            visual.localPosition = Vector3.zero;
        }

        void UpdateHeld(float dt)
        {
            heldTime += dt;
            // the bug that grabbed us is gone: drop
            if (holder == null || (holder is Behaviour b && !b.isActiveAndEnabled) || (holder is BugEnemy bug && bug.IsDead))
            {
                EndHold();
                state = State.Normal;
                return;
            }
            bool press = cocooned || !dodge ? Input.anyKeyDown : ArenaInput.DodgeDown; // cocoon (or no roll): mash anything; grabs: roll
            if (escapeLeft > 0 && press)
            {
                escapeLeft--;
                escapeShake = 0.12f;
                squash = new Vector2(1.15f, 0.88f);
                ArenaFX.Burst(Center, cocooned ? new Color(0.95f, 0.95f, 1f) : new Color(1f, 0.9f, 0.6f), 5, 4f, 0.08f, 0.25f, 0.5f);
                if (escapeLeft <= 0) BreakFree();
                return;
            }
            // wriggle while mashing
            escapeShake -= dt;
            visual.localPosition = escapeShake > 0f ? new Vector3(Random.Range(-0.06f, 0.06f), 0f, 0f) : Vector3.zero;
        }

        void BreakFree()
        {
            bool wasCocoon = cocooned;
            int away = holder is Component c && c != null ? (c.transform.position.x > transform.position.x ? -1 : 1) : -Facing;
            EndHold();
            state = State.Normal;
            invulnTimer = 0.45f;
            Vel = new Vector2(away * 7f, 7f);
            flash.Flash(Color.white, 0.15f);
            ArenaFX.Burst(Center, wasCocoon ? new Color(0.95f, 0.95f, 1f) : new Color(1f, 0.9f, 0.6f), 22, 7f, 0.11f, 0.45f, 0.5f);
            ArenaFX.Ring(Center, new Color(1f, 1f, 1f, 0.8f), 0.3f, 2.4f, 0.2f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.3f, "BROKE FREE", new Color(1f, 0.95f, 0.75f), 0.75f);
            ArenaFX.Shake(0.2f);
        }

        /// <summary>Poison: a few damage every half second for a while (scorpion stings, widow venom).</summary>
        public void ApplyPoison(int damagePerTick, float seconds)
        {
            if (state == State.Dead) return;
            poisonDamage = Mathf.Max(poisonDamage, damagePerTick);
            if (poisonTimer <= 0f) poisonTick = 0.5f;
            poisonTimer = Mathf.Max(poisonTimer, seconds);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.2f, "POISONED", PoisonColor, 0.65f);
        }

        void UpdatePoison(float dt)
        {
            if (poisonTimer <= 0f) return;
            poisonTimer -= dt;
            if (Random.value < 0.25f) ArenaFX.Burst(Center + Random.insideUnitCircle * 0.4f, PoisonColor, 1, 1.5f, 0.07f, 0.4f, 1.5f);
            if ((poisonTick -= dt) > 0f) return;
            poisonTick = 0.5f;
            if (state == State.Dead) return;
            int before = GameSession.Run?.health ?? 0;
            if (GameUIController.Instance != null) GameUIController.Instance.DamagePlayer(poisonDamage);
            else GameSession.Damage(poisonDamage);
            int taken = before - (GameSession.Run?.health ?? 0);
            ArenaFX.DamageNumber(Center + Vector2.up * 0.7f, "-" + taken, PoisonColor, 0.7f);
            flash.Flash(PoisonColor, 0.1f);
            if (GameSession.Run != null && GameSession.Run.health <= 0) Die();
            if (poisonTimer <= 0f) poisonDamage = 0;
        }

        // =====================================================================
        // Getting hit
        // =====================================================================

        /// <summary>Called by bugs and projectiles. Returns what happened so they can react to parries.</summary>
        public HitResult TakeHit(int damage, Vector2 source, float knockback = 7f)
        {
            if (state == State.Dead || invulnTimer > 0f || Invulnerable) return HitResult.Ignored;
            if (state == State.Skill && activeSkill != null)
            {
                if (activeSkill.TryParry(source))
                {
                    UltimateSkill.Feed(UltimateSkill.ParryCharge);
                    return HitResult.Parried;
                }
                if (activeSkill.Invulnerable) return HitResult.Ignored;
                if (activeSkill.SuperArmor)
                {
                    // takes the damage but keeps going (Thunderclap, Flame Slash)
                    ApplyDamage(damage, true);
                    return HitResult.Damaged;
                }
            }

            int fromDir = source.x >= transform.position.x ? 1 : -1;
            if (state == State.Block && fromDir == Facing)
            {
                var shieldPos = Center + new Vector2(0.45f * Facing, 0f);
                if (blockTimer <= parryWindow)
                {
                    ShieldParry();
                    return HitResult.Parried;
                }
                float cost = blockStaminaBase + damage * blockStaminaPerDamage;
                if (stamina < cost)
                {
                    GuardBreak(damage, fromDir, knockback);
                    return HitResult.Damaged;
                }
                Drain(cost);
                guardCounterTimer = guardCounterWindow;
                ArenaFX.Spray(shieldPos, new Vector2(Facing, 0.5f), ParryColor, 8, 6f, 0.5f, 0.08f);
                ArenaFX.Shake(0.15f);
                squash = new Vector2(0.9f, 1.05f);
                Vel = new Vector2(-fromDir * knockback * 0.5f, Vel.y);
                int chip = Mathf.RoundToInt(damage * blockDamageTaken);
                if (chip > 0) ApplyDamage(chip, false);
                return HitResult.Blocked;
            }

            if (HyperArmor)
            {
                // hits hurt, but the swing / charge keeps going
                ApplyDamage(damage, true);
                squash = new Vector2(0.92f, 1.06f);
                return HitResult.Damaged;
            }

            if (state == State.Skill) CancelSkill();
            if (state == State.Drink) EndDrink(); // the sip is wasted
            if (state == State.Held) EndHold();   // a hit knocks you loose
            ApplyDamage(damage, true);
            if (state != State.Dead)
            {
                state = State.Hurt;
                stateTimer = hurtTime;
                anim.Play("hurt", true);
                Vel = new Vector2(-fromDir * knockback, 6f);
            }
            return HitResult.Damaged;
        }

        void ShieldParry()
        {
            var shieldPos = Center + new Vector2(0.45f * Facing, 0f);
            ArenaFX.Ring(shieldPos, ParryColor, 0.4f, 3.2f, 0.25f);
            ArenaFX.Burst(shieldPos, ParryColor, 16, 7f, 0.12f, 0.45f, 1f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.2f, "PARRY!", ParryColor, 0.9f);
            ArenaFX.HitStop(0.12f);
            ArenaFX.Shake(0.3f);
            flash.Flash(ParryColor, 0.2f);
            UltimateSkill.Feed(UltimateSkill.ParryCharge);
        }

        /// <summary>
        /// For grabs that can't be blocked but can be parried (the Mantis's execution): true if a perfect
        /// shield parry or Riposte catches it right now (Riposte does its counter as usual).
        /// </summary>
        public bool TryParryGrab(Vector2 source)
        {
            if (state == State.Dead) return false;
            if (state == State.Skill && activeSkill != null && activeSkill.TryParry(source))
            {
                UltimateSkill.Feed(UltimateSkill.ParryCharge);
                return true;
            }
            int fromDir = source.x >= transform.position.x ? 1 : -1;
            if (state == State.Block && fromDir == Facing && blockTimer <= parryWindow)
            {
                ShieldParry();
                return true;
            }
            return false;
        }

        /// <summary>Blocked with too little stamina: the shield is knocked aside and the knight reels.</summary>
        void GuardBreak(int damage, int fromDir, float knockback)
        {
            stamina = 0f;
            staminaDelay = exhaustedDelay;
            var shieldPos = Center + new Vector2(0.45f * Facing, 0f);
            ArenaFX.DamageNumber(Center + Vector2.up * 1.3f, "GUARD BROKEN", new Color(1f, 0.55f, 0.35f), 0.9f);
            ArenaFX.Burst(shieldPos, ParryColor, 14, 7f, 0.1f, 0.4f, 1f);
            ArenaFX.Ring(shieldPos, new Color(1f, 0.6f, 0.3f, 0.9f), 0.3f, 2.6f, 0.25f);
            ApplyDamage(Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f)), true);
            if (state == State.Dead) return;
            state = State.Hurt;
            stateTimer = guardBreakTime;
            anim.Play("hurt", true);
            Vel = new Vector2(-fromDir * knockback * 0.8f, 3f);
        }

        /// <summary>
        /// A boss parried our swing: knocked back and open for a moment, but no damage.
        /// Super-armored skills (Thunderclap, Flame Slash) shrug it off.
        /// </summary>
        public void Recoil(Vector2 source, float seconds)
        {
            if (state == State.Dead || Invulnerable) return;
            if (state == State.Skill && activeSkill != null)
            {
                if (activeSkill.SuperArmor || activeSkill.Invulnerable) return;
                CancelSkill();
            }
            if (state == State.Drink) EndDrink();
            if (state == State.Held) EndHold();
            int fromDir = source.x >= transform.position.x ? 1 : -1;
            state = State.Hurt;
            stateTimer = seconds;
            anim.Play("hurt", true);
            Vel = new Vector2(-fromDir * 9f, 4f);
            flash.Flash(new Color(1f, 0.9f, 0.5f), 0.2f);
        }

        /// <summary>Damage over time (poison clouds): no knockback, doesn't interrupt anything.</summary>
        public void TakePoison(int damage)
        {
            if (state == State.Dead) return;
            ApplyDamage(damage, false);
        }

        void ApplyDamage(int damage, bool fullHit)
        {
            int before = GameSession.Run?.health ?? 0;
            if (GameUIController.Instance != null) GameUIController.Instance.DamagePlayer(damage);
            else GameSession.Damage(damage);
            int taken = before - (GameSession.Run?.health ?? 0);
            UltimateSkill.Feed(taken * UltimateSkill.HurtCharge); // getting hurt builds it too

            ArenaFX.DamageNumber(Center + Vector2.up * 0.7f, "-" + taken, HurtColor, fullHit ? 1f : 0.8f);
            Sfx.Play("hurt", fullHit ? 0.8f : 0.35f);
            flash.Flash(HurtColor, 0.18f);
            if (fullHit)
            {
                invulnTimer = invulnerableTime;
                ArenaFX.HitStop(0.07f);
                ArenaFX.Shake(0.4f);
                ArenaFX.Burst(Center, HurtColor, 10, 5f, 0.12f, 0.5f, 1f);
            }
            if (GameSession.Run != null && GameSession.Run.health <= 0) Die();
        }

        void Die()
        {
            if (state == State.Dead) return;
            CancelSkill();
            if (state == State.Held) EndHold();
            poisonTimer = 0f;
            flaskInHand.enabled = false;
            state = State.Dead;
            anim.Play("dead", true);
            Sfx.Play("death", 0.9f, 1f, 0f);
            ArenaFX.Shake(0.5f);
            ArenaFX.Burst(Center, HurtColor, 24, 7f, 0.14f, 0.7f, 2f);
        }

        void Revive()
        {
            state = State.Normal;
            invulnTimer = 2f;
            stamina = maxStamina;
            flasksLeft = Mathf.Max(flasksLeft, 1);
            RefreshFlaskHud();
            anim.Play("idle", true);
            flash.Flash(Color.white, 0.4f);
            ArenaFX.Ring(Center, new Color(1f, 0.9f, 0.5f, 0.9f), 0.5f, 4f, 0.4f);
        }

        void FellOut()
        {
            transform.position = spawnPoint;
            rb.linearVelocity = Vector2.zero;
            if (state != State.Dead) ApplyDamage(10, true);
        }

        // =====================================================================
        // Physics, animation, visuals
        // =====================================================================

        void UpdateGravity()
        {
            float g;
            float vy = Vel.y;
            if (state == State.Held || state == State.Dodge && dodgeAirborne) g = 0f;
            else if (state == State.Skill && !grounded && skillHang) g = gravityScale * 0.15f; // hang in the air for the swing
            else if (vy < -0.1f) g = gravityScale * fallMultiplier;
            else if (vy > 0.1f && state == State.Normal && !ArenaInput.JumpHeld) g = gravityScale * lowJumpMultiplier;
            else g = gravityScale;
            rb.gravityScale = g * velScale * velScale; // same arc in the knight's time while the world is slowed
        }

        void FixedUpdate()
        {
            SyncTimeScale();
            var v = Vel;
            float dt = Time.fixedDeltaTime * velScale;
            switch (state)
            {
                case State.Normal:
                    float target = moveInput * runSpeed * SpeedMultiplier * WebPatch.SpeedAt(rb.position, webSlow); // sticky silk slows you
                    float accel = grounded ? (Mathf.Abs(target) > 0.01f ? groundAccel : groundDecel) : airAccel;
                    v.x = Mathf.MoveTowards(v.x, target, accel * dt);
                    break;
                case State.Held:
                    // pulled along by whatever is holding the knight
                    v = Vector2.ClampMagnitude((holdPoint - rb.position) / Mathf.Max(0.001f, dt), 28f);
                    break;
                case State.Block:
                    v.x = Mathf.MoveTowards(v.x, moveInput * blockMoveSpeed, groundDecel * dt);
                    break;
                case State.Drink:
                    v.x = Mathf.MoveTowards(v.x, moveInput * 1.8f, groundAccel * dt);
                    break;
                case State.Attack:
                    v.x = lungeTimer > 0f ? Facing * lungeSpeed : Mathf.MoveTowards(v.x, swing == Swing.Air ? v.x : 0f, groundDecel * 0.5f * dt);
                    break;
                case State.Charge:
                    v.x = Mathf.MoveTowards(v.x, 0f, groundDecel * dt);
                    break;
                case State.Stab:
                    v.x = lungeTimer > 0f ? Facing * lungeSpeed : Mathf.MoveTowards(v.x, 0f, groundDecel * 0.6f * dt);
                    break;
                case State.Critical:
                    v.x = critTime < 0.12f ? Mathf.Clamp((critStandX - rb.position.x) * 22f, -24f, 24f) : Mathf.MoveTowards(v.x, 0f, groundDecel * dt);
                    break;
                case State.Dodge:
                    v.x = dodgeDir * DodgeSpeedNow();
                    if (dodgeAirborne) v.y = 0f;
                    break;
                case State.Skill:
                    if (skillOwnsVelocity) return;
                    if (lungeTimer > 0f) v.x = Facing * lungeSpeed;
                    else if (skillMoveSpeed > 0f) v.x = Mathf.MoveTowards(v.x, moveInput * skillMoveSpeed, groundAccel * dt);
                    else v.x = Mathf.MoveTowards(v.x, 0f, groundDecel * 0.6f * dt);
                    if (!grounded && skillHang) v.y = Mathf.Max(v.y * 0.85f, -1.5f);
                    break;
                case State.Hurt:
                    v.x = Mathf.MoveTowards(v.x, 0f, 20f * dt);
                    break;
                case State.Dead:
                    v.x = Mathf.MoveTowards(v.x, 0f, 30f * dt);
                    break;
            }
            v.y = Mathf.Max(v.y, -maxFallSpeed);
            Vel = v;
        }

        void UpdateAnimation(float dt)
        {
            if (state != State.Normal) return;
            if (grounded)
            {
                bool running = Mathf.Abs(moveInput) > 0.1f && Mathf.Abs(Vel.x) > 0.5f;
                if (landTimer > 0f) anim.Play("land");
                else if (running) anim.Play("run");
                else anim.Play("idle");

                if (running && (footstepTimer -= dt) <= 0f)
                {
                    footstepTimer = 0.22f;
                    ArenaFX.Spray(transform.position, new Vector2(-Facing, 0.6f), DustColor, 2, 2f, 0.4f, 0.09f);
                }
            }
            else
            {
                float vy = Vel.y;
                anim.Play(vy > 2.5f ? "jumpUp" : vy > -2.5f ? "jumpApex" : "fall");
            }
        }

        /// <summary>Squash and stretch the knight for a moment (skills use it on wind-ups and impacts).</summary>
        public void Squash(float x, float y) => squash = new Vector2(x, y);

        void UpdateVisual(float dt)
        {
            squash = Vector2.Lerp(squash, Vector2.one, 1f - Mathf.Exp(-14f * dt));
            bool rolling = state == State.Dodge && !backstep;
            float sx = squash.x * (rolling ? 1.05f : 1f);
            float sy = squash.y * (rolling ? 0.95f : 1f);
            visual.localScale = new Vector3(Facing * sx, sy, 1f);

            if (rolling)
            {
                // tuck into a ball and somersault through the roll (spins around the middle of the body)
                float t = Mathf.Clamp01(dodgeTime / (dodgeDuration * 0.85f));
                float angle = -dodgeDir * 360f * (1f - (1f - t) * (1f - t));
                var rot = Quaternion.Euler(0f, 0f, angle);
                var pivot = new Vector3(0f, RollPivotHeight, 0f);
                visual.localRotation = rot;
                visual.localPosition = pivot - rot * pivot;
            }
            else if (visual.localRotation != Quaternion.identity || visual.localPosition != Vector3.zero)
            {
                visual.localRotation = Quaternion.identity;
                visual.localPosition = Vector3.zero;
            }
            if (state != State.Drink && flaskInHand.enabled) flaskInHand.enabled = false;

            bool blink = invulnTimer > 0f && state != State.Dead && Mathf.Repeat(Time.unscaledTime * 14f, 1f) < 0.5f;
            body.color = blink ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            foreach (var a in Combo)
                Gizmos.DrawWireCube(transform.position + new Vector3(a.boxOffset.x * Facing, a.boxOffset.y), a.boxSize);
        }
    }
}
