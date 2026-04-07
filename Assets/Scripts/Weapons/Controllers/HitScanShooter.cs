using UnityEngine;
using Zenject;

public class HitScanShooter : MonoBehaviour
{
    private const float MinShootDirectionSqrMagnitude = 0.0001f;
    private const float MinRange = 0.1f;

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

        if (!TryGetShotDirection(direction, out Vector3 origin, out Vector3 shotDirection))
            return false;

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

    public bool TryGetPreviewShot(out Vector3 origin, Vector3 direction, out Vector3 shotDirection, out RaycastHit hit)
    {
        hit = default;
        if (!TryGetShotDirection(direction, out origin, out shotDirection))
            return false;

        Physics.Raycast(
            origin,
            shotDirection,
            out hit,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore);

        return true;
    }

    private bool TryGetShotDirection(Vector3 direction, out Vector3 origin, out Vector3 shotDirection)
    {
        origin = firePoint != null ? firePoint.position : transform.position;
        shotDirection = default;

        if (direction.sqrMagnitude < MinShootDirectionSqrMagnitude)
            return false;

        direction.Normalize();
        shotDirection = autoAim != null
            ? autoAim.GetShotDirection(origin, direction, range, hitMask)
            : direction;

        return shotDirection.sqrMagnitude >= MinShootDirectionSqrMagnitude;
    }

    public void SetFirePoint(Transform newFirePoint)
    {
        firePoint = newFirePoint != null ? newFirePoint : transform;
    }

    public void SetDamage(float newDamage) => damage = Mathf.Max(0f, newDamage);
    public void SetRange(float newRange) => range = Mathf.Max(MinRange, newRange);
}
