using UnityEngine;
using Unity.Netcode;

public class BoxStorageShelf : NetworkBehaviour, IInteractable
{
    [Header("Storage Settings")]
    public float storedBoxScale = 0.5f; // How much to shrink the box
    public Transform[] boxSnapPoints;
    
    // Server-only array to track what box is in what slot
    private ulong[] slotOccupants;

    private Renderer[] renderers;
    private Color[] originalColors;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            slotOccupants = new ulong[boxSnapPoints.Length];
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
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

        // 1. If holding a box, store it
        if (item is DeliveryBox box)
        {
            StoreBoxServerRpc(box.NetworkObjectId, interactor.NetworkObjectId);
            interactor.ClearCarriedItemLocally();
            return;
        }

        // 2. If empty handed, pick up a box
        if (item == null)
        {
            PickupBoxServerRpc(interactor.NetworkObjectId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupBoxServerRpc(ulong playerNetworkId)
    {
        // Find the first occupied slot
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] != 0)
            {
                ulong boxId = slotOccupants[i];
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxId, out NetworkObject boxObj))
                {
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
                    {
                        var grabbable = boxObj.GetComponent<GrabbableItem>();
                        var interactor = playerObj.GetComponent<PlayerInteraction>();
                        
                        if (grabbable != null && interactor != null)
                        {
                            slotOccupants[i] = 0; // Empty the slot
                            grabbable.Interact(interactor); // Force the player to pick it up!
                            return;
                        }
                    }
                }
                else
                {
                    // Box was destroyed somehow, clear the slot
                    slotOccupants[i] = 0;
                }
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StoreBoxServerRpc(ulong boxNetworkId, ulong playerNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxNetworkId, out NetworkObject boxObj))
        {
            // Find the first empty slot index
            int emptyIndex = -1;
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] == 0)
                {
                    emptyIndex = i;
                    break;
                }
            }

            if (emptyIndex != -1)
            {
                Transform emptySpot = boxSnapPoints[emptyIndex];
                slotOccupants[emptyIndex] = boxNetworkId; // Claim the slot

                // Securely clear the player's hands on the server
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
                {
                    var interactor = playerObj.GetComponent<PlayerInteraction>();
                    if (interactor != null) interactor.ForceClearHandsServer();
                }

                // Unparent the box to the root of the scene (no NetworkObject parent needed!)
                boxObj.TryRemoveParent();
                
                // Snap position
                boxObj.transform.position = emptySpot.position;
                boxObj.transform.rotation = emptySpot.rotation;
                
                // Use our new safe setup method!
                var grabbable = boxObj.GetComponent<GrabbableItem>();
                if (grabbable != null)
                {
                    grabbable.SetStoredState(emptySpot, storedBoxScale);
                }
            }
        }
    }
}
