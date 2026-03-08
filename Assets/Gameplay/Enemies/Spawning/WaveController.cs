using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class WaveController : MonoBehaviour
{
    [Serializable]
    public class Wave
    {
        [Min(0)] public int enemiesToSpawn = 10;
        [Min(0f)] public float spawnInterval = 1.5f;

        [Tooltip("Лимит одновременно активных врагов в этой волне. 0 = использовать Global Max Alive.")]
        [Min(0)] public int maxAliveOverride = 0;

        [Tooltip("Задержка перед стартом волны (после паузы между волнами).")]
        [Min(0f)] public float startDelay = 0f;

        [Tooltip("Если true — волна завершится только когда все заспавнены И все убиты/деактивированы.")]
        public bool waitUntilCleared = true;
    }

    private enum State
    {
        Idle,
        PreparingWave,
        Spawning,
        WaitingClear,
        PausedByFabricator,
        HaltedByFabricator,
        BetweenWaves,
        Completed
    }

    [Header("Fabricator")]
    [SerializeField] private Fabricator fabricator;
    [SerializeField] private bool autoFindFabricator = true;

    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Waves")]
    [SerializeField] private List<Wave> waves = new();

    [Tooltip("Пауза между волнами (после окончания предыдущей).")]
    [SerializeField] private float timeBetweenWaves = 3f;

    [Tooltip("Запустить автоматически в Start().")]
    [SerializeField] private bool startOnStart = true;

    [Tooltip("После последней волны начать сначала.")]
    [SerializeField] private bool loop = false;

    [Header("Alive Limit")]
    [Tooltip("Глобальный лимит одновременно живых врагов. Используется если в волне maxAliveOverride = 0.")]
    [SerializeField] private int globalMaxAlive = 6;

    [Header("Pool")]
    [Tooltip("Сколько врагов создать заранее. Если 0, будет = max(globalMaxAlive, maxAliveOverride по волнам).")]
    [SerializeField] private int prewarmCount = 0;

    [Tooltip("Если пул закончился, можно ли автоматически расширять его (Instantiate).")]
    [SerializeField] private bool expandPoolIfEmpty = true;

    [Header("NavMesh")]
    [SerializeField] private float navMeshSampleRadius = 2.0f;

    private readonly List<GameObject> pool = new();
    private readonly List<GameObject> alive = new();

    private State state = State.Idle;

    private int currentWaveIndex = -1;
    private int spawnedThisWave = 0;

    private float stateEndTime = 0f;
    private float nextSpawnTime = 0f;
    private float pauseStartedAt = -1f;
    private State stateBeforePause = State.Idle;

    private void OnEnable()
    {
        TryResolveFabricator();
        SubscribeFabricator();
    }

    private void OnDisable()
    {
        UnsubscribeFabricator();
    }

    private void Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        Prewarm(CalcPrewarmCount());

        if (startOnStart)
            StartWaves();
    }

    private void TryResolveFabricator()
    {
        if (fabricator != null || !autoFindFabricator)
            return;

        fabricator = FindObjectOfType<Fabricator>();
    }

    private void SubscribeFabricator()
    {
        if (fabricator == null)
            return;

        fabricator.StateChanged -= OnFabricatorStateChanged;
        fabricator.StateChanged += OnFabricatorStateChanged;
    }

    private void UnsubscribeFabricator()
    {
        if (fabricator == null)
            return;

        fabricator.StateChanged -= OnFabricatorStateChanged;
    }

    private void OnFabricatorStateChanged(FabricatorState _)
    {
        SyncWithFabricatorState();
    }

    private bool ValidateSetup()
    {
        TryResolveFabricator();

        if (fabricator == null)
        {
            Debug.LogError(
                $"{nameof(WaveController)}: Fabricator is not assigned. Assign it in inspector or enable auto-find.",
                this);
            return false;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError($"{nameof(WaveController)}: Enemy Prefab is not assigned.", this);
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Spawn Points are not assigned.", this);
            return false;
        }

        if (waves == null || waves.Count == 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Waves list is empty.", this);
            return false;
        }

        if (globalMaxAlive <= 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Global Max Alive must be > 0.", this);
            return false;
        }

        return true;
    }

    private int CalcPrewarmCount()
    {
        if (prewarmCount > 0) return prewarmCount;

        int max = globalMaxAlive;
        for (int i = 0; i < waves.Count; i++)
        {
            int w = waves[i].maxAliveOverride;
            if (w > max) max = w;
        }
        return max;
    }

    public void StartWaves()
    {
        if (state != State.Idle && state != State.Completed)
            return;

        if (fabricator != null && fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        currentWaveIndex = -1;
        state = State.BetweenWaves;
        stateEndTime = Time.time; // start immediately
        spawnedThisWave = 0;

        SyncWithFabricatorState();
    }

    public void StopWaves()
    {
        state = State.Idle;
    }

    public void RestartWaves()
    {
        StopWaves();
        StartWaves();
    }

    private void Update()
    {
        CleanupAliveList();
        SyncWithFabricatorState();

        switch (state)
        {
            case State.Idle:
            case State.Completed:
                return;

            case State.PausedByFabricator:
                return;

            case State.HaltedByFabricator:
                return;

            case State.BetweenWaves:
                if (Time.time < stateEndTime) return;
                BeginNextWaveOrComplete();
                return;

            case State.PreparingWave:
                if (Time.time < stateEndTime) return;
                state = State.Spawning;
                nextSpawnTime = Time.time; // allow immediate spawn
                return;

            case State.Spawning:
                TickSpawning();
                return;

            case State.WaitingClear:
                TickWaitingClear();
                return;
        }
    }

    private void BeginNextWaveOrComplete()
    {
        if (fabricator == null || fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        int nextIndex = currentWaveIndex + 1;

        if (nextIndex >= waves.Count)
        {
            if (loop)
            {
                currentWaveIndex = -1;
                state = State.BetweenWaves;
                stateEndTime = Time.time; // immediately loop
                return;
            }

            state = State.Completed;
            return;
        }

        currentWaveIndex = nextIndex;
        spawnedThisWave = 0;

        var w = waves[currentWaveIndex];
        state = State.PreparingWave;
        stateEndTime = Time.time + Mathf.Max(0f, w.startDelay);
    }

    private void TickSpawning()
    {
        if (fabricator == null || fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        var w = waves[currentWaveIndex];

        // If we already spawned all enemies for this wave:
        if (spawnedThisWave >= w.enemiesToSpawn)
        {
            if (w.waitUntilCleared)
            {
                state = State.WaitingClear;
            }
            else
            {
                GoToBetweenWaves();
            }
            return;
        }

        int maxAlive = w.maxAliveOverride > 0 ? w.maxAliveOverride : globalMaxAlive;

        // Respect alive cap
        if (alive.Count >= maxAlive) return;

        // Respect spawn interval
        if (Time.time < nextSpawnTime) return;

        nextSpawnTime = Time.time + Mathf.Max(0f, w.spawnInterval);

        var enemy = GetFromPoolOrExpand();
        if (enemy == null)
        {
            // Pool exhausted and expansion disabled — just wait
            return;
        }

        SpawnFromPool(enemy);
        spawnedThisWave++;
    }

    private void TickWaitingClear()
    {
        if (alive.Count > 0) return;

        GoToBetweenWaves();
    }

    private void GoToBetweenWaves()
    {
        if (fabricator == null || fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        state = State.BetweenWaves;
        stateEndTime = Time.time + Mathf.Max(0f, timeBetweenWaves);
    }

    private void SyncWithFabricatorState()
    {
        if (fabricator == null)
            return;

        if (fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        bool canBePaused =
            state == State.PreparingWave ||
            state == State.Spawning ||
            state == State.WaitingClear ||
            state == State.BetweenWaves;

        if (!fabricator.CanSpawn)
        {
            if (canBePaused && state != State.PausedByFabricator)
                PauseByFabricator();
            return;
        }

        if (state == State.PausedByFabricator)
            ResumeAfterFabricatorPause();
    }

    private void PauseByFabricator()
    {
        if (state == State.PausedByFabricator || state == State.HaltedByFabricator)
            return;

        stateBeforePause = state;
        pauseStartedAt = Time.time;
        state = State.PausedByFabricator;

        Debug.Log(
            $"{nameof(WaveController)}: spawning paused by Fabricator. Reason: {fabricator.StopReason}",
            this);
    }

    private void ResumeAfterFabricatorPause()
    {
        if (state != State.PausedByFabricator)
            return;

        float pausedDuration = pauseStartedAt >= 0f ? Time.time - pauseStartedAt : 0f;

        stateEndTime += pausedDuration;
        nextSpawnTime += pausedDuration;

        state = stateBeforePause;
        pauseStartedAt = -1f;

        Debug.Log($"{nameof(WaveController)}: spawning resumed (Fabricator active).", this);
    }

    private void EnterPermanentHalt()
    {
        if (state == State.HaltedByFabricator || state == State.Completed)
            return;

        state = State.HaltedByFabricator;

        Debug.Log(
            $"{nameof(WaveController)}: spawning permanently halted. Reason: {fabricator?.StopReason}",
            this);
    }

    // ---------------- Pool / Spawn (mostly your original logic) ----------------

    private void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var enemy = Instantiate(enemyPrefab, transform);
            enemy.SetActive(false);
            pool.Add(enemy);
        }
    }

    private GameObject GetFromPoolOrExpand()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e != null && !e.activeInHierarchy)
                return e;
        }

        if (!expandPoolIfEmpty) return null;

        var enemy = Instantiate(enemyPrefab, transform);
        enemy.SetActive(false);
        pool.Add(enemy);
        return enemy;
    }

    private void SpawnFromPool(GameObject enemy)
    {
        var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        Vector3 pos = sp.position;

        if (NavMesh.SamplePosition(pos, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            pos = hit.position;

        enemy.transform.SetPositionAndRotation(pos, sp.rotation);
        enemy.SetActive(true);
        ResetEnemy(enemy);
        alive.Add(enemy);
    }

    private void CleanupAliveList()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            var go = alive[i];
            if (go == null || !go.activeInHierarchy)
                alive.RemoveAt(i);
        }
    }

    private void ResetEnemy(GameObject enemy)
    {
        var health = enemy.GetComponentInChildren<HealthComponent>(true);
        if (health != null && health.IsDead)
            health.ResetHealth();

        var agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

}
