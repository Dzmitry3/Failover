using UnityEngine;
using UnityEngine.AI;
using Zenject;

[DisallowMultipleComponent]
public class EnemyDummyAnimation : MonoBehaviour
{
    private const float StopDistanceTolerance = 0.05f;
    private const float MinFacingSqrMagnitude = 0.0001f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    [SerializeField] private EnemyBase enemy;
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

    private bool _deathStarted;
    private bool _attackLocked;
    private bool _attackDamageApplied;
    private float _attackLockEndTime;
    private float _nextAttackTime;
    private float _attackImpactTime;
    private bool _missingProcessorWarned;
    private Transform _targetTransform;
    private HealthComponent _targetHealth;
    private HealthProcessor _healthProcessor;

    [Inject]
    public void Construct(HealthProcessor healthProcessor)
    {
        _healthProcessor = healthProcessor;
    }

    private void InitializeReferences()
    {
        if (enemy == null)
            enemy = GetComponent<EnemyBase>();

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
        ResolveTarget();
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

        _deathStarted = false;
        _attackLocked = false;
        _attackDamageApplied = false;
        _attackLockEndTime = 0f;
        _attackImpactTime = 0f;
        _nextAttackTime = Time.time;
        SetAgentStopped(false);
        SetDead(false);
        SetSpeed(0f);
        ResolveTarget();
    }

    private void OnDisable()
    {
        if (enemy?.Health != null)
            enemy.Health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (_deathStarted)
            return;

        if (_attackLocked)
        {
            TryApplyAttackDamage();

            if (Time.time < _attackLockEndTime)
            {
                SetSpeed(0f);
                return;
            }

            _attackLocked = false;
            SetAgentStopped(false);
        }

        bool isMoving = IsMoving();
        SetSpeed(isMoving ? 1f : 0f);

        if (!isMoving && CanAttack())
            TriggerAttack();
    }

    private void HandleDeath()
    {
        _deathStarted = true;
        _attackLocked = false;
        _attackDamageApplied = true;
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

        if (Time.time < _nextAttackTime)
            return false;

        if (!agent.hasPath || agent.pathPending)
            return false;

        return agent.remainingDistance <= agent.stoppingDistance + StopDistanceTolerance;
    }

    private void TriggerAttack()
    {
        _attackLocked = true;
        _attackDamageApplied = false;
        _attackLockEndTime = Time.time + attackLockDuration;
        _attackImpactTime = Time.time + Mathf.Min(attackImpactDelay, attackLockDuration);
        SetAgentStopped(true);
        SetSpeed(0f);
        animator.ResetTrigger(AttackHash);
        animator.SetTrigger(AttackHash);
        _nextAttackTime = Time.time + attackRepeatDelay;
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
        if (_attackDamageApplied || Time.time < _attackImpactTime || attackDamage <= 0f)
            return;

        _attackDamageApplied = true;

        if (!IsTargetInAttackArc())
            return;

        if (_targetHealth == null || _targetHealth.IsDead)
            return;

        if (_healthProcessor != null)
        {
            _healthProcessor.DealDamage(_targetHealth, attackDamage);
            return;
        }

        _targetHealth.ApplyDelta(-attackDamage);

        if (_missingProcessorWarned)
            return;

        _missingProcessorWarned = true;
        Debug.LogWarning(
            $"{nameof(EnemyDummyAnimation)}: {nameof(HealthProcessor)} was not injected, direct damage fallback is used.",
            this);
    }

    private bool IsTargetInAttackArc()
    {
        if (!ResolveTarget())
            return false;

        Vector3 toTarget = _targetTransform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > attackRange * attackRange)
            return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < MinFacingSqrMagnitude || toTarget.sqrMagnitude < MinFacingSqrMagnitude)
            return false;

        forward.Normalize();
        toTarget.Normalize();

        float minDot = Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad);
        return Vector3.Dot(forward, toTarget) >= minDot;
    }

    private bool ResolveTarget()
    {
        if (_targetHealth != null && !_targetHealth.IsDead)
        {
            _targetTransform = _targetHealth.transform;
            return true;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            _targetTransform = null;
            _targetHealth = null;
            return false;
        }

        _targetTransform = player.transform;
        _targetHealth = player.GetComponentInParent<HealthComponent>();

        if (_targetHealth == null)
            _targetHealth = player.GetComponentInChildren<HealthComponent>(true);

        return _targetHealth != null;
    }
}
