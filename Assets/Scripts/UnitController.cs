using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : NetworkBehaviour
{
    public float MovementSpeed => _movementSpeed;
    
    [SerializeField] private UnitStats stats;  
    [SerializeField] private Color _friendColor;
    [SerializeField] private Color _enemyColor;
    [SerializeField] private LineRenderer _lineRenderer; 
 
    
    [SerializeField] private GameObject _radiusDisplay;
    [SerializeField] private float _radiusDisplayMultiplier;
    private float _movementSpeed;
    private float _attackRange;
    private Color _originalColor;

    private Vector3? _pathDestination;   
    
    private float _fireRate = 1f; 
    private int _damage = 25;
    
    private PlayerController _playerController;
    private TurnManager _turnManager;
    private UnitManager _unitManager;
    private GameManager _gameManager;
    private NavMeshAgent _navAgent;
    private Renderer _unitRenderer; 
    
    private UnitController _currentTarget;   
    private float _lastAttackTime;

    public NetworkVariable<int> currentHP = new NetworkVariable<int>(1);
    private void Start()
    {
        StartCoroutine(MarkEnemiesInRadiusCoroutine());
    }

    public void Initialize(PlayerController playerController, UnitManager unitManager, TurnManager turnManager, GameManager gameManager )
    {
        _playerController = playerController;
        _unitManager = unitManager;
        _turnManager = turnManager;
        _gameManager = gameManager;
    }
    private IEnumerator MarkEnemiesInRadiusCoroutine()
    {
        Transform centerPoint = _radiusDisplay.transform;
        while (enabled) 
        {
            yield return new WaitForSeconds(0.2f);

            MarkEnemiesInRadius(centerPoint.position);
        }
    }
    private void Update()
    {
        if (!IsServer)
        {
            return;               
        }
        
        if (_currentTarget != null)
        {
            if (!_currentTarget.IsSpawned || _currentTarget.currentHP.Value <= 0) 
            {
                StopAttacking(); 
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (dist <= _attackRange)
            {
                _navAgent.isStopped = true;
                if (Time.time - _lastAttackTime >= _fireRate)
                {
                    _lastAttackTime = Time.time;
                    _currentTarget.TakeDamageServerRpc(_damage, OwnerClientId); 
                }
            }
 
        }
    }

    private void StopAttacking()
    {
        _currentTarget = null;
        _navAgent.isStopped = false; 
        ClearPathClientRpc(); 
        Debug.Log($"{name}: Stopping attack and ready to move.");
    }
  
    [SerializeField] private List<UnitController> nearby = new List<UnitController>();
    private void MarkEnemiesInRadius(Vector3 posStartToEnemy)
    {
        if (!_playerController)
        {
            return;
        }
            
        foreach (var enemy in _unitManager.GetLiveEnemyUnitsForPlayer())
        {
            
            if (_unitManager.GetLiveUnitsForPlayer().Contains(this))
            {
                if (!(_playerController.UnitController && _playerController.UnitController == this) || !enemy)
                {
                    return;
                }

                bool hide = true;
                if (Vector3.Distance(posStartToEnemy, enemy.transform.position) <= _attackRange && enemy != this)
                {
                     if (!nearby.Contains(enemy))
                    {
                        nearby.Add(enemy);
                    }
                    hide = false;
                    enemy.ChangeColor(Color.magenta);
                }
                else
                {
                    if (nearby.Contains(enemy))
                    {
                        nearby.Remove(enemy);
                    }
                    hide = true;
                    enemy.ResetColor();
                }
                
                    float dist = Vector3.Distance(posStartToEnemy, enemy.transform.position);
                    Debug.Log($"dist{dist} attackRange {_attackRange} enemy {enemy.name} I am {name} Спрятать {hide}");
            }

        }
    }

    public void ChangeColor(Color color)
    {
        _unitRenderer.material.color = color;
    }
    
    public void ResetColor()
    {
        _unitRenderer.material.color = _originalColor;
    }
    
    [ServerRpc]
    public void AttackTargetServerRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj) &&
            netObj.TryGetComponent(out UnitController enemy))
        {
            if (enemy.currentHP.Value <= 0)
            {
                Debug.LogWarning($"Server: Client {OwnerClientId} attempted to attack an already dead target.");
                StopAttacking(); 
                return;
            }
            _currentTarget = enemy;
            _navAgent.isStopped = false; 
            ClearPathClientRpc(); 
        }
        else
        {
            StopAttacking(); 
        }
    }
    
    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _unitRenderer = GetComponentInChildren<Renderer>();
   
        if (_radiusDisplay != null)
        {
            _radiusDisplay.transform.parent = null; 
        }

        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            _lineRenderer.startWidth = 0.1f;
            _lineRenderer.endWidth = 0.1f;   
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default")); 
            _lineRenderer.startColor = Color.blue; 
            _lineRenderer.endColor = Color.blue;
        }
    }
 
    private void LateUpdate()
    {
        if (!IsOwner) return; 

        if (_radiusDisplay == null) return;

        Vector3 center = _pathDestination.HasValue
            ? _pathDestination.Value
            : transform.position ;
        center.y = 0.1f; // Поднять немного над землей
        
        _radiusDisplay.transform.position = center;

        SyncRadiusDisplay();
    }
    
    public override void OnNetworkSpawn()
    {
        if (IsServer) 
        {
            currentHP.Value = 1; 
        }
        
        ApplyUnitTypeProperties(stats); 

        if (IsOwner)
        {
            _unitRenderer.material.color = _friendColor;
        }
        else
        {
            _unitRenderer.material.color = _enemyColor;
        }
        
        if (_unitRenderer != null)
        {
            _originalColor = _unitRenderer.material.color;
        }
     
        currentHP.OnValueChanged += OnHPChanged; 

        if (_radiusDisplay != null)
        {
            SyncRadiusDisplay();
            _radiusDisplay.SetActive(false); 
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHP.OnValueChanged -= OnHPChanged;
        
        if (_radiusDisplay != null)
        {
            Destroy(_radiusDisplay);
        }
        if (_lineRenderer != null)
        {
            Destroy(_lineRenderer.gameObject); 
        }
        Deselect();
        
       _turnManager.OnEnd();
    }

    private void OnHPChanged(int oldHP, int newHP)
    {
        Debug.Log($"{name} HP изменилось с {oldHP} на {newHP}.");

        if (newHP <= 0)
        {
            Debug.Log($"{name} уничтожен!");
            if (IsServer) 
            {
                _unitManager.DespawnUnits(NetworkObject); 
                NetworkObject.Despawn(); 
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int dmg, ulong senderClientId, ServerRpcParams rpcParams = default)
    {
        ulong instigatorClientId = senderClientId; 

        currentHP.Value -= dmg; 
        Debug.Log($"{name} получил {dmg} урона от клиента ID: {instigatorClientId}. Текущее HP: {currentHP.Value}.");

        ShowDamageInfoClientRpc(dmg, instigatorClientId);
    }


    [ClientRpc]
    private void ShowDamageInfoClientRpc(int dmg, ulong instigatorClientId)
    {
        Debug.Log($"На клиенте: Объект {name} получил {dmg} урона. Нападавший Client ID: {instigatorClientId}.");
    }
    
    private void ApplyUnitTypeProperties(UnitStats stats)
    {
        if (stats == null)
        {
            Debug.LogWarning("UnitStats asset is not assigned to " + name);
            return;
        }

        _movementSpeed = stats.moveSpeed;
        _attackRange   = stats.attackRange;
        _damage        = stats.damage;
        _fireRate      = stats.fireRate; 
        
        _navAgent.speed = _movementSpeed;
        if (_radiusDisplay != null)
        {
            SyncRadiusDisplay(); 
        }
    }
    
    private void SyncRadiusDisplay()
    {
        if (_radiusDisplay == null) return;

        _radiusDisplay.transform.localScale = Vector3.one * ((_attackRange) * _radiusDisplayMultiplier); 
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }

    public void Select()
    {
        // if (!TurnManager.Singleton.IsMyTurn) return; // Закомментировано, если TurnManager еще не реализован или не нужен для этой логики
        
        if (_unitRenderer != null)
        {
            _unitRenderer.material.color = Color.blue; // Highlight the unit green
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(true);
            }
        }

    }

    public void Deselect()
    {
        if (_unitRenderer != null)
        {
            ResetColor();
            if (_radiusDisplay != null)
            {
                _radiusDisplay.SetActive(false);
            }
        }

        foreach (var enemy in nearby)
        {
            if (enemy)
            {
                enemy.ResetColor();;
            }
        }
    
        ClearLocal();
    }

    public void Move(Vector3 targetPosition)
    {
        _pathDestination = targetPosition; 
        MoveServerRpc(targetPosition);
    }
    
    [ClientRpc] 
    public void SetInfiniteSpeedClientRpc() 
    {
        _movementSpeed = 9999; 
        _navAgent.speed = _movementSpeed;
        _navAgent.acceleration = _movementSpeed;
        _navAgent.angularSpeed = _movementSpeed;
        Debug.Log($"{name}: Скорость передвижения установлена на бесконечную на клиенте.");
    }
    
    [ServerRpc]
    public void MoveServerRpc(Vector3 targetPosition)
    {
        Debug.Log($"{name} MoveServerRpc called. Target: {targetPosition}");
        if (_navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = false; 
            _navAgent.SetDestination(targetPosition);
            
            StopAttacking(); 

            NavMeshPath path = new NavMeshPath();
            if (_navAgent.CalculatePath(targetPosition, path))
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

    [ClientRpc]
    private void UpdatePathClientRpc(Vector3[] pathCorners)
    {
        if (_lineRenderer == null) return;

        if (!IsOwner)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
            return;
        }

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

    [ClientRpc]
    private void ClearPathClientRpc()
    {
        if (_lineRenderer == null) return;

        _lineRenderer.enabled = false;
        _lineRenderer.positionCount = 0;
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Path for unit {name} cleared");
    }
    
    private void ClearLocal()
    {
        if (_lineRenderer == null) return;

        _lineRenderer.enabled = false;
        _lineRenderer.positionCount = 0;
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Path for unit {name} cleared");
    }
}