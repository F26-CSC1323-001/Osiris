using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Ground beetle: scuttles toward the player, rears back with a tell, then lunges for a bite.
    /// Turns around at ledges and walls.
    /// </summary>
    public class BeetleBug : BugEnemy
    {
        public float walkSpeed = 2.6f;
        public float attackRange = 2.4f;
        public float tellTime = 0.45f;
        public float lungeSpeed = 10f;
        public float lungeTime = 0.22f;
        public float recoverTime = 0.6f;
        public float cooldown = 0.8f;

        enum Mode { Walk, Tell, Lunge, Recover }
        Mode mode;
        float timer, cooldownTimer, patrolTimer, hopCooldown;
        bool hitThisLunge;
        float vx;

        bool Grounded => rb.linearVelocity.y <= 0.1f &&
                         Physics2D.Raycast((Vector2)transform.position + Vector2.up * 0.1f, Vector2.down, 0.2f, ArenaLayers.GroundMask);

        protected override void Start()
        {
            base.Start();
            facing = Random.value < 0.5f ? -1 : 1;
            anim.Play("walk");
        }

        protected override void Think(float dt)
        {
            cooldownTimer -= dt;
            timer -= dt;

            switch (mode)
            {
                case Mode.Walk:
                {
                    anim.Play("walk");
                    hopCooldown -= dt;
                    var d = ToPlayer;
                    bool sameLevel = Mathf.Abs(d.y) < 1.3f;
                    // Hunt the player across the arena: hop up short ledges, drop down from platforms.
                    bool chasing = PlayerAlive && Mathf.Abs(d.x) < 70f && d.y < 3.5f && d.y > -14f;
                    if (chasing)
                    {
                        FacePlayer();
                        if (sameLevel && Mathf.Abs(d.x) < attackRange && cooldownTimer <= 0f)
                        {
                            mode = Mode.Tell;
                            timer = DelayedTell(tellTime);
                            BeginTell(timer);
                            anim.Play("tell");
                            vx = 0f;
                            break;
                        }
                    }
                    else if ((patrolTimer -= dt) <= 0f)
                    {
                        patrolTimer = Random.Range(1.5f, 3f);
                        facing = -facing;
                    }

                    bool wall = WallAhead(0.8f);
                    bool ledge = !GroundAhead(0.7f);
                    if (chasing && wall && d.y > 0.5f && Grounded && hopCooldown <= 0f)
                    {
                        // hop up onto the ledge the player is standing on
                        rb.linearVelocity = new Vector2(facing * 3.5f, 14f);
                        vx = facing * 3.5f;
                        hopCooldown = 1.2f;
                    }
                    else if (chasing && ledge && !wall && Mathf.Abs(d.x) > 1.5f)
                    {
                        vx = facing * walkSpeed; // drop off the edge to keep after the player
                    }
                    else if (wall || ledge)
                    {
                        if (chasing) vx = Grounded ? 0f : vx;
                        else { facing = -facing; vx = 0f; }
                    }
                    else
                    {
                        vx = facing * walkSpeed;
                    }
                    break;
                }

                case Mode.Tell:
                    vx = -facing * 0.6f; // creeps back while winding up
                    if (timer <= 0f)
                    {
                        EndTell();
                        mode = Mode.Lunge;
                        timer = lungeTime;
                        hitThisLunge = false;
                        anim.Play("lunge");
                        ArenaFX.Spray(transform.position, new Vector2(-facing, 0.4f), new Color(0.6f, 0.55f, 0.5f), 5, 4f, 0.4f, 0.1f);
                    }
                    break;

                case Mode.Lunge:
                    vx = GroundAhead(0.6f) ? facing * lungeSpeed : 0f;
                    if (!hitThisLunge)
                    {
                        var r = HitPlayerInBox(Center + new Vector2(facing * 0.7f, 0f), new Vector2(1.2f, 0.9f), damage, 7f);
                        if (r != HitResult.Ignored) hitThisLunge = true;
                    }
                    if (timer <= 0f)
                    {
                        mode = Mode.Recover;
                        timer = recoverTime;
                        anim.Play("walk");
                    }
                    break;

                case Mode.Recover:
                    vx = Mathf.MoveTowards(vx, 0f, 30f * dt);
                    if (timer <= 0f)
                    {
                        mode = Mode.Walk;
                        cooldownTimer = cooldown;
                    }
                    break;
            }
        }

        // committed to the bite: light hits don't stop it
        protected override bool HyperArmor => mode == Mode.Lunge;

        protected override void OnStagger()
        {
            EndTell();
            mode = Mode.Recover;
            timer = 0.25f;
            vx = rb.linearVelocity.x;
        }

        void FixedUpdate()
        {
            if (IsDead || staggerTimer > 0f || stunTimer > 0f) return;
            rb.linearVelocity = new Vector2(vx * SpeedScale, rb.linearVelocity.y);
        }
    }
}
