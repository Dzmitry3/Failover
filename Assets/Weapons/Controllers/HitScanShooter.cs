using UnityEngine;
using Zenject;

public class HitScanShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;

    [Header("Shot")]
    [SerializeField] private float range = 30f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private LayerMask hitMask; // Damageable | Environment
    [SerializeField] private HitScanAutoAim autoAim;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;
    [SerializeField] private float debugDrawTime = 0.05f;
    [SerializeField] private bool debugDamageLogs = false;
    
    private HealthProcessor _healthProcessor;
    private bool _missingProcessorWarned;
    [Inject]
    public void Construct(HealthProcessor healthProcessor)
    {
        _healthProcessor = healthProcessor;
        //Debug.Log("HealthProcessor injected: " + (_healthProcessor != null));

    }

    private void InitializeReferences()
    {
        if (firePoint == null) firePoint = transform;
        if (autoAim == null) autoAim = GetComponent<HitScanAutoAim>();
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
        Vector3 shotDirection = autoAim != null
            ? autoAim.GetShotDirection(origin, direction, range, hitMask)
            : direction;

        bool hasHit = Physics.Raycast(
            origin,
            shotDirection,
            out hit,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hasHit)
        {
            HealthComponent health = hit.collider.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                if (_healthProcessor != null)
                {
                    _healthProcessor.DealDamage(health, damage);
                }
                else
                {
                    // Safe fallback: still apply damage even if DI setup is broken.
                    health.ApplyDelta(-damage);

                    if (!_missingProcessorWarned)
                    {
                        _missingProcessorWarned = true;
                        Debug.LogWarning(
                            $"{nameof(HitScanShooter)}: {nameof(HealthProcessor)} was not injected, direct damage fallback is used.",
                            this);
                    }
                }

                if (debugDamageLogs)
                {
                    Debug.Log(
                        $"{nameof(HitScanShooter)} hit {hit.collider.name}, damage={damage}, hp={health.Current}/{health.Max}",
                        this);
                }
            }
            else if (debugDamageLogs)
            {
                Debug.Log(
                    $"{nameof(HitScanShooter)} hit {hit.collider.name}, but no {nameof(HealthComponent)} found on target root.",
                    this);
            }
        }

        if (debugDraw)
        {
            Vector3 end = hasHit ? hit.point : origin + shotDirection * range;
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
