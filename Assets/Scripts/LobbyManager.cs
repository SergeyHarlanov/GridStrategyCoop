using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Singleton { get; private set; } 
    
    [Header("UI Элементы")]
    [SerializeField] private Text statusText; 

    [Header("Настройки сцен")]
    [SerializeField] private string gameSceneName = "GameScene"; 
    [SerializeField] private string menuSceneName = "MenuScene"; 
    [SerializeField] private float connectionTimeout = 3f;

    void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("LobbyManager: Awake called. DontDestroyOnLoad applied.");
    }
    
    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;

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

        NetworkManager.Singleton.StartClient();

        float timer = 0f;
        while (timer < connectionTimeout && !NetworkManager.Singleton.IsConnectedClient)
        {
            yield return null; // Ждем один кадр
            timer += Time.deltaTime;
        }

        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("Игра не найдена. Создаем новую...");
            if (statusText != null)
            {
                statusText.text = "Игра не найдена. Создаем новую...";
            }

            NetworkManager.Singleton.Shutdown();
            
            yield return new WaitForEndOfFrame(); 

            NetworkManager.Singleton.StartHost();
        }
    }
    
    private void OnServerStarted()
    {
        if (statusText != null)
        {
            statusText.text = "Вы создали игру! Ожидание игроков...";
        }
        Debug.Log("Хост успешно запущен!");
        
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            StopAllCoroutines(); 
            if (statusText != null)
            {
                statusText.text = $"Вы успешно подключились к серверу! (ID: {clientId})";
            }
            Debug.Log($"Клиент {clientId} успешно подключился.");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            if (statusText != null)
            {
                statusText.text = "Отключено от сервера.";
            }
            Debug.Log("Отключено от сервера.");
        }
    }
}