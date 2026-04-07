using System;
using System.Collections.Generic;
using UnityEngine;

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

    private readonly WaveRunState runState = new();
    private WaveSetupValidator setupValidator;
    private NavMeshSpawnPointSelector spawnPointSelector;

    public bool IsCompleted => runState.IsCompleted;

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

        if (startOnStart && !GameSessionOverlay.RequiresManualStart)
            StartWaves();
    }

    public void StartWaves()
    {
        if (runState.Phase != WaveRunPhase.Idle && !runState.IsCompleted)
            return;

        if (TryEnterPermanentHalt())
            return;

        runState.ResetForRun(Time.time);
        SyncWithFabricatorState();
    }

    private void Update()
    {
        SyncWithFabricatorState();

        switch (runState.Phase)
        {
            case WaveRunPhase.Idle:
            case WaveRunPhase.Completed:
            case WaveRunPhase.PausedByFabricator:
            case WaveRunPhase.HaltedByFabricator:
                return;

            case WaveRunPhase.BetweenWaves:
                if (Time.time < runState.StateEndTime)
                    return;

                BeginNextWaveOrComplete();
                return;

            case WaveRunPhase.PreparingWave:
                if (Time.time < runState.StateEndTime)
                    return;

                runState.SetNextSpawnTime(Time.time, 0f);
                runState.SetPhase(WaveRunPhase.Spawning);
                return;

            case WaveRunPhase.Spawning:
                TickSpawning();
                return;

            case WaveRunPhase.WaitingClear:
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

        setupValidator ??= new WaveSetupValidator(this);
        if (!setupValidator.Validate(fabricator, enemyPool, spawnPoints, EffectiveWaves, globalMaxAlive))
            return false;

        spawnPointSelector = new NavMeshSpawnPointSelector(this, spawnPoints, navMeshSampleRadius);
        return true;
    }

    private void OnValidate()
    {
        ResolveSpawnPoints();
        if (spawnPoints != null && spawnPoints.Length > 0)
            spawnPointSelector = new NavMeshSpawnPointSelector(this, spawnPoints, navMeshSampleRadius);
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

        int nextIndex = runState.CurrentWaveIndex + 1;
        IReadOnlyList<Wave> effectiveWaves = EffectiveWaves;
        if (nextIndex >= effectiveWaves.Count)
        {
            if (loop)
            {
                runState.ResetForRun(Time.time);
                return;
            }

            runState.SetPhase(WaveRunPhase.Completed);
            Completed?.Invoke();
            return;
        }

        Wave wave = effectiveWaves[nextIndex];
        runState.BeginWave(nextIndex, Time.time, wave.startDelay);
    }

    private void TickSpawning()
    {
        if (TryEnterPermanentHalt())
            return;

        Wave wave = EffectiveWaves[runState.CurrentWaveIndex];
        if (runState.SpawnedThisWave >= wave.enemiesToSpawn)
        {
            if (wave.waitUntilCleared)
                runState.SetPhase(WaveRunPhase.WaitingClear);
            else
                EnterBetweenWaves();

            return;
        }

        int maxAlive = wave.maxAliveOverride > 0 ? wave.maxAliveOverride : globalMaxAlive;
        if (enemyPool.AliveCount >= maxAlive)
            return;

        if (Time.time < runState.NextSpawnTime)
            return;

        runState.SetNextSpawnTime(Time.time, wave.spawnInterval);

        GameObject enemy = enemyPool.GetOrCreate();
        if (enemy == null)
            return;

        if (SpawnFromPool(enemy))
            runState.MarkSpawned();
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

        runState.SetTimedPhase(WaveRunPhase.BetweenWaves, Time.time, timeBetweenWaves);
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

        if (runState.Phase == WaveRunPhase.PausedByFabricator)
            ResumeAfterFabricatorPause();
    }

    private void PauseByFabricator()
    {
        if (runState.Phase == WaveRunPhase.PausedByFabricator || runState.Phase == WaveRunPhase.HaltedByFabricator)
            return;

        runState.Pause(Time.time);

        Debug.Log(
            $"{nameof(WaveController)}: spawning paused by Fabricator. Reason: {fabricator.StopReason}",
            this);
    }

    private void ResumeAfterFabricatorPause()
    {
        if (runState.Phase != WaveRunPhase.PausedByFabricator)
            return;

        runState.Resume(Time.time);
        Debug.Log($"{nameof(WaveController)}: spawning resumed (Fabricator active).", this);
    }

    private bool CanPauseForFabricator()
    {
        return runState.Phase == WaveRunPhase.PreparingWave ||
               runState.Phase == WaveRunPhase.Spawning ||
               runState.Phase == WaveRunPhase.BetweenWaves;
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
        if (runState.Phase == WaveRunPhase.HaltedByFabricator || runState.Phase == WaveRunPhase.Completed)
            return;

        runState.SetPhase(WaveRunPhase.HaltedByFabricator);

        Debug.Log(
            $"{nameof(WaveController)}: spawning permanently halted. Reason: {fabricator?.StopReason}",
            this);
    }

    private bool SpawnFromPool(GameObject enemy)
    {
        spawnPointSelector ??= new NavMeshSpawnPointSelector(this, spawnPoints, navMeshSampleRadius);
        if (!spawnPointSelector.TryGetSpawnPose(out Vector3 position, out Quaternion rotation))
            return false;

        enemyPool.Activate(enemy, position, rotation);
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
