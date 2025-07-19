using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UnitManager : NetworkBehaviour
{
    public static UnitManager Singleton { get; private set; }

    // Список всех активных юнитов на сцене.
    // Этот список будет поддерживаться в актуальном состоянии через события спавна/деспавна NetworkObject.
    private List<UnitController> allActiveUnits = new List<UnitController>();

    public override void OnNetworkSpawn()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;

        // Подписываемся на события спавна и деспавна сетевых объектов.
        // Используем корректные имена событий: OnSpawnedObject и OnDespawnedObject.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            GameManager.Singleton.OnSpawnedUnit += OnNetworkObjectSpawned;
            GameManager.Singleton.OnDespawnedUnit += OnNetworkObjectDespawned;
        }
        else
        {
            Debug.LogError("UnitManager: NetworkManager.Singleton or SpawnManager is null on OnNetworkSpawn!");
            return;
        }

        // При первом спавне менеджера, добавляем уже существующие юниты.
        // Это важно для клиентов, которые подключаются к уже идущей игре.
        foreach (NetworkObject netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (netObj.TryGetComponent(out UnitController unit))
            {
                if (!allActiveUnits.Contains(unit))
                {
                    allActiveUnits.Add(unit);
                }
            }
        }

        Debug.Log("UnitManager: Initialized and populated with existing units.");
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий, чтобы избежать утечек памяти.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            GameManager.Singleton.OnSpawnedUnit -= OnNetworkObjectSpawned;
            GameManager.Singleton.OnDespawnedUnit -= OnNetworkObjectDespawned;
        }

        if (Singleton == this)
        {
            Singleton = null;
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
    /// Возвращает количество живых юнитов для данного ClientId.
    /// </summary>
    public int GetLiveEnemyUnitCountForPlayer(ulong clientId)
    {
        int count = 0;
        foreach (UnitController unit in allActiveUnits)
        {
            // Проверяем, что юнит принадлежит этому игроку и его HP больше 0
            if (unit != null && unit.IsSpawned && unit.OwnerClientId == clientId && unit.currentHP.Value > 0)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Возвращает список всех живых юнитов, принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveUnitsForPlayer(ulong clientId)
    {
        List<UnitController> playerUnits = new List<UnitController>();
        foreach (UnitController unit in allActiveUnits)
        {
            if (unit != null && unit.IsSpawned && unit.OwnerClientId == clientId && unit.currentHP.Value > 0)
            {
                playerUnits.Add(unit);
            }
        }
        return playerUnits;
    }

    /// <summary>
    /// Возвращает список всех живых юнитов, НЕ принадлежащих данному ClientId.
    /// </summary>
    public List<UnitController> GetLiveEnemyUnitsForPlayer(ulong clientId)
    {
        List<UnitController> enemyUnits = new List<UnitController>();
        foreach (UnitController unit in allActiveUnits)
        {
            // Убедитесь, что юнит не принадлежит текущему игроку и не является сервером, если вы хост
            // (или просто другим клиентом, если вы клиент), и его HP больше 0
            if (unit != null && unit.IsSpawned && unit.OwnerClientId != clientId && unit.currentHP.Value > 0)
            {
                enemyUnits.Add(unit);
            }
        }
        return enemyUnits;
    }
    
    /// <summary>
    /// Возвращает количество живых юнитов, НЕ принадлежащих данному ClientId.
    /// </summary>
    public int GetLiveFriendUnitCountForPlayer(ulong clientId)
    {
        int count = 0;
        foreach (UnitController unit in allActiveUnits)
        {
            // Проверяем, что юнит не принадлежит этому игроку и его HP больше 0
            if (unit != null && unit.IsSpawned && unit.OwnerClientId != clientId && unit.currentHP.Value > 0)
            {
                count++;
            }
        }
        return count;
    }
}