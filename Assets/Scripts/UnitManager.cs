using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Zenject;

public class UnitManager : NetworkBehaviour
{

    // Список всех активных юнитов на сцене.
    // Этот список будет поддерживаться в актуальном состоянии через события спавна/деспавна NetworkObject.
    private List<UnitController> allActiveUnits = new List<UnitController>();

    [Inject] private PlayerController _playerController;
    [Inject] private TurnManager _turnManager;
    [Inject] private GameManager _gameManager;
    
    [SerializeField] private List<UnitController> friend;

    public override void OnNetworkSpawn()
    {
     

        // Подписываемся на события спавна и деспавна сетевых объектов.
        // Используем корректные имена событий: OnSpawnedObject и OnDespawnedObject.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            Debug.Log("SUCBRIC");
            _gameManager.OnSpawnedUnit += OnNetworkObjectSpawned;
            _gameManager.OnDespawnedUnit += OnNetworkObjectDespawned;
        }
        else
        {
            Debug.LogError("UnitManager: NetworkManager.Singleton or SpawnManager is null on OnNetworkSpawn!");
            return;
        }

        // При первом спавне менеджера, добавляем уже существующие юниты.
        // Это важно для клиентов, которые подключаются к уже идущей игре.
    //    if (!IsServer) // Только для клиентов
        {
            StartCoroutine(PopulateUnitsAfterDelay());
            StartCoroutine(WaitInitializeUnitsCoroutine());
        }
        //  else
        {
          //  InitialUnits();
        }
  

    }
    
    private IEnumerator WaitInitializeUnitsCoroutine()
    {
        yield return new WaitUntil(() => enemy.Count != 0);
        foreach (UnitController item in enemy)
        {
            item.Initialize(_playerController, this, _turnManager, _gameManager);
        }

        {
            foreach (UnitController item in friend)
            {
                item.Initialize(_playerController, this, _turnManager, _gameManager);
            }
        }
    }

    private IEnumerator PopulateUnitsAfterDelay()
    {
        List<UnitController> enemyUnitsForPlayer1 = GetLiveEnemyUnitsForPlayer(1);

        while (enemyUnitsForPlayer1.Count == 0)
        {
            yield return new WaitForSeconds(0.2f);
            enemyUnitsForPlayer1 = GetLiveEnemyUnitsForPlayer(1);
            Debug.Log($"Вражеские юниты для игрока найдены."+enemyUnitsForPlayer1.Count);
        }

        InitialUnits();
 
    }

    private void InitialUnits()
    {
        GetLiveEnemyUnitsForPlayer(1);
        GetLiveUnitsForPlayer(1);
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий, чтобы избежать утечек памяти.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            
_gameManager.OnSpawnedUnit -= OnNetworkObjectSpawned;
_gameManager.OnDespawnedUnit -= OnNetworkObjectDespawned;
        }

    }

    // Обработчик события спавна сетевого объекта
    private void OnNetworkObjectSpawned(NetworkObject networkObject)
    {
        if (networkObject.TryGetComponent(out UnitController unit))
        {
            if (!allActiveUnits.Contains(unit))
            {
                allActiveUnits.Add(unit);
                Debug.Log($"UnitManager: Added new unit {unit.name} to tracking. Total units: {allActiveUnits.Count}");
            }
        }
    }

    // Обработчик события деспавна сетевого объекта
    private void OnNetworkObjectDespawned(NetworkObject networkObject)
    {
        if (networkObject.TryGetComponent(out UnitController unit))
        {
            if (allActiveUnits.Remove(unit))
            {
                Debug.Log($"UnitManager: Removed unit {unit.name} from tracking. Total units: {allActiveUnits.Count}");
            }
        }
    }

 
    /// <summary>
    /// Возвращает список всех живых юнитов, принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveUnitsForPlayer(ulong clientId)
    {
        Debug.Log("UpdateFriendMark");

        if (friend.Count > 0)
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
        friend = new List<UnitController>(playerUnits);
     //   friend.ForEach(x=>x.Initialize(_playerController, this, _turnManager));
        return playerUnits;
    }

    [SerializeField] private List<UnitController> enemy;
    /// <summary>
    /// Возвращает список всех живых юнитов, НЕ принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveEnemyUnitsForPlayer(ulong clientId)
    {
        if (enemy.Count > 0)
        {
        //    return enemy;
        }
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
        enemy = new List<UnitController>(enemyUnits);
     //   enemy.ForEach(x=>x.Initialize(_playerController, this, _turnManager));
        return enemyUnits;
    }
    
    /// <summary>
    /// Возвращает количество живых юнитов, НЕ принадлежащих данному ClientId.

    
    public List<UnitController> GetLiveAllUnitsForPlayer()
    {
        return allActiveUnits;
    }
}