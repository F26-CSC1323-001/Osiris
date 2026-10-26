using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    public float speed = 1f;
    public float distance = 2f;

    private Vector3 startPosition;
    private int direction = 1;


    public int health = 3;

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

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}
