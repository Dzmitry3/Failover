using UnityEngine;

public class HitScanShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;

    [Header("Shot")]
    [SerializeField] private float range = 30f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private LayerMask hitMask = ~0; // по умолчанию всё

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField] private float debugDrawTime = 0.05f;

    private void InitializeReferences()
    {
        if (firePoint == null) firePoint = transform;
    }

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();
    }

    /// <summary>
    /// Стреляет по направлению (обычно нормализованному). Возвращает true, если попали во что-то.
    /// </summary>
    public bool Shoot(Vector3 direction, out RaycastHit hit)
    {
        hit = default;

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();

        Vector3 origin = firePoint.position;

        bool hasHit = Physics.Raycast(
            origin,
            direction,
            out hit,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hasHit)
        {
            // Collider может быть на дочернем объекте, поэтому ищем здоровье в родителях
            HealthComponent health = hit.collider.GetComponentInParent<HealthComponent>();
            if (health != null)
                health.TakeDamage(damage);
        }

        if (debugDraw)
        {
            Vector3 end = hasHit ? hit.point : origin + direction * range;
            Debug.DrawLine(origin, end, hasHit ? Color.red : Color.yellow, debugDrawTime);
        }

        return hasHit;
    }

    public void SetFirePoint(Transform newFirePoint)
    {
        firePoint = newFirePoint != null ? newFirePoint : transform;
    }

    public void SetDamage(float newDamage) => damage = Mathf.Max(0f, newDamage);
    public void SetRange(float newRange) => range = Mathf.Max(0.1f, newRange);
}
