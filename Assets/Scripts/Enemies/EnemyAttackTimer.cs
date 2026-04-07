using UnityEngine;

public sealed class EnemyAttackTimer
{
    private readonly float attackLockDuration;
    private readonly float attackRepeatDelay;
    private readonly float attackImpactDelay;
    private bool damageApplied;
    private float lockEndTime;
    private float nextAttackTime;
    private float impactTime;

    public EnemyAttackTimer(float attackLockDuration, float attackRepeatDelay, float attackImpactDelay)
    {
        this.attackLockDuration = Mathf.Max(0.1f, attackLockDuration);
        this.attackRepeatDelay = Mathf.Max(0.1f, attackRepeatDelay);
        this.attackImpactDelay = Mathf.Max(0f, attackImpactDelay);
    }

    public bool IsLocked { get; private set; }

    public void Reset(float time)
    {
        IsLocked = false;
        damageApplied = false;
        lockEndTime = 0f;
        impactTime = 0f;
        nextAttackTime = time;
    }

    public bool CanAttack(float time)
    {
        return !IsLocked && time >= nextAttackTime;
    }

    public void BeginAttack(float time)
    {
        IsLocked = true;
        damageApplied = false;
        lockEndTime = time + attackLockDuration;
        impactTime = time + Mathf.Min(attackImpactDelay, attackLockDuration);
        nextAttackTime = time + attackRepeatDelay;
    }

    public bool TryRelease(float time)
    {
        if (!IsLocked || time < lockEndTime)
            return false;

        IsLocked = false;
        return true;
    }

    public bool ShouldApplyDamage(float time, float damage)
    {
        return IsLocked && !damageApplied && time >= impactTime && damage > 0f;
    }

    public void MarkDamageApplied()
    {
        damageApplied = true;
    }

    public void Abort()
    {
        IsLocked = false;
        damageApplied = true;
        lockEndTime = 0f;
        impactTime = 0f;
    }
}
