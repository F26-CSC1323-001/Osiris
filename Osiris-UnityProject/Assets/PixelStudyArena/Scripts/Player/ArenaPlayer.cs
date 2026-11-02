using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Mockup 2 knight: movement only. Walks/runs left and right, jumps (one jump, no double jump),
    /// drops through platforms (Down + Jump). No attacks, dodge, shield or skills.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class ArenaPlayer : MonoBehaviour
    {
        public static ArenaPlayer Instance { get; private set; }

        [Header("Parts")]
        public Transform visual;
        public SpriteRenderer body;
        public PixelAnimator anim;

        [Header("Run")]
        public float runSpeed = 8f;
        public float groundAccel = 80f;
        public float groundDecel = 110f;
        public float airAccel = 55f;

        [Header("Jump")]
        public float jumpHeight = 4.4f;
        public float gravityScale = 4.2f;
        public float fallMultiplier = 1.6f;
        public float lowJumpMultiplier = 2.4f;
        public float maxFallSpeed = 24f;
        [Tooltip("A moment after walking off a ledge where you can still jump.")]
        public float coyoteTime = 0.1f;
        [Tooltip("Pressing jump just before landing still jumps.")]
        public float jumpBufferTime = 0.13f;

        public int Facing { get; private set; } = 1;
        public bool IsDead => false;
        public bool IsGrounded => grounded;
        public Vector2 Center => (Vector2)transform.position + new Vector2(0f, 0.95f);

        static readonly Color DustColor = new Color(0.75f, 0.78f, 0.82f, 0.9f);

        Rigidbody2D rb;
        CapsuleCollider2D capsule;
        bool grounded, wasGrounded;
        float coyoteTimer, jumpBufferTimer, landTimer, footstepTimer, ignoreTimer, lowestFallSpeed, moveInput;
        Vector2 squash = Vector2.one;
        Vector3 spawnPoint;
        Collider2D ignoredPlatform;
        readonly List<Collider2D> hits = new List<Collider2D>();
        ContactFilter2D groundFilter, platformFilter;

        float JumpVelocity => Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * gravityScale * jumpHeight);

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            rb.gravityScale = gravityScale;
            groundFilter = Filter(ArenaLayers.GroundMask);
            platformFilter = Filter(ArenaLayers.PlatformMask);
            spawnPoint = transform.position;
        }

        static ContactFilter2D Filter(int mask)
        {
            var f = new ContactFilter2D();
            f.SetLayerMask(mask);
            f.useTriggers = false;
            return f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => anim.Play("idle");

        void Update()
        {
            if (Time.timeScale < 0.01f) return; // a menu has paused the game
            float dt = Time.deltaTime;

            jumpBufferTimer -= dt;
            coyoteTimer -= dt;
            landTimer -= dt;
            if (ignoredPlatform != null && (ignoreTimer -= dt) <= 0f)
            {
                Physics2D.IgnoreCollision(capsule, ignoredPlatform, false);
                ignoredPlatform = null;
            }

            CheckGround();
            moveInput = ArenaInput.Move;
            if (Mathf.Abs(moveInput) > 0.1f) Facing = moveInput > 0f ? 1 : -1;
            if (ArenaInput.JumpDown) jumpBufferTimer = jumpBufferTime;

            if (jumpBufferTimer > 0f)
            {
                if (ArenaInput.Down && grounded && TryDropThrough()) jumpBufferTimer = 0f;
                else if (coyoteTimer > 0f) Jump(); // one jump only: no double jump in the air
            }

            UpdateGravity();
            UpdateAnimation(dt);
            UpdateVisual(dt);

            if (transform.position.y < -6f) Respawn();
        }

        void CheckGround()
        {
            wasGrounded = grounded;
            var b = capsule.bounds;
            hits.Clear();
            Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y - 0.04f), new Vector2(b.size.x * 0.85f, 0.1f), 0f, groundFilter, hits);
            bool touching = false;
            foreach (var h in hits)
                if (h != ignoredPlatform) touching = true;

            grounded = touching && rb.linearVelocity.y <= 0.1f;
            if (grounded)
            {
                coyoteTimer = coyoteTime;
                if (!wasGrounded) OnLand();
            }
            else lowestFallSpeed = Mathf.Min(lowestFallSpeed, rb.linearVelocity.y);
        }

        void OnLand()
        {
            if (lowestFallSpeed < -11f)
            {
                squash = new Vector2(1.25f, 0.78f);
                landTimer = 0.1f;
                var feet = (Vector2)transform.position;
                ArenaFX.Spray(feet, Vector2.left + Vector2.up * 0.2f, DustColor, 4, 3.5f, 0.3f, 0.12f);
                ArenaFX.Spray(feet, Vector2.right + Vector2.up * 0.2f, DustColor, 4, 3.5f, 0.3f, 0.12f);
            }
            lowestFallSpeed = 0f;
        }

        void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpVelocity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            grounded = false;
            squash = new Vector2(0.8f, 1.2f);
            var feet = (Vector2)transform.position;
            ArenaFX.Spray(feet, Vector2.down + Vector2.left * 0.6f, DustColor, 3, 3f, 0.3f, 0.1f);
            ArenaFX.Spray(feet, Vector2.down + Vector2.right * 0.6f, DustColor, 3, 3f, 0.3f, 0.1f);
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
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -4f);
            return true;
        }

        void Respawn()
        {
            transform.position = spawnPoint;
            rb.position = spawnPoint;
            rb.linearVelocity = Vector2.zero;
        }

        void UpdateGravity()
        {
            float vy = rb.linearVelocity.y;
            if (vy < -0.1f) rb.gravityScale = gravityScale * fallMultiplier;
            else if (vy > 0.1f && !ArenaInput.JumpHeld) rb.gravityScale = gravityScale * lowJumpMultiplier; // short hop on a tap
            else rb.gravityScale = gravityScale;
        }

        void FixedUpdate()
        {
            var v = rb.linearVelocity;
            float target = moveInput * runSpeed;
            float accel = grounded ? (Mathf.Abs(target) > 0.01f ? groundAccel : groundDecel) : airAccel;
            v.x = Mathf.MoveTowards(v.x, target, accel * Time.fixedDeltaTime);
            v.y = Mathf.Max(v.y, -maxFallSpeed);
            rb.linearVelocity = v;
        }

        void UpdateAnimation(float dt)
        {
            if (grounded)
            {
                bool running = Mathf.Abs(moveInput) > 0.1f && Mathf.Abs(rb.linearVelocity.x) > 0.5f;
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
                float vy = rb.linearVelocity.y;
                anim.Play(vy > 2.5f ? "jumpUp" : vy > -2.5f ? "jumpApex" : "fall");
            }
        }

        void UpdateVisual(float dt)
        {
            squash = Vector2.Lerp(squash, Vector2.one, 1f - Mathf.Exp(-14f * dt));
            visual.localScale = new Vector3(Facing * squash.x, squash.y, 1f);
        }
    }
}
