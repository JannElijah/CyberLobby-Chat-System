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

    private void OnEnable() => interactAction.Enable();
    private void OnDisable() => interactAction.Disable();
    private void OnDestroy() => interactAction.performed -= OnInteractPerformed;

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        // SCENARIO 1: We are ALREADY carrying a box
        if (carriedItemNetworkId.Value != 0)
        {
            // Drop it! (In the future, we will check if we are looking at an unpacking station first)
            DropItemServerRpc();
            return;
        }

        // SCENARIO 2: We are EMPTY HANDED
        // Do an overlap sphere to find a box to pick up
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, interactionRadius, interactableLayer);

        if (hitColliders.Length > 0)
        {
            // Get the interface from the object we hit (or its parent, in case it's on the root of the box)
            IInteractable interactable = hitColliders[0].GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(this);
            }
        }
    }

    /// <summary>
    /// Called by the GrabbableItem.cs script to tell the server we grabbed it.
    /// </summary>
    public void RequestGrabItem(GrabbableItem item)
    {
        if (!IsOwner) return;
        GrabItemServerRpc(item.NetworkObjectId);
    }

    [ServerRpc]
    private void GrabItemServerRpc(ulong itemNetworkId)
    {
        carriedItemNetworkId.Value = itemNetworkId;
        
        // In Unity Netcode, the Server securely manages the parenting of objects
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject itemObj))
        {
            itemObj.TrySetParent(transform); // Parent the box to the player on the network
        }
    }

    [ServerRpc]
    private void DropItemServerRpc()
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemNetworkId.Value, out NetworkObject itemObj))
        {
            itemObj.TryRemoveParent(); // Unparent the box on the network
        }

        carriedItemNetworkId.Value = 0; // 0 means empty handed
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Gizmos.DrawWireSphere(sphereCenter, interactionRadius);
    }
}
