using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class GrabbableItem : NetworkBehaviour, IInteractable
{
    private Collider itemCollider;
    private Rigidbody rb;

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    public void Interact(PlayerInteraction interactor)
    {
        // Tell the interactor that we want to be grabbed!
        interactor.RequestGrabItem(this);
    }

    /// <summary>
    /// Called automatically by the player on all clients when this item is picked up or dropped.
    /// </summary>
    public void SetGrabbedState(bool isGrabbed)
    {
        // We disable the collider while carried so it doesn't bump the player 
        // or block our interaction raycasts!
        if (itemCollider != null)
        {
            itemCollider.enabled = !isGrabbed;
        }

        // We disable physics simulation while carried so it doesn't wiggle out of our hands
        if (rb != null)
        {
            rb.isKinematic = isGrabbed;
        }
    }
}
