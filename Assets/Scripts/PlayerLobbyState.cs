using Unity.Netcode;
using UnityEngine;

public class PlayerLobbyState : NetworkBehaviour
{
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(false);

    // Вызывается только на владельце NetworkObject (локальный клиент)
    public void SetReadyState(bool ready)
    {
        if (IsOwner) // Только владелец может изменять свой статус готовности
        {
            SetReadyStateServerRpc(ready);
        }
    }

    [ServerRpc] // Вызывается клиентом, выполняется на сервере
    private void SetReadyStateServerRpc(bool ready)
    {
        IsReady.Value = ready;
        Debug.Log($"Player {OwnerClientId} is now ready: {ready}");

        // После изменения статуса готовности, сервер может проверить, все ли готовы
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (!IsServer) return; // Только сервер проверяет это

        bool allReady = true;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                PlayerLobbyState playerState = client.PlayerObject.GetComponent<PlayerLobbyState>();
                if (playerState != null && !playerState.IsReady.Value)
                {
                    allReady = false;
                    break;
                }
            }
        }

        if (allReady)
        {
            Debug.Log("Все игроки готовы! Начинаем игру...");
            // Загрузка игровой сцены
            NetworkManager.Singleton.SceneManager.LoadScene("GameSceneName", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Подписываемся на изменение NetworkVariable (для UI или других визуальных обновлений)
        IsReady.OnValueChanged += OnReadyStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        IsReady.OnValueChanged -= OnReadyStateChanged;
    }

    private void OnReadyStateChanged(bool oldReady, bool newReady)
    {
        Debug.Log($"Player {OwnerClientId} ready state changed from {oldReady} to {newReady}");
        // Здесь можно обновить UI, чтобы показать, что игрок готов
    }
}