using UnityEngine;
using Zenject;

public class HitScanShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;

    [Header("Shot")]
    [SerializeField] private float range = 30f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private LayerMask hitMask = ~0; 

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField] private float debugDrawTime = 0.05f;
    
    private HealthProcessor _healthProcessor;
    [Inject]
    public void Construct(HealthProcessor healthProcessor)
    {
        _healthProcessor = healthProcessor;
        //Debug.Log("HealthProcessor injected: " + (_healthProcessor != null));

    }

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
            HealthComponent health = hit.collider.GetComponentInParent<HealthComponent>();
            if (health != null)
                _healthProcessor.DealDamage(health, damage);
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
