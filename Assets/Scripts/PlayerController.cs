using Unity.Netcode;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private UnitController selectedUnit;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // Этот скрипт должен работать только если мы в активной сетевой сессии
        if (!NetworkManager.Singleton.IsClient)
        {
            return;
        }

        // Левая кнопка мыши - выбор юнита
        if (Input.GetMouseButtonDown(0))
        {
            HandleSelection();
        }
        
        // Правая кнопка мыши - отдать приказ на перемещение
        else if (Input.GetMouseButtonDown(1) && selectedUnit != null)
        {
            HandleMovement();
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

    private void HandleMovement()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Отправляем серверу команду на перемещение выбранного юнита
            selectedUnit.MoveServerRpc(hit.point);
        }
    }
}