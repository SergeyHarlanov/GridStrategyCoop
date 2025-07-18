using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject unitPrefab;

    [Header("Точки спавна")]
    [SerializeField] private Transform[] player1SpawnPoints;
    [SerializeField] private Transform[] player2SpawnPoints;

    private bool gameStarted = false;

    public override void OnNetworkSpawn()
    {
        // Проверяем, есть ли на этом объекте NetworkObject. Без этого метод не вызовется.
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Сервер запущен. Жду клиентов...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

           // if (NetworkManager.Singleton.ConnectedClients.Count == 1)
            {
                SpawnGroupUnits();
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Выводим в консоль каждый раз, когда кто-то подключается
        Debug.Log($"GameManager: Клиент {clientId} подключился. Всего клиентов: {NetworkManager.Singleton.ConnectedClients.Count}");

        SpawnGroupUnits();
    }
    
    private void SpawnGroupUnits()
    {
        if (IsServer && NetworkManager.Singleton.ConnectedClients.Count >= 1 && !gameStarted)
        {
            //     gameStarted = true;
            Debug.Log("!!! GameManager: УСЛОВИЕ ДЛЯ СТАРТА ВЫПОЛНЕНО. Начинаю спавн. !!!");



            if (NetworkManager.Singleton.ConnectedClients.Count == 1)
            {
                ulong player1Id = NetworkManager.Singleton.ConnectedClientsIds[0];
                SpawnUnitsForPlayer(player1Id, player1SpawnPoints);
            }

            if (NetworkManager.Singleton.ConnectedClients.Count == 2)
            {
                ulong player2Id = NetworkManager.Singleton.ConnectedClientsIds[1];
                SpawnUnitsForPlayer(player2Id, player2SpawnPoints);
            }

        }
        else
        {          Debug.Log($"$!!! GameManager: УСЛОВИЕ ДЛЯ СТАРТА  НЕ ВЫПОЛНЕНО. Начинаю спавн. !!! {IsServer} : {NetworkManager.Singleton.ConnectedClients.Count} : {gameStarted}");
        }

    }

    private void SpawnUnitsForPlayer(ulong ownerId, Transform[] spawnPoints)
    {
        Debug.Log($"-- Спавню 5 юнитов для игрока {ownerId} --");
        if(spawnPoints.Length < 5)
        {
            Debug.LogError($"!!! ОШИБКА: Недостаточно точек спавна для игрока {ownerId}. Нужно 5. Найдено: {spawnPoints.Length}");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject unitInstance = Instantiate(unitPrefab, spawnPoints[i].position, spawnPoints[i].rotation);
            NetworkObject networkObject = unitInstance.GetComponent<NetworkObject>();
            networkObject.SpawnWithOwnership(ownerId);
            Debug.Log($"Юнит {i+1} для игрока {ownerId} создан.");
        }
    }
}