using UnityEngine;
using Unity.Netcode;
using TMPro;

public class NetworkPingDisplay : MonoBehaviour
{
    private TextMeshProUGUI pingText;

    // This magically injects the UI into the game automatically without needing a prefab!
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var go = new GameObject("NetworkPingDisplay");
        DontDestroyOnLoad(go);
        go.AddComponent<NetworkPingDisplay>();
    }

    private void Start()
    {
        // 1. Create a Screen Space Canvas
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Always render on top of everything else

        // 2. Create the Text element
        var textGo = new GameObject("PingText");
        textGo.transform.SetParent(transform, false);
        pingText = textGo.AddComponent<TextMeshProUGUI>();
        
        // 3. Anchor it perfectly to the Top-Right corner
        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -20); // 20px padding from the edge
        rect.sizeDelta = new Vector2(300, 50);

        pingText.alignment = TextAlignmentOptions.TopRight;
        pingText.fontSize = 28;
        pingText.fontStyle = FontStyles.Bold;
        
        // Use the Pawmart font if it exists, otherwise it defaults
        TMP_FontAsset pawmartFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");
        if (pawmartFont != null) pingText.font = pawmartFont;
    }

    private void Update()
    {
        if (pingText == null || NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            // We are the Client, ask the transport layer for our round-trip time (RTT) to the server
            ulong serverId = NetworkManager.ServerClientId;
            ulong rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(serverId);
            
            pingText.text = $"Ping: {rtt} ms";
            
            // Color code the ping dynamically!
            if (rtt < 80) pingText.color = new Color(0.4f, 1f, 0.4f); // Green
            else if (rtt < 150) pingText.color = Color.yellow; // Yellow
            else pingText.color = new Color(1f, 0.4f, 0.4f); // Red
        }
        else if (NetworkManager.Singleton.IsHost)
        {
            // Host is the server, so ping is practically zero
            pingText.text = "Ping: 0 ms (Host)";
            pingText.color = new Color(0.4f, 1f, 0.4f);
        }
        else
        {
            // Offline menu
            pingText.text = "";
        }
    }
}
