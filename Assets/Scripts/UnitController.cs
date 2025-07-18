using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : NetworkBehaviour
{
    [SerializeField] private UnitStats stats;     // вешаем нужный asset
    
    private NavMeshAgent navAgent;
    private Renderer unitRenderer; // For visual selection feedback
    private Color originalColor;
    
    [SerializeField] private GameObject _radiusDisplay;

    // Properties for unit stats based on type
    public float movementSpeed;
    public float attackRange;

    private Vector3? pathDestination;   // финальная точка пути
    
    private float fireRate = 1f; // сек
    private int   damage = 25;

    private UnitController currentTarget;   // кого бьём
    private float          lastAttackTime;

    // NetworkVariable для HP, синхронизируется автоматически со всеми клиентами.
    public NetworkVariable<int> currentHP = new NetworkVariable<int>(1); // Изменено на 1 HP, как вы указали

    private void Update()
    {
        //     if (!IsServer) return;               // вся логика только на сервере

        if (currentTarget != null)
        {
            // Проверяем, существует ли еще цель и активна ли она в сети
            if (!currentTarget.IsSpawned)
            {
                currentTarget = null; // Цель уничтожена, сбрасываем
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
                // слишком далеко – идём к цели
                navAgent.isStopped = false;
                navAgent.SetDestination(currentTarget.transform.position);
            }
        }
    }

    // команда «атаковать цель»
    [ServerRpc]
    public void AttackTargetServerRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj) &&
            netObj.TryGetComponent(out UnitController enemy))
        {
            currentTarget = enemy;
        }
        else
        {
            currentTarget = null;
        }
    }
    
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        unitRenderer = GetComponentInChildren<Renderer>();
        if (unitRenderer != null)
        {
            originalColor = unitRenderer.material.color;
        }

        if (_radiusDisplay != null)
        {
            _radiusDisplay.transform.parent = null;
        }
    }
    
    private void LateUpdate()
    {
        if (_radiusDisplay == null) return;

        Vector3 center = pathDestination.HasValue
            ? pathDestination.Value
            : transform.position ;
        center.y = 0.1f;
        
        _radiusDisplay.transform.position = center;
    }
    
    public override void OnNetworkSpawn()
    {
      //  if (IsServer) // Важно: IsServer для инициализации NetworkVariable и применения свойств
        {
            currentHP.Value = 1; // Устанавливаем начальное HP на сервере, как вы указали
            ApplyUnitTypeProperties(stats);
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
        if (stats != null)
        {
            _radiusDisplay.transform.localScale = Vector3.one * (stats.attackRange * 2f);
        }
    }

    /// <summary>
    /// This method will be called locally to display selection.
    /// </summary>
    public void Select()
    {
        if (unitRenderer != null)
        {
            unitRenderer.material.color = Color.green; // Highlight the unit green
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Removes visual highlighting.
    /// </summary>
    public void Deselect()
    {
        if (unitRenderer != null)
        {
            unitRenderer.material.color = originalColor;
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Client calls this method, which then executes on the SERVER.
    /// </summary>
    [ServerRpc]
    public void MoveServerRpc(Vector3 targetPosition)
    {
        Debug.Log("Move (double right-click) MoveServerRpc");
        // On the server, we set the destination for the NavMeshAgent
        // NetworkTransform автоматически синхронизирует движение для всех клиентов
        if (navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(targetPosition);
            
            pathDestination = targetPosition;
        }
        else
        {
            Debug.LogWarning("NavMeshAgent is not on a NavMesh. Cannot set destination.");
        }
    }
    
    public void ClearPath()
    {
        pathDestination = null;
    }
    
    float GetUnitRadius(UnitController unit)
    {
        // самый простой способ: половина максимального размера коллайдера
        Collider c = unit.GetComponent<Collider>();
        if (c == null) return 0f;

        // для CapsuleCollider или SphereCollider
        if (c is CapsuleCollider cc) return cc.radius * Mathf.Max(unit.transform.lossyScale.x, unit.transform.lossyScale.z);
        if (c is SphereCollider sc)  return sc.radius * unit.transform.lossyScale.x;
        if (c is BoxCollider bc)     return Mathf.Max(bc.size.x, bc.size.z) * 0.5f * Mathf.Max(unit.transform.lossyScale.x, unit.transform.lossyScale.z);

        // fallback — половина диагонали bounds
        return c.bounds.extents.magnitude;
    }
}