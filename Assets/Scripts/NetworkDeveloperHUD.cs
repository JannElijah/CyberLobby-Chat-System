using Unity.Netcode;
using UnityEngine;

public class NetworkDeveloperHUD : MonoBehaviour
{
    void OnGUI()
    {
        // This creates a small UI area in the top-left corner of the screen
        GUILayout.BeginArea(new Rect(10, 10, 150, 100));

        // NEW FIX: Only try to draw the buttons IF the Network Manager actually exists and is awake!
        if (NetworkManager.Singleton != null)
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
                if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            }
        }

        GUILayout.EndArea(); // Now it will always reach this line safely
    }
}