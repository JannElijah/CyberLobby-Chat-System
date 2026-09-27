using UnityEngine;
using Unity.Netcode;

public class DeliveryManager : NetworkBehaviour
{
    public GameObject deliveryBoxPrefab;
    
    [Header("Loading Docks (Match Size)")]
    public Transform[] loadingDocks;
    public Animator[] garageDoorAnimators;
    
    [Header("Delivery Animation")]
    public Vector3 throwForce = new Vector3(-10f, 5f, 0f); // Adjust this to throw into the store
    public float timeBetweenThrows = 0.5f;

    public int boxesPerMorning = 3;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameLoopManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            
            if (GameLoopManager.Instance.CurrentPhase.Value == GamePhase.MorningPrep)
            {
                StartCoroutine(DeliverySequence());
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
            StartCoroutine(DeliverySequence());
        }
    }

    private void Update()
    {
        // Debug command: Press ',' to spawn a delivery instantly (Server/Host only)
        if (IsServer && Input.GetKeyDown(KeyCode.Comma))
        {
            StartCoroutine(DeliverySequence());
        }
    }

    private System.Collections.IEnumerator DeliverySequence()
    {
        if (loadingDocks.Length == 0) yield break;

        // Randomly pick which garage door to use for this delivery!
        int dockIndex = Random.Range(0, loadingDocks.Length);
        Transform selectedDock = loadingDocks[dockIndex];
        Animator selectedDoor = (garageDoorAnimators.Length > dockIndex) ? garageDoorAnimators[dockIndex] : null;

        // 1. Open the selected garage door
        if (selectedDoor != null)
        {
            selectedDoor.SetTrigger("Open");
        }
        
        // Wait a second for the door to roll up
        yield return new WaitForSeconds(1.5f);

        // 2. Throw the boxes in one by one
        for (int i = 0; i < boxesPerMorning; i++)
        {
            GameObject boxInstance = Instantiate(deliveryBoxPrefab, selectedDock.position, Quaternion.identity);
            
            var netObj = boxInstance.GetComponent<NetworkObject>();
            netObj.Spawn();

            var boxScript = boxInstance.GetComponent<DeliveryBox>();
            if (boxScript != null)
            {
                boxScript.productType.Value = (ProductType)Random.Range(0, System.Enum.GetValues(typeof(ProductType)).Length);
            }

            // Apply the physics force to throw it into the store!
            Rigidbody rb = boxInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddTorque(new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f)), ForceMode.Impulse);
                Vector3 randomThrow = throwForce + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), Random.Range(-1f, 1f));
                rb.AddForce(randomThrow, ForceMode.Impulse);
            }

            yield return new WaitForSeconds(timeBetweenThrows);
        }

        // Wait a couple of seconds for the boxes to settle
        yield return new WaitForSeconds(3f);

        // 3. Close the garage door
        if (selectedDoor != null)
        {
            selectedDoor.SetTrigger("Close");
        }
    }
}
