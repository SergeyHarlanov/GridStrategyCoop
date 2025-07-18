using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private Text statusText; // Текст для отображения статуса

    [Header("Настройки")]
    [SerializeField] private string gameSceneName = "GameScene"; // Название игровой сцены
    [SerializeField] private float connectionTimeout = 3f; // Время ожидания подключения (в секундах)

    private void Start()
    {
        // Подписываемся на события NetworkManager
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;

            // Запускаем логику подключения/создания сразу при старте
            StartCoroutine(TryToConnect());
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton не найден!");
            if (statusText != null)
            {
                statusText.text = "Ошибка: NetworkManager не найден.";
            }
        }
    }

    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
    }

    private IEnumerator TryToConnect()
    {
        if (statusText != null)
        {
            statusText.text = "Поиск существующей игры...";
        }
        Debug.Log("Поиск существующей игры...");

        // 1. Пытаемся запустить клиент
        NetworkManager.Singleton.StartClient();

        // 2. Ждем заданное время или пока клиент не подключится
        float timer = 0f;
        while (timer < connectionTimeout && !NetworkManager.Singleton.IsConnectedClient)
        {
            yield return null; // Ждем один кадр
            timer += Time.deltaTime;
        }

        // 3. Если по истечении тайм-аута мы всё ещё не подключены...
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("Игра не найдена. Создаем новую...");
            if (statusText != null)
            {
                statusText.text = "Игра не найдена. Создаем новую...";
            }

            // Останавливаем неудачную попытку подключения
            NetworkManager.Singleton.Shutdown();
            
            // Ждем кадр, чтобы NetworkManager успел корректно остановиться
            yield return new WaitForEndOfFrame(); 

            // ...запускаем хост
            NetworkManager.Singleton.StartHost();
        }
    }
    
    // Вызывается, когда ХОСТ успешно запущен
    private void OnServerStarted()
    {
        if (statusText != null)
        {
            statusText.text = "Вы создали игру! Ожидание игроков...";
        }
        Debug.Log("Хост успешно запущен!");
        
        // Автоматически переходим на игровую сцену
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // Вызывается, когда КЛИЕНТ успешно подключился к хосту
    private void OnClientConnected(ulong clientId)
    {
        // Проверяем, что это наш собственный клиент
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            StopAllCoroutines(); // Останавливаем корутину поиска
            if (statusText != null)
            {
                statusText.text = $"Вы успешно подключились к серверу! (ID: {clientId})";
            }
            Debug.Log($"Клиент {clientId} успешно подключился.");
        }
    }

    // Вызывается при отключении клиента
    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            if (statusText != null)
            {
                statusText.text = "Отключено от сервера.";
            }
            Debug.Log("Отключено от сервера.");
            // Здесь можно добавить логику для возврата в главное меню, если необходимо
            // SceneManager.LoadScene("MainMenuScene");
        }
    }
}