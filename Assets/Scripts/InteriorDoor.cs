using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Animator))]
public class InteriorDoor : NetworkBehaviour
{
    [Header("Detection Zones")]
    [Tooltip("Drag the Trigger_Front child GameObject here")]
    public GameObject triggerFront;
    
    [Tooltip("Drag the Trigger_Back child GameObject here")]
    public GameObject triggerBack;

    private Animator animator;

    // NetworkVariables securely track how many players are standing in each zone.
    // Only the server can change these values, ensuring perfectly synchronized doors.
    private NetworkVariable<int> frontOccupancy = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );
    
    private NetworkVariable<int> backOccupancy = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Dynamically add a helper component to the child objects so we don't have to manually manage multiple scripts!
        if (triggerFront != null)
        {
            var frontZone = triggerFront.AddComponent<DoorTriggerZone>();
            frontZone.Initialize(this, true);
        }

        if (triggerBack != null)
        {
            var backZone = triggerBack.AddComponent<DoorTriggerZone>();
            backZone.Initialize(this, false);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Whenever the server updates the player count in a zone, automatically update the animations
        frontOccupancy.OnValueChanged += (oldVal, newVal) => UpdateAnimator();
        backOccupancy.OnValueChanged += (oldVal, newVal) => UpdateAnimator();
        
        // Trigger initial state just in case someone joins late while a door is already open
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("OpenFront", frontOccupancy.Value > 0);
            animator.SetBool("OpenBack", backOccupancy.Value > 0);
        }
    }

    // Called automatically by the child DoorTriggerZone components
    public void HandleZoneEnter(bool isFront, Collider other)
    {
        // Only the server handles physics overlap logic to prevent race conditions
        if (!IsServer) return;

        // Filter: Only count objects tagged explicitly as "Player"
        if (other.CompareTag("Player"))
        {
            if (isFront) frontOccupancy.Value++;
            else backOccupancy.Value++;
        }
    }

    // Called automatically by the child DoorTriggerZone components
    public void HandleZoneExit(bool isFront, Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            if (isFront) frontOccupancy.Value = Mathf.Max(0, frontOccupancy.Value - 1);
            else backOccupancy.Value = Mathf.Max(0, backOccupancy.Value - 1);
        }
    }
}

/// <summary>
/// A lightweight helper script that we automatically attach to the Trigger_Front and Trigger_Back GameObjects.
/// It forwards physics trigger events up to the main InteriorDoor script.
/// </summary>
public class DoorTriggerZone : MonoBehaviour
{
    private InteriorDoor parentDoor;
    private bool isFront;

    public void Initialize(InteriorDoor door, bool front)
    {
        parentDoor = door;
        isFront = front;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentDoor != null)
            parentDoor.HandleZoneEnter(isFront, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentDoor != null)
            parentDoor.HandleZoneExit(isFront, other);
    }
}
