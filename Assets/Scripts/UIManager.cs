// UIManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    [Header("Turn Management UI")]
    [SerializeField] private TextMeshProUGUI currentPlayerText;
    [SerializeField] private TextMeshProUGUI timeRemainingText;
    [SerializeField] private TextMeshProUGUI actionsRemainingText;

    [Header("General Game UI")]
    [SerializeField] private TextMeshProUGUI statusMessageText; 
    [SerializeField] private GameObject gameUIContainer; 

    private TurnManager turnManager; // Теперь будем получать через Singleton

    void Awake()
    {
        Debug.Log("UIManager: Awake called.");
    }

    void Start()
    {
        Debug.Log("UIManager: Start called.");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("UIManager: NetworkManager.Singleton is NOT found! Cannot initialize UI.");
            gameUIContainer?.SetActive(false);
            return;
        }

        // --- ИЗМЕНЕНИЕ ЗДЕСЬ: Используем TurnManager.Singleton ---
        // Проверяем, что Singleton TurnManager уже инициализирован
        if (TurnManager.Singleton == null)
        {
            // Если он не найден сразу (например, из-за порядка выполнения скриптов),
            // можно попробовать найти его или отложить инициализацию.
            // Для сетевых синглтонов лучше дождаться, пока он будет готов.
            Debug.LogError("UIManager: TurnManager.Singleton is NOT ready yet! Retrying later...");
            // Можно добавить задержку или использовать корутину для ожидания
            // Для немедленной отладки, если это не работает, то проблема в порядке инициализации
            gameUIContainer?.SetActive(false);
            return;
        }
        turnManager = TurnManager.Singleton; // Присваиваем ссылку

        Debug.Log("UIManager: TurnManager found via Singleton.");
        // --- КОНЕЦ ИЗМЕНЕНИЯ ---

        turnManager.CurrentPlayerClientId.OnValueChanged += OnCurrentPlayerChanged;
        turnManager.TimeRemainingInTurn.OnValueChanged += OnTimeRemainingChanged;
        turnManager.ActionsRemaining.OnValueChanged += OnActionsRemainingChanged;
        turnManager.OnTurnStartAnnounce += OnTurnStartAnnounceHandler; 
        Debug.Log("UIManager: Subscribed to TurnManager events.");

        UpdateUI(turnManager.CurrentPlayerClientId.Value, turnManager.TimeRemainingInTurn.Value, turnManager.ActionsRemaining.Value);
        SetStatusMessage("Ожидание подключения других игроков...", Color.white);
        Debug.Log("UIManager: Initial UI update performed.");

        if (gameUIContainer != null)
        {
            gameUIContainer.SetActive(true);
            Debug.Log("UIManager: Game UI Container activated.");
        }
        else
        {
            Debug.LogWarning("UIManager: Game UI Container not assigned in Inspector!");
        }
    }

    void OnDestroy()
    {
        Debug.Log("UIManager: OnDestroy called.");

        if (turnManager != null)
        {
            turnManager.CurrentPlayerClientId.OnValueChanged -= OnCurrentPlayerChanged;
            turnManager.TimeRemainingInTurn.OnValueChanged -= OnTimeRemainingChanged;
            turnManager.ActionsRemaining.OnValueChanged -= OnActionsRemainingChanged;
            turnManager.OnTurnStartAnnounce -= OnTurnStartAnnounceHandler;
            Debug.Log("UIManager: Unsubscribed from TurnManager events.");
        }

        gameUIContainer?.SetActive(false);
    }

    void Update()
    {
        // Убедитесь, что NetworkManager и TurnManager готовы, и это клиентская сторона (хост тоже является клиентом)
        // Теперь проверяем turnManager на null, так как он может быть null, если TurnManager.Singleton не был готов в Start()
        if (NetworkManager.Singleton == null || turnManager == null || !NetworkManager.Singleton.IsClient) return;

        // Обновление UI времени КАЖДЫЙ КАДР
        if (timeRemainingText != null)
        {
            timeRemainingText.text = $"Время: {Mathf.CeilToInt(turnManager.TimeRemainingInTurn.Value)}с";
            
            if (NetworkManager.Singleton.IsHost) 
            {
                Debug.Log($"[Host UI Debug] TimeRemainingInTurn.Value: {turnManager.TimeRemainingInTurn.Value:F1}");
            }
            else 
            {
                Debug.Log($"[Client UI Debug] TimeRemainingInTurn.Value: {turnManager.TimeRemainingInTurn.Value:F1}");
            }
        }
        else
        {
            Debug.LogWarning("UIManager: TimeRemainingText not assigned in Inspector!");
        }
        
    }

    private void OnCurrentPlayerChanged(ulong oldId, ulong newId)
    {
        // Проверка на turnManager на случай, если событие пришло до инициализации
        if (turnManager == null) return;
        Debug.Log($"UIManager: Current Player ID changed to {newId}. Updating UI.");
        UpdateUI(newId, turnManager.TimeRemainingInTurn.Value, turnManager.ActionsRemaining.Value);
    }

    private void OnTimeRemainingChanged(float oldTime, float newTime)
    {
        Debug.Log($"UIManager: Time remaining NetworkVariable changed event received: {newTime:F1}.");
    }

    private void OnActionsRemainingChanged(int oldActions, int newActions)
    {
        // Проверка на turnManager на случай, если событие пришло до инициализации
        if (turnManager == null) return;
        Debug.Log($"UIManager: Actions remaining NetworkVariable changed to {newActions}. Updating UI.");
        UpdateUI(turnManager.CurrentPlayerClientId.Value, turnManager.TimeRemainingInTurn.Value, newActions);
    }

    private void OnTurnStartAnnounceHandler(ulong playerClientId)
    {
        Debug.Log($"UIManager: Received OnTurnStartAnnounce for client ID: {playerClientId}");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == playerClientId)
        {
            SetStatusMessage("ВАШ ХОД!", Color.green);
        }
        else
        {
            SetStatusMessage($"Ход игрока {playerClientId}", Color.yellow);
        }
    }
    
    private void UpdateUI(ulong currentPlayerId, float timeRemaining, int actionsRemaining)
    {
        if (currentPlayerText != null)
        {
            string currentPlayerName = "Нет игрока";
            Color textColor = Color.white;

            if (currentPlayerId != 0) 
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == currentPlayerId)
                {
                    currentPlayerName = "ВЫ";
                    textColor = Color.green;
                }
                else
                {
                    currentPlayerName = $"Игрок {currentPlayerId}";
                    textColor = Color.white; 
                }
            }
            currentPlayerText.text = $"Текущий ход: {currentPlayerName}";
            currentPlayerText.color = textColor;
            Debug.Log($"[UI Debug] Setting CurrentPlayerText: {currentPlayerText.text}"); 
        }
        else
        {
            Debug.LogWarning("UIManager: CurrentPlayerText not assigned in Inspector!");
        }

        if (actionsRemainingText != null)
        {
            actionsRemainingText.text = $"Действий: {actionsRemaining}";
            actionsRemainingText.color = (actionsRemaining > 0) ? Color.white : Color.red;
            Debug.Log($"[UI Debug] Setting ActionsRemainingText: {actionsRemainingText.text}");
        }
        else
        {
            Debug.LogWarning("UIManager: ActionsRemainingText not assigned in Inspector!");
        }
    }

    public void SetStatusMessage(string message, Color color)
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = message;
            statusMessageText.color = color;
            Debug.Log($"[UI Debug] Setting StatusMessageText: {statusMessageText.text}");
        }
        else
        {
            Debug.LogWarning("UIManager: StatusMessageText not assigned in Inspector!");
        }
    }
}