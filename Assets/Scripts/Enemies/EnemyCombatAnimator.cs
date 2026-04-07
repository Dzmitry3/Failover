using UnityEngine;
using UnityEngine.AI;
using Zenject;

[DisallowMultipleComponent]
public class EnemyCombatAnimator : MonoBehaviour
{
    private const float StopDistanceTolerance = 0.05f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private EnemyLifecycle enemy;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.01f)] private float moveSpeedThreshold = 0.1f;
    [SerializeField, Min(0.1f)] private float attackLockDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float attackRepeatDelay = 1.2f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0f)] private float attackImpactDelay = 0.35f;
    [SerializeField, Min(0.1f)] private float attackRange = 2.2f;
    [SerializeField, Range(0f, 180f)] private float attackAngle = 90f;
    [SerializeField] private string playerTag = "Player";

    private bool deathStarted;
    private bool missingProcessorWarned;
    private DamageApplier damageApplier;
    private EnemyAttackTimer attackTimer;
    private EnemyTargetTracker targetTracker;

    [Inject]
    public void Construct(DamageApplier damageApplier)
    {
        this.damageApplier = damageApplier;
    }

    private void InitializeReferences()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyLifecycle>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();
        RebuildRuntimeState();
    }

    private void OnEnable()
    {
        if (enemy?.Health != null)
            enemy.Health.OnDeath += HandleDeath;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.ResetTrigger(AttackHash);
        }

        RebuildRuntimeState();
        attackTimer.Reset(Time.time);
        deathStarted = false;
        SetAgentStopped(false);
        SetDead(false);
        SetSpeed(0f);
    }

    private void OnDisable()
    {
        if (enemy?.Health != null)
            enemy.Health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (deathStarted)
            return;

        if (attackTimer.IsLocked)
        {
            TryApplyAttackDamage();

            if (!attackTimer.TryRelease(Time.time))
            {
                SetSpeed(0f);
                return;
            }

            SetAgentStopped(false);
        }

        bool isMoving = IsMoving();
        SetSpeed(isMoving ? 1f : 0f);

        if (!isMoving && CanAttack())
            TriggerAttack();
    }

    private void HandleDeath()
    {
        deathStarted = true;
        attackTimer.Abort();
        SetAgentStopped(true);
        SetSpeed(0f);
        SetDead(true);
    }

    private bool IsMoving()
    {
        if (agent == null || !agent.enabled)
            return false;

        float thresholdSqr = moveSpeedThreshold * moveSpeedThreshold;

        if (agent.velocity.sqrMagnitude >= thresholdSqr)
            return true;

        if (!agent.hasPath || agent.pathPending)
            return false;

        if (agent.remainingDistance <= agent.stoppingDistance + StopDistanceTolerance)
            return false;

        return agent.desiredVelocity.sqrMagnitude >= thresholdSqr;
    }

    private bool CanAttack()
    {
        if (animator == null || agent == null || !agent.enabled)
            return false;

        if (!attackTimer.CanAttack(Time.time))
            return false;

        if (!agent.hasPath || agent.pathPending)
            return false;

        return agent.remainingDistance <= agent.stoppingDistance + StopDistanceTolerance;
    }

    private void TriggerAttack()
    {
        attackTimer.BeginAttack(Time.time);
        SetAgentStopped(true);
        SetSpeed(0f);
        animator.ResetTrigger(AttackHash);
        animator.SetTrigger(AttackHash);
    }

    private void SetAgentStopped(bool isStopped)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = isStopped;

        if (isStopped)
            agent.ResetPath();
    }

    private void SetSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, speed);
    }

    private void SetDead(bool isDead)
    {
        if (animator != null)
            animator.SetBool(IsDeadHash, isDead);
    }

    private void TryApplyAttackDamage()
    {
        if (!attackTimer.ShouldApplyDamage(Time.time, attackDamage))
            return;

        attackTimer.MarkDamageApplied();

        if (!targetTracker.IsTargetInAttackArc(transform))
            return;

        if (!targetTracker.TryGetLiveTarget(out HealthComponent targetHealth) || targetHealth == null || targetHealth.IsDead)
            return;

        if (damageApplier != null)
        {
            damageApplier.DealDamage(targetHealth, attackDamage);
            return;
        }

        targetHealth.ApplyDelta(-attackDamage);

        if (missingProcessorWarned)
            return;

        missingProcessorWarned = true;
        Debug.LogWarning(
            $"{nameof(EnemyCombatAnimator)}: {nameof(DamageApplier)} was not injected, direct damage fallback is used.",
            this);
    }

    private void RebuildRuntimeState()
    {
        attackTimer = new EnemyAttackTimer(attackLockDuration, attackRepeatDelay, attackImpactDelay);
        targetTracker = new EnemyTargetTracker(playerTag, attackRange, attackAngle);
    }
}
