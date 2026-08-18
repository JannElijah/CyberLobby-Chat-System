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

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            playerName = "User_" + UnityEngine.Random.Range(100, 1000).ToString();
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
                ChatInputField.ActivateInputField();
                return;
            }

            lastSendTime = Time.time; 
            SubmitMessageServerRpc(inputText, playerName);
            ChatInputField.ActivateInputField();
        }
    }

    private bool ProcessLocalCommand(string commandText)
    {
        bool isCommand = false;
        if (commandText.StartsWith("/help"))
        {
            AddMessageToDisplay("[System]", "Available commands: /ping, /nick <name>", "#00FF00");
            isCommand = true;
        }
        else if (commandText.StartsWith("/ping"))
        {
            float frameTimeMs = Time.deltaTime * 1000f;
            ulong rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            AddMessageToDisplay("[System]", $"Frame Time: {frameTimeMs:F2}ms | RTT: {rtt}ms", "#00FF00");
            isCommand = true;
        }
        else if (commandText.StartsWith("/nick "))
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
        else if (commandText.StartsWith("/clear"))
        {
            ClearChatHistory();
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

    // UPDATED: New Netcode syntax for ServerRpc
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitMessageServerRpc(string message, string senderName, RpcParams rpcParams = default)
    {
        if (message.Length > maxMessageLength)
        {
            message = message.Substring(0, maxMessageLength);
        }

        ulong senderId = rpcParams.Receive.SenderClientId;
        
        string cleanMessage = FilterMessage(message);
        string playerColorHex = (senderId == 0) ? "red" : "#00AAFF"; 
        
        BroadcastMessageClientRpc(senderName, cleanMessage, playerColorHex, senderId);
    }

    private string FilterMessage(string input)
    {
        // 1. Strip Rich Text (to prevent UI injection attacks)
        string output = Regex.Replace(input, @"<.*?>", string.Empty);

        // 2. Profanity Filter
        foreach (string word in bannedWords)
        {
            // Use word boundaries \b to avoid accidentally censoring parts of normal words
            string pattern = $@"\b{Regex.Escape(word)}\b";
            
            // Replace the matched word with asterisks of the exact same length
            output = Regex.Replace(output, pattern, m => new string('*', m.Length), RegexOptions.IgnoreCase);
        }
        return output;
    }

    // UPDATED: New Netcode syntax for ClientRpc
    [Rpc(SendTo.Everyone)]
    public void BroadcastMessageClientRpc(string senderName, string messageContent, string senderColorHex, ulong originalSenderId)
    {
        bool isLocal = (originalSenderId == NetworkManager.Singleton.LocalClientId);
        
        // ASSIGNMENT RUBRIC: Force Neon Green text for all players
        if (senderName != "[System]")
        {
            senderColorHex = "#00FF66"; 
        }

        AddMessageToDisplay(senderName, messageContent, senderColorHex, isLocal);
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