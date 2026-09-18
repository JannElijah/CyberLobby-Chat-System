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
            if (senderName == "[System]" || senderName.StartsWith("To ") || senderName.EndsWith("(Whisper)")) 
            {
                PlayerNameText.text = $"<color={senderColorHex}>[{timestamp}] {senderName}</color>";
            } 
            else 
            {
                PlayerNameText.text = $"<color={senderColorHex}>[{timestamp}] {senderName}:</color>";
            }
            // Left-align text
            PlayerNameText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (MessageBodyText != null)
        {
            // Only apply custom colors to the message body if it's a system broadcast
            if (senderName == "[System]" || senderName == "[Anti-Cheat]") 
            {
                MessageBodyText.text = $"<color={senderColorHex}>{messageContent}</color>";
            }
            else 
            {
                // Normal player text uses the default TMP Text color (should be dark/readable)
                MessageBodyText.text = messageContent;
            }
            
            // Always left-align for terminal style
            MessageBodyText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (ProfileImage != null)
        {
            // Hide the avatar block since we aren't dynamically assigning sprites yet
            ProfileImage.gameObject.SetActive(false);
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
