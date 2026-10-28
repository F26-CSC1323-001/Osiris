using UnityEngine;


public class CharacterMovement : MonoBehaviour
{
    public Animator animator;
    public bool isMoving = false;
    public bool isRunning = false;
    public bool isDefending = false;
    public float jumpForce = 5f;
    public Rigidbody2D rb;

    public Transform attackPoint;
    public float attackRange = 1f;
    public int attackDamage = 1;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");

        transform.position += new Vector3(moveX, 0, 0) * 3f * Time.deltaTime;

        isMoving = moveX != 0;
        isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;
        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsRunning", isRunning);
        
        // Jump with Space Bar
        if(Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetTrigger("Jump");
            if (rb == null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
        }
        
        // Attack while clicking Left Mouse
        if (Input.GetMouseButtonDown(0) && !isDefending)
        {
            animator.SetTrigger("Attack");
            Attack();
            
        }
        
        // Defend while holding G
        if (Input.GetKey(KeyCode.B))
        {
            isDefending = true;
        }
        else
        {
            isDefending = false;
        }
        animator.SetBool("IsDefending", isDefending);

        
   

        Debug.Log("IsMoving: " + isMoving);
        Debug.Log("IsRunning" + isRunning);
       
        
    }

    
void Attack()
    {
        if (attackPoint == null)
        {
            Debug.LogError("Attack Point is not assigned!");
            return;
        }
        
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackPoint.position, attackRange);

        

        foreach (Collider2D hit in hitObjects)
        {
            
       
              EnemyScript enemyScript = hit.GetComponentInParent<EnemyScript>();
                if (enemyScript != null)
                {
                    enemyScript.TakeDamage(attackDamage);
                    Debug.Log("Enemy is hit!");                    

                }
            
        }
    }
}
