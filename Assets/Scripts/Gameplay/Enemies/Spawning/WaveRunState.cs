using UnityEngine;

public enum WaveRunPhase
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

public sealed class WaveRunState
{
    private float pauseStartedAt = -1f;
    private WaveRunPhase phaseBeforePause = WaveRunPhase.Idle;

    public WaveRunPhase Phase { get; private set; } = WaveRunPhase.Idle;
    public int CurrentWaveIndex { get; private set; } = -1;
    public int SpawnedThisWave { get; private set; }
    public float StateEndTime { get; private set; }
    public float NextSpawnTime { get; private set; }
    public bool IsCompleted => Phase == WaveRunPhase.Completed;

    public void ResetForRun(float time)
    {
        CurrentWaveIndex = -1;
        SpawnedThisWave = 0;
        SetTimedPhase(WaveRunPhase.BetweenWaves, time, 0f);
    }

    public void BeginWave(int waveIndex, float time, float startDelay)
    {
        CurrentWaveIndex = waveIndex;
        SpawnedThisWave = 0;
        SetTimedPhase(WaveRunPhase.PreparingWave, time, startDelay);
    }

    public void MarkSpawned()
    {
        SpawnedThisWave++;
    }

    public void SetPhase(WaveRunPhase phase)
    {
        Phase = phase;
    }

    public void SetTimedPhase(WaveRunPhase phase, float time, float delaySeconds)
    {
        Phase = phase;
        StateEndTime = time + Mathf.Max(0f, delaySeconds);
    }

    public void SetNextSpawnTime(float time, float delaySeconds)
    {
        NextSpawnTime = time + Mathf.Max(0f, delaySeconds);
    }

    public void Pause(float time)
    {
        phaseBeforePause = Phase;
        pauseStartedAt = time;
        Phase = WaveRunPhase.PausedByFabricator;
    }

    public void Resume(float time)
    {
        float pausedDuration = pauseStartedAt >= 0f ? time - pauseStartedAt : 0f;
        StateEndTime += pausedDuration;
        NextSpawnTime += pausedDuration;
        Phase = phaseBeforePause;
        pauseStartedAt = -1f;
    }
}
