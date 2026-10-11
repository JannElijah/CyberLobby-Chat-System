using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Serializable input command structure representing a single frame's player input.
/// Carries sequence numbering and timestamp/deltaTime for deterministic simulation and reconciliation.
/// </summary>
public struct InputCommand : INetworkSerializable, IEquatable<InputCommand>
{
    public uint sequenceNumber;
    public float timestamp;
    public float deltaTime;
    public Vector2 moveInput;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref sequenceNumber);
        serializer.SerializeValue(ref timestamp);
        serializer.SerializeValue(ref deltaTime);
        serializer.SerializeValue(ref moveInput);
    }

    public bool Equals(InputCommand other)
    {
        return sequenceNumber == other.sequenceNumber &&
               Mathf.Approximately(timestamp, other.timestamp) &&
               Mathf.Approximately(deltaTime, other.deltaTime) &&
               moveInput == other.moveInput;
    }
}

/// <summary>
/// Authoritative state broadcasted from the server to clients.
/// Packages the latest authoritative transform alongside the last processed sequence number.
/// </summary>
public struct ServerState : INetworkSerializable
{
    public uint lastProcessedSequenceNumber;
    public Vector3 position;
    public Quaternion rotation;
    public float timestamp;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref lastProcessedSequenceNumber);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref timestamp);
    }
}

/// <summary>
/// Handles local input gathering and maintains the local input history buffer queue.
/// Part of Lab 7: Client-Side Movement Prediction.
/// </summary>
public class PlayerInput : MonoBehaviour
{
    private InputAction moveAction;
    private uint currentSequenceNumber = 0;

    // Local input history buffer queue storing unacknowledged local commands
    private readonly List<InputCommand> inputHistory = new List<InputCommand>();

    public IReadOnlyList<InputCommand> InputHistory => inputHistory;
    public int PendingCount => inputHistory.Count;
    public Vector2 CurrentRawInput { get; private set; }

    private void Awake()
    {
        // Programmatic New Input System binding for plug-and-play functionality
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    private void Update()
    {
        // Check if player is focused in a UI field (like the in-game global chat)
        bool isTyping = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
        CurrentRawInput = isTyping ? Vector2.zero : (moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero);
    }

    /// <summary>
    /// Generates a sequenced, timestamped InputCommand for the current frame.
    /// </summary>
    public InputCommand CreateInputCommand(float deltaTime)
    {
        currentSequenceNumber++;
        return new InputCommand
        {
            sequenceNumber = currentSequenceNumber,
            timestamp = Time.time,
            deltaTime = deltaTime,
            moveInput = CurrentRawInput
        };
    }

    /// <summary>
    /// Enqueues an unacknowledged command into the local history buffer.
    /// </summary>
    public void BufferCommand(InputCommand command)
    {
        inputHistory.Add(command);
    }

    /// <summary>
    /// Drops all older inputs up to the last processed sequence number acknowledged by the server.
    /// </summary>
    public void PruneAcknowledgedCommands(uint lastProcessedSequenceNumber)
    {
        inputHistory.RemoveAll(cmd => cmd.sequenceNumber <= lastProcessedSequenceNumber);
    }

    /// <summary>
    /// Clears the input history buffer.
    /// </summary>
    public void ClearHistory()
    {
        inputHistory.Clear();
    }
}
