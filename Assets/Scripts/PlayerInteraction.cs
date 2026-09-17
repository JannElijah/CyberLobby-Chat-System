using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRadius = 1.5f;
    public Vector3 interactionOffset = new Vector3(0f, 0.5f, 1f); // Offset in front of the player
    public LayerMask interactableLayer;

    [Header("Carry Settings")]
    [Tooltip("Assign the empty GameObject placed exactly where the hands hold the box in your animation")]
    public Transform carryPoint;

    private Animator animator;
    private InputAction interactAction;
    private InputAction throwAction;
    private IInteractable currentlyHighlightedItem;

    // Securely sync exactly WHICH item the player is carrying across the server
    private NetworkVariable<ulong> carriedItemNetworkId = new NetworkVariable<ulong>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Track the physical object locally
    private NetworkObject currentlyCarriedObject;
    private GrabbableItem currentlyCarriedItemScript;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonSouth");

        interactAction.performed += OnInteractPerformed;

        throwAction = new InputAction("Throw", InputActionType.Button);
        throwAction.AddBinding("<Keyboard>/q");
        throwAction.AddBinding("<Gamepad>/buttonEast");

        throwAction.performed += OnThrowPerformed;
    }

    public override void OnNetworkSpawn()
    {
        carriedItemNetworkId.OnValueChanged += OnCarriedItemChanged;

        // If we join a game late, and the player is already holding a box, snap it into their hands instantly!
        if (carriedItemNetworkId.Value != 0)
        {
            OnCarriedItemChanged(0, carriedItemNetworkId.Value);
        }
    }

    private void OnEnable() 
    { 
        interactAction.Enable(); 
        throwAction.Enable(); 
    }
    
    private void OnDisable() 
    { 
        interactAction.Disable(); 
        throwAction.Disable(); 
    }
    
    public override void OnDestroy() 
    { 
        base.OnDestroy();
        interactAction.performed -= OnInteractPerformed; 
        throwAction.performed -= OnThrowPerformed;
    }

    private void LateUpdate()
    {
        // Force the held object to perfectly match the hand position every frame.
        // This overrides any network jitter and handles client-side prediction cleanly!
        if (currentlyCarriedObject != null && carryPoint != null)
        {
            currentlyCarriedObject.transform.position = carryPoint.position;
            currentlyCarriedObject.transform.rotation = carryPoint.rotation;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Find what we are looking at continuously for highlighting
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, interactionRadius, interactableLayer);
        IInteractable closestInteractable = null;
        
        if (hitColliders.Length > 0)
        {
            float closestDistance = float.MaxValue;
            foreach (var hit in hitColliders)
            {
                var interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    // Use ClosestPoint to accurately find the nearest edge of large/off-center colliders
                    float dist = Vector3.Distance(sphereCenter, hit.ClosestPoint(sphereCenter));
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestInteractable = interactable;
                    }
                }
            }
        }

        if (closestInteractable != currentlyHighlightedItem)
        {
            if (currentlyHighlightedItem != null)
            {
                currentlyHighlightedItem.SetHighlight(false);
            }
            currentlyHighlightedItem = closestInteractable;
            if (currentlyHighlightedItem != null)
            {
                currentlyHighlightedItem.SetHighlight(true);
            }
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        IInteractable hitInteractable = currentlyHighlightedItem;

        // SCENARIO 1: We are ALREADY carrying an item
        if (carriedItemNetworkId.Value != 0)
        {
            if (hitInteractable != null && !(hitInteractable is GrabbableItem))
            {
                // Interact with the station/shelf while holding an item
                hitInteractable.Interact(this);
            }
            else
            {
                // Otherwise drop it on the floor
                // CLIENT-SIDE PREDICTION: Instantly drop locally to avoid visual delay
                if (currentlyCarriedItemScript != null)
                {
                    currentlyCarriedItemScript.SetGrabbedState(false);
                    animator.SetBool("IsCarrying", false);
                    currentlyCarriedObject = null;
                    currentlyCarriedItemScript = null;
                }

                DropItemServerRpc();
            }
            return;
        }

        // SCENARIO 2: We are EMPTY HANDED
        if (hitInteractable != null)
        {
            hitInteractable.Interact(this);
        }
    }

    /// <summary>
    /// Called by the GrabbableItem.cs script to tell the server we grabbed it.
    /// </summary>
    public void RequestGrabItem(GrabbableItem item)
    {
        if (!IsOwner) return;

        // CLIENT-SIDE PREDICTION: Instantly grab locally to avoid visual delay
        item.SetGrabbedState(true);
        if (carryPoint != null)
        {
            item.transform.position = carryPoint.position;
            item.transform.rotation = carryPoint.rotation;
        }
        
        animator.SetBool("IsCarrying", true);
        currentlyCarriedObject = item.NetworkObject;
        currentlyCarriedItemScript = item;

        GrabItemServerRpc(item.NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void GrabItemServerRpc(ulong itemNetworkId)
    {
        carriedItemNetworkId.Value = itemNetworkId;
        
        // In Unity Netcode, the Server securely manages the parenting of objects
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject itemObj))
        {
            itemObj.TrySetParent(carryPoint != null ? carryPoint : transform); // Parent the box to the player on the network
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DropItemServerRpc()
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemNetworkId.Value, out NetworkObject itemObj))
        {
            itemObj.TryRemoveParent(); // Unparent the box on the network
        }

        carriedItemNetworkId.Value = 0; // 0 means empty handed
    }

    // Added helper for client-side drop prediction (used by Trashcan)
    public void ClearCarriedItemLocally()
    {
        if (currentlyCarriedItemScript != null)
        {
            currentlyCarriedItemScript.SetGrabbedState(false);
            animator.SetBool("IsCarrying", false);
            currentlyCarriedObject = null;
            currentlyCarriedItemScript = null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TrashCarriedItemServerRpc()
    {
        ulong id = carriedItemNetworkId.Value;
        carriedItemNetworkId.Value = 0; // Clear it FIRST before destroying to prevent animation bugs

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject itemObj))
        {
            itemObj.Despawn();
        }
    }

    private void OnThrowPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner || carriedItemNetworkId.Value == 0) return;

        // CLIENT-SIDE PREDICTION
        ClearCarriedItemLocally();

        ThrowItemServerRpc(transform.forward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ThrowItemServerRpc(Vector3 throwDirection)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemNetworkId.Value, out NetworkObject itemObj))
        {
            itemObj.TryRemoveParent();
            
            Rigidbody rb = itemObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                // Launch forward extremely fast, ignoring mass
                Vector3 force = (throwDirection + Vector3.up * 0.25f).normalized * 20f;
                rb.AddForce(force, ForceMode.VelocityChange);
            }
        }
        carriedItemNetworkId.Value = 0;
    }

    /// <summary>
    /// Triggers on ALL clients automatically when a player picks up or drops a box.
    /// </summary>
    private void OnCarriedItemChanged(ulong previousItemId, ulong newItemId)
    {
        // 1. Did we DROP a box?
        if (previousItemId != 0 && currentlyCarriedObject != null)
        {
            currentlyCarriedItemScript?.SetGrabbedState(false); // Turn colliders back on
            currentlyCarriedObject = null;
            currentlyCarriedItemScript = null;
        }

        // 2. Did we GRAB a box?
        if (newItemId != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newItemId, out NetworkObject itemObj))
            {
                currentlyCarriedObject = itemObj;
                currentlyCarriedItemScript = itemObj.GetComponent<GrabbableItem>();
                
                if (currentlyCarriedItemScript != null)
                {
                    currentlyCarriedItemScript.SetGrabbedState(true); // Turn off colliders so it doesn't bounce around
                }

                // Instantly snap the box visually to the player's carry point (hands)
                if (carryPoint != null)
                {
                    itemObj.transform.position = carryPoint.position;
                    itemObj.transform.rotation = carryPoint.rotation;
                }
            }
        }

        // 3. Update the Animator (true if holding something, false if empty handed)
        animator.SetBool("IsCarrying", newItemId != 0);
    }

    public GrabbableItem GetCarriedItem()
    {
        return currentlyCarriedItemScript;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Gizmos.DrawWireSphere(sphereCenter, interactionRadius);
    }
}
