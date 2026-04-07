using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    private const float MinFireRate = 0.01f;

    [Header("Data")]
    [SerializeField] private WeaponDefinition weaponData;

    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform rotateRoot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private HitscanShooter shooter;
    [SerializeField] private AimPreviewRenderer aimPreviewRenderer;

    [Header("Aiming")]
    [SerializeField] private LayerMask aimGroundMask;
    [SerializeField] private float maxAimRayDistance = 200f;
    [SerializeField] private bool rotateToAim = false;

    private bool attackHeld;
    private bool hasAimPoint;
    private Vector3 currentAimPoint;
    private PlayerInput playerInput;
    private InputAction aimPointAction;
    private InputAction lookAction;
    private bool gameplayInputEnabled = true;
    private WeaponAimSolver aimSolver;
    private WeaponFireCooldown fireCooldown;

    private void InitializeReferences()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (rotateRoot == null)
            rotateRoot = transform;

        if (shooter == null)
            shooter = GetComponent<HitscanShooter>();

        if (aimPreviewRenderer == null)
            aimPreviewRenderer = GetComponent<AimPreviewRenderer>();

        if (aimPreviewRenderer == null)
            aimPreviewRenderer = gameObject.AddComponent<AimPreviewRenderer>();

        if (playerInput == null)
            playerInput = GetComponentInParent<PlayerInput>();

        fireCooldown ??= new WeaponFireCooldown(MinFireRate);
        RefreshAimSolver();
        InitializeInputActions();
    }

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();

        if (weaponData == null)
            Debug.LogError($"{nameof(WeaponController)}: {nameof(WeaponDefinition)} is not assigned.", this);

        if (shooter == null)
            Debug.LogError($"{nameof(WeaponController)}: {nameof(HitscanShooter)} is not assigned.", this);

        if (firePoint != null && shooter != null)
            shooter.SetFirePoint(firePoint);

        if (aimPreviewRenderer != null)
        {
            aimPreviewRenderer.SetFirePoint(firePoint != null ? firePoint : transform);
            aimPreviewRenderer.SetShooter(shooter);
        }

        ApplyWeaponDataToShooter();
    }

    private void ApplyWeaponDataToShooter()
    {
        if (weaponData == null || shooter == null)
            return;

        shooter.SetDamage(weaponData.damage);
        shooter.SetRange(weaponData.range);

        if (aimPreviewRenderer != null)
            aimPreviewRenderer.SetPreviewDistance(weaponData.range);
    }

    // Assign this in PlayerInput -> Events -> Player -> Fire (CallbackContext)
    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            attackHeld = true;
            FireOnce();
            return;
        }

        if (ctx.canceled)
            attackHeld = false;
    }

    private void Update()
    {
        if (weaponData == null || shooter == null)
            return;

        if (!gameplayInputEnabled)
        {
            hasAimPoint = false;
            aimPreviewRenderer?.HidePreview();
            return;
        }

        if (aimCamera == null && Camera.main != null)
        {
            aimCamera = Camera.main;
            RefreshAimSolver();
        }

        hasAimPoint = TryGetAimPoint(out currentAimPoint);

        if (aimPreviewRenderer != null)
            aimPreviewRenderer.UpdatePreview(currentAimPoint, hasAimPoint);

        if (rotateToAim && hasAimPoint)
            aimSolver?.RotateTowards(currentAimPoint);

        if (!attackHeld || !weaponData.automatic)
            return;

        FireOnce();
    }

    private void FireOnce()
    {
        if (weaponData == null || shooter == null)
            return;

        if (!hasAimPoint && !TryGetAimPoint(out currentAimPoint))
            return;

        if (!fireCooldown.CanFire(Time.time))
            return;

        if (ShootTowards(currentAimPoint))
            fireCooldown.Consume(Time.time, weaponData.fireRate);
    }

    public bool TryGetAimDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!gameplayInputEnabled || !hasAimPoint || rotateRoot == null || aimSolver == null)
            return false;

        return aimSolver.TryGetDirection(rotateRoot.position, currentAimPoint, ignoreVertical: true, out direction);
    }

    public bool TryGetAimPointWorld(out Vector3 aimPoint)
    {
        aimPoint = currentAimPoint;
        return gameplayInputEnabled && hasAimPoint;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;
        if (enabled)
            return;

        attackHeld = false;
        hasAimPoint = false;
        currentAimPoint = Vector3.zero;
        aimPreviewRenderer?.HidePreview();
    }

    private bool ShootTowards(Vector3 aimPoint)
    {
        Vector3 origin = firePoint != null ? firePoint.position : shooter.transform.position;
        if (aimSolver == null || !aimSolver.TryGetDirection(origin, aimPoint, ignoreVertical: true, out Vector3 direction))
            return false;

        shooter.Shoot(direction, out _);
        aimPreviewRenderer?.Flash();
        return true;
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
            RefreshAimSolver();
        }

        if (aimSolver == null || rotateRoot == null)
            return false;

        if (aimPointAction == null || lookAction == null)
            InitializeInputActions();

        Vector2 pointerInput = aimPointAction != null ? aimPointAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        return aimSolver.TryGetAimPoint(
            pointerInput,
            aimPointAction != null && aimPointAction.enabled,
            lookInput,
            lookAction != null && lookAction.enabled,
            out aimPoint);
    }

    private void RefreshAimSolver()
    {
        aimSolver = aimCamera != null && rotateRoot != null
            ? new WeaponAimSolver(aimCamera, rotateRoot, aimGroundMask, maxAimRayDistance)
            : null;
    }

    private void InitializeInputActions()
    {
        if (playerInput == null)
            playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput?.actions == null)
            return;

        aimPointAction = playerInput.actions.FindAction("Player/AimPoint", throwIfNotFound: false);
        lookAction = playerInput.actions.FindAction("Player/Look", throwIfNotFound: false);
    }
}
