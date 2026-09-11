using UnityEngine;
using Unity.Netcode;
using System;

public class Shelf : NetworkBehaviour, IInteractable
{
    [Header("Shelf Settings")]
    public ProductType acceptedProductType;
    public int maxStock = 10;

    public NetworkVariable<int> currentStock = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<int> OnStockChanged;

    public override void OnNetworkSpawn()
    {
        currentStock.OnValueChanged += (oldVal, newVal) => OnStockChanged?.Invoke(newVal);
        OnStockChanged?.Invoke(currentStock.Value);
    }

    public void Interact(PlayerInteraction interactor)
    {
        var item = interactor.GetCarriedItem();
        
        if (item is DeliveryBox box)
        {
            // If the box is open, has items, and matches the product type
            if (box.isOpen.Value && box.itemCount.Value > 0 && box.productType.Value == acceptedProductType)
            {
                // We have room on the shelf
                if (currentStock.Value < maxStock)
                {
                    StockShelfServerRpc(box.NetworkObjectId);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StockShelfServerRpc(ulong boxNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(boxNetworkId, out NetworkObject boxObj))
        {
            var box = boxObj.GetComponent<DeliveryBox>();
            if (box != null && box.isOpen.Value && box.itemCount.Value > 0 && currentStock.Value < maxStock)
            {
                box.RemoveItemServerRpc();
                currentStock.Value++;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeItemServerRpc()
    {
        // Used by Customer AI to take an item
        if (currentStock.Value > 0)
        {
            currentStock.Value--;
        }
    }
}
