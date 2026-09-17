using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Animator))]
public class Trashcan : NetworkBehaviour, IInteractable
{
    private Animator animator;
    private Renderer[] renderers;
    private Color[] originalColors;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        // Smoothly open and close the lid when the player looks at it
        if (animator != null)
        {
            animator.SetBool("IsOpen", isHighlighted);
        }

        // Add the standard visual glow
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = isHighlighted ? originalColors[i] * 1.5f : originalColors[i];
                }
            }
        }
    }

    public void Interact(PlayerInteraction interactor)
    {
        var item = interactor.GetCarriedItem();
        
        if (item is DeliveryBox box)
        {
            // Only allow throwing away EMPTY boxes
            if (box.itemCount.Value == 0)
            {
                // Tell the server to despawn the box and clear our carried state
                interactor.TrashCarriedItemServerRpc();
                
                // Instantly clear it locally to avoid visual lag or animation bugs
                interactor.ClearCarriedItemLocally();
            }
        }
    }
}
