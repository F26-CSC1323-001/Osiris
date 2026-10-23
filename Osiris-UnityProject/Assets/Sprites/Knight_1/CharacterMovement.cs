using UnityEngine;


public class CharacterMovement : MonoBehaviour
{
    public Animator animator;
    public bool isMoving = false;
    public bool isRunning = false;
    public float jumpForce = 5f;
    public Rigidbody2D rb;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");

        transform.position += new Vector3(moveX, 0, 0) * 3f * Time.deltaTime;

        isMoving = moveX != 0;
        isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;
        if(Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetTrigger("Jump");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            
        }

        Debug.Log("IsMoving: " + isMoving);
        Debug.Log("IsRunning" + isRunning);
       
        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsRunning", isRunning);
    }
}
