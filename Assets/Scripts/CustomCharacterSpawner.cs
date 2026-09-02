using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CustomCharacterSpawner : MonoBehaviour
{
    [Tooltip("Add Capybara first (Host), then Beaver (Client)")]
    public List<GameObject> characterPrefabs;

    private void Start()
    {
        // Wait for the server to start to hook into the connection events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnCharacter;
        }
    }

    private void OnServerStarted()
    {
        // Subscribe to any new clients connecting
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnCharacter;

        // The host connects immediately and might miss the callback, so spawn their character right away!
        SpawnCharacter(NetworkManager.Singleton.LocalClientId);
    }

    private void SpawnCharacter(ulong clientId)
    {
        if (characterPrefabs == null || characterPrefabs.Count == 0)
        {
            Debug.LogWarning("CustomCharacterSpawner: No prefabs assigned in the inspector!");
            return;
        }

        // Pick character based on client ID (0 = first prefab, 1 = second prefab)
        // If 3 people join, it loops back to the first prefab!
        int index = (int)(clientId % (ulong)characterPrefabs.Count);
        GameObject prefabToSpawn = characterPrefabs[index];

        // Instantiate the object
        GameObject spawnedCharacter = Instantiate(prefabToSpawn, new Vector3(0, 2f, 0), Quaternion.identity);
        
        // Tell the network to spawn it as the official player object for this client
        spawnedCharacter.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
