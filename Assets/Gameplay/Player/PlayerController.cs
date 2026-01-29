using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private Vector2 moveInput;
    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    // This method is called by Player Input component (Send Messages mode)
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (moveInput.magnitude < 0.01f) return;

        // Convert input to 3D movement direction
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // Move the character
        if (characterController != null)
        {
            characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
    }
}
