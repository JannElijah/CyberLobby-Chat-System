using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using TMPro;

public class OfflineSceneUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField ipAddressInputField;

    private void Start()
    {
        // Load saved username if it exists
        if (usernameInputField != null && PlayerPrefs.HasKey("PlayerName"))
        {
            usernameInputField.text = PlayerPrefs.GetString("PlayerName");
        }
        
        // Load last used IP or default to localhost
        if (ipAddressInputField != null && PlayerPrefs.HasKey("LastIP"))
        {
            ipAddressInputField.text = PlayerPrefs.GetString("LastIP");
        }
        else if (ipAddressInputField != null)
        {
            ipAddressInputField.text = "127.0.0.1";
        }
    }

    public void HostGame()
    {
        SaveSettings();
        
        if (NetworkManager.Singleton.StartHost())
        {
            // The server/host tells all clients to load the Lobby Scene together
            NetworkManager.Singleton.SceneManager.LoadScene("1_LobbyScene", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("Failed to start host.");
        }
    }

    public void JoinGame()
    {
        SaveSettings();

        // Configure the UnityTransport to connect to the provided IP Address
        string ipAddress = ipAddressInputField != null ? ipAddressInputField.text : "127.0.0.1";
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "127.0.0.1";
        }
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ipAddress;
        }
        else
        {
            Debug.LogWarning("No UnityTransport found on NetworkManager!");
        }

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log("Client started. Connecting to " + ipAddress + "...");
        }
        else
        {
            Debug.LogError("Failed to start client.");
        }
    }

    private void SaveSettings()
    {
        // Save Username
        if (usernameInputField != null && !string.IsNullOrEmpty(usernameInputField.text))
        {
            PlayerPrefs.SetString("PlayerName", usernameInputField.text);
        }
        else
        {
            // Generate a random name if empty
            PlayerPrefs.SetString("PlayerName", "Player" + Random.Range(1000, 9999));
        }

        // Save IP Address
        if (ipAddressInputField != null && !string.IsNullOrEmpty(ipAddressInputField.text))
        {
            PlayerPrefs.SetString("LastIP", ipAddressInputField.text);
        }
        
        PlayerPrefs.Save();
    }
}
