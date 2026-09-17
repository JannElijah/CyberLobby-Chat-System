using UnityEngine;
using Unity.Netcode;

public class UnpackingStation : NetworkBehaviour, IInteractable
{
    private Renderer[] renderers;
    private Color[] originalColors;

    private void Awake()
    {
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
        var item = interactor.GetCarriedItem();
        
        if (item is DeliveryBox box)
        {
            if (!box.isOpen.Value)
            {
                // The player is holding a sealed box and interacting with the station
                // Tell the server to open the box!
                box.OpenBoxServerRpc();
            }
        }
    }
}
