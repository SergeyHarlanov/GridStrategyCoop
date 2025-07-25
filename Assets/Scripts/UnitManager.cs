using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public class UnitManager : NetworkBehaviour
{
    public event Action<NetworkObject> OnSpawnedUnit; 
    public event Action<NetworkObject> OnDespawnedUnit;
    
    [Inject] private PlayerController _playerController;
    [Inject] private TurnManager _turnManager;
    [Inject] private GameManager _gameManager;
    [Inject] private GameSettings _gameSettings;
    
    private List<UnitController> _friend = new List<UnitController>();
    private List<UnitController> _enemy = new List<UnitController>();

    private bool _player1UnitsSpawned = false;
    private bool _player2UnitsSpawned = false;
    
    private List<UnitController> _allActiveUnits = new List<UnitController>();

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
      
            OnSpawnedUnit += OnNetworkObjectSpawned;
            OnDespawnedUnit += OnNetworkObjectDespawned;
     
        }
        else
        {
            Debug.LogError("UnitManager: NetworkManager.Singleton or SpawnManager is null on OnNetworkSpawn!");
            return;
        }
       
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Server started. Waiting for clients...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        
        CheckAndSpawnExistingClients();

        StartCoroutine(WaitInitializeUnitsCoroutine());

    }
    
    //ожидаем когда все игроки добавяться в массив что бы их инцииализировать 
    private IEnumerator WaitInitializeUnitsCoroutine()
    {
        while (_friend.Count == 0)
        {
            GetLiveEnemyUnitsForPlayer();
            GetLiveUnitsForPlayer();
            yield return new WaitForSeconds(0.1f);
        }

        foreach (UnitController item in _friend)
        {
           item.Initialize(_playerController, this, _turnManager, _gameManager);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            OnSpawnedUnit -= OnNetworkObjectSpawned;
            OnDespawnedUnit -= OnNetworkObjectDespawned;
        }
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnNetworkObjectSpawned(NetworkObject networkObject)
    {
        if (networkObject.TryGetComponent(out UnitController unit))
        {
            if (!_allActiveUnits.Contains(unit))
            {
                _allActiveUnits.Add(unit);
                Debug.Log($"UnitManager: Added new unit {unit.name} to tracking. Total units: {_allActiveUnits.Count}");
            }
        }
    }

    private void OnNetworkObjectDespawned(NetworkObject networkObject)
    {
        if (networkObject.TryGetComponent(out UnitController unit))
        {
            if (_allActiveUnits.Remove(unit))
            {
                Debug.Log($"UnitManager: Removed unit {unit.name} from tracking. Total units: {_allActiveUnits.Count}");
            }
        }
    }

 
    /// <summary>
    /// Возвращает список всех живых юнитов, принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveUnitsForPlayer()
    {
        Debug.Log("UpdateFriendMark"+NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.Count);

        if (_friend.Count > 0)
        {
         //   return friend;
        }
        List<UnitController> playerUnits = new List<UnitController>();
        foreach (var item in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            UnitController unit = item.GetComponent<UnitController>();
            if (unit != null && unit.IsSpawned && unit.IsOwner && unit.currentHP.Value > 0)
            {
                playerUnits.Add(unit);
            }
        }
        _friend = new List<UnitController>(playerUnits);
        return playerUnits;
    }

    /// <summary>
    /// Возвращает список всех живых юнитов, НЕ принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveEnemyUnitsForPlayer()
    {
        List<UnitController> enemyUnits = new List<UnitController>();
        foreach (var item in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
           UnitController unit = item.GetComponent<UnitController>();
            // Убедитесь, что юнит не принадлежит текущему игроку и не является сервером, если вы хост
            // (или просто другим клиентом, если вы клиент), и его HP больше 0
            if (unit != null && unit.IsSpawned && !unit.IsOwner && unit.currentHP.Value > 0)
            {
                enemyUnits.Add(unit);
            }
        }
        _enemy = new List<UnitController>(enemyUnits);
        return enemyUnits;
    }
    
    /// <summary>
    /// Возвращает количество живых юнитов, НЕ принадлежащих данному ClientId.

    public List<UnitController> GetLiveAllUnitsForPlayer()
    {
        return _allActiveUnits;
    }
    
    private void CheckAndSpawnExistingClients()
    {
        if (IsServer)
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
    }
    
    private void SpawnUnitsForPlayer(ulong ownerId, Transform[] spawnPoints)
    {
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

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            Debug.Log($"GameManager: Client {clientId} connected.");

            CheckAndSpawnExistingClients(); // Проверяем и спавним юнитов, если все игроки подключены
        }
    }
    
    public void DespawnUnits(NetworkObject networkObject)
    {
        OnDespawnedUnit.Invoke(networkObject);
    }
}