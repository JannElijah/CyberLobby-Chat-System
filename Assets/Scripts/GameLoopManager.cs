using UnityEngine;
using Unity.Netcode;
using System;

public enum GamePhase
{
    Waiting,
    MorningPrep,
    ShiftActive,
    NightUpgrade
}

public class GameLoopManager : NetworkBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(
        GamePhase.Waiting,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> PhaseTimeRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Phase Durations")]
    public float morningPrepDuration = 30f;
    public float shiftActiveDuration = 120f;

    public event Action<GamePhase> OnPhaseChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        CurrentPhase.OnValueChanged += (oldVal, newVal) => OnPhaseChanged?.Invoke(newVal);
        
        if (IsServer)
        {
            // Start the Morning Prep shortly after scene load
            Invoke(nameof(StartMorningPrep), 2f);
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (CurrentPhase.Value == GamePhase.MorningPrep || CurrentPhase.Value == GamePhase.ShiftActive)
        {
            PhaseTimeRemaining.Value -= Time.deltaTime;
            
            if (PhaseTimeRemaining.Value <= 0)
            {
                if (CurrentPhase.Value == GamePhase.MorningPrep)
                {
                    StartShiftActive();
                }
                else if (CurrentPhase.Value == GamePhase.ShiftActive)
                {
                    StartNightUpgrade();
                }
            }
        }
    }

    public void StartMorningPrep()
    {
        CurrentPhase.Value = GamePhase.MorningPrep;
        PhaseTimeRemaining.Value = morningPrepDuration;
    }

    public void StartShiftActive()
    {
        CurrentPhase.Value = GamePhase.ShiftActive;
        PhaseTimeRemaining.Value = shiftActiveDuration;
    }

    public void StartNightUpgrade()
    {
        CurrentPhase.Value = GamePhase.NightUpgrade;
        PhaseTimeRemaining.Value = 0f;
    }
}
