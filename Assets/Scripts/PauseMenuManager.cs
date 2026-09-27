using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    private GameObject pauseCanvasObj;
    private bool isPaused = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject go = new GameObject("PauseMenuManager");
        go.AddComponent<PauseMenuManager>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        // Only allow opening the pause menu if we are in an active network session
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePauseMenu();
            }
        }
        else if (isPaused)
        {
            // Close it forcefully if the network drops while it's open
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            ShowPauseMenu();
        }
        else
        {
            if (pauseCanvasObj != null) Destroy(pauseCanvasObj);
        }
    }

    private void ShowPauseMenu()
    {
        if (pauseCanvasObj != null) Destroy(pauseCanvasObj);

        // Load Theme Assets
        TMP_FontAsset pawmartFont = Resources.Load<TMP_FontAsset>("LilitaOne-Regular SDF");
        Sprite woodSprite = Resources.Load<Sprite>("wooden_buttons");
        Color creamColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        Color darkBrown = new Color(0.35f, 0.20f, 0.10f, 1f);

        // 1. Create Canvas
        pauseCanvasObj = new GameObject("PauseMenuCanvas");
        Canvas canvas = pauseCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998; // Just below the disconnect popup (9999)
        pauseCanvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        pauseCanvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(pauseCanvasObj);

        // 2. Create Background Panel (Dimmer)
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(pauseCanvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.65f); 
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // 3. Create Menu Box (Cream)
        GameObject boxObj = new GameObject("Box");
        boxObj.transform.SetParent(panelObj.transform, false);
        Image boxImage = boxObj.AddComponent<Image>();
        boxImage.color = creamColor; 
        
        Outline boxOutline = boxObj.AddComponent<Outline>();
        boxOutline.effectColor = darkBrown;
        boxOutline.effectDistance = new Vector2(4, -4);

        RectTransform boxRect = boxObj.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(400, 350);

        // 4. Create Title Text (Dark Brown)
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(boxObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "PAUSED";
        if (pawmartFont != null) titleText.font = pawmartFont;
        titleText.fontSize = 50;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = darkBrown;
        titleText.alignment = TextAlignmentOptions.Center;
        
        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = creamColor;
        titleOutline.effectDistance = new Vector2(2, -2);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, 100);
        titleRect.anchoredPosition = new Vector2(0, -60);

        // 5. Create Resume Button
        CreateButton(boxObj.transform, "Resume", new Vector2(0, 20), woodSprite, pawmartFont, creamColor, darkBrown, () => { TogglePauseMenu(); });

        // 6. Create Disconnect Button
        CreateButton(boxObj.transform, "Disconnect", new Vector2(0, -70), woodSprite, pawmartFont, creamColor, darkBrown, () => {
            TogglePauseMenu();
            StartCoroutine(GracefulDisconnectRoutine());
        });
    }

    private System.Collections.IEnumerator GracefulDisconnectRoutine()
    {
        if (NetworkManager.Singleton != null) 
        {
            if (NetworkManager.Singleton.IsHost)
            {
                // Kick all clients gracefully so they get the disconnect popup instantly
                var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
                foreach (var clientId in connectedClients)
                {
                    if (clientId != NetworkManager.Singleton.LocalClientId)
                    {
                        NetworkManager.Singleton.DisconnectClient(clientId);
                    }
                }
                
                // Wait for the transport layer to actually send the packets over the network
                yield return new WaitForSeconds(0.5f);
            }
            
            NetworkManager.Singleton.Shutdown();
        }
        
        SceneManager.LoadScene("0_OfflineScene", LoadSceneMode.Single);
    }

    private void CreateButton(Transform parent, string text, Vector2 pos, Sprite sprite, TMP_FontAsset font, Color textColor, Color outlineColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(text + "Button");
        btnObj.transform.SetParent(parent, false);
        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = Color.white;
        if (sprite != null) {
            btnImage.sprite = sprite;
            btnImage.type = Image.Type.Sliced;
        } else {
            btnImage.color = new Color(0.6f, 0.4f, 0.2f, 1f); // Fallback brown
        }
        
        Button btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); 
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1f;
        btn.colors = cb;

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = outlineColor;
        btnOutline.effectDistance = new Vector2(2, -2);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(250, 60);
        btnRect.anchoredPosition = pos;

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = text;
        if (font != null) btnText.font = font;
        btnText.fontSize = 28;
        btnText.color = textColor; 
        btnText.alignment = TextAlignmentOptions.Center;
        
        Outline btnTextOutline = btnTextObj.AddComponent<Outline>();
        btnTextOutline.effectColor = outlineColor;
        btnTextOutline.effectDistance = new Vector2(1, -1);

        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;
        btnTextRect.anchoredPosition = new Vector2(0, 5); // Offset for wood graphic

        btn.onClick.AddListener(onClick);
    }
}
