using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;          // обычно Main Camera
    [SerializeField] private Transform rotateRoot;      // что поворачивать (Player root)
    [SerializeField] private HitScanShooter shooter;    // компонент HitScanShooter (можно на этом же объекте)
    [SerializeField] private Transform firePoint;       // опционально: если хочешь прокинуть в shooter
    [SerializeField] private WeaponData weaponData;

    [Header("Aiming")]
    [SerializeField] private LayerMask aimGroundMask;   // слой пола (Ground)
    [SerializeField] private float maxAimRayDistance = 200f;
    [SerializeField] private bool rotateToAim = true;

    [Header("Fire Rate")]
    [SerializeField] private float fireRate = 6f;       // выстрелов в секунду

    private float _nextFireTime;
    private bool _fireHeld;

    private void Reset()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (rotateRoot == null) rotateRoot = transform;
        if (shooter == null) shooter = GetComponent<HitScanShooter>();
    }

    private void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (rotateRoot == null) rotateRoot = transform;
        if (shooter == null) shooter = GetComponent<HitScanShooter>();

        if (shooter == null)
            Debug.LogError($"{nameof(WeaponController)}: HitScanShooter is not assigned.", this);

        // Если хочешь, чтобы shooter стрелял именно из firePoint
        if (firePoint != null && shooter != null)
            shooter.SetFirePoint(firePoint);
        
        if (weaponData == null)
        {
            Debug.LogError("WeaponData is not assigned", this);
            return;
        }

        if (shooter == null)
        {
            Debug.LogError("HitScanShooter is not assigned", this);
            return;
        }

        // прокидываем цифры в shooter
        shooter.SetDamage(weaponData.damage);
        shooter.SetRange(weaponData.range);

        // скорострельность берём из данных
        fireRate = weaponData.fireRate;
    }

    private void Update()
    {
        if (!_fireHeld) return;

        if (!TryGetAimPoint(out var aimPoint)) return;

        if (rotateToAim) RotateTowards(aimPoint);

        // Удержание: стреляем с ограничением по fireRate
        TryFireAt(aimPoint, ignoreRateLimit: false);
        if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
            _fireHeld = false;

    }

    // PlayerInput Behaviour = Send Messages
    public void OnAttack(InputValue value)
    {
        //Debug.Log("OnAttack: " + value.isPressed);
        bool pressed = value.isPressed;

        // Нажатие: один выстрел сразу
        if (pressed && !_fireHeld)
        {
            _fireHeld = true;
            TryFireOnce(ignoreRateLimit: true);   // мгновенный выстрел
            return;
        }

        // Отпускание: прекратить автоогонь
        if (!pressed)
        {
            _fireHeld = false;
        }
    }
    
    private void TryFireOnce(bool ignoreRateLimit)
    {
        if (!TryGetAimPoint(out var aimPoint)) return;

        if (rotateToAim) RotateTowards(aimPoint);

        TryFireAt(aimPoint, ignoreRateLimit: ignoreRateLimit);
    }


    public void FireOnce()
    {
        if (TryGetAimPoint(out var aimPoint))
        {
            if (rotateToAim) RotateTowards(aimPoint);
            TryFireAt(aimPoint, ignoreRateLimit: true);
        }
    }

    private void TryFireAt(Vector3 aimPoint, bool ignoreRateLimit = false)
    {
        if (shooter == null) return;

        if (!ignoreRateLimit)
        {
            if (Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));
        }

        Vector3 origin = (firePoint != null) ? firePoint.position : shooter.transform.position;
        Vector3 dir = aimPoint - origin;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        shooter.Shoot(dir, out _);
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;

        if (aimCamera == null) return false;
        if (Mouse.current == null) return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = aimCamera.ScreenPointToRay(screenPos);

        // 1) Попадание в коллайдер пола
        if (Physics.Raycast(ray, out var hit, maxAimRayDistance, aimGroundMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            return true;
        }

        // 2) Фолбэк: пересечение с плоскостью на высоте rotateRoot
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
        Vector3 dir = worldPoint - rotateRoot.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        rotateRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
