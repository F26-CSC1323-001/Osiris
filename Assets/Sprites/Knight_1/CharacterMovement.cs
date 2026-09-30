using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
    public Animator animator;
    public bool isMoving = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        isMoving = Input.GetAxisRaw("Horizontal") != 0;
        animator.SetBool("isMoving", isMoving);
    }
}
