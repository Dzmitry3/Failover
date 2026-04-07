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

    private readonly List<GameObject> pool = new();
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
        for (int i = 0; i < pool.Count; i++)
        {
            GameObject enemy = pool[i];
            if (enemy != null && !enemy.activeInHierarchy)
                return enemy;
        }

        if (!expandPoolIfEmpty)
            return null;

        return CreatePooledEnemy();
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
        while (pool.Count < targetCount)
            CreatePooledEnemy();
    }

    private GameObject CreatePooledEnemy()
    {
        GameObject enemy = container != null
            ? container.InstantiatePrefab(enemyPrefab, transform)
            : Instantiate(enemyPrefab, transform);
        RegisterTracker(enemy);
        enemy.SetActive(false);
        pool.Add(enemy);
        return enemy;
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
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.PrepareForSpawn();
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

    private sealed class EnemyPoolTracker : MonoBehaviour
    {
        private EnemyPool owner;
        private GameObject trackedEnemy;
        private EnemyBase trackedEnemyBase;

        public void Bind(EnemyPool newOwner, GameObject enemy)
        {
            if (trackedEnemyBase != null)
                trackedEnemyBase.AliveStateChanged -= HandleAliveStateChanged;

            owner = newOwner;
            trackedEnemy = enemy;
            trackedEnemyBase = enemy != null ? enemy.GetComponent<EnemyBase>() : null;

            if (trackedEnemyBase != null)
                trackedEnemyBase.AliveStateChanged += HandleAliveStateChanged;
        }

        private void OnDisable()
        {
            owner?.NotifyReturned(trackedEnemy);
        }

        private void OnDestroy()
        {
            if (trackedEnemyBase != null)
                trackedEnemyBase.AliveStateChanged -= HandleAliveStateChanged;
        }

        private void HandleAliveStateChanged(EnemyBase _, bool alive)
        {
            owner?.NotifyAliveStateChanged(trackedEnemy, alive);
        }
    }
}
