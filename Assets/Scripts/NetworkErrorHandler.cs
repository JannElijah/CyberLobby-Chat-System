using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class NetworkErrorHandler : MonoBehaviour
{
    private static NetworkErrorHandler _instance;
    private GameObject errorCanvasObj;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("NetworkErrorHandler");
            go.AddComponent<NetworkErrorHandler>();
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        // If we are the client that got disconnected, OR if the server disconnected (which drops us)
        if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
        {
            // Shutdown the network transport cleanly
            NetworkManager.Singleton.Shutdown();

            // Load the main menu/offline scene
            SceneManager.LoadScene("0_OfflineScene", LoadSceneMode.Single);

            // Show dynamic UI popup
            ShowDisconnectPopup("Connection Lost", "The connection to the host was lost or you were disconnected. Returning to the main menu.");
        }
    }

    private void ShowDisconnectPopup(string title, string message)
    {
        if (errorCanvasObj != null) Destroy(errorCanvasObj);

        // 1. Create Canvas
        errorCanvasObj = new GameObject("NetworkErrorCanvas");
        Canvas canvas = errorCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Ensure it's on top of everything
        errorCanvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        errorCanvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(errorCanvasObj);

        // 2. Create Background Panel
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(errorCanvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.85f); // Dark semi-transparent
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // 3. Create Message Box
        GameObject boxObj = new GameObject("Box");
        boxObj.transform.SetParent(panelObj.transform, false);
        Image boxImage = boxObj.AddComponent<Image>();
        boxImage.color = new Color(0.15f, 0.15f, 0.15f, 1f); // Dark grey
        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(500, 250);

        // 4. Create Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(boxObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.red;
        titleText.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, -20);
        titleRect.anchoredPosition = new Vector2(0, 10);

        // 5. Create Message Text
        GameObject msgObj = new GameObject("MessageText");
        msgObj.transform.SetParent(boxObj.transform, false);
        TextMeshProUGUI msgText = msgObj.AddComponent<TextMeshProUGUI>();
        msgText.text = message;
        msgText.fontSize = 24;
        msgText.color = Color.white;
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.enableWordWrapping = true;
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = Vector2.zero;
        msgRect.anchorMax = new Vector2(1, 0.6f);
        msgRect.sizeDelta = new Vector2(-40, 0);

        // 6. Create Close Button
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(boxObj.transform, false);
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Button btn = btnObj.AddComponent<Button>();
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(150, 50);
        btnRect.anchoredPosition = new Vector2(0, 40);

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "OK";
        btnText.fontSize = 24;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(() => { Destroy(errorCanvasObj); });
    }
}
