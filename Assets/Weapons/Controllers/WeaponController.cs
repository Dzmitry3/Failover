using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Camera aimCamera;         
    [SerializeField] private Transform rotateRoot;     
    [SerializeField] private Transform firePoint;     
    [SerializeField] private HitScanShooter shooter;  

    [Header("Aiming")]
    [SerializeField] private LayerMask aimGroundMask; 
    [SerializeField] private float maxAimRayDistance = 200f;
    [SerializeField] private bool rotateToAim = true;

    private bool _attackHeld;
    private float _nextFireTime;

    private void InitializeReferences()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (rotateRoot == null) rotateRoot = transform;
        if (shooter == null) shooter = GetComponent<HitScanShooter>();
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

        ApplyWeaponDataToShooter();
    }

    private void ApplyWeaponDataToShooter()
    {
        if (weaponData == null || shooter == null) return;

        shooter.SetDamage(weaponData.damage);
        shooter.SetRange(weaponData.range);
    }

    
    public void OnAttack(InputValue value)
    {
        bool pressed = value.isPressed;
        
        if (pressed && !_attackHeld)
        {
            _attackHeld = true;
            FireOnce(ignoreRateLimit: true);
            return;
        }
        
        if (!pressed)
            _attackHeld = false;
    }

    private void Update()
    {
        if (!_attackHeld) return;
        if (weaponData == null || shooter == null) return;
        
        if (!weaponData.automatic) return;

        FireOnce(ignoreRateLimit: false);
    }

    private void FireOnce(bool ignoreRateLimit)
    {
        if (weaponData == null || shooter == null) return;
        if (!TryGetAimPoint(out var aimPoint)) return;

        if (rotateToAim) RotateTowards(aimPoint);

        if (!ignoreRateLimit)
        {
            if (Time.time < _nextFireTime) return;
            ConsumeFireCooldown();
        }

        ShootTowards(aimPoint);
    }

    private void ConsumeFireCooldown()
    {
        float rate = Mathf.Max(0.01f, weaponData.fireRate);
        _nextFireTime = Time.time + (1f / rate);
    }

    private bool TryGetHorizontalDirection(Vector3 from, Vector3 to, out Vector3 direction)
    {
        direction = to - from;
        direction.y = 0f;
        return direction.sqrMagnitude >= 0.0001f;
    }

    private void ShootTowards(Vector3 aimPoint)
    {
        Vector3 origin = (firePoint != null) ? firePoint.position : shooter.transform.position;
        if (!TryGetHorizontalDirection(origin, aimPoint, out var dir)) return;

        shooter.Shoot(dir, out _);
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        if (aimCamera == null) return false;
        if (Mouse.current == null) return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
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

    private void RotateTowards(Vector3 worldPoint)
    {
        if (!TryGetHorizontalDirection(rotateRoot.position, worldPoint, out var dir)) return;

        rotateRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
