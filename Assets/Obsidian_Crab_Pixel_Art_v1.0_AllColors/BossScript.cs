
using UnityEngine;

public class BossScript : MonoBehaviour
{
    [Header("Boss Stats")]
    public int health = 20;
    public int damage = 2;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float attackRange = 1.5f;

    [Header("Combat")]
    public float attackCooldown = 2f;

    [Header("Player")]
    public Transform player;

    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private float nextAttackTime;
    private bool isDead = false;
    private bool isAttacking = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (isDead || player == null)
            return;

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        // Follow the player
        if (distance > attackRange)
        {
            animator.SetBool("IsMoving", true);
            isAttacking = false;
        }
        else
        {
            animator.SetBool("IsMoving", false);

            // Attack when close enough
            if (Time.time >= nextAttackTime && !isAttacking)
            {
                AttackPlayer();
            }
        }

        // Face the player
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX =
                player.position.x > transform.position.x;
        }
    }

    void FixedUpdate()
    {
        if (isDead || player == null)
            return;

        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        if (distance > attackRange)
        {
            Vector2 direction =
                (player.position - transform.position).normalized;

            rb.MovePosition(
                rb.position + direction * moveSpeed * Time.fixedDeltaTime
            );
        }
    }

    void AttackPlayer()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        animator.SetTrigger("Attack");

        Debug.Log("Boss attacks!");
    }

    // Called by the knight when the boss gets hit
    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        health -= amount;

        Debug.Log("Boss health remaining: " + health);

        if (health <= 0)
        {
            isDead = true;

            animator.SetBool("IsMoving", false);
            animator.SetTrigger("Death");

            rb.simulated = false;

            return;
        }

        animator.SetTrigger("Hurt");
    }

    // Called by an animation event when the attack animation finishes
    public void FinishAttack()
    {
        isAttacking = false;
    }
}

