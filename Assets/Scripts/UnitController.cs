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
        movementSpeed = stats.moveSpeed; // Slower speed
        attackRange =stats.attackRange; // Longer range
              
      
        navAgent.speed = movementSpeed; // Apply the speed to the NavMeshAgent
    }
    
    private void SyncRadiusDisplay()
    {
        _radiusDisplay.transform.localScale = Vector3.one * (stats.attackRange * 2f);
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
}
// UnitType.cs
public enum UnitType
{
    RangedSlow,
    MeleeFast
    // новые типы пишем сюда
}