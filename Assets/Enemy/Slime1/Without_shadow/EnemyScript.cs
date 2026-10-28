using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    public float speed = 1f;
    public float distance = 2f;

    private Vector3 startPosition;
    private int direction = 1;
    private Animator animator;



    public int health = 5;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        transform.position += Vector3.right * direction * speed * Time.deltaTime;

        if(transform.position.x >= startPosition.x + distance)
        {
            direction = -1;
        }
        else if (transform.position.x <= startPosition.x - distance)
        {
            direction = 1;
        }
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
    }
    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log("Enemy health remaining:" + health);

        // plays the hurt animation
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }        
        
        // Enemy dies when health reaches zero
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}
