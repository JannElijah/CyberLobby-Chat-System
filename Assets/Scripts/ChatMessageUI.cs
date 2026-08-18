using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        if (PlayerNameText != null)
        {
            // Apply the color via rich text to the TMP component
            PlayerNameText.text = $"<color={senderColorHex}>{senderName}</color>";
            PlayerNameText.alignment = isLocalPlayer ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        }

        if (MessageBodyText != null)
        {
            MessageBodyText.text = messageContent;
            MessageBodyText.alignment = isLocalPlayer ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        }

        if (layoutGroup != null)
        {
            // Pushes the layout to the right or left
            layoutGroup.childAlignment = isLocalPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            
            // Reverses the arrangement so the avatar (if any) swaps to the correct side!
            layoutGroup.reverseArrangement = isLocalPlayer; 
        }

        if (ProfileImage != null)
        {
            if (senderName == "[System]")
            {
                // Hide the profile icon entirely for System messages
                ProfileImage.gameObject.SetActive(false);
            }
            else
            {
                ProfileImage.gameObject.SetActive(true);
                // Color the avatar to match the player's name color
                if (ColorUtility.TryParseHtmlString(senderColorHex, out Color parsedColor))
                {
                    ProfileImage.color = parsedColor;
                }
            }
        }
    }
}
