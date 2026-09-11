using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class NetworkLobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    [Tooltip("Text to show how many players are ready")]
    public TMP_Text statusText;
    [Tooltip("Text on the Ready button to show current state")]
    public TMP_Text readyButtonText;

    // Track how many players are currently ready
    private NetworkVariable<int> playersReady = new NetworkVariable<int>(0);
    
    // Server-side list of clients who have readied up
    private List<ulong> readyClients = new List<ulong>();
    
    // Local client state
    private bool isLocalPlayerReady = false;

    private void Update()
    {
        // Update the UI text to show readiness
        if (statusText != null)
        {
            statusText.text = $"Players Ready: {playersReady.Value} / 2";
        }

        // Only the Server/Host should check for scene transition
        if (IsServer && playersReady.Value >= 2)
        {
            // Transition to gameplay scene when both players are ready
            NetworkManager.Singleton.SceneManager.LoadScene("2_GameplayScene", LoadSceneMode.Single);
            enabled = false; // Prevent multiple loads
        }
    }

    /// <summary>
    /// Call this method from the Ready button's OnClick event in the Lobby UI.
    /// </summary>
    public void OnReadyButtonClicked()
    {
        isLocalPlayerReady = !isLocalPlayerReady;

        if (readyButtonText != null)
        {
            readyButtonText.text = isLocalPlayerReady ? "Unready" : "Ready";
        }

        // Send RPC to server to update readiness
        SetReadyStateServerRpc(isLocalPlayerReady);
    }

    [Rpc(SendTo.Server)]
    private void SetReadyStateServerRpc(bool isReady, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (isReady)
        {
            if (!readyClients.Contains(clientId))
            {
                readyClients.Add(clientId);
            }
        }
        else
        {
            if (readyClients.Contains(clientId))
            {
                readyClients.Remove(clientId);
            }
        }

        // Update the NetworkVariable so all clients see the new count
        playersReady.Value = readyClients.Count;
    }
}
