using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : NetworkBehaviour
{
    private NavMeshAgent navAgent;
    private Renderer unitRenderer; // Для визуального отклика выбора
    private Color originalColor;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        unitRenderer = GetComponentInChildren<Renderer>();
        if (unitRenderer != null)
        {
            originalColor = unitRenderer.material.color;
        }
    }

    /// <summary>
    /// Этот метод будет вызываться локально для отображения выбора.
    /// </summary>
    public void Select()
    {
        if (unitRenderer != null)
        {
            unitRenderer.material.color = Color.green; // Подсвечиваем юнита зеленым
        }
    }

    /// <summary>
    /// Снимает визуальное выделение.
    /// </summary>
    public void Deselect()
    {
        if (unitRenderer != null)
        {
            unitRenderer.material.color = originalColor;
        }
    }

    /// <summary>
    /// Клиент вызывает этот метод, который затем исполняется на СЕРВЕРЕ.
    /// </summary>
    [ServerRpc]
    public void MoveServerRpc(Vector3 targetPosition)
    {
        // На сервере мы задаем точку назначения для NavMeshAgent
        // NetworkTransform автоматически синхронизирует перемещение для всех клиентов
        navAgent.SetDestination(targetPosition);
    }
}