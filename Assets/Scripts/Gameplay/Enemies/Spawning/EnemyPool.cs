using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyPool : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Pool")]
    [Tooltip("If the pool is exhausted, create a new enemy instance.")]
    [SerializeField] private bool expandPoolIfEmpty = true;

    private readonly List<GameObject> pool = new();

    public bool IsConfigured => enemyPrefab != null;

    public int ActiveCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                GameObject enemy = pool[i];
                if (enemy != null && enemy.activeInHierarchy)
                    count++;
            }

            return count;
        }
    }

    public bool HasAnyActive => ActiveCount > 0;

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

        enemy.transform.SetPositionAndRotation(position, rotation);
        enemy.SetActive(true);
        ResetEnemy(enemy);
    }

    private void PrewarmTo(int targetCount)
    {
        while (pool.Count < targetCount)
            CreatePooledEnemy();
    }

    private GameObject CreatePooledEnemy()
    {
        GameObject enemy = Instantiate(enemyPrefab, transform);
        enemy.SetActive(false);
        pool.Add(enemy);
        return enemy;
    }

    private void ResetEnemy(GameObject enemy)
    {
        HealthComponent health = enemy.GetComponentInChildren<HealthComponent>(true);
        if (health != null && health.IsDead)
            health.ResetHealth();

        NavMeshAgent agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }
}
