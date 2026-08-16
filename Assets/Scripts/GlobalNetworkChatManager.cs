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
    public TextMeshProUGUI ChatDisplayAreaBox;
    public Button SendTextButton;
    public ScrollRect ChatScrollRect;

    [Header("Security & Spam Prevention")]
    public float messageCooldown = 1.5f; 
    private float lastSendTime = -100f;
    
    private string[] bannedWords = { "spam", "hack", "cheat", "badword" };
    
    private string playerName;
    private Dictionary<ulong, string> clientHandles = new Dictionary<ulong, string>();

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            playerName = "User_" + UnityEngine.Random.Range(100, 1000).ToString();
            ChatDisplayAreaBox.text += $"<color=#00AAFF>[System]</color> You have been assigned the handle: {playerName}\n";
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
        string sysMessage = $"<color=yellow>[System] {handle} has connected.</color>";
        BroadcastMessageClientRpc(sysMessage);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        string handle = clientHandles.TryGetValue(clientId, out string h) ? h : $"Client {clientId}";
        clientHandles.Remove(clientId);
        string sysMessage = $"<color=yellow>[System] {handle} has disconnected.</color>";
        BroadcastMessageClientRpc(sysMessage);
    }

    void Start()
    {
        if (SendTextButton != null)
        {
            SendTextButton.onClick.AddListener(OnSendButtonClicked);
        }
        if (ChatInputField != null)
        {
            ChatInputField.onSubmit.AddListener(OnChatSubmit);
        }
    }

    private void OnChatSubmit(string text)
    {
        OnSendButtonClicked();
    }

    void OnSendButtonClicked()
    {
        if (Time.time - lastSendTime < messageCooldown)
        {
            Debug.LogWarning("Spam prevention: Please wait before sending another message.");
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
            ChatDisplayAreaBox.text += "<color=#00FF00>[System]</color> Available commands: /ping, /nick <name>\n";
            isCommand = true;
        }
        else if (commandText.StartsWith("/ping"))
        {
            float frameTimeMs = Time.deltaTime * 1000f;
            ulong rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            string sysMessage = $"<color=#00FF00>[System]</color> Frame Time: {frameTimeMs:F2}ms | RTT: {rtt}ms";
            ChatDisplayAreaBox.text += sysMessage + "\n";
            isCommand = true;
        }
        else if (commandText.StartsWith("/nick "))
        {
            string newName = commandText.Substring(6).Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                playerName = newName;
                ChatDisplayAreaBox.text += $"<color=#00FF00>[System]</color> Handle changed to: {playerName}\n";
            }
            isCommand = true;
        }

        if (isCommand)
        {
            StartCoroutine(ForceScrollDown());
        }
        
        return isCommand;
    }

    // UPDATED: New Netcode syntax for ServerRpc
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitMessageServerRpc(string message, string senderName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        
        string cleanMessage = FilterMessage(message);
        string playerColor = (senderId == 0) ? "red" : "#00AAFF"; 
        
        string formattedMessage = $"<color={playerColor}>{senderName}:</color> {cleanMessage}";
        BroadcastMessageClientRpc(formattedMessage);
    }

    private string FilterMessage(string input)
    {
        string output = input;
        foreach (string word in bannedWords)
        {
            output = Regex.Replace(output, word, "***", RegexOptions.IgnoreCase);
        }
        return output;
    }

    // UPDATED: New Netcode syntax for ClientRpc
    [Rpc(SendTo.Everyone)]
    public void BroadcastMessageClientRpc(string formattedMessage)
    {
        ChatDisplayAreaBox.text += formattedMessage + "\n";
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