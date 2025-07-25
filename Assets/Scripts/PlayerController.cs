using Unity.Netcode;
using UnityEngine;
using Zenject;

public class PlayerController : MonoBehaviour
{
    public UnitController UnitController => selectedUnit;
    private UnitController selectedUnit;
    private Camera mainCamera;

    private float lastRightClickTime;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f; 

    [Inject] private TurnManager _turnManager;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        bool isMyTurn = NetworkManager.Singleton.LocalClientId == _turnManager.CurrentPlayerClientId.Value;
        bool hasActions = _turnManager.ActionsRemaining.Value > 0;

        if (Input.GetMouseButtonDown(0))
            HandleSelection();

        if (Input.GetMouseButtonDown(1) && selectedUnit != null)
        {
            if (!isMyTurn)
            {
                Debug.LogWarning("Не ваш ход! Дождитесь своей очереди.");
                return;
            }
            if (!hasActions)
            {
                Debug.LogWarning("Нет доступных действий на этот ход!");
                return;
            }
            int actionsNumber = _turnManager.ActionsRemaining.Value;
            float timeSinceLastClick = Time.time - lastRightClickTime;
            if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD && actionsNumber == 2)
            {
                Debug.Log("Unit Move (double right-click)");
                HandleMovement();
                
                _turnManager.UseActionServerRpc(NetworkManager.Singleton.LocalClientId);
            }
             if (Input.GetMouseButtonDown(1) && selectedUnit && actionsNumber == 1) 
             {
                HandleAttack();
                 _turnManager.UseActionServerRpc(NetworkManager.Singleton.LocalClientId);
             }
            lastRightClickTime = Time.time;
        }

        if (Input.GetKeyDown(KeyCode.Space) && isMyTurn) 
        {
            _turnManager.EndTurnServerRpc();
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
            else 
            {
                if (selectedUnit != null)
                {
                    selectedUnit.Deselect();
                    selectedUnit = null;
                }
            }
        }
    }
    

    private void HandleAttack()   
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent(out UnitController enemy) &&
                !enemy.NetworkObject.IsOwner)   // чужой юнит
            {
        
                selectedUnit.AttackTargetServerRpc(enemy.NetworkObject);
                Debug.Log($"Приказ атаковать {enemy.name}");
           
            }
        }
    }
    
    // PlayerController.cs
    private void HandleMovement()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        Vector3 target = hit.point;

        Vector3 dir    = target - selectedUnit.transform.position;
        dir.y = 0;

        float distance = dir.magnitude;
        float maxStep  = selectedUnit.MovementSpeed ; 

        Vector3 finalTarget;
        if (distance <= maxStep)
        {
            finalTarget = target;
        }
        else
        {
            finalTarget = selectedUnit.transform.position + dir.normalized * maxStep;
        }
        selectedUnit.Move(finalTarget);
    }
}