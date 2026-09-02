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

    private Animator animator;
    private InputAction interactAction;

    // Network variable to sync the carry state across all clients
    private NetworkVariable<bool> isCarrying = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Create the interact action programmatically
        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonSouth");

        // Subscribe to the performed event
        interactAction.performed += OnInteractPerformed;
    }

    public override void OnNetworkSpawn()
    {
        // When the variable changes (e.g. from server), update the animator locally
        isCarrying.OnValueChanged += (bool previousValue, bool newValue) =>
        {
            UpdateAnimator(newValue);
        };

        // Initialize with current value just in case we joined late
        UpdateAnimator(isCarrying.Value);
    }

    private void OnEnable()
    {
        interactAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
    }

    private void OnDestroy()
    {
        interactAction.performed -= OnInteractPerformed;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        // Perform overlap sphere to find interactables
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, interactionRadius, interactableLayer);

        if (hitColliders.Length > 0)
        {
            // We found at least one interactable object!
            // Optional: You could get an IInteractable interface from hitColliders[0] here and call Interact().
            
            // For now, toggle the carry state as requested.
            ToggleCarryStateServerRpc();
        }
    }

    [ServerRpc]
    private void ToggleCarryStateServerRpc()
    {
        // Toggle the network variable (server-side). This will trigger OnValueChanged on all clients.
        isCarrying.Value = !isCarrying.Value;
    }

    private void UpdateAnimator(bool state)
    {
        animator.SetBool("IsCarrying", state);
    }

    // Visualize the interaction sphere in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 sphereCenter = transform.TransformPoint(interactionOffset);
        Gizmos.DrawWireSphere(sphereCenter, interactionRadius);
    }
}
