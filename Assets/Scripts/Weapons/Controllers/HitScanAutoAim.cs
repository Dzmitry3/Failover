using UnityEngine;

[DisallowMultipleComponent]
public class HitscanAutoAim : MonoBehaviour
{
    private const string AimPointName = "AimPoint";
    private const float DirectionEpsilonSqr = 0.0001f;
    private const float MinForwardDistance = 0.01f;
    private const float MinAssistRadius = 0.01f;
    private const float TargetPointHeightFactor = 0f;

    [Header("Vertical Assist")]
    [SerializeField] private bool verticalAutoAim = false;
    [SerializeField] private float verticalAutoAimRadius = 1.25f;

    [Header("Horizontal Assist")]
    [SerializeField] private bool horizontalAutoAim = true;
    [SerializeField] private float horizontalAutoAimMaxAngle = 6f;

    public Vector3 GetShotDirection(
        Vector3 origin,
        Vector3 inputDirection,
        float range,
        LayerMask hitMask)
    {
        Vector3 normalizedInput = inputDirection.normalized;

        if (!verticalAutoAim && !horizontalAutoAim)
            return normalizedInput;

        Vector3 flatDir = new Vector3(normalizedInput.x, 0f, normalizedInput.z);
        if (flatDir.sqrMagnitude < DirectionEpsilonSqr)
            return normalizedInput;

        flatDir.Normalize();

        if (!TryGetBestTargetHit(origin, flatDir, range, hitMask, verticalAutoAimRadius, out RaycastHit bestHit))
            return normalizedInput;

        HealthComponent bestHealth = bestHit.collider != null
            ? bestHit.collider.GetComponentInParent<HealthComponent>()
            : null;
        bool hasAimPoint = TryGetAimPoint(bestHealth, out Vector3 aimPoint);
        Vector3 targetPoint = hasAimPoint ? aimPoint : GetFallbackTargetPoint(bestHit);
        Vector3 toTarget = targetPoint - origin;
        Vector3 targetFlat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (targetFlat.sqrMagnitude < DirectionEpsilonSqr)
            return normalizedInput;

        if (horizontalAutoAim)
        {
            float maxAngle = Mathf.Max(0f, horizontalAutoAimMaxAngle);
            float flatAngle = Vector3.Angle(flatDir, targetFlat.normalized);
            if (flatAngle > maxAngle)
                return normalizedInput;
        }

        if (!verticalAutoAim && !hasAimPoint)
            toTarget.y = 0f;

        if (toTarget.sqrMagnitude < DirectionEpsilonSqr)
            return normalizedInput;

        return toTarget.normalized;
    }

    private bool TryGetBestTargetHit(
        Vector3 origin,
        Vector3 flatDirection,
        float range,
        LayerMask hitMask,
        float radius,
        out RaycastHit bestHit)
    {
        RaycastHit[] assistHits = Physics.SphereCastAll(
            origin,
            Mathf.Max(MinAssistRadius, radius),
            flatDirection,
            range,
            hitMask,
            QueryTriggerInteraction.Ignore);

        bestHit = default;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < assistHits.Length; i++)
        {
            RaycastHit candidate = assistHits[i];
            HealthComponent health = candidate.collider.GetComponentInParent<HealthComponent>();
            if (health == null || health.IsDead)
                continue;

            Vector3 targetPoint = TryGetAimPoint(health, out Vector3 aimPoint)
                ? aimPoint
                : GetFallbackTargetPoint(candidate);
            if (!HasLineOfSight(origin, targetPoint, health, range, hitMask))
                continue;

            if (candidate.distance >= bestDistance)
                continue;

            bestDistance = candidate.distance;
            bestHit = candidate;
            found = true;
        }

        return found;
    }

    private static bool TryGetAimPoint(HealthComponent health, out Vector3 aimPoint)
    {
        Transform aimPointTransform = FindAimPoint(health);
        if (aimPointTransform != null)
        {
            aimPoint = aimPointTransform.position;
            return true;
        }

        aimPoint = default;
        return false;
    }

    private static Vector3 GetFallbackTargetPoint(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null)
            return hit.point;

        Bounds bounds = collider.bounds;
        return bounds.center + Vector3.up * (bounds.extents.y * TargetPointHeightFactor);
    }

    private static Transform FindAimPoint(HealthComponent health)
    {
        if (health == null)
            return null;

        Transform[] transforms = health.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate.name == AimPointName)
                return candidate;
        }

        return null;
    }

    private static bool HasLineOfSight(
        Vector3 origin,
        Vector3 targetPoint,
        HealthComponent targetHealth,
        float maxRange,
        LayerMask hitMask)
    {
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= MinForwardDistance || distance > maxRange)
            return false;

        Vector3 direction = toTarget / distance;
        if (!Physics.Raycast(origin, direction, out RaycastHit blockingHit, distance, hitMask, QueryTriggerInteraction.Ignore))
            return false;

        HealthComponent hitHealth = blockingHit.collider.GetComponentInParent<HealthComponent>();
        return hitHealth == targetHealth;
    }
}
