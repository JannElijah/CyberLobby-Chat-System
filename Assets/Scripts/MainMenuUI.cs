using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void HostGame()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            // Only the server/host can trigger a networked scene load
            NetworkManager.Singleton.SceneManager.LoadScene("1_LobbyScene", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("Failed to start host.");
        }
    }

    public void JoinGame()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log("Client started. Waiting for connection...");
        }
        else
        {
            Debug.LogError("Failed to start client.");
        }
    }
}
