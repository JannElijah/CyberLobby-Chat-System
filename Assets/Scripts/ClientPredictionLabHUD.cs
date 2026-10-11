using UnityEngine;
using Unity.Netcode;

/// <summary>
/// On-screen Lab Dashboard providing real-time toggles and comparative metrics for:
/// 1. Client Prediction Mode (ON / OFF) - Hotkey: P
/// 2. Simulated Latency (200ms RTT) - Hotkey: L
/// 3. Real-time metrics (Input response time, RTT, buffer length, reconciliation drift).
/// Part of Lab 7 Deliverables.
/// </summary>
public class ClientPredictionLabHUD : MonoBehaviour
{
    private bool showHUD = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindFirstObjectByType<ClientPredictionLabHUD>() == null)
        {
            var go = new GameObject("ClientPredictionLabHUD");
            DontDestroyOnLoad(go);
            go.AddComponent<ClientPredictionLabHUD>();
        }
    }

    private void Update()
    {
        // Only active during gameplay
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        // Hotkey P: Toggle Client-Side Prediction
        if (Input.GetKeyDown(KeyCode.P))
        {
            PlayerMovement.IsPredictionEnabled = !PlayerMovement.IsPredictionEnabled;
        }

        // Hotkey L: Toggle 200ms Simulated Latency
        if (Input.GetKeyDown(KeyCode.L))
        {
            PlayerMovement.IsSimulatedLagEnabled = !PlayerMovement.IsSimulatedLagEnabled;
        }

        // Hotkey J: Test Desync & Reconciliation
        if (Input.GetKeyDown(KeyCode.J))
        {
            TriggerDesyncTest();
        }

        // Hotkey H: Toggle HUD visibility
        if (Input.GetKeyDown(KeyCode.H))
        {
            showHUD = !showHUD;
        }
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        // Do not display in Menu, Offline, or Lobby scenes
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Menu") || currentScene.Contains("Offline") || currentScene.Contains("Lobby"))
        {
            return;
        }

        // Position neatly at Top-Left
        GUILayout.BeginArea(new Rect(20, 20, 380, 295), GUI.skin.box);

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = Color.white;

        GUILayout.Label("🎮 Lab 7: Client Prediction HUD", headerStyle);
        GUILayout.Space(6);

        // 1. Prediction Toggle Button
        bool isPred = PlayerMovement.IsPredictionEnabled;
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = isPred ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);

        string predButtonText = isPred 
            ? "Prediction Mode: [ ON (Predicted) ] (Press P)" 
            : "Prediction Mode: [ OFF (Server-Auth) ] (Press P)";

        if (GUILayout.Button(predButtonText, GUILayout.Height(32)))
        {
            PlayerMovement.IsPredictionEnabled = !PlayerMovement.IsPredictionEnabled;
        }

        GUILayout.Space(4);

        // 2. Simulated Latency Toggle Button
        bool isLag = PlayerMovement.IsSimulatedLagEnabled;
        GUI.backgroundColor = isLag ? new Color(1f, 0.6f, 0.1f) : new Color(0.7f, 0.7f, 0.7f);

        string lagButtonText = isLag
            ? "Simulated Lag (200ms RTT): [ ON ] (Press L)"
            : "Simulated Lag (200ms RTT): [ OFF ] (Press L)";

        if (GUILayout.Button(lagButtonText, GUILayout.Height(30)))
        {
            PlayerMovement.IsSimulatedLagEnabled = !PlayerMovement.IsSimulatedLagEnabled;
        }

        GUI.backgroundColor = originalColor;
        GUILayout.Space(8);

        // 3. Comparative Metrics
        GUILayout.Label("📊 Comparative Performance Metrics:", EditorBoldLabelStyle());

        string responseTimeText = isPred 
            ? "< 1 ms (Instant Prediction)" 
            : $"{PlayerMovement.LastInputResponseTimeMs:F1} ms (Round-Trip Delay)";

        GUILayout.Label($"• Input Response Time: {responseTimeText}");
        
        string activeRttText = isLag 
            ? $"{PlayerMovement.SimulatedRttMs:F0} ms (Simulated)" 
            : GetTransportRttString();
            
        GUILayout.Label($"• Connection RTT: {activeRttText}");
        GUILayout.Label($"• Buffered Commands in Queue: {PlayerMovement.LiveBufferQueueCount} frames");
        GUILayout.Label($"• Reconciliations Triggered: {PlayerMovement.TotalReconciliations}");
        GUILayout.Label($"• Last Reconciliation Drift: {PlayerMovement.LastReconciliationDrift * 1000f:F1} mm");

        GUILayout.Space(4);
        if (GUILayout.Button("Simulate Drift Bump (+0.25m) (Press J)", GUILayout.Height(24)))
        {
            TriggerDesyncTest();
        }

        GUILayout.Space(2);
        GUIStyle tipStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Italic
        };
        tipStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        GUILayout.Label("Press [H] to hide/show this panel", tipStyle);

        GUILayout.EndArea();
    }

    private void TriggerDesyncTest()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsOwner)
            {
                p.SimulateDesyncBump(0.25f);
                break;
            }
        }
    }

    private string GetTransportRttString()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            ulong rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            return $"{rtt} ms (Network)";
        }
        return "0 ms (Host)";
    }

    private GUIStyle EditorBoldLabelStyle()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1f, 0.85f, 0.4f);
        return style;
    }
}
