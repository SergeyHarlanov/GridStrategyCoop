using System;
using Unity.Netcode;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{

   [Inject] private PlayerController _playerController;
    [Header("Префабы")]
    // Предполагаем, что _unitsPrefabForSpawn[0] - это префаб юнита, который вы хотите спавнить 5 раз.
    [SerializeField] private GameObject[] _unitsPrefabForSpawn; 
    // Добавим ссылку на UIManager
    [SerializeField] private UIManager uiManager; 
    
    [Header("Точки спавна")]
    [SerializeField] private Transform[] player1SpawnPoints;
    [SerializeField] private Transform[] player2SpawnPoints;

    public  int MAX_PLAYERS = 2; // Максимальное количество игроков в комнате
    
    // Флаги для отслеживания, были ли уже заспавнены юниты для каждого игрока
    private bool player1UnitsSpawned = false;
    private bool player2UnitsSpawned = false;

    public event Action<NetworkObject> OnSpawnedUnit; 
    public event Action<NetworkObject> OnDespawnedUnit;

    [Inject] private DiContainer _container;
    [Inject] private UnitManager _unitManager;
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
    // НОВОЕ: ClientRpc для оповещения всех клиентов о готовности игры
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

    // Метод для проверки и спавна юнитов для уже подключенных клиентов при старте сервера
    private void CheckAndSpawnExistingClients()
    {
        Debug.Log("Spawning");
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
    [ServerRpc] // Этот метод в GameManager может быть ServerRpc, если его вызывает клиент (например, чит-кнопка)
    // Но если он вызывается только сервером (например, из OnNetworkSpawn), атрибут [ServerRpc] здесь не нужен.
    public void SetAllUnitsInfiniteMovementSpeedServerRpc()
    {
        foreach (UnitController unit in _unitManager.GetLiveAllUnitsForPlayer())
        {
            // НОВОЕ: Вызывайте ClientRpc метод
            unit.SetInfiniteSpeedClientRpc(); // <--- Вызывайте ClientRpc версию
        }
        Debug.Log("Всем юнитам установлена 'бесконечная' скорость передвижения на сервере (запущена ClientRpc).");
    }
}