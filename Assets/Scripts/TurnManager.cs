// TurnManager.cs
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Singleton { get; private set; }

    // NetworkVariables для синхронизации состояния хода
    public NetworkVariable<ulong> CurrentPlayerClientId = new NetworkVariable<ulong>(0); // ID клиента, чей сейчас ход
    public NetworkVariable<float> TimeRemainingInTurn = new NetworkVariable<float>(0);   // Оставшееся время хода
    public NetworkVariable<int> ActionsRemaining = new NetworkVariable<int>(0);          // Оставшиеся действия
    public NetworkVariable<int> TurnNumber = new NetworkVariable<int>(0);              // <--- НОВОЕ: Текущий номер хода

    [SerializeField] public float turnDuration = 60f; // Длительность хода в секундах
    [SerializeField] private int maxActionsPerTurn = 2; // Максимальное количество действий за ход

    private List<ulong> connectedPlayerClientIds = new List<ulong>(); // Список всех подключенных игроков
    private int currentPlayerIndex = -1; // Индекс текущего игрока в списке (-1 означает, что ход еще не начался)

    // Событие, которое UIManager будет слушать для оповещения о начале хода
    public event Action<ulong> OnTurnStartAnnounce;

    public override void OnNetworkSpawn()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Singleton = this;
        }

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
        TurnNumber.OnValueChanged += OnTurnNumberChanged; // <--- НОВОЕ: Подписка на изменение номера хода
    }

    public override void OnNetworkDespawn()
    {
        if (Singleton == this)
        {
            Singleton = null;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedServer;
        }
        CurrentPlayerClientId.OnValueChanged -= OnCurrentPlayerChanged;
        TimeRemainingInTurn.OnValueChanged -= OnTimeRemainingChanged;
        ActionsRemaining.OnValueChanged -= OnActionsRemainingChanged;
        TurnNumber.OnValueChanged -= OnTurnNumberChanged; // <--- НОВОЕ: Отписка от изменения номера хода
    }

    private IEnumerator CheckAndStartGameWhenReady()
    {
        yield return new WaitForSeconds(2.0f); 

        if (IsServer)
        {
            connectedPlayerClientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();

            if (GameManager.Singleton != null && connectedPlayerClientIds.Count == GameManager.Singleton.MAX_PLAYERS && CurrentPlayerClientId.Value == 0)
            {
                Debug.Log("TurnManager: All players connected. Starting game!");
                StartNextTurn();
            }
            else
            {
                Debug.Log($"TurnManager: Waiting for players. Currently {connectedPlayerClientIds.Count}/{(GameManager.Singleton != null ? GameManager.Singleton.MAX_PLAYERS : "N/A")} connected.");
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

        if (GameManager.Singleton != null && connectedPlayerClientIds.Count == GameManager.Singleton.MAX_PLAYERS && CurrentPlayerClientId.Value == 0)
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

            if (CurrentPlayerClientId.Value == clientId || (GameManager.Singleton != null && connectedPlayerClientIds.Count < GameManager.Singleton.MAX_PLAYERS))
            {
                Debug.Log("TurnManager: Current player disconnected or not enough players. Ending turn/resetting game.");
                EndTurnInternal();
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return;

    //    if (CurrentPlayerClientId.Value != 0)
        {
            TimeRemainingInTurn.Value -= Time.deltaTime;
            if (TimeRemainingInTurn.Value <= 0)
            {
                Debug.Log($"Server: Turn time expired for Client ID: {CurrentPlayerClientId.Value}");
                EndTurnInternal();
            }
        }
    }

    private void StartNextTurn()
    {
        if (!IsServer) return;

        if (GameManager.Singleton == null || connectedPlayerClientIds.Count < GameManager.Singleton.MAX_PLAYERS)
        {
            Debug.LogWarning("TurnManager: Not enough players to start next turn or GameManager not ready. Resetting game state.");
            CurrentPlayerClientId.Value = 0; 
            TimeRemainingInTurn.Value = 0;
            ActionsRemaining.Value = 0;
            TurnNumber.Value = 0; // Сброс номера хода
            currentPlayerIndex = -1; 
            return;
        }

        TurnNumber.Value++; // <--- НОВОЕ: Инкрементируем номер хода при каждом новом ходе

        // Определяем следующего игрока
        // Если это первый ход (currentPlayerIndex == -1) или если список игроков пуст/некорректен
        if (currentPlayerIndex == -1 || connectedPlayerClientIds.Count == 0) 
        {
            currentPlayerIndex = 0; // Начинаем с первого игрока
        }
        else
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % connectedPlayerClientIds.Count;
        }
        
        CurrentPlayerClientId.Value = connectedPlayerClientIds[currentPlayerIndex];

        TimeRemainingInTurn.Value = turnDuration;
        ActionsRemaining.Value = maxActionsPerTurn;

        Debug.Log($"Server: Starting turn {TurnNumber.Value} for Client ID: {CurrentPlayerClientId.Value}. Actions: {ActionsRemaining.Value}, Time: {TimeRemainingInTurn.Value:F1}s");

        AnnounceTurnStartClientRpc(CurrentPlayerClientId.Value);
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

    private void OnCurrentPlayerChanged(ulong oldId, ulong newId)
    {
        Debug.Log($"Client: Current Player changed from {oldId} to {newId}");
    }

    private void OnTimeRemainingChanged(float oldTime, float newTime)
    {
        // UI времени обновляется в Update() для плавности, поэтому здесь ничего не делаем.
        Debug.Log($"UIManager: Time remaining NetworkVariable changed event received: {newTime:F1}.");
    }

    private void OnActionsRemainingChanged(int oldActions, int newActions)
    {
        Debug.Log($"Client: Actions remaining changed from {oldActions} to {newActions}");
    }

    // <--- НОВОЕ: Обработчик изменения номера хода
    private void OnTurnNumberChanged(int oldTurn, int newTurn)
    {
        Debug.Log($"Client: Turn number changed from {oldTurn} to {newTurn}");
    }
}