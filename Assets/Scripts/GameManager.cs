using System;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    public static GameManager Singleton { get; private set; }
    [Header("Префабы")]
    // Предполагаем, что _unitsPrefabForSpawn[0] - это префаб юнита, который вы хотите спавнить 5 раз.
    [SerializeField] private GameObject[] _unitsPrefabForSpawn; 

    [Header("Точки спавна")]
    [SerializeField] private Transform[] player1SpawnPoints;
    [SerializeField] private Transform[] player2SpawnPoints;

    public  int MAX_PLAYERS = 2; // Максимальное количество игроков в комнате
    
    // Флаги для отслеживания, были ли уже заспавнены юниты для каждого игрока
    private bool player1UnitsSpawned = false;
    private bool player2UnitsSpawned = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Server started. Waiting for clients...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            // NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected; // Можно добавить для обработки отключений

            // Проверяем состояние при старте сервера на случай, если клиенты уже подключены
            // Например, при перезапуске сервера.
            CheckAndSpawnExistingClients();
        }
    }

    private void Awake()
    {
        Singleton = this;
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
        Debug.Log($"GameManager: Client {clientId} connected. Total clients: {NetworkManager.Singleton.ConnectedClients.Count}");

        // Отклоняем подключение, если в комнате уже максимум игроков
        if (NetworkManager.Singleton.ConnectedClients.Count > MAX_PLAYERS)
        {
            Debug.LogWarning($"GameManager: Client {clientId} attempted to connect but room is full (max {MAX_PLAYERS} players). Disconnecting client.");
            NetworkManager.Singleton.DisconnectClient(clientId);
            return; // Прекращаем выполнение метода
        }

        // Вызываем логику спавна юнитов для только что подключившегося клиента
        SpawnUnitsForConnectedClient(clientId);
    }

    // Метод для проверки и спавна юнитов для уже подключенных клиентов при старте сервера
    private void CheckAndSpawnExistingClients()
    {
        if (NetworkManager.Singleton.ConnectedClients.Count >= 1 && !player1UnitsSpawned)
        {
            ulong player1Id = NetworkManager.Singleton.ConnectedClientsIds[0];
            SpawnUnitsForPlayer(player1Id, player1SpawnPoints);
            player1UnitsSpawned = true;
        }

        if (NetworkManager.Singleton.ConnectedClients.Count >= 2 && !player2UnitsSpawned)
        {
            ulong player2Id = NetworkManager.Singleton.ConnectedClientsIds[1];
            SpawnUnitsForPlayer(player2Id, player2SpawnPoints);
            player2UnitsSpawned = true;
        }
    }

    // Метод, который вызывается при каждом подключении клиента для спавна его юнитов
    private void SpawnUnitsForConnectedClient(ulong newClientId)
    {
        if (!IsServer) return; // Убедимся, что мы на сервере

        // Проверяем, является ли это первым игроком, и не спавнили ли мы для него юнитов
        if (NetworkManager.Singleton.ConnectedClientsIds[0] == newClientId && !player1UnitsSpawned)
        {
            Debug.Log($"GameManager: Spawning units for Player 1 (Client ID: {newClientId})");
            SpawnUnitsForPlayer(newClientId, player1SpawnPoints);
            player1UnitsSpawned = true;
        }
        // Проверяем, является ли это вторым игроком, и не спавнили ли мы для него юнитов
        else if (NetworkManager.Singleton.ConnectedClients.Count >= 2 && NetworkManager.Singleton.ConnectedClientsIds[1] == newClientId && !player2UnitsSpawned)
        {
            Debug.Log($"GameManager: Spawning units for Player 2 (Client ID: {newClientId})");
            SpawnUnitsForPlayer(newClientId, player2SpawnPoints);
            player2UnitsSpawned = true;
        }
        else
        {
            Debug.Log($"GameManager: Units already spawned for client {newClientId} or not their turn.");
        }
    }

    private void SpawnUnitsForPlayer(ulong ownerId, Transform[] spawnPoints)
    {
        Debug.Log($"-- Spawning 5 units for player {ownerId} --");
        if (spawnPoints.Length < 5)
        {
            Debug.LogError($"!!! ERROR: Not enough spawn points for player {ownerId}. Need 5. Found: {spawnPoints.Length}");
            return;
        }
        
        // Убедимся, что у нас есть хотя бы один префаб для спавна
        if (_unitsPrefabForSpawn == null || _unitsPrefabForSpawn.Length == 0 || _unitsPrefabForSpawn[0] == null)
        {
            Debug.LogError("!!! ERROR: No unit prefab assigned in _unitsPrefabForSpawn[0]! Cannot spawn units.");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject unitInstance = Instantiate(_unitsPrefabForSpawn[i], spawnPoints[i].position, spawnPoints[i].rotation);
            unitInstance.name += Random.Range(0, 9999); 

            NetworkObject networkObject = unitInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"!!! ERROR: Prefab {_unitsPrefabForSpawn[0].name} is missing NetworkObject component!");
                Destroy(unitInstance); 
                continue;
            }

            networkObject.SpawnWithOwnership(ownerId); 
            Debug.Log($"Unit {unitInstance.name} for player {ownerId} spawned.");
        }
    }
}