using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class GrabbableItem : NetworkBehaviour, IInteractable
{
    private Collider itemCollider;
    private Rigidbody rb;
    
    private Renderer[] renderers;
    private Color[] originalColors;
    private Vector3 originalScale;

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        originalScale = transform.localScale; // Remember the prefab's true scale!

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for(int i = 0; i < renderers.Length; i++) 
        {
            if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (renderers == null) return;
        for(int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = isHighlighted ? originalColors[i] * 1.5f : originalColors[i];
            }
        }
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

        // Always restore the exact original scale when picking up
        if (isGrabbed) transform.localScale = originalScale;

        // We disable the NetworkTransform entirely while grabbed so it doesn't fight the local parent.
        // The item will naturally follow the player's NetworkTransform over the network!
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            nt.enabled = !isGrabbed;
        }
    }
    public void SetStoredState(Transform snapPoint, float scaleMultiplier)
    {
        // 1. Re-enable collider so it can be grabbed again
        if (itemCollider != null) itemCollider.enabled = true;

        // 2. Keep physics completely frozen so it stays perfectly on the shelf
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // 3. Make it visually smaller to fit
        transform.localScale = Vector3.one * scaleMultiplier;

        // 4. Re-enable NetworkTransform so the clients see the scale and position change!
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            nt.enabled = true;
        }
    }
}
