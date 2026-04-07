using UnityEngine;

public sealed class EnemyTargetTracker
{
    private const float MinFacingSqrMagnitude = 0.0001f;

    private readonly string playerTag;
    private readonly float attackRange;
    private readonly float attackAngle;
    private Transform targetTransform;
    private HealthComponent targetHealth;

    public EnemyTargetTracker(string playerTag, float attackRange, float attackAngle)
    {
        this.playerTag = playerTag;
        this.attackRange = Mathf.Max(0.1f, attackRange);
        this.attackAngle = Mathf.Clamp(attackAngle, 0f, 180f);
    }

    public bool TryGetLiveTarget(out HealthComponent health)
    {
        if (targetHealth != null && !targetHealth.IsDead)
        {
            targetTransform = targetHealth.transform;
            health = targetHealth;
            return true;
        }

        return ResolveTarget(out health);
    }

    public bool IsTargetInAttackArc(Transform attackerTransform)
    {
        if (attackerTransform == null || !TryGetLiveTarget(out _))
            return false;

        Vector3 toTarget = targetTransform.position - attackerTransform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > attackRange * attackRange)
            return false;

        Vector3 forward = attackerTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < MinFacingSqrMagnitude || toTarget.sqrMagnitude < MinFacingSqrMagnitude)
            return false;

        forward.Normalize();
        toTarget.Normalize();

        float minDot = Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad);
        return Vector3.Dot(forward, toTarget) >= minDot;
    }

    private bool ResolveTarget(out HealthComponent health)
    {
        health = null;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            targetTransform = null;
            targetHealth = null;
            return false;
        }

        targetTransform = player.transform;
        targetHealth = player.GetComponentInParent<HealthComponent>();

        if (targetHealth == null)
            targetHealth = player.GetComponentInChildren<HealthComponent>(true);

        health = targetHealth;
        return health != null;
    }
}
