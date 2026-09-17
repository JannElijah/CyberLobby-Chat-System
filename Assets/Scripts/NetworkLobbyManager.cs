using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using Unity.Collections;
using System;

public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public int CharacterId; // 0 for Capybara, 1 for Beaver
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref CharacterId);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerState other)
    {
        return ClientId == other.ClientId && 
               PlayerName == other.PlayerName && 
               CharacterId == other.CharacterId && 
               IsReady == other.IsReady;
    }
}

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("Center UI References")]
    [Tooltip("Text to show how many players are ready")]
    public TMP_Text statusText;
    [Tooltip("Text on the Ready button to show current state")]
    public TMP_Text readyButtonText;

    private Coroutine gameStartCoroutine;
    private bool isCountingDown = false;

    // This dictionary will persist character choices for the CustomCharacterSpawner
    public static Dictionary<ulong, int> ClientCharacterSelections = new Dictionary<ulong, int>();

    // Synchronized list of players in the lobby
    public NetworkList<LobbyPlayerState> LobbyPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        LobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            
            // Add Host immediately
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
        }

        LobbyPlayers.OnListChanged += HandleLobbyPlayersStateChanged;

        // Send local player data to server once spawned
        string playerName = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
        int defaultCharacter = (int)NetworkManager.Singleton.LocalClientId % 2; // Host=0(Capybara), Client=1(Beaver)
        UpdatePlayerStateServerRpc(NetworkManager.Singleton.LocalClientId, playerName, defaultCharacter, false);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
        LobbyPlayers.OnListChanged -= HandleLobbyPlayersStateChanged;
    }

    private void HandleClientConnected(ulong clientId)
    {
        // Check if the RPC already added them
        foreach (var player in LobbyPlayers)
        {
            if (player.ClientId == clientId) return;
        }

        // Add default state for new client
        LobbyPlayers.Add(new LobbyPlayerState 
        { 
            ClientId = clientId, 
            PlayerName = "Joining...", 
            CharacterId = (int)clientId % 2, 
            IsReady = false 
        });
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                LobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdatePlayerStateServerRpc(ulong clientId, string playerName, int characterId, bool isReady)
    {
        bool found = false;
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                LobbyPlayers[i] = new LobbyPlayerState
                {
                    ClientId = clientId,
                    PlayerName = playerName,
                    CharacterId = characterId,
                    IsReady = isReady
                };
                found = true;
                CheckGameStart();
                break;
            }
        }

        if (!found)
        {
            LobbyPlayers.Add(new LobbyPlayerState
            {
                ClientId = clientId,
                PlayerName = playerName,
                CharacterId = characterId,
                IsReady = isReady
            });
            CheckGameStart();
        }
    }

    private void CheckGameStart()
    {
        int readyCount = 0;
        foreach (var player in LobbyPlayers)
        {
            if (player.IsReady) readyCount++;
        }

        // Wait until both players are connected and ready
        if (readyCount == 2 && LobbyPlayers.Count == 2)
        {
            // Save choices to static dictionary for the Spawner to use
            ClientCharacterSelections.Clear();
            foreach (var player in LobbyPlayers)
            {
                ClientCharacterSelections[player.ClientId] = player.CharacterId;
            }

            if (gameStartCoroutine == null)
            {
                gameStartCoroutine = StartCoroutine(GameStartCountdownRoutine());
            }
        }
        else
        {
            if (gameStartCoroutine != null)
            {
                StopCoroutine(gameStartCoroutine);
                gameStartCoroutine = null;
                UpdateCountdownUIClientRpc(-1);
            }
        }
    }

    private System.Collections.IEnumerator GameStartCountdownRoutine()
    {
        for (int i = 3; i > 0; i--)
        {
            UpdateCountdownUIClientRpc(i);
            yield return new WaitForSeconds(1f);
        }

        UpdateCountdownUIClientRpc(0);
        NetworkManager.Singleton.SceneManager.LoadScene("2_GameplayScene", LoadSceneMode.Single);
        enabled = false;
    }

    [ClientRpc]
    private void UpdateCountdownUIClientRpc(int count)
    {
        if (count > 0)
        {
            isCountingDown = true;
            if (statusText != null) statusText.text = $"Game Starting in {count}...";
        }
        else if (count == 0)
        {
            isCountingDown = true;
            if (statusText != null) statusText.text = "Loading...";
        }
        else
        {
            isCountingDown = false;
            // Force a UI refresh of the ready text
            HandleLobbyPlayersStateChanged(new NetworkListEvent<LobbyPlayerState>());
        }
    }

    private void HandleLobbyPlayersStateChanged(NetworkListEvent<LobbyPlayerState> changeEvent)
    {
        int readyCount = 0;
        bool localIsReady = false;

        foreach (var player in LobbyPlayers)
        {
            if (player.IsReady) readyCount++;
            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                localIsReady = player.IsReady;
            }
        }

        if (statusText != null && !isCountingDown)
        {
            statusText.text = $"PLAYERS READY:\n{readyCount} / {LobbyPlayers.Count}";
        }

        if (readyButtonText != null)
        {
            readyButtonText.text = localIsReady ? "UNREADY" : "READY";
        }
    }

    /// <summary>
    /// Call this method from the Ready button's OnClick event in the Lobby UI.
    /// </summary>
    public void OnReadyButtonClicked()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;
        foreach (var player in LobbyPlayers)
        {
            if (player.ClientId == localId)
            {
                UpdatePlayerStateServerRpc(localId, player.PlayerName.ToString(), player.CharacterId, !player.IsReady);
                break;
            }
        }
    }

    /// <summary>
    /// Call this method from the "Toggle Character" button in the Lobby UI.
    /// </summary>
    public void OnToggleCharacterClicked()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;
        foreach (var player in LobbyPlayers)
        {
            if (player.ClientId == localId)
            {
                int nextChar = (player.CharacterId + 1) % 2; // Swap between 0 and 1
                UpdatePlayerStateServerRpc(localId, player.PlayerName.ToString(), nextChar, player.IsReady);
                break;
            }
        }
    }
}
