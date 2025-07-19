// PlayerController.cs
using Unity.Netcode;
using UnityEngine;
// using UnityEngine.UI; // Если у вас есть UI для вывода сообщений

public class PlayerController : MonoBehaviour
{
    private UnitController selectedUnit;
    private Camera mainCamera;

    // поля для отслеживания двойного щелчка
    private float lastRightClickTime;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f; // секунды

    // Для UI
    // [SerializeField] private Text turnStatusText; // Текст для отображения статуса хода
    // [SerializeField] private Text actionsRemainingText; // Текст для отображения оставшихся действий

    private void Start()
    {
        mainCamera = Camera.main;
        // Убедитесь, что TurnManager.Singleton инициализирован до использования
        // Возможно, лучше подписаться на событие NetworkManager.Singleton.OnClientConnectedCallback
        // и инициализировать здесь, чтобы убедиться, что TurnManager готов.
        // Или убедитесь, что TurnManager появляется раньше в иерархии выполнения.
    }

    void Update()
    {
        // Клиентский код выполняется только для LocalClientId.
        // Мы хотим, чтобы клиент мог взаимодействовать, но действия проверялись на сервере.
        if (!NetworkManager.Singleton.IsClient) return;
        if (TurnManager.Singleton == null) return; // Ждем, пока TurnManager будет готов

        // Проверяем, является ли это ходом текущего игрока
        bool isMyTurn = NetworkManager.Singleton.LocalClientId == TurnManager.Singleton.CurrentPlayerClientId.Value;
        bool hasActions = TurnManager.Singleton.ActionsRemaining.Value > 0;

        // Обновление UI (пример)
        // if (turnStatusText != null)
        // {
        //     turnStatusText.text = isMyTurn ? "ВАШ ХОД!" : $"Ход игрока {TurnManager.Singleton.CurrentPlayerClientId.Value}";
        //     turnStatusText.color = isMyTurn ? Color.green : Color.red;
        // }
        // if (actionsRemainingText != null)
        // {
        //     actionsRemainingText.text = $"Действий: {TurnManager.Singleton.ActionsRemaining.Value}";
        // }


        // ЛКМ – выбор юнита (без изменений, это локальное действие)
        if (Input.GetMouseButtonDown(0))
            HandleSelection();

        // ПКМ – отдать приказ
        if (Input.GetMouseButtonDown(1) && selectedUnit != null)
        {
            // Проверка хода и действий
            if (!isMyTurn)
            {
                Debug.LogWarning("Не ваш ход! Дождитесь своей очереди.");
                // Отправьте ClientRpc сообщение об ошибке игроку, если хотите
                return;
            }
            if (!hasActions)
            {
                Debug.LogWarning("Нет доступных действий на этот ход!");
                // Отправьте ClientRpc сообщение об ошибке игроку, если хотите
                return;
            }

            // Логика двойного щелчка
            float timeSinceLastClick = Time.time - lastRightClickTime;
            if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
            {
                Debug.Log("Unit Move (double right-click)");
                HandleMovement();
                // Отправляем запрос на использование действия
                TurnManager.Singleton.UseActionServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            // else if (Input.GetMouseButtonDown(1) && selectedUnit) // Эта строка дублируется и может быть удалена
            // {
            //     HandleAttack();
            //     // Отправляем запрос на использование действия
            //     TurnManager.Singleton.UseActionServerRpc(NetworkManager.Singleton.LocalClientId);
            // }
            lastRightClickTime = Time.time;
        }

        // Переключение хода по нажатию клавиши (для тестирования)
        if (Input.GetKeyDown(KeyCode.Space) && isMyTurn) // Можно добавить кнопку в UI
        {
            TurnManager.Singleton.EndTurnServerRpc();
        }
    }

    private void HandleSelection()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent<UnitController>(out UnitController unit))
            {
                if (unit.NetworkObject.IsOwner)
                {
                    if (selectedUnit != null)
                    {
                        selectedUnit.Deselect();
                    }
                    selectedUnit = unit;
                    selectedUnit.Select();
                }
            }
            else // Если кликнули не по юниту, снимаем выделение
            {
                if (selectedUnit != null)
                {
                    selectedUnit.Deselect();
                    selectedUnit = null;
                }
            }
        }
    }
    
    // В HandleAttack и HandleMovement вызовы ServerRpc для юнитов, но теперь они должны быть
    // "разрешены" только после использования действия через TurnManager.
    private void HandleAttack()   // вызывать по ПКМ, если не двойной клик для движения
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent(out UnitController enemy) &&
                !enemy.NetworkObject.IsOwner)   // чужой юнит
            {
                // Сервер все равно проверит дистанцию, но можно и здесь сделать предпроверку для UI
                // float dist = Vector3.Distance(selectedUnit.transform.position, enemy.transform.position);
                // if (dist <= selectedUnit.attackRange) // Эта проверка должна быть на сервере, но здесь для удобства пользователя
                // {
                    selectedUnit.AttackTargetServerRpc(enemy.NetworkObject);
                    Debug.Log($"Приказ атаковать {enemy.name}");
                // }
                // else
                // {
                //     Debug.Log("Цель слишком далеко!");
                // }
            }
        }
    }
    
    // PlayerController.cs
    private void HandleMovement()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        Vector3 target = hit.point;
        // Расчет finalTarget для локального предсказания.
        // Сервер должен будет провести свою проверку.
        Vector3 dir    = target - selectedUnit.transform.position;
        dir.y = 0;

        float distance = dir.magnitude;
        float maxStep  = selectedUnit.movementSpeed * TurnManager.Singleton.turnDuration; // Можно использовать максимальное перемещение за ход

        Vector3 finalTarget;
        // Эта логика ограничения дистанции перемещения должна быть также на сервере
        if (distance <= maxStep)
        {
            finalTarget = target;
        }
        else
        {
            finalTarget = selectedUnit.transform.position + dir.normalized * maxStep;
            Debug.Log("maxstep"+maxStep);
        }

        selectedUnit.MoveServerRpc(finalTarget);
    }
}