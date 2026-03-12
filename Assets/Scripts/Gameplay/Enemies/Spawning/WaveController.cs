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
        [Min(0)] public int maxAliveOverride = 0;
        [Min(0f)] public float startDelay = 0f;
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

    [Header("Pool")]
    [SerializeField] private EnemyPool enemyPool;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Waves")]
    [SerializeField] private List<Wave> waves = new();

    [Header("Timing")]
    [SerializeField] private float timeBetweenWaves = 3f;
    [SerializeField] private bool startOnStart = true;
    [SerializeField] private bool loop = false;

    [Header("Alive Limit")]
    [SerializeField] private int globalMaxAlive = 6;

    [Header("Pool Warmup")]
    [SerializeField] private int prewarmCount = 0;

    [Header("NavMesh")]
    [SerializeField] private float navMeshSampleRadius = 2.0f;

    private State state = State.Idle;
    private int currentWaveIndex = -1;
    private int spawnedThisWave;
    private float stateEndTime;
    private float nextSpawnTime;
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

        enemyPool.Initialize(CalcPrewarmCount());

        if (startOnStart)
            StartWaves();
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
        spawnedThisWave = 0;
        state = State.BetweenWaves;
        stateEndTime = Time.time;

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
        SyncWithFabricatorState();

        switch (state)
        {
            case State.Idle:
            case State.Completed:
            case State.PausedByFabricator:
            case State.HaltedByFabricator:
                return;

            case State.BetweenWaves:
                if (Time.time < stateEndTime)
                    return;

                BeginNextWaveOrComplete();
                return;

            case State.PreparingWave:
                if (Time.time < stateEndTime)
                    return;

                state = State.Spawning;
                nextSpawnTime = Time.time;
                return;

            case State.Spawning:
                TickSpawning();
                return;

            case State.WaitingClear:
                TickWaitingClear();
                return;
        }
    }

    private bool ValidateSetup()
    {
        TryResolveFabricator();

        if (enemyPool == null)
            enemyPool = GetComponent<EnemyPool>();

        if (fabricator == null)
        {
            Debug.LogError(
                $"{nameof(WaveController)}: Fabricator is not assigned. Assign it in inspector or enable auto-find.",
                this);
            return false;
        }

        if (enemyPool == null)
        {
            Debug.LogError($"{nameof(WaveController)}: EnemyPool is not assigned.", this);
            return false;
        }

        if (!enemyPool.IsConfigured)
        {
            Debug.LogError($"{nameof(WaveController)}: EnemyPool is missing Enemy Prefab.", this);
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

    private int CalcPrewarmCount()
    {
        if (prewarmCount > 0)
            return prewarmCount;

        int max = globalMaxAlive;
        for (int i = 0; i < waves.Count; i++)
        {
            int waveMaxAlive = waves[i].maxAliveOverride;
            if (waveMaxAlive > max)
                max = waveMaxAlive;
        }

        return max;
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
                stateEndTime = Time.time;
                return;
            }

            state = State.Completed;
            return;
        }

        currentWaveIndex = nextIndex;
        spawnedThisWave = 0;

        Wave wave = waves[currentWaveIndex];
        state = State.PreparingWave;
        stateEndTime = Time.time + Mathf.Max(0f, wave.startDelay);
    }

    private void TickSpawning()
    {
        if (fabricator == null || fabricator.IsPermanentlyShutdown)
        {
            EnterPermanentHalt();
            return;
        }

        Wave wave = waves[currentWaveIndex];
        if (spawnedThisWave >= wave.enemiesToSpawn)
        {
            if (wave.waitUntilCleared)
            {
                state = State.WaitingClear;
            }
            else
            {
                GoToBetweenWaves();
            }

            return;
        }

        int maxAlive = wave.maxAliveOverride > 0 ? wave.maxAliveOverride : globalMaxAlive;
        if (enemyPool.ActiveCount >= maxAlive)
            return;

        if (Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + Mathf.Max(0f, wave.spawnInterval);

        GameObject enemy = enemyPool.GetOrCreate();
        if (enemy == null)
            return;

        SpawnFromPool(enemy);
        spawnedThisWave++;
    }

    private void TickWaitingClear()
    {
        if (enemyPool.HasAnyActive)
            return;

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

    private void SpawnFromPool(GameObject enemy)
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        Vector3 position = spawnPoint.position;

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            position = hit.position;

        enemyPool.Activate(enemy, position, spawnPoint.rotation);
    }
}
