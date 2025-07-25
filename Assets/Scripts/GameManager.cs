using System;
using Unity.Netcode;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    public  int MAX_PLAYERS = 2; // Максимальное количество игроков в комнате
    
    [Inject] private DiContainer _container;
    [Inject] private UnitManager _unitManager;
    [Inject] private PlayerController _playerController;
    [Inject] private UIManager uiManager;
    [Inject] private GameSettings _gameSettings;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Server started. Waiting for clients...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"GameManager: Client {clientId} connected.");


        if (IsServer && NetworkManager.Singleton.ConnectedClientsList.Count >= MAX_PLAYERS)
        {
            Debug.Log("GameManager: All players connected. Announcing game start to UIManager.");
            AnnounceGameReadyClientRpc();
        }
    }
    
    [ClientRpc]
    private void AnnounceGameReadyClientRpc()
    {
        Debug.Log("GameManager: Received AnnounceGameReadyClientRpc. Signaling UIManager.");
        if (uiManager != null)
        {
            uiManager._waitingPlayerWindow.SetActive(false);
        }
        else
        {
            Debug.LogWarning("GameManager: UIManager reference is not set, cannot hide waiting window.");
        }
    }

    [ServerRpc] 
    public void SetAllUnitsInfiniteMovementSpeedServerRpc()
    {
        foreach (UnitController unit in _unitManager.GetLiveAllUnitsForPlayer())
        {
            unit.SetInfiniteSpeedClientRpc(); // <--- Вызывайте ClientRpc версию
        }
        Debug.Log("Всем юнитам установлена 'бесконечная' скорость передвижения на сервере (запущена ClientRpc).");
    }
}