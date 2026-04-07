using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class WaveController : MonoBehaviour
{
    public event Action Completed;

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
    [SerializeField] private WaveSequenceData waveSequence;
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

    public bool IsCompleted => state == State.Completed;

    private IReadOnlyList<Wave> EffectiveWaves =>
        waveSequence != null && waveSequence.HasWaves ? waveSequence.Waves : waves;

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

        if (startOnStart && !GameFlowUI.RequiresManualStart)
            StartWaves();
    }

    public void StartWaves()
    {
        if (state != State.Idle && state != State.Completed)
            return;

        if (TryEnterPermanentHalt())
            return;

        currentWaveIndex = -1;
        spawnedThisWave = 0;
        SetTimedState(State.BetweenWaves, 0f);
        SyncWithFabricatorState();
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

                SetNextSpawnTime(0f);
                state = State.Spawning;
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
        ResolveSpawnPoints();

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

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError($"{nameof(WaveController)}: Spawn Points contain null entries.", this);
                return false;
            }
        }

        if (EffectiveWaves == null || EffectiveWaves.Count == 0)
        {
            Debug.LogError($"{nameof(WaveController)}: no waves are configured in scene or WaveSequenceData.", this);
            return false;
        }

        if (globalMaxAlive <= 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Global Max Alive must be > 0.", this);
            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        ResolveSpawnPoints();
    }

    private void TryResolveFabricator()
    {
        if (fabricator != null || !autoFindFabricator)
            return;

        fabricator = FindFirstObjectByType<Fabricator>();
    }

    private void ResolveSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        List<Transform> resolvedPoints = null;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
                continue;

            if (spawnPoint.childCount <= 0)
            {
                resolvedPoints ??= new List<Transform>(spawnPoints.Length);
                resolvedPoints.Add(spawnPoint);
                continue;
            }

            resolvedPoints ??= new List<Transform>(spawnPoints.Length + spawnPoint.childCount);
            for (int childIndex = 0; childIndex < spawnPoint.childCount; childIndex++)
            {
                Transform child = spawnPoint.GetChild(childIndex);
                if (child != null)
                    resolvedPoints.Add(child);
            }
        }

        if (resolvedPoints != null && resolvedPoints.Count > 0)
            spawnPoints = resolvedPoints.ToArray();
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
        IReadOnlyList<Wave> effectiveWaves = EffectiveWaves;
        for (int i = 0; i < effectiveWaves.Count; i++)
        {
            int waveMaxAlive = effectiveWaves[i].maxAliveOverride;
            if (waveMaxAlive > max)
                max = waveMaxAlive;
        }

        return max;
    }

    private void BeginNextWaveOrComplete()
    {
        if (TryEnterPermanentHalt())
            return;

        int nextIndex = currentWaveIndex + 1;
        IReadOnlyList<Wave> effectiveWaves = EffectiveWaves;
        if (nextIndex >= effectiveWaves.Count)
        {
            if (loop)
            {
                currentWaveIndex = -1;
                SetTimedState(State.BetweenWaves, 0f);
                return;
            }

            state = State.Completed;
            Completed?.Invoke();
            return;
        }

        currentWaveIndex = nextIndex;
        spawnedThisWave = 0;

        Wave wave = effectiveWaves[currentWaveIndex];
        SetTimedState(State.PreparingWave, wave.startDelay);
    }

    private void TickSpawning()
    {
        if (TryEnterPermanentHalt())
            return;

        Wave wave = EffectiveWaves[currentWaveIndex];
        if (spawnedThisWave >= wave.enemiesToSpawn)
        {
            if (wave.waitUntilCleared)
                state = State.WaitingClear;
            else
                EnterBetweenWaves();

            return;
        }

        int maxAlive = wave.maxAliveOverride > 0 ? wave.maxAliveOverride : globalMaxAlive;
        if (enemyPool.AliveCount >= maxAlive)
            return;

        if (Time.time < nextSpawnTime)
            return;

        SetNextSpawnTime(wave.spawnInterval);

        GameObject enemy = enemyPool.GetOrCreate();
        if (enemy == null)
            return;

        if (SpawnFromPool(enemy))
            spawnedThisWave++;
    }

    private void TickWaitingClear()
    {
        if (enemyPool.HasAnyAlive)
            return;

        EnterBetweenWaves();
    }

    private void EnterBetweenWaves()
    {
        if (TryEnterPermanentHalt())
            return;

        SetTimedState(State.BetweenWaves, timeBetweenWaves);
    }

    private void SyncWithFabricatorState()
    {
        if (fabricator == null)
            return;

        if (TryEnterPermanentHalt())
            return;

        if (!fabricator.CanSpawn)
        {
            if (CanPauseForFabricator())
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

    private bool CanPauseForFabricator()
    {
        return state == State.PreparingWave ||
               state == State.Spawning ||
               state == State.BetweenWaves;
    }

    private bool TryEnterPermanentHalt()
    {
        if (fabricator != null && !fabricator.IsPermanentlyShutdown)
            return false;

        EnterPermanentHalt();
        return true;
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

    private void SetTimedState(State newState, float delaySeconds)
    {
        state = newState;
        stateEndTime = Time.time + Mathf.Max(0f, delaySeconds);
    }

    private void SetNextSpawnTime(float delaySeconds)
    {
        nextSpawnTime = Time.time + Mathf.Max(0f, delaySeconds);
    }

    private bool SpawnFromPool(GameObject enemy)
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        Vector3 position = spawnPoint.position;

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning(
                $"{nameof(WaveController)}: failed to find NavMesh near spawn point {spawnPoint.name}. Enemy spawn was skipped.",
                spawnPoint);
            return false;
        }

        position = hit.position;

        enemyPool.Activate(enemy, position, spawnPoint.rotation);
        return true;
    }
}

[CreateAssetMenu(fileName = "WaveSequence_", menuName = "Game/Waves/Wave Sequence", order = 2)]
public class WaveSequenceData : ScriptableObject
{
    [SerializeField] private List<WaveController.Wave> waves = new();

    public IReadOnlyList<WaveController.Wave> Waves => waves;
    public bool HasWaves => waves != null && waves.Count > 0;
}
