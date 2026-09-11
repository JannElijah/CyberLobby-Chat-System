using UnityEngine;
using Unity.Netcode;

public class UnpackingStation : NetworkBehaviour, IInteractable
{
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
