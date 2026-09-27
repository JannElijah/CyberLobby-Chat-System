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

    private bool hasSubscribed = false;

    private void Update()
    {
        if (!hasSubscribed && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            hasSubscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (hasSubscribed && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        // If we are the client that got disconnected, OR if the server disconnected (which drops us)
        if (clientId == NetworkManager.Singleton.LocalClientId || clientId == NetworkManager.ServerClientId)
        {
            // Start a coroutine to handle it next frame
            StartCoroutine(DelayedDisconnectRoutine());
        }
    }

    private System.Collections.IEnumerator DelayedDisconnectRoutine()
    {
        yield return null;

        if (NetworkManager.Singleton != null)
        {
            // CRITICAL FIX: Calling Shutdown() on a client that was already kicked by the server 
            // causes a secondary deadlock. Instead, we forcefully destroy the NetworkManager GameObject.
            // It will be re-instantiated freshly when we load the offline scene anyway!
            Destroy(NetworkManager.Singleton.gameObject);
        }

        // Load the main menu/offline scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("0_OfflineScene", UnityEngine.SceneManagement.LoadSceneMode.Single);

        // Show dynamic UI popup
        ShowDisconnectPopup("Connection Lost", "The connection to the host was lost or you were disconnected. Returning to the main menu.");
    }

    private void ShowDisconnectPopup(string title, string message)
    {
        if (errorCanvasObj != null) Destroy(errorCanvasObj);

        // Load Theme Assets
        TMP_FontAsset pawmartFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");
        Sprite woodSprite = Resources.Load<Sprite>("wooden_buttons");
        Color creamColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        Color darkBrown = new Color(0.35f, 0.20f, 0.10f, 1f);

        // 1. Create Canvas
        errorCanvasObj = new GameObject("NetworkErrorCanvas");
        Canvas canvas = errorCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Ensure it's on top of everything
        errorCanvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        errorCanvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(errorCanvasObj);

        // 2. Create Background Panel (Dimmer)
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(errorCanvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.75f); 
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // 3. Create Message Box (Cream)
        GameObject boxObj = new GameObject("Box");
        boxObj.transform.SetParent(panelObj.transform, false);
        Image boxImage = boxObj.AddComponent<Image>();
        boxImage.color = creamColor; 
        
        // Add Outline to Box
        Outline boxOutline = boxObj.AddComponent<Outline>();
        boxOutline.effectColor = darkBrown;
        boxOutline.effectDistance = new Vector2(4, -4);

        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(550, 300);

        // 4. Create Title Text (Dark Brown)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(boxObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = title;
        if (pawmartFont != null) titleText.font = pawmartFont;
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = darkBrown;
        titleText.alignment = TextAlignmentOptions.Center;
        
        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = creamColor; // Light outline so dark text pops
        titleOutline.effectDistance = new Vector2(2, -2);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, -20);
        titleRect.anchoredPosition = new Vector2(0, 30);

        // 5. Create Message Text (Dark Brown)
        GameObject msgObj = new GameObject("MessageText");
        msgObj.transform.SetParent(boxObj.transform, false);
        TextMeshProUGUI msgText = msgObj.AddComponent<TextMeshProUGUI>();
        msgText.text = message;
        if (pawmartFont != null) msgText.font = pawmartFont;
        msgText.fontSize = 24;
        msgText.color = darkBrown;
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.textWrappingMode = TextWrappingModes.Normal;
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = Vector2.zero;
        msgRect.anchorMax = new Vector2(1, 0.6f);
        msgRect.sizeDelta = new Vector2(-60, 0);
        msgRect.anchoredPosition = new Vector2(0, 30);

        // 6. Create Close Button (Wooden)
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(boxObj.transform, false);
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = Color.white;
        if (woodSprite != null) {
            btnImage.sprite = woodSprite;
            btnImage.type = Image.Type.Sliced;
        } else {
            btnImage.color = new Color(0.6f, 0.4f, 0.2f, 1f); // Fallback color
        }
        Button btn = btnObj.AddComponent<Button>();
        
        // Add Button Hover Transition
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); // Slightly darker wood on hover
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1f;
        btn.colors = cb;

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = darkBrown;
        btnOutline.effectDistance = new Vector2(2, -2);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(180, 60);
        btnRect.anchoredPosition = new Vector2(0, 50);

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "OK";
        if (pawmartFont != null) btnText.font = pawmartFont;
        btnText.fontSize = 28;
        btnText.color = creamColor; // Light text on wood
        btnText.alignment = TextAlignmentOptions.Center;
        
        Outline btnTextOutline = btnTextObj.AddComponent<Outline>();
        btnTextOutline.effectColor = darkBrown;
        btnTextOutline.effectDistance = new Vector2(1, -1);

        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        btnTextRect.anchoredPosition = new Vector2(0, 5); // Slight offset for wood button graphic

        btn.onClick.AddListener(() => { Destroy(errorCanvasObj); });
    }
}
