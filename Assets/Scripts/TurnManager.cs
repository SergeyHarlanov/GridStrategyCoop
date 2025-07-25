using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;
using Zenject;

public class TurnManager : NetworkBehaviour
{
    [Inject] private UnitManager _unitManager;
    [Inject] private GameManager _gameManager;
    [Inject] private GameSettings _gameSettings;
    
    public bool IsMyTurn 
    {
        get
        {
            return NetworkManager.Singleton != null && 
                   NetworkManager.Singleton.LocalClientId == CurrentPlayerClientId.Value;
        }
    }
    
    public int MaxActionsPerTurn => _maxActionsPerTurn;

    public NetworkVariable<ulong> CurrentPlayerClientId = new NetworkVariable<ulong>(0);
    public NetworkVariable<float> TimeRemainingInTurn = new NetworkVariable<float>(0);
    public NetworkVariable<int> ActionsRemaining = new NetworkVariable<int>(0);
    public NetworkVariable<int> TurnNumber = new NetworkVariable<int>(0);

    [Header("Settings")]
    [SerializeField] private float _turnDuration = 60f; 
    [SerializeField] private int _maxActionsPerTurn = 2;

    private List<ulong> connectedPlayerClientIds = new List<ulong>();
    private int currentPlayerIndex = -1;

    public event Action<ulong> OnTurnStartAnnounce;
    public event Action<bool> OnEndGameAnnounce;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("TurnManager: Server spawned. Waiting for clients to connect...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedServer;

            StartCoroutine(CheckAndStartGameWhenReady());
        }

        CurrentPlayerClientId.OnValueChanged += OnCurrentPlayerChanged;
        TimeRemainingInTurn.OnValueChanged += OnTimeRemainingChanged;
        ActionsRemaining.OnValueChanged += OnActionsRemainingChanged;
        TurnNumber.OnValueChanged += OnTurnNumberChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedServer;
        }
        CurrentPlayerClientId.OnValueChanged -= OnCurrentPlayerChanged;
        TimeRemainingInTurn.OnValueChanged -= OnTimeRemainingChanged;
        ActionsRemaining.OnValueChanged -= OnActionsRemainingChanged;
        TurnNumber.OnValueChanged -= OnTurnNumberChanged;
    }

    private IEnumerator CheckAndStartGameWhenReady()
    {
        yield return new WaitForSeconds(2.0f); 

        if (IsServer)
        {
            connectedPlayerClientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();

            if (_gameManager != null && connectedPlayerClientIds.Count == _gameManager.MAX_PLAYERS && CurrentPlayerClientId.Value == 0)
            {
                Debug.Log("TurnManager: All players connected. Starting game!");
                StartNextTurn();
            }
            else
            {
                Debug.Log($"TurnManager: Waiting for players. Currently {connectedPlayerClientIds.Count}/{(_gameManager != null ? _gameManager.MAX_PLAYERS : "N/A")} connected.");
            }
        }
    }

    private void OnClientConnectedServer(ulong clientId)
    {
        Debug.Log($"TurnManager: Client {clientId} connected to server. Total: {NetworkManager.Singleton.ConnectedClients.Count}");

        if (!connectedPlayerClientIds.Contains(clientId))
        {
            connectedPlayerClientIds.Add(clientId);
        }
        connectedPlayerClientIds = connectedPlayerClientIds.OrderBy(id => id).ToList(); 

        if (_gameManager != null && connectedPlayerClientIds.Count == _gameManager.MAX_PLAYERS && CurrentPlayerClientId.Value == 0)
        {
            Debug.Log("TurnManager: All players connected. Initiating game start.");
            StartNextTurn();
        }
    }

    private void OnClientDisconnectedServer(ulong clientId)
    {
        Debug.Log($"TurnManager: Client {clientId} disconnected from server.");
        if (connectedPlayerClientIds.Contains(clientId))
        {
            connectedPlayerClientIds.Remove(clientId);
            connectedPlayerClientIds = connectedPlayerClientIds.OrderBy(id => id).ToList(); 

            if (CurrentPlayerClientId.Value == clientId || (_gameManager != null && connectedPlayerClientIds.Count < _gameManager.MAX_PLAYERS))
            {
                Debug.Log("TurnManager: Current player disconnected or not enough players. Ending turn/resetting game.");
                EndTurnInternal();
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        TimeRemainingInTurn.Value -= Time.deltaTime;
        if (TimeRemainingInTurn.Value <= 0)
        {
            Debug.Log($"Server: Turn time expired for Client ID: {CurrentPlayerClientId.Value}");
            EndTurnInternal();
        }
    }

    private void StartNextTurn()
    {
        if (!IsServer) return;
        
        if (NetworkManager.Singleton.ConnectedClients.Count != 2)
        {
            return;
        }
        
        if (_gameManager == null || connectedPlayerClientIds.Count < _gameManager.MAX_PLAYERS)
        {
            Debug.LogWarning("TurnManager: Not enough players to start next turn or GameManager not ready. Resetting game state.");
            CurrentPlayerClientId.Value = 0; 
            TimeRemainingInTurn.Value = 0;
            ActionsRemaining.Value = 0;
            TurnNumber.Value = 0;
            currentPlayerIndex = -1; 
            return;
        }

        TurnNumber.Value++;

        OnEnd();

        if (currentPlayerIndex == -1 || connectedPlayerClientIds.Count == 0) 
        {
            currentPlayerIndex = 0;
        }
        else
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % connectedPlayerClientIds.Count;
        }
        
        CurrentPlayerClientId.Value = connectedPlayerClientIds[currentPlayerIndex];
        TimeRemainingInTurn.Value = _turnDuration;
        ActionsRemaining.Value = MaxActionsPerTurn;

        Debug.Log($"Server: Starting turn {TurnNumber.Value} for Client ID: {CurrentPlayerClientId.Value}. Actions: {ActionsRemaining.Value}, Time: {TimeRemainingInTurn.Value:F1}s");

        AnnounceTurnStartClientRpc(CurrentPlayerClientId.Value);
    }

    public void OnEnd()
    {
        CheckGameEndStatusOnServer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == CurrentPlayerClientId.Value)
        {
            Debug.Log($"Server: Turn end requested by Client ID: {rpcParams.Receive.SenderClientId}");
            EndTurnInternal();
        }
        else
        {
            Debug.LogWarning($"Server: Client {rpcParams.Receive.SenderClientId} attempted to end turn out of sequence. Current turn: {CurrentPlayerClientId.Value}");
        }
    }

    private void EndTurnInternal()
    {
        if (!IsServer) return;

        Debug.Log($"Server: Ending turn for Client ID: {CurrentPlayerClientId.Value}");
        StartNextTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void UseActionServerRpc(ulong requestingClientId, int cost = 1)
    {
        if (requestingClientId == CurrentPlayerClientId.Value)
        {
            if (ActionsRemaining.Value >= cost)
            {
                ActionsRemaining.Value -= cost;
                Debug.Log($"Server: Client {requestingClientId} used {cost} action(s). Remaining: {ActionsRemaining.Value}");

                if (ActionsRemaining.Value <= 0)
                {
                    Debug.Log($"Server: Client {requestingClientId} has no actions left. Ending turn automatically.");
                    EndTurnInternal();
                }
            }
            else
            {
                Debug.LogWarning($"Server: Client {requestingClientId} attempted to use action but not enough actions. Remaining: {ActionsRemaining.Value}");
            }
        }
        else
        {
            Debug.LogWarning($"Server: Client {requestingClientId} attempted to use action but it's not their turn. Current turn: {CurrentPlayerClientId.Value}");
        }
    }

    [ClientRpc]
    private void AnnounceTurnStartClientRpc(ulong playerClientId)
    {
        Debug.Log($"Client: Turn started for Player ID: {playerClientId}");
        OnTurnStartAnnounce?.Invoke(playerClientId); 
    }

    [ClientRpc]
    private void EndGameClientRpc(ulong playerClientId, bool hasPlayerWon)
    {
        OnEndGameAnnounce?.Invoke(hasPlayerWon);
    }

    private void CheckGameEndStatusOnServer()
    {
        if (!IsServer) return;

        Debug.Log("Server: Checking game end conditions...");

        if (TurnNumber.Value >= _gameSettings.CountStepLimit)
        {
            Debug.Log($"Server: Turn limit {_gameSettings.CountStepLimit} reached. Evaluating game end based on unit counts.");

            int friendCount = _unitManager.GetLiveUnitsForPlayer().Count;
            int enemyCount =_unitManager.GetLiveEnemyUnitsForPlayer().Count;

            if (friendCount == enemyCount)
            {
                Debug.Log(
                    $"Server: Turn limit reached. Friendly units ({friendCount}) == Enemy units ({enemyCount}). Setting infinite speed.");
                _gameManager.SetAllUnitsInfiniteMovementSpeedServerRpc();
                return;
            }
            else
            {
                bool currentPlayerHasWon = (friendCount > enemyCount);
                Debug.Log(
                    $"Server: Turn limit reached. Friendly units ({friendCount}) != Enemy units ({enemyCount}). Player {CurrentPlayerClientId.Value} won: {currentPlayerHasWon}. Ending game.");

                foreach (ulong playerId in connectedPlayerClientIds)
                {
                    EndGameClientRpc(playerId, currentPlayerHasWon);
                }
                EndGameClientRpc(connectedPlayerClientIds[0], currentPlayerHasWon);
            }

            return;
        }
    }

    private void OnCurrentPlayerChanged(ulong oldId, ulong newId)
    {
        Debug.Log($"Client: Current Player changed from {oldId} to {newId}");
    }

    private void OnTimeRemainingChanged(float oldTime, float newTime)
    {
    }

    private void OnActionsRemainingChanged(int oldActions, int newActions)
    {
        Debug.Log($"Client: Actions remaining changed from {oldActions} to {newActions}");
    }

    private void OnTurnNumberChanged(int oldTurn, int newTurn)
    {
        Debug.Log($"Client: Turn number changed from {oldTurn} to {newTurn}");
    }
}