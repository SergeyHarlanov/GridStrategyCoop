using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Cube : NetworkBehaviour // Куб должен быть NetworkBehaviour
{
    private NetworkTransform networkTransform; // Ссылка на NetworkTransform

    private void Awake()
    {
        // Получаем ссылку на NetworkTransform в Awake
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        // Этот метод вызывается, когда объект заспавнен по сети.
        // Убедитесь, что вы на сервере, чтобы избежать ошибок на клиентах,
        // хотя изменение enabled безопасно на всех сторонах.
        StartCoroutine(NetworkTransformDisabledCoroutine());
    }

    private IEnumerator NetworkTransformDisabledCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
            
        if (IsServer && networkTransform != null) 
        {
            //  networkTransform.enabled = false;
            Debug.Log($"Cube {NetworkObject.NetworkObjectId} NetworkTransform disabled on server.");
        }
        else if (networkTransform != null) // Для клиентов тоже можно отключить, если нужно
        {
            //   networkTransform.enabled = false;
            Debug.Log($"Cube {NetworkObject.NetworkObjectId} NetworkTransform disabled on client {NetworkManager.Singleton.LocalClientId}.");
        }
    }

    // Если куб когда-либо деспавнится, и вам нужно его "очистить" или подготовить к повторному спавну
    public override void OnNetworkDespawn()
    {
        // Не обязательно что-то делать здесь для вашей текущей задачи
        // Если объект уничтожается, NetworkTransform тоже исчезнет.
    }
}