using UnityEngine;

[DisallowMultipleComponent]
public class HitScanAutoAim : MonoBehaviour
{
    [Header("Vertical Assist")]
    [SerializeField] private bool verticalAutoAim = true;
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
        if (flatDir.sqrMagnitude < 0.0001f)
            return normalizedInput;

        flatDir.Normalize();

        if (!TryGetBestTargetHit(origin, flatDir, range, hitMask, verticalAutoAimRadius, out RaycastHit bestHit))
            return normalizedInput;

        Vector3 toTarget = bestHit.collider.bounds.center - origin;
        Vector3 targetFlat = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 aimedFlatDir = flatDir;

        if (horizontalAutoAim && targetFlat.sqrMagnitude > 0.0001f)
        {
            float maxRadians = Mathf.Deg2Rad * Mathf.Max(0f, horizontalAutoAimMaxAngle);
            aimedFlatDir = Vector3.RotateTowards(flatDir, targetFlat.normalized, maxRadians, 0f).normalized;
        }

        float forwardDistance = Vector3.Dot(toTarget, aimedFlatDir);
        if (forwardDistance <= 0.01f)
            return normalizedInput;

        Vector3 adjusted = aimedFlatDir * forwardDistance;
        if (verticalAutoAim)
            adjusted += Vector3.up * toTarget.y;
        else
            adjusted += Vector3.up * (normalizedInput.y * forwardDistance);

        if (adjusted.sqrMagnitude < 0.0001f)
            return normalizedInput;

        return adjusted.normalized;
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
            Mathf.Max(0.01f, radius),
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

            if (candidate.distance >= bestDistance)
                continue;

            bestDistance = candidate.distance;
            bestHit = candidate;
            found = true;
        }

        return found;
    }
}
