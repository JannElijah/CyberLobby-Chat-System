using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class ChatMessageUI : MonoBehaviour
{
    public TMP_Text PlayerNameText;
    public TMP_Text MessageBodyText;
    public Image ProfileImage;
    
    [Header("Optional Alignment Settings")]
    public HorizontalLayoutGroup layoutGroup;

    /// <summary>
    /// Sets up the message UI with the provided sender name and message content.
    /// It automatically applies the senderColorHex to the name if provided.
    /// </summary>
    public void SetupMessage(string senderName, string messageContent, string senderColorHex = "#FFFFFF", bool isLocalPlayer = false)
    {
        // Add timestamp
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");

        if (PlayerNameText != null)
        {
            // Format to look like a terminal prompt
            if (senderName == "[System]" || senderName.StartsWith("To ") || senderName.EndsWith("(Whisper)")) 
            {
                PlayerNameText.text = $"<color={senderColorHex}>[{timestamp}] {senderName}</color>";
            } 
            else 
            {
                PlayerNameText.text = $"<color={senderColorHex}>[{timestamp}] {senderName}@neon-os:~$</color>";
            }
            // Always left-align for terminal style
            PlayerNameText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (MessageBodyText != null)
        {
            // Apply color to the message body text as well
            MessageBodyText.text = $"<color={senderColorHex}>{messageContent}</color>";
            // Always left-align for terminal style
            MessageBodyText.alignment = TextAlignmentOptions.TopLeft;
            
            if (senderName == "[System]")
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(TypewriterEffect());
                }
            }
        }

        if (layoutGroup != null)
        {
            // Always align to the left
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.reverseArrangement = false; 
        }

        if (ProfileImage != null)
        {
            // Hide the avatar block completely for a true terminal aesthetic
            ProfileImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator TypewriterEffect()
    {
        if (MessageBodyText == null) yield break;
        
        MessageBodyText.maxVisibleCharacters = 0;
        
        // Wait for TMP to parse the text layout
        yield return null;
        
        int totalChars = MessageBodyText.textInfo.characterCount;

        for (int i = 0; i <= totalChars; i++)
        {
            MessageBodyText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(0.01f);
        }
    }

    void Start()
    {
        StartCoroutine(PopInAnimation());
    }

    private IEnumerator PopInAnimation()
    {
        float duration = 0.35f;
        float time = 0f;
        
        transform.localScale = Vector3.zero;

        // "Ease Out Back" formula variables for a bouncy pop
        float c1 = 1.70158f;
        float c3 = c1 + 1f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            
            // Ease out back math
            float curve = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            
            // Prevent going into negative scale
            if (curve < 0) curve = 0;

            transform.localScale = new Vector3(curve, curve, curve);
            yield return null;
        }
        
        transform.localScale = Vector3.one;
    }
}
