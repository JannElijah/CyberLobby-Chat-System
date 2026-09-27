using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
using Unity.Netcode.Components;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class CustomerAI : NetworkBehaviour
{
    private NavMeshAgent agent;
    public Animator animator; // Optional: Link your Bear/Possum Animator here
    
    [Header("AI Settings")]
    public float wanderRadius = 10f;
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;

    private float waitTimer;
    private bool isWaiting = false;

    // Syncs the speed from the Server to all Clients
    public NetworkVariable<float> syncSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            agent.enabled = false; 
            return;
        }

        // Find the floor BEFORE turning the agent on to prevent the Unity warning
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 100.0f, NavMesh.AllAreas))
        {
            // Physically move the object to the floor, accounting for any Base Offset on the NavMeshAgent
            transform.position = hit.position - new Vector3(0, agent.baseOffset, 0);
            
            // Now that we are on the floor, it is safe to turn the agent on
            agent.enabled = true;
            agent.Warp(hit.position);
            
            PickNewWanderTarget();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Failed to find NavMesh within 100 units.");
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        // 1. SERVER updates the synchronized speed variable
        if (IsServer && agent.isOnNavMesh)
        {
            syncSpeed.Value = agent.velocity.magnitude;
        }

        // 2. EVERYONE (Host and Client) animates based on the synchronized speed
        if (animator != null)
        {
            animator.SetFloat("Speed", syncSpeed.Value);
        }

        // 3. Only the Server runs the actual AI pathfinding logic
        if (!IsServer) return;

        // Safety check: Prevent the 999+ errors if the agent is floating slightly above the floor
        if (!agent.isOnNavMesh) 
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!isWaiting)
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
            }
            else
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0)
                {
                    isWaiting = false;
                    PickNewWanderTarget();
                }
            }
        }
    }

    private void PickNewWanderTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}
