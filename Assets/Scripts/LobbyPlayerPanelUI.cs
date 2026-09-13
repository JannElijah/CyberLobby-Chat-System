using UnityEngine;
using TMPro;

public class LobbyPlayerPanelUI : MonoBehaviour
{
    [Tooltip("0 for the Host (Left panel), 1 for the Client (Right panel)")]
    public int playerIndex = 0;

    [Header("UI Elements")]
    public TMP_Text playerNameText;
    public TMP_Text characterSelectionText;
    public TMP_Text readyStatusText;
    
    [Tooltip("Optional: A parent GameObject containing the player details, to hide when no one is in this slot")]
    public GameObject activePlayerVisual;
    [Tooltip("Optional: A 'Waiting for Player...' text object to show when this slot is empty")]
    public GameObject waitingForPlayerVisual;

    [Header("3D Character Models")]
    [Tooltip("Drag the actual 3D model from your scene here")]
    public GameObject capybaraModel;
    [Tooltip("Drag the actual 3D model from your scene here")]
    public GameObject beaverModel;

    private void Update()
    {
        if (NetworkLobbyManager.Instance == null || NetworkLobbyManager.Instance.LobbyPlayers == null)
            return;

        // Check if there is a player connected at this index
        if (playerIndex < NetworkLobbyManager.Instance.LobbyPlayers.Count)
        {
            // Player exists in this slot
            if (waitingForPlayerVisual != null) waitingForPlayerVisual.SetActive(false);
            if (activePlayerVisual != null) activePlayerVisual.SetActive(true);

            LobbyPlayerState state = NetworkLobbyManager.Instance.LobbyPlayers[playerIndex];

            if (playerNameText != null)
            {
                playerNameText.text = state.PlayerName.ToString();
            }

            if (characterSelectionText != null)
            {
                characterSelectionText.text = state.CharacterId == 0 ? "Capybara" : "Beaver";
            }

            if (readyStatusText != null)
            {
                readyStatusText.text = state.IsReady ? "READY!" : "Not Ready";
                readyStatusText.color = state.IsReady ? Color.green : Color.red;
            }

            // Toggle 3D Models
            if (capybaraModel != null) capybaraModel.SetActive(state.CharacterId == 0);
            if (beaverModel != null) beaverModel.SetActive(state.CharacterId == 1);
        }
        else
        {
            // Slot is empty
            if (waitingForPlayerVisual != null) waitingForPlayerVisual.SetActive(true);
            if (activePlayerVisual != null) activePlayerVisual.SetActive(false);

            // Hide 3D Models when slot is empty
            if (capybaraModel != null) capybaraModel.SetActive(false);
            if (beaverModel != null) beaverModel.SetActive(false);
        }
    }
}
