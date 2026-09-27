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
        // Initialize for EVERYONE so clients don't crash when predicting!
        slotOccupants = new ulong[boxSnapPoints.Length];
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
            // Find the first empty slot locally for prediction
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
                Transform predictedSpot = boxSnapPoints[emptyIndex];
                
                // CLIENT-SIDE PREDICTION: Claim the slot locally immediately!
                slotOccupants[emptyIndex] = box.NetworkObjectId; 

                // Clear hands visually, and pass true to permanently KEEP NetworkTransform OFF
                // so it doesn't rubber-band back to our 150ms-old network position!
                interactor.ClearCarriedItemLocally(true);
                box.transform.SetParent(null);

                // INSTANT SNAP LOCALLY
                box.transform.position = predictedSpot.position;
                box.transform.rotation = predictedSpot.rotation;
                box.transform.localScale = Vector3.one * storedBoxScale;
                
                var rb = box.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                // Tell the server to make it official
                StoreBoxServerRpc(box.NetworkObjectId, interactor.NetworkObjectId, emptyIndex);
            }
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
                        slotOccupants[i] = 0; // Empty the slot on server
                        
                        // Tell ONLY the specific client who clicked it to pick it up!
                        ClientRpcParams clientRpcParams = new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerObj.OwnerClientId } }
                        };
                        PickupBoxClientRpc(boxId, i, clientRpcParams);
                        
                        // Also tell everyone else that the slot is now empty
                        ClearSlotClientRpc(i);
                        return;
                    }
                }
                else
                {
                    slotOccupants[i] = 0;
                }
            }
        }
    }

    [ClientRpc]
    private void ClearSlotClientRpc(int slotIndex)
    {
        slotOccupants[slotIndex] = 0;
    }

    [ClientRpc]
    private void PickupBoxClientRpc(ulong boxNetworkId, int slotIndex, ClientRpcParams clientRpcParams = default)
    {
        slotOccupants[slotIndex] = 0; // Empty it locally too
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxNetworkId, out NetworkObject boxObj))
        {
            var grabbable = boxObj.GetComponent<GrabbableItem>();
            var interactor = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
            
            if (grabbable != null && interactor != null)
            {
                grabbable.Interact(interactor); // Force the local player to pick it up!
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StoreBoxServerRpc(ulong boxNetworkId, ulong playerNetworkId, int requestedSlotIndex)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxNetworkId, out NetworkObject boxObj))
        {
            // Verify the slot is still empty (Server Authority!)
            if (slotOccupants[requestedSlotIndex] == 0 || slotOccupants[requestedSlotIndex] == boxNetworkId)
            {
                Transform emptySpot = boxSnapPoints[requestedSlotIndex];
                slotOccupants[requestedSlotIndex] = boxNetworkId; // Claim the slot on Server

                // Securely clear the player's hands on the server
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
                {
                    var interactor = playerObj.GetComponent<PlayerInteraction>();
                    if (interactor != null) interactor.ForceClearHandsServer();
                }

                // Unparent the box on the network
                boxObj.TryRemoveParent();
                
                var grabbable = boxObj.GetComponent<GrabbableItem>();
                if (grabbable != null)
                {
                    grabbable.SetStoredState(emptySpot, storedBoxScale);
                }

                // Snap the position instantly on the server
                boxObj.transform.position = emptySpot.position;
                boxObj.transform.rotation = emptySpot.rotation;
                
                // Tell other clients to instantly snap it too (avoids slow rubber-banding interpolation)
                SnapBoxClientRpc(boxNetworkId, emptySpot.position, emptySpot.rotation, requestedSlotIndex);
            }
        }
    }

    [ClientRpc]
    private void SnapBoxClientRpc(ulong boxNetworkId, Vector3 targetPosition, Quaternion targetRotation, int slotIndex)
    {
        slotOccupants[slotIndex] = boxNetworkId;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxNetworkId, out NetworkObject boxObj))
        {
            // CRITICAL FIX: If another player was holding this box on our screen, we must forcefully clear 
            // their visual hands and pass keepNetworkTransformDisabled=true, otherwise the moment the Server 
            // clears their hands, the Box will rubber-band back to the floor!
            var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p.currentlyCarriedObject != null && p.currentlyCarriedObject.NetworkObjectId == boxNetworkId)
                {
                    p.ClearCarriedItemLocally(keepNetworkTransformDisabled: true);
                }
            }

            // Instantly snap for all remote observing players
            boxObj.transform.position = targetPosition;
            boxObj.transform.rotation = targetRotation;
            boxObj.transform.localScale = Vector3.one * storedBoxScale;
            
            // Ensure NetworkTransform is unconditionally disabled so it never fights the shelf position
            var grabbable = boxObj.GetComponent<GrabbableItem>();
            if (grabbable != null) grabbable.isStored = true;

            var nt = boxObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.enabled = false;
        }
    }
}
