using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerUpperBodyAim))]
public class PlayerController : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int LocomotionSpeedHash = Animator.StringToHash("LocomotionSpeed");

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float visualTurnSpeed = 720f;

    [Header("References")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private Camera movementCamera;
    [SerializeField] private HealthComponent health;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    private Vector2 moveInput;
    private float verticalVelocity;
    private CharacterController controller;
    private Animator animator;
    private Transform visualRoot;
    private Transform movementCameraTransform;
    private bool isMovementLocked;
    private bool isDead;
    private Vector3 desiredMoveDirection;

    private void InitializeMovementCameraTransform()
    {
        movementCameraTransform = movementCamera != null
            ? movementCamera.transform
            : Camera.main != null ? Camera.main.transform : null;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        visualRoot = animator != null ? animator.transform : null;
        InitializeMovementCameraTransform();

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();

        if (health == null)
            health = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnHealthChanged += HandleHealthChanged;
        }

        isDead = health != null && health.IsDead;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    // Bind in PlayerInput -> Events -> Player -> Move (CallbackContext)
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (isMovementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = ctx.ReadValue<Vector2>();
    }

    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;
        if (locked)
            moveInput = Vector2.zero;
    }

    private void Update()
    {
        if (isDead)
        {
            desiredMoveDirection = Vector3.zero;
            UpdateAnimator();
            return;
        }

        HandleMovement();
        UpdateVisualFacing();
        UpdateAnimator();
    }

    private void HandleMovement()
    {
        Vector3 move = Vector3.zero;
        desiredMoveDirection = Vector3.zero;
        if (!isMovementLocked && moveInput.sqrMagnitude > 0.0001f)
        {
            if (movementCameraTransform == null && Camera.main != null)
                InitializeMovementCameraTransform();

            Vector3 camForward = movementCameraTransform != null ? movementCameraTransform.forward : Vector3.forward;
            Vector3 camRight = movementCameraTransform != null ? movementCameraTransform.right : Vector3.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 desired = (camRight * moveInput.x + camForward * moveInput.y);

            if (desired.sqrMagnitude > 1f)
                desired.Normalize();

            desiredMoveDirection = desired;
            move = desired * moveSpeed;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateVisualFacing()
    {
        if (visualRoot == null)
            return;

        if (!TryGetVisualFacingDirection(out var facingDirection))
            return;

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRotation,
            visualTurnSpeed * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        Vector3 localMoveDirection = Vector3.zero;
        if (visualRoot != null && desiredMoveDirection.sqrMagnitude > 0.0001f)
            localMoveDirection = visualRoot.InverseTransformDirection(desiredMoveDirection);

        Vector2 locomotionBlend = Vector2.zero;
        if (localMoveDirection.sqrMagnitude > 0.0001f)
        {
            locomotionBlend = new Vector2(localMoveDirection.x, localMoveDirection.z);
            float dominantAxis = Mathf.Max(Mathf.Abs(locomotionBlend.x), Mathf.Abs(locomotionBlend.y));
            if (dominantAxis > 1f || dominantAxis > 0.0001f)
                locomotionBlend /= dominantAxis;
        }

        float locomotionSpeed = 0f;
        float maxCurrentSpeed = Mathf.Max(0.0001f, moveSpeed);
        Vector3 planarVelocity = controller.velocity;
        planarVelocity.y = 0f;
        locomotionSpeed = Mathf.Clamp01(planarVelocity.magnitude / maxCurrentSpeed);

        animator.SetFloat(MoveXHash, Mathf.Clamp(locomotionBlend.x, -1f, 1f));
        animator.SetFloat(MoveYHash, Mathf.Clamp(locomotionBlend.y, -1f, 1f));
        animator.SetFloat(LocomotionSpeedHash, locomotionSpeed);
    }

    private bool TryGetVisualFacingDirection(out Vector3 facingDirection)
    {
        facingDirection = Vector3.zero;

        if (weaponController != null && weaponController.TryGetAimDirection(out facingDirection))
            return true;

        if (desiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            facingDirection = desiredMoveDirection.normalized;
            return true;
        }

        return false;
    }

    private void HandleDeath()
    {
        isDead = true;
        isMovementLocked = true;
        moveInput = Vector2.zero;
        desiredMoveDirection = Vector3.zero;
    }

    private void HandleHealthChanged(float currentHealth, float _)
    {
        if (currentHealth > 0f)
            isDead = false;
    }
}
