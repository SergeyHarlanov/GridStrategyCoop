using System;
using Unity.Netcode;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    public int MAX_PLAYERS = 2; // Максимальное количество игроков в комнате
    
    public event Action<NetworkObject> OnSpawnedUnit; 
    public event Action<NetworkObject> OnDespawnedUnit;

    [Inject] private DiContainer _container;
    [Inject] private UnitManager _unitManager;
    [Inject] private PlayerController _playerController;
    [Inject] private UIManager uiManager;
    [Inject] private GameSettings _gameSettings;
    
    private bool _player1UnitsSpawned = false;
    private bool _player2UnitsSpawned = false;
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Server started. Waiting for clients...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            CheckAndSpawnExistingClients();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            // NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"GameManager: Client {clientId} connected.");

        CheckAndSpawnExistingClients(); // Проверяем и спавним юнитов, если все игроки подключены

        // НОВОЕ: Если количество подключенных клиентов достигло MAX_PLAYERS
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
    
    private void CheckAndSpawnExistingClients()
    {
        if (NetworkManager.Singleton.ConnectedClients.Count >= 1 && !_player1UnitsSpawned)
        {
            ulong player1Id = NetworkManager.Singleton.ConnectedClientsIds[0];
            SpawnUnitsForPlayer(player1Id, _gameSettings.PointsSpawnPlayer1);
            _player1UnitsSpawned = true;
        }

        if (NetworkManager.Singleton.ConnectedClients.Count >= 2 && !_player2UnitsSpawned)
        {
            ulong player2Id = NetworkManager.Singleton.ConnectedClientsIds[1];
            SpawnUnitsForPlayer(player2Id, _gameSettings.PointsSpawnPlayer2);
            _player2UnitsSpawned = true;
        }
    }

    private void SpawnUnitsForConnectedClient(ulong newClientId)
    {
        if (!IsServer) return; // Убедимся, что мы на сервере

        if (NetworkManager.Singleton.ConnectedClientsIds[0] == newClientId && !_player1UnitsSpawned)
        {
            Debug.Log($"GameManager: Spawning units for Player 1 (Client ID: {newClientId})");
            SpawnUnitsForPlayer(newClientId, _gameSettings.PointsSpawnPlayer1);
            _player1UnitsSpawned = true;
        }
        else if (NetworkManager.Singleton.ConnectedClients.Count >= 2 && NetworkManager.Singleton.ConnectedClientsIds[1] == newClientId && !_player2UnitsSpawned)
        {
            Debug.Log($"GameManager: Spawning units for Player 2 (Client ID: {newClientId})");
            SpawnUnitsForPlayer(newClientId, _gameSettings.PointsSpawnPlayer2);
            _player2UnitsSpawned = true;
        }
        else
        {
            Debug.Log($"GameManager: Units already spawned for client {newClientId} or not their turn.");
        }
    }

    private void SpawnUnitsForPlayer(ulong ownerId, Transform[] spawnPoints)
    {
        // ИЗМЕНЕНО: Проверка использует новое поле
        Debug.Log($"-- Spawning {_gameSettings.NumberOfUnits} units for player {ownerId} --");
        if (spawnPoints.Length < _gameSettings.NumberOfUnits)
        {
            // ИЗМЕНЕНО: Сообщение об ошибке теперь динамическое
            Debug.LogError($"!!! ERROR: Not enough spawn points for player {ownerId}. Need {_gameSettings.NumberOfUnits}. Found: {spawnPoints.Length}");
            return;
        }
        
        if (_gameSettings.PrefabUnits.Length == 0)
        {
            Debug.LogError("!!! ERROR: No unit prefab assigned in GameSettings");
            return;
        }

        // ИЗМЕНЕНО: Цикл использует новое поле
        for (int i = 0; i < _gameSettings.NumberOfUnits; i++)
        {
            GameObject randUnit = _gameSettings.PrefabUnits[Random.Range(0, _gameSettings.PrefabUnits.Length)];
            GameObject unitInstance = Instantiate(randUnit, spawnPoints[i].position, spawnPoints[i].rotation);
            unitInstance.name += Random.Range(0, 9999);

            NetworkObject networkObject = unitInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"!!! ERROR: Prefab {randUnit.name} is missing NetworkObject component!");
                Destroy(unitInstance); 
                continue;
            }
            Debug.Log(networkObject);
            OnSpawnedUnit.Invoke(networkObject);
            networkObject.SpawnWithOwnership(ownerId); 
            Debug.Log($"Unit {unitInstance.name} for player {ownerId} spawned.");
        }
    }

    public void DespawnUnits(NetworkObject networkObject)
    {
        OnDespawnedUnit.Invoke(networkObject);
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