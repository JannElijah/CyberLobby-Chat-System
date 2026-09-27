using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
using Unity.Netcode.Components;
using System;
using TMPro;

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

    public enum CustomerState
    {
        Wandering,
        GoingToShelf,
        WaitingForRestock,
        Leaving
    }

    [Header("Shopping AI Settings")]
    public float patienceTime = 15f; 
    private float patienceTimer;
    
    [Header("Debug Status (Read Only)")]
    public CustomerState currentState = CustomerState.Wandering;
    public NetworkVariable<ProductType> desiredProduct = new NetworkVariable<ProductType>(
        writePerm: NetworkVariableWritePermission.Server
    );
    private Shelf targetShelf;
    private TextMeshProUGUI floatingText;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        CreateSpeechBubble();
        
        // Listen for changes so late-joiners or updates instantly refresh the UI
        desiredProduct.OnValueChanged += (oldVal, newVal) => UpdateSpeechBubble();
        UpdateSpeechBubble();

        if (!IsServer)
        {
            agent.enabled = false; 
            return;
        }

        // Find the floor BEFORE turning the agent on to prevent the Unity warning
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 100.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position - new Vector3(0, agent.baseOffset, 0);
            agent.enabled = true;
            agent.Warp(hit.position);
            
            StartShoppingTrip();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Failed to find NavMesh within 100 units.");
        }
    }

    private void CreateSpeechBubble()
    {
        // 1. Create a World Space Canvas
        GameObject canvasObj = new GameObject("SpeechBubbleCanvas");
        canvasObj.transform.SetParent(this.transform, false);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(5, 2); // Physical size in the world
        // Set localPosition on the RectTransform after Canvas creation to ensure it sticks
        canvasRect.localPosition = new Vector3(0, 20f, 0);

        // 2. Add the Billboard script to the Canvas
        canvasObj.AddComponent<BillboardUI>();

        // 3. Create the Text Object
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        
        // Note: Using TextMeshProUGUI since it's on a Canvas now!
        floatingText = textObj.AddComponent<TextMeshProUGUI>(); 
        floatingText.alignment = TextAlignmentOptions.Center;
        floatingText.fontSize = 2.5f; 
        
        // Let's make it a bright color with an outline so it pops
        floatingText.color = new Color(0.96f, 0.92f, 0.84f, 1f); // Cream
        
        // Load our PawMart font
        TMP_FontAsset pawmartFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");
        if (pawmartFont != null) floatingText.font = pawmartFont;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
    }

    private void UpdateSpeechBubble()
    {
        if (floatingText != null)
        {
            floatingText.text = desiredProduct.Value.ToString();
        }
    }

    private void StartShoppingTrip()
    {
        // 1. Pick a random product they want to buy
        Array productTypes = Enum.GetValues(typeof(ProductType));
        desiredProduct.Value = (ProductType)productTypes.GetValue(UnityEngine.Random.Range(0, productTypes.Length));
        
        // 2. Find the shelf in the store that holds this product
        Shelf[] allShelves = FindObjectsByType<Shelf>(FindObjectsSortMode.None);
        foreach (Shelf shelf in allShelves)
        {
            if (shelf.acceptedProductType == desiredProduct.Value)
            {
                targetShelf = shelf;
                break;
            }
        }

        if (targetShelf != null)
        {
            // Found a shelf, go to it!
            currentState = CustomerState.GoingToShelf;
            agent.SetDestination(targetShelf.transform.position);
        }
        else
        {
            // No shelf for this item exists yet, just wander or leave
            Debug.LogWarning($"[{gameObject.name}] Could not find shelf for {desiredProduct.Value}");
            PickNewWanderTarget();
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        // Sync Animation (Fallback to actual speed if NavMeshAgent gets confused)
        if (IsServer) 
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) {
                syncSpeed.Value = agent.velocity.magnitude;
            } else {
                syncSpeed.Value = 0f;
            }
        }
        
        if (animator != null) {
            animator.SetFloat("Speed", syncSpeed.Value);
        }

        if (!IsServer) return; // Only server handles logic

        // Safety check
        if (!agent.isOnNavMesh) 
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) agent.Warp(hit.position);
            return;
        }

        switch (currentState)
        {
            case CustomerState.GoingToShelf:
                HandleGoingToShelf();
                break;
            case CustomerState.WaitingForRestock:
                HandleWaitingForRestock();
                break;
            case CustomerState.Wandering:
                HandleWandering();
                break;
            case CustomerState.Leaving:
                HandleLeaving();
                break;
        }
    }

    private void HandleGoingToShelf()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1.5f)
        {
            // Arrived at the shelf!
            if (targetShelf.currentStock.Value > 0)
            {
                // Buy the item!
                targetShelf.TakeItemServerRpc();
                Debug.Log($"[{gameObject.name}] Successfully bought {desiredProduct.Value}!");
                
                // For now, after they get the item, they just leave the store
                LeaveStore();
            }
            else
            {
                // Oh no, the shelf is empty! Wait for a player to restock it.
                currentState = CustomerState.WaitingForRestock;
                patienceTimer = patienceTime;
                
                // Force the agent to stop completely so the velocity hits 0 and they go to Idle
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
    }

    private void HandleWaitingForRestock()
    {
        // Keep checking if a player magically restocked the shelf while we wait
        if (targetShelf.currentStock.Value > 0)
        {
            targetShelf.TakeItemServerRpc();
            Debug.Log($"[{gameObject.name}] Player restocked! Bought {desiredProduct.Value}!");
            LeaveStore();
            return;
        }

        // Tick down patience
        patienceTimer -= Time.deltaTime;
        if (patienceTimer <= 0)
        {
            Debug.Log($"[{gameObject.name}] Got tired of waiting for {desiredProduct.Value}. Leaving angry!");
            LeaveStore();
        }
    }

    private void LeaveStore()
    {
        currentState = CustomerState.Leaving;
        
        // Let the agent move again
        agent.isStopped = false;
        
        // Pick a random point far away to simulate leaving (or you can create a specific "Exit" object later)
        Vector3 exitDirection = transform.position + (transform.forward * 15f);
        if (NavMesh.SamplePosition(exitDirection, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void HandleLeaving()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj.InScenePlaced == true)
                netObj.Despawn(false); // Just unspawn it if it was hand-placed in the scene
            else
                netObj.Despawn(true);  // Destroy it if it was a dynamically spawned prefab
        }
    }

    private void HandleWandering()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!isWaiting)
            {
                isWaiting = true;
                waitTimer = UnityEngine.Random.Range(minWaitTime, maxWaitTime);
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
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}
