using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI_NavMesh : MonoBehaviour
{
    private const float StopDistanceTolerance = 0.05f;
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const float FaceTargetTurnSpeed = 15f;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";

    [Header("Behaviour")]
    [SerializeField] private float repathInterval = 0.1f;
    [SerializeField] private float stopDistance = 2.0f;
    [SerializeField] private bool faceTargetOnStop = true;

    private NavMeshAgent agent;
    private float nextRepathTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        ResolveTarget();
        agent.stoppingDistance = stopDistance;
    }

    private void Update()
    {
        if (target == null)
        {
            ResolveTarget();
            if (target == null)
                return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // Repath periodically so SetDestination is not called every frame.
        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(target.position);
        }

        // Rotate toward the player when the agent reaches stop distance.
        if (faceTargetOnStop && agent.hasPath && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance + StopDistanceTolerance)
                FaceTargetXZ(target.position);
        }
    }

    private void FaceTargetXZ(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < MinDirectionSqrMagnitude)
            return;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, FaceTargetTurnSpeed * Time.deltaTime);
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null)
            target = go.transform;
    }
}
