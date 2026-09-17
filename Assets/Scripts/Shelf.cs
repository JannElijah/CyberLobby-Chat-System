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

    private Renderer[] renderers;
    private Color[] originalColors;

    public override void OnNetworkSpawn()
    {
        currentStock.OnValueChanged += (oldVal, newVal) => OnStockChanged?.Invoke(newVal);
        OnStockChanged?.Invoke(currentStock.Value);

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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeItemServerRpc()
    {
        // Used by Customer AI to take an item
        if (currentStock.Value > 0)
        {
            currentStock.Value--;
        }
    }
}
