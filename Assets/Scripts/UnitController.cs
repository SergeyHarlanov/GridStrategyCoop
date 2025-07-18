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

    private void Update()
    {
        if (!IsServer) return;               // вся логика только на сервере

        if (currentTarget != null)
        {
            // проверяем дистанцию
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (dist <= attackRange)
            {
                // останавливаемся и бьём
                navAgent.isStopped = true;
                if (Time.time - lastAttackTime >= fireRate)
                {
                    lastAttackTime = Time.time;
                    currentTarget.TakeDamageServerRpc(damage);
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

// принимаем урон
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int dmg, ServerRpcParams rpcParams = default)
    {
        // здесь реализуйте HP
        Debug.Log($"{name} получил {dmg} урона");
        // if (hp <= 0) NetworkObject.Despawn();
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

        _radiusDisplay.transform.parent = null;

        SyncRadiusDisplay();
    }
    
    private void LateUpdate()
    {
        // если юнит выбран – показываем радиус
        if (_radiusDisplay == null) return;

        Vector3 center = pathDestination.HasValue
            ? pathDestination.Value
            : transform.position ;
        center.y = 0.1f;
        
        _radiusDisplay.transform.position = center;
    }
    
    public override void OnNetworkSpawn()
    {
        // Only the server should set the initial properties based on unitType
        if (IsServer)
        {
            // Set the unit type (this would typically be set when the unit is spawned)
            // For now, let's assume it's set before OnNetworkSpawn by the spawner
            // Example: unitType.Value = UnitType.RangedSlow;

            ApplyUnitTypeProperties(stats);
        }
        
    }
    
    private void ApplyUnitTypeProperties(UnitStats stats)
    {
        movementSpeed = stats.moveSpeed;
        attackRange   = stats.attackRange;
        damage        = stats.damage;
        fireRate      = stats.fireRate;      // если используете его вместо attackCooldown

        navAgent.speed = movementSpeed;
    }
    

    private void SyncRadiusDisplay()
    {
        _radiusDisplay.transform.localScale = Vector3.one * (stats.attackRange * 2f * transform.localScale.x);
    }

    /// <summary>
    /// This method will be called locally to display selection.
    /// </summary>
    public void Select()
    {
        if (unitRenderer != null)
        {
            unitRenderer.material.color = Color.green; // Highlight the unit green
            _radiusDisplay.SetActive(true);
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
            _radiusDisplay.SetActive(false);
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
        // NetworkTransform automatically synchronizes movement for all clients
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
// UnitType.cs
public enum UnitType
{
    RangedSlow,
    MeleeFast
    // новые типы пишем сюда
}