using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI_NavMesh : MonoBehaviour
{
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
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go != null) target = go.transform;
        }

        agent.stoppingDistance = stopDistance;
    }

    private void Update()
    {
        if (target == null) return;

        // периодически обновляем путь, чтобы не спамить SetDestination каждый кадр
        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(target.position);
        }

        // доворот к игроку
        if (faceTargetOnStop && agent.hasPath && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                FaceTargetXZ(target.position);
            }
        }
    }

    private void FaceTargetXZ(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 15f * Time.deltaTime);
    }
}