using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro; 
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

public class GlobalNetworkChatManager : NetworkBehaviour
{
    public TMP_InputField ChatInputField;
    public GameObject MessagePrefab;
    public Transform ChatContentParent;
    public Button SendTextButton;
    public ScrollRect ChatScrollRect;

    [Header("Security & Spam Prevention")]
    public float messageCooldown = 1.5f; 
    public int maxMessageLength = 200;
    private float lastSendTime = -100f;
    
    [Header("Chat Settings")]
    public int maxChatLines = 50;
    private Queue<GameObject> chatHistory = new Queue<GameObject>();

    private string[] bannedWords = { "spam", "hack", "cheat", "fuck", "bitch", "dumbass", "shit", "asshole" , "nigga", "faggot", "nazi", "kys", "kill yourself", "nigger", "penis", "vagina", "cock", "pussy", "fuckyou", "motherfucker", "Retard", "Retarded", "fucker", "hell" };
    
    private string playerName;
    private Dictionary<ulong, string> clientHandles = new Dictionary<ulong, string>();

    // Command History
    private List<string> sentHistory = new List<string>();
    private int historyIndex = -1;

    [Header("Advanced Features")]
    public TMP_Text TypingIndicatorText;
    private Dictionary<ulong, string> clientColors = new Dictionary<ulong, string>();
    private string[] neonColors = { "#00FFFF", "#FFFF00", "#FF6600", "#00FF66" };
    private Dictionary<ulong, float> typingClients = new Dictionary<ulong, float>();
    private bool isTypingLocally = false;

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            if (IsServer)
            {
                playerName = "[ROOT] Admin";
            }
            else
            {
                playerName = "User_" + UnityEngine.Random.Range(100, 1000).ToString();
            }
            AddMessageToDisplay("[System]", $"You have been assigned the handle: {playerName}", "#00AAFF");
            DeclareHandleServerRpc(playerName);
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DeclareHandleServerRpc(string handle, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        clientHandles[clientId] = handle;

        if (clientId == NetworkManager.ServerClientId)
        {
            clientColors[clientId] = "#FF0033"; // Red for Admin
        }
        else
        {
            clientColors[clientId] = neonColors[UnityEngine.Random.Range(0, neonColors.Length)];
        }

        BroadcastMessageClientRpc("[System]", $"{handle} has connected.", "yellow", ulong.MaxValue);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ChangeHandleServerRpc(string oldName, string newName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        clientHandles[clientId] = newName;
        BroadcastMessageClientRpc("[System]", $"{oldName} changed their nickname to {newName}", "yellow", ulong.MaxValue);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        string handle = clientHandles.TryGetValue(clientId, out string h) ? h : $"Client {clientId}";
        clientHandles.Remove(clientId);
        clientColors.Remove(clientId);
        typingClients.Remove(clientId);
        UpdateTypingUI();
        BroadcastMessageClientRpc("[System]", $"{handle} has disconnected.", "yellow", ulong.MaxValue);
    }

    void Start()
    {
        if (SendTextButton != null)
        {
            SendTextButton.onClick.AddListener(OnSendButtonClicked);
        }
        if (ChatInputField != null)
        {
            ChatInputField.characterLimit = maxMessageLength;
            ChatInputField.onSubmit.AddListener(OnChatSubmit);
            ChatInputField.onValueChanged.AddListener(OnInputValueChanged);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (ChatInputField != null && !ChatInputField.isFocused)
            {
                ChatInputField.ActivateInputField();
            }
        }

        if (ChatInputField != null && ChatInputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (sentHistory.Count > 0)
                {
                    historyIndex++;
                    if (historyIndex >= sentHistory.Count) historyIndex = sentHistory.Count - 1;
                    ChatInputField.text = sentHistory[sentHistory.Count - 1 - historyIndex];
                    ChatInputField.caretPosition = ChatInputField.text.Length;
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (sentHistory.Count > 0)
                {
                    historyIndex--;
                    if (historyIndex < 0) 
                    {
                        historyIndex = -1;
                        ChatInputField.text = "";
                    }
                    else 
                    {
                        ChatInputField.text = sentHistory[sentHistory.Count - 1 - historyIndex];
                        ChatInputField.caretPosition = ChatInputField.text.Length;
                    }
                }
            }
        }
    }

    private void OnInputValueChanged(string text)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient || !NetworkManager.Singleton.IsConnectedClient) return;

        bool typingNow = text.Length > 0 && !text.StartsWith("/");
        if (typingNow != isTypingLocally)
        {
            isTypingLocally = typingNow;
            SetTypingStateServerRpc(typingNow);
        }
    }

    private void OnChatSubmit(string text)
    {
        OnSendButtonClicked();
    }

    void OnSendButtonClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient || !NetworkManager.Singleton.IsConnectedClient)
        {
            AddMessageToDisplay("[System]", "Error: You are disconnected from the server.", "red");
            return;
        }

        if (Time.time - lastSendTime < messageCooldown)
        {
            float remainingTime = messageCooldown - (Time.time - lastSendTime);
            AddMessageToDisplay("[System]", $"Spam prevention: Please wait {remainingTime:F1}s before sending another message.", "red");
            return; 
        }

        if (!string.IsNullOrWhiteSpace(ChatInputField.text))
        {
            string inputText = ChatInputField.text;
            ChatInputField.text = "";

            if (ProcessLocalCommand(inputText))
            {
                sentHistory.Add(inputText);
                historyIndex = -1;
                ChatInputField.ActivateInputField();
                isTypingLocally = false;
                SetTypingStateServerRpc(false);
                return;
            }

            sentHistory.Add(inputText);
            historyIndex = -1;
            lastSendTime = Time.time; 
            isTypingLocally = false;
            SetTypingStateServerRpc(false);
            SubmitMessageServerRpc(inputText, playerName);
            ChatInputField.ActivateInputField();
        }
    }

    private bool ProcessLocalCommand(string commandText)
    {
        bool isCommand = false;
        string lowerCmd = commandText.ToLower();
        
        if (lowerCmd.StartsWith("/help"))
        {
            AddMessageToDisplay("[System]", "Available commands: /ping, /nick <name>, /players, /w <name> <message>, /kick <name>", "#00FF00");
            isCommand = true;
        }
        else if (lowerCmd.StartsWith("/ping"))
        {
            float frameTimeMs = Time.deltaTime * 1000f;
            ulong rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            AddMessageToDisplay("[System]", $"Frame Time: {frameTimeMs:F2}ms | RTT: {rtt}ms", "#00FF00");
            isCommand = true;
        }
        else if (lowerCmd.StartsWith("/nick "))
        {
            string newName = commandText.Substring(6).Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                string oldName = playerName;
                playerName = newName;
                ChangeHandleServerRpc(oldName, newName);
            }
            isCommand = true;
        }
        else if (lowerCmd.StartsWith("/clear"))
        {
            ClearChatHistory();
            AddMessageToDisplay("[System]", "SYSTEM REBOOT INITIATED...\nMEMORY FLUSHED.\nWELCOME TO NEON-OS.", "#00FF66");
            isCommand = true;
        }

        return isCommand;
    }

    private void ClearChatHistory()
    {
        while (chatHistory.Count > 0)
        {
            GameObject oldMsg = chatHistory.Dequeue();
            if (oldMsg != null) Destroy(oldMsg);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetTypingStateServerRpc(bool isTyping, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        UpdateTypingClientRpc(clientId, isTyping);
    }

    [Rpc(SendTo.Everyone)]
    public void UpdateTypingClientRpc(ulong clientId, bool isTyping)
    {
        if (isTyping)
        {
            typingClients[clientId] = Time.time;
        }
        else
        {
            typingClients.Remove(clientId);
        }
        UpdateTypingUI();
    }

    private void UpdateTypingUI()
    {
        if (TypingIndicatorText == null) return;
        
        List<string> typingNames = new List<string>();
        foreach (var id in typingClients.Keys)
        {
            if (id != NetworkManager.LocalClientId && clientHandles.ContainsKey(id))
            {
                typingNames.Add(clientHandles[id]);
            }
        }

        if (typingNames.Count == 1)
            TypingIndicatorText.text = $"{typingNames[0]} is typing...";
        else if (typingNames.Count > 1)
            TypingIndicatorText.text = "Multiple people are typing...";
        else
            TypingIndicatorText.text = "";
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitMessageServerRpc(string message, string senderName, RpcParams rpcParams = default)
    {
        if (message.Length > maxMessageLength)
        {
            message = message.Substring(0, maxMessageLength);
        }

        ulong senderId = rpcParams.Receive.SenderClientId;

        // SERVER-SIDE COMMAND PARSING
        string lowerMsg = message.ToLower();
        
        if (lowerMsg.StartsWith("/kick "))
        {
            if (senderId != NetworkManager.ServerClientId)
            {
                TargetedMessageClientRpc("[System]", "Access Denied: Only [ROOT] Admin can use /kick.", "red", RpcTarget.Single(senderId, RpcTargetUse.Temp));
                return;
            }

            string afterCommand = message.Substring(6).Trim();
            ulong? targetId = null;
            string targetName = "";

            foreach(var kvp in clientHandles)
            {
                string handle = kvp.Value;
                string cleanHandle = handle.Replace("[ROOT] ", ""); // Allow matching without [ROOT] prefix
                
                if (afterCommand.Equals(handle, System.StringComparison.OrdinalIgnoreCase) ||
                    afterCommand.Equals(cleanHandle, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetId = kvp.Key;
                    targetName = handle;
                    break;
                }
            }

            if (targetId.HasValue && targetId.Value != NetworkManager.ServerClientId)
            {
                BroadcastMessageClientRpc("[System]", $"[ROOT] Admin has forcefully kicked {targetName} from the server.", "red", ulong.MaxValue);
                NetworkManager.Singleton.DisconnectClient(targetId.Value);
            }
            else
            {
                TargetedMessageClientRpc("[System]", $"Player '{afterCommand}' not found or cannot be kicked.", "red", RpcTarget.Single(senderId, RpcTargetUse.Temp));
            }
            return;
        }
        else if (lowerMsg.StartsWith("/players"))
        {
            string playerList = "Connected players: ";
            foreach(var handle in clientHandles.Values) {
                playerList += handle + ", ";
            }
            playerList = playerList.TrimEnd(',', ' ');
            
            TargetedMessageClientRpc("[System]", playerList, "#00AAFF", RpcTarget.Single(senderId, RpcTargetUse.Temp));
            return;
        }
        else if (lowerMsg.StartsWith("/w "))
        {
            string afterCommand = message.Substring(3).Trim();
            ulong? targetId = null;
            string targetName = "";
            string whisperMsg = "";

            foreach(var kvp in clientHandles)
            {
                string handle = kvp.Value;
                string cleanHandle = handle.Replace("[ROOT] ", ""); 

                // Check against full handle
                if (afterCommand.StartsWith(handle, System.StringComparison.OrdinalIgnoreCase) && 
                   (afterCommand.Length == handle.Length || afterCommand[handle.Length] == ' '))
                {
                    targetId = kvp.Key;
                    targetName = handle;
                    whisperMsg = afterCommand.Substring(handle.Length).Trim();
                    break;
                }
                // Check against handle without [ROOT] prefix
                else if (afterCommand.StartsWith(cleanHandle, System.StringComparison.OrdinalIgnoreCase) &&
                        (afterCommand.Length == cleanHandle.Length || afterCommand[cleanHandle.Length] == ' '))
                {
                    targetId = kvp.Key;
                    targetName = handle;
                    whisperMsg = afterCommand.Substring(cleanHandle.Length).Trim();
                    break;
                }
            }

            if (targetId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(whisperMsg))
                {
                    TargetedMessageClientRpc("[System]", "Usage: /w <name> <message>", "red", RpcTarget.Single(senderId, RpcTargetUse.Temp));
                    return;
                }

                string cleanMessage = FilterMessage(whisperMsg);
                
                TargetedMessageClientRpc(senderName + " (Whisper)", cleanMessage, "#FF00FF", RpcTarget.Single(targetId.Value, RpcTargetUse.Temp));

                if (targetId.Value != senderId)
                {
                    TargetedMessageClientRpc("To " + targetName, cleanMessage, "#FF00FF", RpcTarget.Single(senderId, RpcTargetUse.Temp));
                }
            }
            else
            {
                string failedName = afterCommand.Split(' ')[0];
                TargetedMessageClientRpc("[System]", $"Player '{failedName}' not found.", "red", RpcTarget.Single(senderId, RpcTargetUse.Temp));
            }
            return;
        }
        
        string cleanBroadcast = FilterMessage(message);
        string playerColorHex = clientColors.ContainsKey(senderId) ? clientColors[senderId] : "#00FF66"; 
        
        BroadcastMessageClientRpc(senderName, cleanBroadcast, playerColorHex, senderId);
    }

    private string FilterMessage(string input)
    {
        string output = Regex.Replace(input, @"<.*?>", string.Empty);

        foreach (string word in bannedWords)
        {
            string pattern = $@"\b{Regex.Escape(word)}\b";
            output = Regex.Replace(output, pattern, m => new string('*', m.Length), RegexOptions.IgnoreCase);
        }
        return output;
    }

    [Rpc(SendTo.Everyone)]
    public void BroadcastMessageClientRpc(string senderName, string messageContent, string senderColorHex, ulong originalSenderId)
    {
        bool isLocal = (originalSenderId == NetworkManager.Singleton.LocalClientId);
        
        AddMessageToDisplay(senderName, messageContent, senderColorHex, isLocal);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void TargetedMessageClientRpc(string senderName, string messageContent, string senderColorHex, RpcParams rpcParams = default)
    {
        AddMessageToDisplay(senderName, messageContent, senderColorHex, false);
    }

    private void AddMessageToDisplay(string senderName, string messageContent, string senderColorHex, bool isLocalPlayer = false)
    {
        if (MessagePrefab == null || ChatContentParent == null) return;

        GameObject newMsgObj = Instantiate(MessagePrefab, ChatContentParent);
        ChatMessageUI msgUI = newMsgObj.GetComponent<ChatMessageUI>();
        
        if (msgUI != null)
        {
            msgUI.SetupMessage(senderName, messageContent, senderColorHex, isLocalPlayer);
        }

        chatHistory.Enqueue(newMsgObj);
        if (chatHistory.Count > maxChatLines)
        {
            GameObject oldMsg = chatHistory.Dequeue();
            Destroy(oldMsg);
        }

        StartCoroutine(ForceScrollDown());
    }

    private IEnumerator ForceScrollDown()
    {
        yield return new WaitForEndOfFrame();
        if (ChatScrollRect != null)
        {
            ChatScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}