using UnityEngine;
using Unity.Netcode;

public class DeliveryManager : NetworkBehaviour
{
    public GameObject deliveryBoxPrefab;
    public Transform loadingDock;

    public int boxesPerMorning = 3;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameLoopManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            
            // If already in morning prep (e.g. late sub), though server spawns it first
            if (GameLoopManager.Instance.CurrentPhase.Value == GamePhase.MorningPrep)
            {
                SpawnMorningDeliveries();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(GamePhase newPhase)
    {
        if (newPhase == GamePhase.MorningPrep)
        {
            SpawnMorningDeliveries();
        }
    }

    private void SpawnMorningDeliveries()
    {
        for (int i = 0; i < boxesPerMorning; i++)
        {
            // Add a little random offset so they don't stack perfectly inside each other
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0.5f * i, Random.Range(-1f, 1f));
            GameObject boxInstance = Instantiate(deliveryBoxPrefab, loadingDock.position + offset, Quaternion.identity);
            
            var netObj = boxInstance.GetComponent<NetworkObject>();
            netObj.Spawn();

            var boxScript = boxInstance.GetComponent<DeliveryBox>();
            if (boxScript != null)
            {
                // Randomly assign a product type
                boxScript.productType.Value = (ProductType)Random.Range(0, System.Enum.GetValues(typeof(ProductType)).Length);
            }
        }
    }
}
