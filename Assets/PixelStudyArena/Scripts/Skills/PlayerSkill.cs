using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Base class for the knight's skills (Dead Cells-style: a key, a cooldown, an icon on the HUD).
    ///
    /// To add a new skill:
    ///   1. Add an entry to SkillCatalog (id, name, key, unlocked at start or not).
    ///   2. Make a class that extends PlayerSkill and fill in TryActivate / Tick.
    ///   3. Add it to the Knight in ArenaBuilder.BuildPlayer. The HUD shows seven slots, in component order.
    /// </summary>
    [RequireComponent(typeof(ArenaPlayer))]
    public abstract class PlayerSkill : MonoBehaviour
    {
        [Header("Skill")]
        [Tooltip("Id from SkillCatalog. Decides whether the skill is unlocked this run.")]
        public string skillId;
        public string displayName = "Skill";
        public KeyCode key = KeyCode.Z;
        public KeyCode gamepadButton = KeyCode.None;
        public float cooldown = 7f;
        public Sprite icon;

        public float CooldownLeft { get; protected set; }
        public float Cooldown01 => cooldown <= 0f ? 0f : Mathf.Clamp01(CooldownLeft / cooldown);
        public virtual bool Ready => CooldownLeft <= 0f;
        public string KeyLabel => key.ToString();
        public bool Unlocked => string.IsNullOrEmpty(skillId) || SkillCatalog.IsUnlocked(GameSession.Run, skillId);

        /// <summary>While true, hits on the knight are ignored (e.g. mid-slam).</summary>
        public virtual bool Invulnerable => false;

        /// <summary>While true, hits still hurt but don't interrupt the skill.</summary>
        public virtual bool SuperArmor => false;

        /// <summary>Return true to parry an incoming hit (Riposte).</summary>
        public virtual bool TryParry(Vector2 source) => false;

        protected ArenaPlayer player;

        protected virtual void Awake() => player = GetComponent<ArenaPlayer>();

        protected virtual void Update()
        {
            if (CooldownLeft > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - Time.deltaTime);
        }

        public bool Pressed() =>
            Input.GetKeyDown(key) || (gamepadButton != KeyCode.None && Input.GetKeyDown(gamepadButton));

        protected void StartCooldown() => CooldownLeft = cooldown;

        /// <summary>Start the skill. Return false to refuse (the player stays in control).</summary>
        public abstract bool TryActivate();

        /// <summary>Runs every frame while the skill has control of the knight. Return true when finished.</summary>
        public abstract bool Tick(float dt);

        /// <summary>Called if the knight gets hit or dies mid-skill.</summary>
        public virtual void Cancel() { }

        // ---------- helpers for skills ----------

        static readonly List<Collider2D> Hits = new List<Collider2D>();

        static ContactFilter2D EnemyFilter()
        {
            var f = new ContactFilter2D();
            f.SetLayerMask(ArenaLayers.EnemyMask);
            f.useTriggers = true;
            return f;
        }

        static List<BugEnemy> Collect()
        {
            var list = new List<BugEnemy>();
            foreach (var h in Hits)
            {
                var bug = h.GetComponentInParent<BugEnemy>();
                if (bug != null && bug.CanBeTargeted && !list.Contains(bug)) list.Add(bug);
            }
            return list;
        }

        protected static List<BugEnemy> EnemiesInBox(Vector2 center, Vector2 size)
        {
            Hits.Clear();
            Physics2D.OverlapBox(center, size, 0f, EnemyFilter(), Hits);
            return Collect();
        }

        protected static List<BugEnemy> EnemiesInCircle(Vector2 center, float radius)
        {
            Hits.Clear();
            Physics2D.OverlapCircle(center, radius, EnemyFilter(), Hits);
            return Collect();
        }

        /// <summary>Damage with Sharper Blade upgrades and a little random spread.</summary>
        protected int Scaled(int amount) => Mathf.Max(1, Mathf.RoundToInt(amount * player.AttackMultiplier * Random.Range(0.92f, 1.08f)));
    }
}
