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
    [SerializeField] private TextMeshProUGUI turnNumberText; // <--- НОВОЕ: Для номера хода
    [SerializeField] private TextMeshProUGUI movementPossibleText; // <--- НОВОЕ: Для возможности передвижения
    [SerializeField] private TextMeshProUGUI attackPossibleText;   // <--- НОВОЕ: Для возможности атаки
    
    [Header("General Game UI")]
    [SerializeField] private TextMeshProUGUI statusMessageText; 
    [SerializeField] private GameObject gameUIContainer; 
    [SerializeField] public GameObject _waitingPlayerWindow;
    [SerializeField] public GameObject _EndGameWindow;
    [SerializeField] private Text endGameResultText;   // <--- НОВОЕ: Для возможности атаки

    private TurnManager turnManager; 

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

        if (TurnManager.Singleton == null)
        {
            Debug.LogError("UIManager: TurnManager.Singleton is NOT ready yet! Retrying later...");
            gameUIContainer?.SetActive(false);
            return;
        }
        turnManager = TurnManager.Singleton; 

        Debug.Log("UIManager: TurnManager found via Singleton.");

        turnManager.CurrentPlayerClientId.OnValueChanged += OnCurrentPlayerChanged;
        turnManager.TimeRemainingInTurn.OnValueChanged += OnTimeRemainingChanged;
        turnManager.ActionsRemaining.OnValueChanged += OnActionsRemainingChanged;
        turnManager.TurnNumber.OnValueChanged += OnTurnNumberChanged; // <--- НОВОЕ: Подписка на номер хода
        turnManager.OnTurnStartAnnounce += OnTurnStartAnnounceHandler; 
        turnManager.OnEndGameAnnounce += OnEndGameHandler; 
        Debug.Log("UIManager: Subscribed to TurnManager events.");

        // Инициализируем UI с текущими значениями
        UpdateUI(turnManager.CurrentPlayerClientId.Value, turnManager.TimeRemainingInTurn.Value, turnManager.ActionsRemaining.Value, turnManager.TurnNumber.Value);
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

        // Initially show the waiting window if this client is the host (ID 0)
        // This is a common pattern for the host to wait for other players.
        if (NetworkManager.Singleton.IsHost && _waitingPlayerWindow != null)
        {
            _waitingPlayerWindow.SetActive(true);
            Debug.Log("UIManager: Waiting Player Window activated for Host.");
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
            turnManager.TurnNumber.OnValueChanged -= OnTurnNumberChanged; // <--- НОВОЕ: Отписка
            turnManager.OnTurnStartAnnounce -= OnTurnStartAnnounceHandler;
            turnManager.OnEndGameAnnounce -= OnEndGameHandler; 
            Debug.Log("UIManager: Unsubscribed from TurnManager events.");
        }

        gameUIContainer?.SetActive(false);
    }

    void Update()
    {
        if (NetworkManager.Singleton == null || turnManager == null || !NetworkManager.Singleton.IsClient) return;

        // Обновление UI времени КАЖДЫЙ КАДР
        if (timeRemainingText != null)
        {
            timeRemainingText.text = $"Время: {Mathf.CeilToInt(turnManager.TimeRemainingInTurn.Value)}с";
        }
        else
        {
            Debug.LogWarning("UIManager: TimeRemainingText not assigned in Inspector!");
        }
    }

    private void OnCurrentPlayerChanged(ulong oldId, ulong newId)
    {
        if (turnManager == null) return;
        Debug.Log($"UIManager: Current Player ID changed to {newId}. Updating UI.");
        UpdateUI(newId, turnManager.TimeRemainingInTurn.Value, turnManager.ActionsRemaining.Value, turnManager.TurnNumber.Value);

        // Hide the waiting player window if a current player is identified (newId is not 0)
        if (newId != 0 && _waitingPlayerWindow != null && _waitingPlayerWindow.activeSelf)
        {
            _waitingPlayerWindow.SetActive(false);
            Debug.Log("UIManager: Waiting Player Window deactivated as a player has joined.");
        }
    }

    private void OnTimeRemainingChanged(float oldTime, float newTime)
    {
//        Debug.Log($"UIManager: Time remaining NetworkVariable changed event received: {newTime:F1}.");
        // UI времени обновляется в Update() для плавности, поэтому здесь ничего не делаем.
    }

    private void OnActionsRemainingChanged(int oldActions, int newActions)
    {
        if (turnManager == null) return;
        Debug.Log($"UIManager: Actions remaining NetworkVariable changed to {newActions}. Updating UI.");
        UpdateUI(turnManager.CurrentPlayerClientId.Value, turnManager.TimeRemainingInTurn.Value, newActions, turnManager.TurnNumber.Value);
    }

    private void OnTurnNumberChanged(int oldTurn, int newTurn)
    {
        if (turnManager == null) return;
        Debug.Log($"UIManager: Turn number NetworkVariable changed to {newTurn}. Updating UI.");
        UpdateUI(turnManager.CurrentPlayerClientId.Value, turnManager.TimeRemainingInTurn.Value, turnManager.ActionsRemaining.Value, newTurn);
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
    
    private void OnEndGameHandler(bool isHostWin)
    {
        string stringEndGame = "";
        if (!GameManager.Singleton.IsServer)
        {
            isHostWin = !isHostWin;
            //     return;
        }
        stringEndGame = isHostWin ? "Победил" : "Проиграл";
     //   if(playerClientId != turnManager.CurrentPlayerClientId.Value) return;
        
        if (endGameResultText != null)
        {
            endGameResultText.text = stringEndGame;
            // Здесь мы устанавливаем цвет в зависимости от результата
            if (stringEndGame == "Победил")
            {
                endGameResultText.color = Color.green; // Зеленый для победы
            }
            else if (stringEndGame == "Проиграл")
            {
                endGameResultText.color = Color.red; // Красный для поражения
            }
            else if (stringEndGame == "Ничья") // Если вы добавили обработку ничьей
            {
                endGameResultText.color = Color.yellow; // Желтый для ничьей
            }
            else
            {
                endGameResultText.color = Color.white; // Белый по умолчанию
            }
        }
        
        _EndGameWindow.SetActive(true);
        Debug.Log($"UIManager: end for client ID: {playerClientId}");
    }
    
    
    private void UpdateUI(ulong currentPlayerId, float timeRemaining, int actionsRemaining, int turnNumber)
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

        if (turnNumberText != null)
        {
            turnNumberText.text = $"Ход: {turnNumber}";
            Debug.Log($"[UI Debug] Setting TurnNumberText: {turnNumberText.text}");
        }
        else
        {
            Debug.LogWarning("UIManager: TurnNumberText not assigned in Inspector!");
        }
        
        int actionsNumber = TurnManager.Singleton.ActionsRemaining.Value;
        bool isMyTurn = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == currentPlayerId;
        bool canPerformActionMove = isMyTurn && actionsNumber == 2;
        bool canPerformActionAttack = isMyTurn && actionsNumber == 1;
        
        if (movementPossibleText != null)
        {
            movementPossibleText.text = canPerformActionMove ? "Передвижение: ДА" : "Передвижение: НЕТ";
            movementPossibleText.color = canPerformActionMove ? Color.green : Color.red;
        }
        else
        {
            Debug.LogWarning("UIManager: MovementPossibleText not assigned in Inspector!");
        }

        if (attackPossibleText != null)
        {
            attackPossibleText.text = canPerformActionAttack ? "Атака: ДА" : "Атака: НЕТ";
            attackPossibleText.color = canPerformActionAttack ? Color.green : Color.red;
        }
        else
        {
            Debug.LogWarning("UIManager: AttackPossibleText not assigned in Inspector!");
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