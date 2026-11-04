using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Bloated acid tick: keeps its distance, swells up (tell), then lobs acid at the player.
    /// </summary>
    public class AcidTick : BugEnemy
    {
        public AcidBlob blobPrefab;
        public float walkSpeed = 1.6f;
        public Vector2 preferredDistance = new Vector2(4.5f, 8.5f);
        public float tellTime = 0.6f;
        public float spitCooldown = 2.4f;
        public float flightTime = 0.9f;

        enum Mode { Walk, Tell, Recover }
        Mode mode;
        float timer, cooldownTimer;
        float vx;

        protected override void Start()
        {
            base.Start();
            cooldownTimer = Random.Range(1f, 2f);
            anim.Play("idle");
        }

        protected override void Think(float dt)
        {
            cooldownTimer -= dt;
            timer -= dt;

            switch (mode)
            {
                case Mode.Walk:
                {
                    if (!PlayerAlive) { vx = 0f; anim.Play("idle"); break; }
                    var d = ToPlayer;
                    float dist = Mathf.Abs(d.x);
                    FacePlayer();
                    int moveDir = 0;
                    if (dist < preferredDistance.x) moveDir = -facing;
                    else if (dist > preferredDistance.y) moveDir = facing;

                    if (moveDir != 0)
                    {
                        int keep = facing;
                        facing = moveDir;
                        bool blocked = !GroundAhead(0.7f) || WallAhead(0.8f);
                        facing = keep;
                        vx = blocked ? 0f : moveDir * walkSpeed;
                    }
                    else
                    {
                        vx = 0f;
                    }
                    anim.Play(Mathf.Abs(vx) > 0.1f ? "walk" : "idle");

                    if (cooldownTimer <= 0f && d.magnitude < 13f)
                    {
                        mode = Mode.Tell;
                        timer = tellTime * telegraphScale;
                        BeginTell(timer);
                        anim.Play("charge");
                        vx = 0f;
                    }
                    break;
                }

                case Mode.Tell:
                    FacePlayer();
                    if (timer <= 0f)
                    {
                        EndTell();
                        Spit();
                        mode = Mode.Recover;
                        timer = 0.5f;
                        anim.Play("spit", true);
                    }
                    break;

                case Mode.Recover:
                    if (timer <= 0f)
                    {
                        mode = Mode.Walk;
                        cooldownTimer = spitCooldown;
                    }
                    break;
            }
        }

        void Spit()
        {
            if (!PlayerAlive || blobPrefab == null) return;
            var mouth = Center + new Vector2(facing * 0.55f, 0.2f);
            var blob = Instantiate(blobPrefab, mouth, Quaternion.identity);
            var lead = new Vector2(Random.Range(-0.6f, 0.6f), 0f);
            blob.Launch((Vector2)player.transform.position + Vector2.up * 0.6f + lead, flightTime, damage);
            ArenaFX.Burst(mouth, bloodColor, 6, 3f, 0.1f, 0.3f, 1f);
        }

        protected override void OnStagger()
        {
            EndTell();
            mode = Mode.Recover;
            timer = 0.3f;
        }

        void FixedUpdate()
        {
            if (IsDead || staggerTimer > 0f || stunTimer > 0f) return;
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
