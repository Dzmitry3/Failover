using System.Collections.Generic;
using UnityEngine;

public sealed class WaveSetupValidator
{
    private readonly MonoBehaviour owner;

    public WaveSetupValidator(MonoBehaviour owner)
    {
        this.owner = owner;
    }

    public bool Validate(
        Fabricator fabricator,
        EnemyPool enemyPool,
        Transform[] spawnPoints,
        IReadOnlyList<WaveController.Wave> waves,
        int globalMaxAlive)
    {
        if (fabricator == null)
        {
            Debug.LogError(
                $"{nameof(WaveController)}: Fabricator is not assigned. Assign it in inspector or enable auto-find.",
                owner);
            return false;
        }

        if (enemyPool == null)
        {
            Debug.LogError($"{nameof(WaveController)}: EnemyPool is not assigned.", owner);
            return false;
        }

        if (!enemyPool.IsConfigured)
        {
            Debug.LogError($"{nameof(WaveController)}: EnemyPool is missing Enemy Prefab.", owner);
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Spawn Points are not assigned.", owner);
            return false;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogError($"{nameof(WaveController)}: Spawn Points contain null entries.", owner);
                return false;
            }
        }

        if (waves == null || waves.Count == 0)
        {
            Debug.LogError($"{nameof(WaveController)}: no waves are configured in scene or WaveSequenceData.", owner);
            return false;
        }

        if (globalMaxAlive <= 0)
        {
            Debug.LogError($"{nameof(WaveController)}: Global Max Alive must be > 0.", owner);
            return false;
        }

        return true;
    }
}
