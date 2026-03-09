using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    private Vector2 moveInput;
    private float verticalVelocity;
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Bind in PlayerInput → Events → Player → Move (CallbackContext)
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // --- Horizontal movement (camera-relative on XZ) ---
        Vector3 move = Vector3.zero;
        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;

            Vector3 camForward = cam != null ? cam.forward : Vector3.forward;
            Vector3 camRight   = cam != null ? cam.right   : Vector3.right;

            camForward.y = 0f;
            camRight.y   = 0f;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 desired = (camRight * moveInput.x + camForward * moveInput.y);

            // чтобы диагональ не была быстрее
            if (desired.sqrMagnitude > 1f) desired.Normalize();

            move = desired * moveSpeed;
        }

        // --- Ground check & gravity ---
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        verticalVelocity += gravity * Time.deltaTime;

        // --- Final movement ---
        Vector3 velocity = move + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}