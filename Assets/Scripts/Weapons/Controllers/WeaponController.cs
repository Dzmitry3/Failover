using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    private const float MinFireRate = 0.01f;
    private const float MinHorizontalDirectionSqrMagnitude = 0.0001f;
    private const float MinShotDirectionSqrMagnitude = 0.0001f;
    private const float MinGamepadAimInputSqrMagnitude = 0.04f;

    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform rotateRoot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private HitScanShooter shooter;
    [SerializeField] private AimPreviewRenderer aimPreviewRenderer;

    [Header("Aiming")]
    [SerializeField] private LayerMask aimGroundMask;
    [SerializeField] private float maxAimRayDistance = 200f;
    [SerializeField] private bool rotateToAim = false;

    private bool _attackHeld;
    private float _nextFireTime;
    private bool _hasAimPoint;
    private Vector3 _currentAimPoint;
    private PlayerInput _playerInput;
    private InputAction _aimPointAction;
    private InputAction _lookAction;
    private bool _gameplayInputEnabled = true;

    private void InitializeReferences()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (rotateRoot == null) rotateRoot = transform;
        if (shooter == null) shooter = GetComponent<HitScanShooter>();
        if (aimPreviewRenderer == null) aimPreviewRenderer = GetComponent<AimPreviewRenderer>();
        if (aimPreviewRenderer == null) aimPreviewRenderer = gameObject.AddComponent<AimPreviewRenderer>();
        if (_playerInput == null) _playerInput = GetComponentInParent<PlayerInput>();
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
            Debug.LogError($"{nameof(WeaponController)}: WeaponData is not assigned.", this);

        if (shooter == null)
            Debug.LogError($"{nameof(WeaponController)}: HitScanShooter is not assigned.", this);

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
        if (weaponData == null || shooter == null) return;

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
            _attackHeld = true;
            FireOnce();
            return;
        }

        if (ctx.canceled)
        {
            _attackHeld = false;
        }
    }

    private void Update()
    {
        if (weaponData == null || shooter == null) return;
        if (!_gameplayInputEnabled)
        {
            _hasAimPoint = false;
            aimPreviewRenderer?.HidePreview();
            return;
        }

        _hasAimPoint = TryGetAimPoint(out _currentAimPoint);

        if (aimPreviewRenderer != null)
            aimPreviewRenderer.UpdatePreview(_currentAimPoint, _hasAimPoint);

        // Rotate independently from shooting so it works even when automatic=false.
        if (rotateToAim && _hasAimPoint)
            RotateTowards(_currentAimPoint);

        if (!_attackHeld) return;
        if (!weaponData.automatic) return;

        FireOnce();
    }

    private void FireOnce()
    {
        if (weaponData == null || shooter == null) return;
        if (!_hasAimPoint && !TryGetAimPoint(out _currentAimPoint)) return;
        if (Time.time < _nextFireTime) return;

        if (ShootTowards(_currentAimPoint))
            ConsumeFireCooldown();
    }

    public bool TryGetAimDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!_gameplayInputEnabled)
            return false;

        return _hasAimPoint &&
               TryGetDirection(rotateRoot.position, _currentAimPoint, ignoreVertical: true, out direction);
    }

    public bool TryGetAimPointWorld(out Vector3 aimPoint)
    {
        aimPoint = _currentAimPoint;
        return _gameplayInputEnabled && _hasAimPoint;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        _gameplayInputEnabled = enabled;
        if (enabled)
            return;

        _attackHeld = false;
        _hasAimPoint = false;
        _currentAimPoint = Vector3.zero;
        aimPreviewRenderer?.HidePreview();
    }

    private void ConsumeFireCooldown()
    {
        float rate = Mathf.Max(MinFireRate, weaponData.fireRate);
        _nextFireTime = Time.time + (1f / rate);
    }

    private bool TryGetHorizontalDirection(Vector3 from, Vector3 to, out Vector3 direction)
    {
        return TryGetDirection(from, to, ignoreVertical: true, out direction);
    }

    private bool ShootTowards(Vector3 aimPoint)
    {
        Vector3 origin = (firePoint != null) ? firePoint.position : shooter.transform.position;
        if (!TryGetDirection(origin, aimPoint, ignoreVertical: true, out var dir)) return false;

        shooter.Shoot(dir, out _);
        aimPreviewRenderer?.Flash();
        return true;
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera == null || rotateRoot == null)
            return false;

        if (TryGetPointerAimPoint(out aimPoint))
            return true;

        if (TryGetLookAimPoint(out aimPoint))
            return true;

        return false;
    }

    private bool TryGetPointerAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;
        if (_aimPointAction == null)
            InitializeInputActions();

        if (_aimPointAction == null || !_aimPointAction.enabled)
            return false;

        Vector2 screenPos = _aimPointAction.ReadValue<Vector2>();
        if (screenPos.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        Ray ray = aimCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out var hit, maxAimRayDistance, aimGroundMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            return true;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, rotateRoot.position.y, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            aimPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private bool TryGetLookAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;
        if (_lookAction == null)
            InitializeInputActions();

        if (_lookAction == null || !_lookAction.enabled)
            return false;

        Vector2 lookInput = _lookAction.ReadValue<Vector2>();
        if (lookInput.sqrMagnitude < MinGamepadAimInputSqrMagnitude)
            return false;

        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 cameraRight = aimCamera.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude < MinHorizontalDirectionSqrMagnitude ||
            cameraRight.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
        {
            return false;
        }

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 aimDirection = cameraRight * lookInput.x + cameraForward * lookInput.y;
        if (aimDirection.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        aimPoint = rotateRoot.position + aimDirection.normalized * maxAimRayDistance;
        return true;
    }

    private static bool TryGetDirection(Vector3 from, Vector3 to, bool ignoreVertical, out Vector3 direction)
    {
        direction = to - from;

        if (!ignoreVertical)
            return direction.sqrMagnitude >= MinShotDirectionSqrMagnitude;

        direction.y = 0f;
        if (direction.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        direction.Normalize();
        return true;
    }

    private void RotateTowards(Vector3 worldPoint)
    {
        if (!TryGetHorizontalDirection(rotateRoot.position, worldPoint, out var dir)) return;
        rotateRoot.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void InitializeInputActions()
    {
        if (_playerInput == null)
            _playerInput = GetComponentInParent<PlayerInput>();

        if (_playerInput?.actions == null)
            return;

        _aimPointAction = _playerInput.actions.FindAction("Player/AimPoint", throwIfNotFound: false);
        _lookAction = _playerInput.actions.FindAction("Player/Look", throwIfNotFound: false);
    }
}
