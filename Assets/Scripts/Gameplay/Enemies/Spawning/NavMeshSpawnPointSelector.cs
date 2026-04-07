using UnityEngine;
using UnityEngine.AI;

public sealed class NavMeshSpawnPointSelector
{
    private readonly MonoBehaviour owner;
    private readonly Transform[] spawnPoints;
    private readonly float navMeshSampleRadius;

    public NavMeshSpawnPointSelector(MonoBehaviour owner, Transform[] spawnPoints, float navMeshSampleRadius)
    {
        this.owner = owner;
        this.spawnPoints = spawnPoints;
        this.navMeshSampleRadius = navMeshSampleRadius;
    }

    public bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (spawnPoints == null || spawnPoints.Length == 0)
            return false;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        position = spawnPoint.position;
        rotation = spawnPoint.rotation;

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        Debug.LogWarning(
            $"{nameof(WaveController)}: failed to find NavMesh near spawn point {spawnPoint.name}. Enemy spawn was skipped.",
            owner);
        return false;
    }
}
