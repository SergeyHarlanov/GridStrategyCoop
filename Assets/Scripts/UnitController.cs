using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic; // Добавлено для List<Vector3>

[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : NetworkBehaviour
{
    [SerializeField] private UnitStats stats;     // вешаем нужный asset
    [SerializeField] private Color _friendColor;
    [SerializeField] private Color _enemyColor;
    
    [SerializeField] private LineRenderer _lineRenderer; // Перетащите сюда LineRenderer из инспектора
    
    private NavMeshAgent navAgent;
    private Renderer unitRenderer; // For visual selection feedback
    public Color originalColor;
    
    [SerializeField] private GameObject _radiusDisplay;
    [SerializeField] private float _radiusDisplayMultiplier;
    // Properties for unit stats based on type
    public float movementSpeed;
    public float attackRange;

    // Сделаем pathDestination NetworkVariable, чтобы синхронизировать целевую точку
    // Или, что лучше, использовать ClientRpc для отправки всего пути.
    // Пока оставим как есть, но будем отправлять путь через ClientRpc
    private Vector3? pathDestination;   // финальная точка пути (локально для клиента, для сервера это navAgent.destination)
    
    private float fireRate = 1f; // сек
    private int   damage = 25;

    private UnitController currentTarget;   // кого бьём
    private float          lastAttackTime;

    // NetworkVariable для HP, синхронизируется автоматически со всеми клиентами.
    public NetworkVariable<int> currentHP = new NetworkVariable<int>(1); 

    private void Update()
    {
        // Вся логика, которая изменяет состояние юнита (атака, перемещение) должна быть на сервере.
        if (!IsServer)
        {
            // Для клиента, который является владельцем, обновляем отрисовку пути,
            // используя полученные по RPC точки.
            // НЕ ИСПОЛЬЗУЕМ navAgent.path НА КЛИЕНТЕ НАПРЯМУЮ ДЛЯ ОТОБРАЖЕНИЯ.
            // Эта часть логики теперь будет управляться через ClientRpc
            return;               
        }
        
        // Логика атаки только на сервере
        if (currentTarget != null)
        {
            // Проверяем, существует ли еще цель и активна ли она в сети
            if (!currentTarget.IsSpawned || currentTarget.currentHP.Value <= 0) // Добавили проверку на HP цели
            {
                StopAttacking(); // Цель уничтожена или мертва, сбрасываем состояние атаки
                return;
            }

            // проверяем дистанцию
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (dist <= attackRange)
            {
                // останавливаемся и бьём
                navAgent.isStopped = true;
                if (Time.time - lastAttackTime >= fireRate)
                {
                    lastAttackTime = Time.time;
                    // Вызываем TakeDamageServerRpc на целевом объекте
                    // Передаем ClientId атакующего, то есть владельца этого UnitController
                    currentTarget.TakeDamageServerRpc(damage, OwnerClientId); 
                }
            }
            else
            {
                /*
                // слишком далеко – идём к цели
                // В этом блоке SetDestination вызывается только если юнит не движется к цели
                // или если текущий путь не ведет к цели (можно добавить более сложные проверки)
                if (navAgent.isStopped || Vector3.Distance(navAgent.destination, currentTarget.transform.position) > 0.1f)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(currentTarget.transform.position);
                }
                */
            }
        }
        else // Если currentTarget == null, юнит не атакует
        {
            // Если юнит не имеет цели атаки и не движется по пути, разрешаем ему быть остановленным,
            // но если есть pathDestination, он должен двигаться к нему.
            // Логика движения к pathDestination уже в MoveServerRpc и на клиенте.
        }
    }

    // Новый метод для сброса состояния атаки
    private void StopAttacking()
    {
        currentTarget = null;
        navAgent.isStopped = false; // Разрешаем агенту двигаться
        ClearPathClientRpc(); // Очищаем путь отрисовки у всех клиентов
        Debug.Log($"{name}: Stopping attack and ready to move.");
    }

    private void MarkEnemiesInRadius(Vector3 posStartToEnemy)
    {
        foreach (var enemy in UnitManager.Singleton.GetLiveEnemyUnitsForPlayer(NetworkObjectId))
        {
            if (Vector3.Distance(posStartToEnemy, enemy.transform.position) <= attackRange && enemy != this)
            {
               enemy.unitRenderer.material.color = Color.magenta;
            }
            else
            {
                enemy.unitRenderer.material.color = enemy.originalColor;
            }
            Debug.Log("");
        }
    }
    
    // команда «атаковать цель»
    [ServerRpc]
    public void AttackTargetServerRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj) &&
            netObj.TryGetComponent(out UnitController enemy))
        {
            // Убедимся, что цель жива, прежде чем начинать атаковать
            if (enemy.currentHP.Value <= 0)
            {
                Debug.LogWarning($"Server: Client {OwnerClientId} attempted to attack an already dead target.");
                StopAttacking(); // Нельзя атаковать мертвую цель
                return;
            }
            currentTarget = enemy;
            // После получения новой цели, сразу убедимся, что юнит не остановлен,
            // чтобы он мог начать движение к цели, если она далеко.
            navAgent.isStopped = false; 
            ClearPathClientRpc(); // Отменяем отрисовку пути, если начинаем атаковать
        }
        else
        {
            StopAttacking(); // Невалидная цель, сбрасываем
        }
    }
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        unitRenderer = GetComponentInChildren<Renderer>();
   
        if (_radiusDisplay != null)
        {
            _radiusDisplay.transform.parent = null; // Отсоединяем от родителя
        }

        // ApplyUnitTypeProperties(stats); // <--- Эту строку лучше вызывать в OnNetworkSpawn для сервера
        // Инициализация LineRenderer
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            _lineRenderer.startWidth = 0.1f; // Начальная толщина линии
            _lineRenderer.endWidth = 0.1f;   // Конечная толщина линии
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Простой материал
            _lineRenderer.startColor = Color.blue; // Цвет линии
            _lineRenderer.endColor = Color.blue;
        }
    }
    
    private void LateUpdate()
    {
        if (!IsOwner) return; // Только владелец юнита должен видеть свой радиус и путь

        if (_radiusDisplay == null) return;

        Vector3 center = pathDestination.HasValue
            ? pathDestination.Value
            : transform.position ;
        center.y = 0.1f; // Поднять немного над землей
        
        _radiusDisplay.transform.position = center;

        SyncRadiusDisplay();
    }
    
    public override void OnNetworkSpawn()
    {
        if (IsServer) // Важно: IsServer для инициализации NetworkVariable и применения свойств
        {
            currentHP.Value = 1; // Устанавливаем начальное HP на сервере, как вы указали
            // ApplyUnitTypeProperties(stats); // Вызываем здесь, чтобы stats были применены на сервере
        }
        
        // Применяем свойства для всех, чтобы они получили правильные speed, attackRange и т.д.
        // Это должно быть вызвано на всех клиентах, когда юнит спавнится.
        ApplyUnitTypeProperties(stats); 

        if (IsOwner)
        {
            unitRenderer.material.color = _friendColor;
        }
        else
        {
            unitRenderer.material.color = _enemyColor;
        }
        
        if (unitRenderer != null)
        {
            originalColor = unitRenderer.material.color;
        }
     
        // Подписываемся на событие изменения HP на всех клиентах.
        currentHP.OnValueChanged += OnHPChanged; 

        if (_radiusDisplay != null)
        {
            SyncRadiusDisplay();
            _radiusDisplay.SetActive(false); // Изначально скрываем
        }
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от события при деспавне, чтобы избежать утечек памяти.
        currentHP.OnValueChanged -= OnHPChanged;
        
        // Если _radiusDisplay был отсоединен от родителя, его нужно уничтожить вручную.
        if (_radiusDisplay != null)
        {
            Destroy(_radiusDisplay);
        }
        // Также очищаем LineRenderer при деспавне
        if (_lineRenderer != null)
        {
            // Здесь мы уничтожаем LineRenderer, если он является отдельным GameObject
            // Если он является частью этого же GameObject, он уничтожится вместе с ним.
            // Предполагаем, что _lineRenderer может быть отдельным объектом.
            Destroy(_lineRenderer.gameObject); 
        }
    }
    
    // Этот метод будет вызываться на всех клиентах, когда currentHP изменится на сервере.
    // Важно: он вызывается на том объекте, чье HP изменилось.
    private void OnHPChanged(int oldHP, int newHP)
    {
        Debug.Log($"{name} HP изменилось с {oldHP} на {newHP}.");

        // Если HP упало до 0 или ниже, и этот объект является сервером, деспавним его.
        // Это гарантирует, что уничтожается именно тот юнит, который получил урон.
        if (newHP <= 0)
        {
            Debug.Log($"{name} уничтожен!");
            if (IsServer) 
            {
                // Деспавним NetworkObject, на котором вызвано это событие (то есть, текущий юнит)
                GameManager.Singleton.DespawnUnits(NetworkObject); // Убедитесь, что GameManager.Singleton существует
                NetworkObject.Despawn(); 
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    // Добавили параметр senderClientId для передачи ID нападающего
    public void TakeDamageServerRpc(int dmg, ulong senderClientId, ServerRpcParams rpcParams = default)
    {
        // senderClientId - это ID игрока, который нанес урон, переданный из Update.
        ulong instigatorClientId = senderClientId; 

        // Применяем урон к currentHP.
        // Так как currentHP - это NetworkVariable, ее изменение будет автоматически
        // синхронизировано со всеми клиентами.
        currentHP.Value -= dmg; 
        Debug.Log($"{name} получил {dmg} урона от клиента ID: {instigatorClientId}. Текущее HP: {currentHP.Value}.");

        // Вызываем ClientRpc, чтобы уведомить всех клиентов, кто нанес урон.
        ShowDamageInfoClientRpc(dmg, instigatorClientId);
    }


    [ClientRpc]
    private void ShowDamageInfoClientRpc(int dmg, ulong instigatorClientId)
    {
        // Этот код будет выполнен на КАЖДОМ клиенте (включая хост и игрока, который нанес урон).
        Debug.Log($"На клиенте: Объект {name} получил {dmg} урона. Нападавший Client ID: {instigatorClientId}.");

        // Здесь вы можете добавить логику для отображения информации об уроне в UI:
        // - Всплывающий текст с количеством урона над персонажем.
        // - Сообщение в чате типа "Игрок X нанес Y урона Игроку Z".
        // - Визуальный эффект, указывающий на источник урона.
    }
    
    private void ApplyUnitTypeProperties(UnitStats stats)
    {
        if (stats == null)
        {
            Debug.LogWarning("UnitStats asset is not assigned to " + name);
            return;
        }

        movementSpeed = stats.moveSpeed;
        attackRange   = stats.attackRange;
        damage        = stats.damage;
        fireRate      = stats.fireRate;      // если используете его вместо attackCooldown

        navAgent.speed = movementSpeed;
        if (_radiusDisplay != null)
        {
            SyncRadiusDisplay(); // Вызываем здесь, так как attackRange теперь установлен
        }
    }
    
    private void SyncRadiusDisplay()
    {
        if (_radiusDisplay == null) return;

        // Вариант 1: Если _radiusDisplay — сфера (радиус = 1 в Unity)
        _radiusDisplay.transform.localScale = Vector3.one * ((attackRange) * _radiusDisplayMultiplier); // чтобы диаметр = attackRange * 2

        // Вариант 2: Если _radiusDisplay — Plane (10x10)
        // _radiusDisplay.transform.localScale = new Vector3(attackRange / 5f, 1f, attackRange / 5f);
    }

    private void OnDrawGizmosSelected()
    {
        // Рисуем радиус атаки (attackRange — это радиус)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    /// <summary>
    /// This method will be called locally to display selection.
    /// </summary>
    public void Select()
    {
        // if (!TurnManager.Singleton.IsMyTurn) return; // Закомментировано, если TurnManager еще не реализован или не нужен для этой логики
        
        if (unitRenderer != null)
        {
            unitRenderer.material.color = Color.blue; // Highlight the unit green
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(true);
            }
        }
        // При выборе юнита, если у него есть путь, отрисовываем его
        // Теперь путь будет отрисовываться на клиенте после получения RPC
        // Поэтому здесь явный вызов DrawPath(navAgent.path) не нужен,
        // так как он уже должен быть обновлен через UpdatePathClientRpc
    }

    /// <summary>
    /// Removes visual highlighting.
    /// </summary>
    public void Deselect()
    {
        Debug.Log("Deselect"+name);
        if (unitRenderer != null)
        {
            unitRenderer.material.color = originalColor;
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(false);
            }
        }
        // При снятии выделения очищаем путь
     //   ClearPathClientRpc(); // Очищаем путь у всех клиентов
     ClearLocal();
    }

    public void Move(Vector3 targetPosition)
    {
        // Клиентский вызов, который запускает RPC на сервере
        pathDestination = targetPosition; // Запоминаем цель для локального использования (например, для радиуса)

        MarkEnemiesInRadius(pathDestination.Value); // Предполагается, что эта логика для ИИ или вспомогательной функции
        MoveServerRpc(targetPosition);
    }

    /// <summary>
    /// Client calls this method, which then executes on the SERVER.
    /// </summary>
    [ServerRpc]
    public void MoveServerRpc(Vector3 targetPosition)
    {
        Debug.Log($"{name} MoveServerRpc called. Target: {targetPosition}");
        // On the server, we set the destination for the NavMeshAgent
        // NetworkTransform автоматически синхронизирует движение для всех клиентов
        if (navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false; 
            navAgent.SetDestination(targetPosition);
            
            // После получения новой команды на перемещение, отменяем текущую атаку
            StopAttacking(); 

            // **ВАЖНО:** Отправляем путь клиентам после его расчета на сервере
            NavMeshPath path = new NavMeshPath();
            if (navAgent.CalculatePath(targetPosition, path))
            {
                // Отправляем точки пути всем клиентам
                UpdatePathClientRpc(path.corners);
            }
            else
            {
                Debug.LogWarning("Server: Failed to calculate path for targetPosition: " + targetPosition);
                ClearPathClientRpc(); // Очищаем путь, если его не удалось рассчитать
            }
        }
        else
        {
            Debug.LogWarning("NavMeshAgent is not on a NavMesh. Cannot set destination.");
            ClearPathClientRpc(); // Очищаем путь, если агент не на NavMesh
        }
    }
    
    // Этот метод ClearPath() больше не используется напрямую, его функционал перенесен в ClearPathClientRpc()
    // public void ClearPath() 
    // {
    //     return; // Этот return делает метод бесполезным, лучше удалить его или переиспользовать
    //     pathDestination = null; 
    //     if (_lineRenderer != null)
    //     {
    //         _lineRenderer.enabled = false;
    //         _lineRenderer.positionCount = 0;
    //     }
    // }
    
    // НОВОЕ: ClientRpc для отрисовки пути на клиентах
    [ClientRpc]
    private void UpdatePathClientRpc(Vector3[] pathCorners)
    {
        if (_lineRenderer == null) return;

        // Только владелец юнита должен видеть свой путь
        if (!IsOwner)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            return;
        }

        // Если путь пуст или недействителен, скрываем LineRenderer
        if (pathCorners == null || pathCorners.Length == 0)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.enabled = true;
        _lineRenderer.positionCount = pathCorners.Length;
        for (int i = 0; i < pathCorners.Length; i++)
        {
            Vector3 point = pathCorners[i];
            point.y += 0.15f; // Немного поднять линию над землей, чтобы избежать z-fighting
            _lineRenderer.SetPosition(i, point);
        }
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Received path with {pathCorners.Length} points for unit {name}");
    }

    // НОВОЕ: ClientRpc для очистки пути на клиентах
    [ClientRpc]
    private void ClearPathClientRpc()
    {
        if (_lineRenderer == null) return;
            //   if (!IsOwner) return; // Только владелец должен очищать отображение пути своего юнита

        _lineRenderer.enabled = false;
        _lineRenderer.positionCount = 0;
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Path for unit {name} cleared");
    }
    
    private void ClearLocal()
    {
        if (_lineRenderer == null) return;
        //   if (!IsOwner) return; // Только владелец должен очищать отображение пути своего юнита

        _lineRenderer.enabled = false;
        _lineRenderer.positionCount = 0;
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Path for unit {name} cleared");
    }

    // Метод для отрисовки пути (теперь используется только для локального NavMeshAgent, если IsServer)
    // Его вызов из Update удален, так как он больше не нужен для клиентов
    private void DrawPath(NavMeshPath path)
    {
        if (_lineRenderer == null) return;

        if (path == null || path.status == NavMeshPathStatus.PathInvalid || path.corners.Length < 2)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            return;
        }

        _lineRenderer.enabled = true;
        _lineRenderer.positionCount = path.corners.Length;
        for (int i = 0; i < path.corners.Length; i++)
        {
            Vector3 point = path.corners[i];
            point.y += 0.15f; // Немного поднять линию над землей, чтобы избежать z-fighting
            _lineRenderer.SetPosition(i, point);
        }
    }
}