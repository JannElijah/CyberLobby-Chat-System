using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CustomCharacterSpawner : MonoBehaviour
{
    [Tooltip("Add Capybara first (Host), then Beaver (Client)")]
    public List<GameObject> characterPrefabs;

    private void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // Subscribe to any new clients connecting (late joiners)
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnCharacter;

            // Spawn characters for all clients that are already connected!
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnCharacter(clientId);
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnCharacter;
        }
    }

    private void SpawnCharacter(ulong clientId)
    {
        if (characterPrefabs == null || characterPrefabs.Count == 0)
        {
            Debug.LogWarning("CustomCharacterSpawner: No prefabs assigned in the inspector!");
            return;
        }

        // Prevent double-spawning! If the client already has a player object, do nothing.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null) return; 
        }

        // Check if the lobby passed down a character selection for this client
        int index = 0;
        if (NetworkLobbyManager.ClientCharacterSelections.TryGetValue(clientId, out int chosenChar))
        {
            index = chosenChar;
        }
        else
        {
            // Fallback if they bypassed the lobby (e.g. testing directly in Gameplay scene)
            index = (int)(clientId % (ulong)characterPrefabs.Count);
        }

        // Safety check to ensure index is within bounds
        index = Mathf.Clamp(index, 0, characterPrefabs.Count - 1);
        GameObject prefabToSpawn = characterPrefabs[index];

        // Instantiate the object
        GameObject spawnedCharacter = Instantiate(prefabToSpawn, new Vector3(0, 2f, 0), Quaternion.identity);
        
        // Tell the network to spawn it as the official player object for this client
        spawnedCharacter.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
