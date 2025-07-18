using Unity.Netcode;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private UnitController selectedUnit;
    private Camera mainCamera;


    // поля для отслеживания двойного щелчка
    private float lastRightClickTime;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f; // секунды

    private void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        // ЛКМ – выбор юнита (без изменений)
        if (Input.GetMouseButtonDown(0))
            HandleSelection();

        // ПКМ – отдать приказ только после двойного щелчка
        if (Input.GetMouseButtonDown(1) && selectedUnit != null)
        {
            float timeSinceLastClick = Time.time - lastRightClickTime;

            if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
            {
                Debug.Log("Unit Move (double right-click)");
                HandleMovement();
            }

            lastRightClickTime = Time.time;
        }
    }

    private void HandleSelection()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Пытаемся получить компонент UnitController из объекта, по которому кликнули
            if (hit.collider.TryGetComponent<UnitController>(out UnitController unit))
            {
                // САМОЕ ВАЖНОЕ: проверяем, являемся ли мы владельцем этого юнита
                if (unit.NetworkObject.IsOwner)
                {
                    // Снимаем выделение с предыдущего юнита, если он был
                    if (selectedUnit != null)
                    {
                        selectedUnit.Deselect();
                    }
                    
                    // Выбираем нового юнита и подсвечиваем его
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

    // PlayerController.cs
    private void HandleMovement()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Vector3 target = hit.point;
        Vector3 dir    = target - selectedUnit.transform.position;
        dir.y = 0;

        float distance = dir.magnitude;
        float maxStep  = selectedUnit.movementSpeed;   // берём из stats

        Vector3 finalTarget;
        if (distance <= maxStep)
        {
            finalTarget = target;               // клик в пределах досягаемости
        }
        else
        {
            finalTarget = selectedUnit.transform.position + dir.normalized * maxStep;
        }

        selectedUnit.MoveServerRpc(finalTarget);
    }
}