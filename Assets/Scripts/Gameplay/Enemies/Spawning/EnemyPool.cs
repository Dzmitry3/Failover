using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

[DisallowMultipleComponent]
public class EnemyPool : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Pool")]
    [Tooltip("If the pool is exhausted, create a new enemy instance.")]
    [SerializeField] private bool expandPoolIfEmpty = true;

    private readonly Queue<GameObject> pool = new();
    private readonly HashSet<GameObject> pooledEnemies = new();
    private readonly HashSet<GameObject> queuedEnemies = new();
    private readonly HashSet<GameObject> activeEnemies = new();
    private readonly HashSet<GameObject> aliveEnemies = new();
    private DiContainer container;

    public bool IsConfigured => enemyPrefab != null;
    public int AliveCount => aliveEnemies.Count;
    public bool HasAnyAlive => aliveEnemies.Count > 0;

    [Inject]
    public void Construct([InjectOptional] DiContainer diContainer)
    {
        container = diContainer;
    }

    public void Initialize(int requestedPrewarmCount)
    {
        if (!IsConfigured)
        {
            Debug.LogError($"{nameof(EnemyPool)}: Enemy Prefab is not assigned.", this);
            return;
        }

        PrewarmTo(Mathf.Max(0, requestedPrewarmCount));
    }

    public GameObject GetOrCreate()
    {
        GameObject enemy = TryDequeueAvailableEnemy();
        if (enemy != null)
            return enemy;

        if (!expandPoolIfEmpty)
            return null;

        CreatePooledEnemy();
        return TryDequeueAvailableEnemy();
    }

    public void Activate(GameObject enemy, Vector3 position, Quaternion rotation)
    {
        if (enemy == null)
            return;

        if (activeEnemies.Add(enemy) == false)
        {
            Debug.LogWarning($"{nameof(EnemyPool)}: tried to activate an enemy that is already tracked as active.", enemy);
            return;
        }

        enemy.transform.SetPositionAndRotation(position, rotation);
        ResetEnemy(enemy);
        enemy.SetActive(true);
    }

    public void NotifyReturned(GameObject enemy)
    {
        if (enemy == null)
            return;

        activeEnemies.Remove(enemy);
        aliveEnemies.Remove(enemy);
        EnqueueAvailableEnemy(enemy);
    }

    public void NotifyAliveStateChanged(GameObject enemy, bool alive)
    {
        if (enemy == null)
            return;

        if (alive)
            aliveEnemies.Add(enemy);
        else
            aliveEnemies.Remove(enemy);
    }

    private void PrewarmTo(int targetCount)
    {
        while (pooledEnemies.Count < targetCount)
            CreatePooledEnemy();
    }

    private GameObject CreatePooledEnemy()
    {
        GameObject enemy = container != null
            ? container.InstantiatePrefab(enemyPrefab, transform)
            : Instantiate(enemyPrefab, transform);
        pooledEnemies.Add(enemy);
        RegisterTracker(enemy);
        enemy.SetActive(false);
        EnqueueAvailableEnemy(enemy);
        return enemy;
    }

    private void NotifyDestroyed(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
        aliveEnemies.Remove(enemy);
        queuedEnemies.Remove(enemy);
        pooledEnemies.Remove(enemy);
    }

    private void RegisterTracker(GameObject enemy)
    {
        EnemyPoolTracker tracker = enemy.GetComponent<EnemyPoolTracker>();
        if (tracker == null)
            tracker = enemy.AddComponent<EnemyPoolTracker>();

        tracker.Bind(this, enemy);
    }

    private void ResetEnemy(GameObject enemy)
    {
        EnemyLifecycle enemyLifecycle = enemy.GetComponent<EnemyLifecycle>();
        if (enemyLifecycle != null)
        {
            enemyLifecycle.PrepareForSpawn();
        }
        else
        {
            HealthComponent health = enemy.GetComponentInChildren<HealthComponent>(true);
            if (health != null && health.IsDead)
                health.ResetHealth();
        }

        NavMeshAgent agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    private GameObject TryDequeueAvailableEnemy()
    {
        while (pool.Count > 0)
        {
            GameObject enemy = pool.Dequeue();
            queuedEnemies.Remove(enemy);

            if (enemy != null && !enemy.activeInHierarchy)
                return enemy;
        }

        return null;
    }

    private void EnqueueAvailableEnemy(GameObject enemy)
    {
        if (enemy == null || pooledEnemies.Contains(enemy) == false || queuedEnemies.Add(enemy) == false)
            return;

        pool.Enqueue(enemy);
    }

    private sealed class EnemyPoolTracker : MonoBehaviour
    {
        private EnemyPool owner;
        private GameObject trackedEnemy;
        private EnemyLifecycle trackedEnemyLifecycle;

        public void Bind(EnemyPool newOwner, GameObject enemy)
        {
            if (trackedEnemyLifecycle != null)
                trackedEnemyLifecycle.AliveStateChanged -= HandleAliveStateChanged;

            owner = newOwner;
            trackedEnemy = enemy;
            trackedEnemyLifecycle = enemy != null ? enemy.GetComponent<EnemyLifecycle>() : null;

            if (trackedEnemyLifecycle != null)
                trackedEnemyLifecycle.AliveStateChanged += HandleAliveStateChanged;
        }

        private void OnDisable()
        {
            owner?.NotifyReturned(trackedEnemy);
        }

        private void OnDestroy()
        {
            if (trackedEnemyLifecycle != null)
                trackedEnemyLifecycle.AliveStateChanged -= HandleAliveStateChanged;

            owner?.NotifyDestroyed(trackedEnemy);
        }

        private void HandleAliveStateChanged(EnemyLifecycle _, bool alive)
        {
            owner?.NotifyAliveStateChanged(trackedEnemy, alive);
        }
    }
}
