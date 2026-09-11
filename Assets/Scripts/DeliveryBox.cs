using UnityEngine;
using Unity.Netcode;
using System;

public class DeliveryBox : GrabbableItem
{
    public NetworkVariable<ProductType> productType = new NetworkVariable<ProductType>(
        ProductType.Cereal,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> itemCount = new NetworkVariable<int>(
        4, // 4 items per box
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Event so we can update visuals when the box opens or empties
    public event Action<bool> OnBoxStateChanged;
    public event Action<int> OnItemCountChanged;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isOpen.OnValueChanged += (oldVal, newVal) => OnBoxStateChanged?.Invoke(newVal);
        itemCount.OnValueChanged += (oldVal, newVal) => OnItemCountChanged?.Invoke(newVal);
        
        // Trigger initial state locally
        OnBoxStateChanged?.Invoke(isOpen.Value);
        OnItemCountChanged?.Invoke(itemCount.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void OpenBoxServerRpc()
    {
        if (!isOpen.Value)
        {
            isOpen.Value = true;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveItemServerRpc()
    {
        if (isOpen.Value && itemCount.Value > 0)
        {
            itemCount.Value--;
            if (itemCount.Value <= 0)
            {
                // Box is empty, destroy it
                NetworkObject.Despawn(true);
            }
        }
    }
}
