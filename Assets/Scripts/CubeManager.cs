using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq; 

public class CubeManager : NetworkBehaviour
{
    [System.Serializable]
    public class ExclusionZone
    {
        public Transform zoneTransform;
        public float zoneRadius = 1.0f;
    }

    [Header("Cube Settings")]
    public GameObject cubePrefab; // Assign your Cube Prefab (MUST have NetworkObject component)
    public int numberOfCubes = 50;
    public float areaSizeX = 10f;
    public float areaSizeZ = 10f;
    public float minSpacing = 1.5f;

    [Header("Exclusion Zones")]
    public List<ExclusionZone> exclusionZones = new List<ExclusionZone>();

    [Header("Gizmo Settings")]
    public Color gizmoColor = Color.yellow;
    public Color exclusionGizmoColor = Color.red;
    public bool showGizmoInGame = false;

    // _cubesParent теперь будет использоваться только для визуальной организации в редакторе
    // и для Editor-Only генерации. Сетевые объекты не будут к нему прикрепляться.
    private Transform _cubesParent; 
    private List<Vector3> _generatedPositions = new List<Vector3>(); 

    // НОВОЕ: Список для отслеживания заспавненных сетевых объектов кубов
    private List<GameObject> _spawnedNetworkCubes = new List<GameObject>(); // Список сетевых кубов

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("CubeManager: Server spawning cubes for all clients.");
            GenerateAndSpawnCubesNetworkServerRpc(); 
        }
        else // Для клиентов
        {
            Debug.Log("CubeManager: Client waiting for cubes from server.");
        }
    }

    public override void OnNetworkDespawn()
    {
        _spawnedNetworkCubes.Clear();
    }


    // НОВЫЙ МЕТОД: Генерация и спавн кубов по сети (только на сервере)
    [ServerRpc(RequireOwnership = false)] // Можно вызывать с клиента (если не хост), но исполняется на сервере
    public void GenerateAndSpawnCubesNetworkServerRpc()
    {
        if (!IsServer) return; // Убеждаемся, что код выполняется только на сервере

        // Очищаем все ранее заспавненные сетевые кубы перед генерацией новых
        // Это важно, чтобы избежать дублирования при повторных вызовах (если они будут)
        ClearSpawnedNetworkCubes(); 

        _generatedPositions.Clear(); // Очищаем список позиций для новой генерации

        Debug.Log("CubeManager: Generating and Spawning cubes for network...");

        int maxAttemptsPerCube = 100; // Ограничение попыток для каждой куба

        for (int i = 0; i < numberOfCubes; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            int attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                randomPosition = new Vector3(
                    Random.Range(-areaSizeX / 2f, areaSizeX / 2f),
                    0f, // Высота кубов
                    Random.Range(-areaSizeZ / 2f, areaSizeZ / 2f)
                );

                bool tooCloseToExisting = false;
                foreach (Vector3 existingPos in _generatedPositions)
                {
                    if (Vector3.Distance(randomPosition, existingPos) < minSpacing)
                    {
                        tooCloseToExisting = true;
                        break;
                    }
                }

                if (tooCloseToExisting)
                {
                    attemptCount++;
                    continue;
                }

                bool inExclusionZone = false;
                foreach (ExclusionZone zone in exclusionZones)
                {
                    if (Vector3.Distance(randomPosition, zone.zoneTransform.position) < zone.zoneRadius)
                    {
                        inExclusionZone = true;
                        break;
                    }
                }

                if (inExclusionZone)
                {
                    attemptCount++;
                    continue;
                }

                positionFound = true;
                attemptCount++;
            }

            if (positionFound)
            {
                // ИНСТАНЦИРУЕМ КУБ НА СЕРВЕРЕ И СЕТЕВОЙ СПАВН
                // Это создаст куб на сервере и синхронизирует его со всеми клиентами.
                GameObject newCubeInstance = Instantiate(cubePrefab, randomPosition, Quaternion.identity);
                newCubeInstance.name = $"NetworkCube_{i}";

                NetworkObject netObj = newCubeInstance.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"CubeManager: Prefab {cubePrefab.name} is missing NetworkObject component! Cannot spawn network cube.");
                    Destroy(newCubeInstance); // Уничтожаем, так как не можем заспавнить по сети
                    continue;
                }

                netObj.Spawn(); // <--- САМЫЙ ВАЖНЫЙ ВЫЗОВ для сетевого спавна!
                _spawnedNetworkCubes.Add(newCubeInstance); // Добавляем в список отслеживания
                _generatedPositions.Add(randomPosition); // Добавляем в список сгенерированных позиций
                Debug.Log($"CubeManager: Spawned Network Cube {newCubeInstance.name} at {randomPosition}");
            }
            else
            {
                Debug.LogWarning($"CubeManager: Could not find a suitable position for network cube {i + 1} after {maxAttemptsPerCube} attempts. Consider increasing area size, decreasing number of cubes, reducing minSpacing, or adjusting exclusion zones.");
                break;
            }
        }
        Debug.Log($"CubeManager: Successfully spawned {_spawnedNetworkCubes.Count} network cubes.");
    }
    
    // Метод для очистки сетевых кубов (только на сервере)
    [ServerRpc(RequireOwnership = false)]
    public void ClearSpawnedNetworkCubesServerRpc()
    {
        if (!IsServer) return; // Только сервер может уничтожать сетевые объекты

        ClearSpawnedNetworkCubes();
    }

    private void ClearSpawnedNetworkCubes()
    {
        // Деспавним и уничтожаем все сетевые кубы
        // На сервере NetworkObject.Despawn() также уничтожает GameObject по умолчанию
        foreach (GameObject cube in _spawnedNetworkCubes)
        {
            if (cube != null)
            {
                NetworkObject netObj = cube.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(); // Деспавнит и уничтожает объект по сети
                    Debug.Log($"CubeManager: Despawned network cube {cube.name}");
                }
                else
                {
                    // Если по какой-то причине объект не заспавнен (например, ошибка), просто уничтожаем локально
                    Destroy(cube);
                    Debug.LogWarning($"CubeManager: Destroyed non-network-spawned cube {cube.name} locally.");
                }
            }
        }
        _spawnedNetworkCubes.Clear();
        _generatedPositions.Clear();
        Debug.Log("CubeManager: Cleared all spawned network cubes.");
    }

    // ВЫЗЫВАЙТЕ ЭТОТ МЕТОД ТОЛЬКО В РЕДАКТОРЕ для визуального размещения
    // и не в игровом режиме. Не используйте для сетевой игры.
    public void GenerateCubesLocalForEditor()
    {
        if (Application.isPlaying) 
        {
            Debug.LogWarning("GenerateCubesLocalForEditor should only be used in Editor mode, not during play. Use GenerateAndSpawnCubesNetworkServerRpc for network spawning.");
            return;
        }

        ClearCubesLocalForEditor(); // Очищаем старые кубы в редакторе

        if (_cubesParent == null)
        {
            _cubesParent = new GameObject("CubesParent (Editor Only)").transform;
        }

        Debug.Log("Generating cubes locally for editor...");

        int maxAttemptsPerCube = 100; // Ограничение попыток для каждой куба

        for (int i = 0; i < numberOfCubes; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            int attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                randomPosition = new Vector3(
                    Random.Range(-areaSizeX / 2f, areaSizeX / 2f),
                    0f, // Высота кубов
                    Random.Range(-areaSizeZ / 2f, areaSizeZ / 2f)
                );

                bool tooCloseToExisting = false;
                foreach (Vector3 existingPos in _generatedPositions)
                {
                    if (Vector3.Distance(randomPosition, existingPos) < minSpacing)
                    {
                        tooCloseToExisting = true;
                        break;
                    }
                }

                if (tooCloseToExisting)
                {
                    attemptCount++;
                    continue;
                }

                bool inExclusionZone = false;
                foreach (ExclusionZone zone in exclusionZones)
                {
                    if (Vector3.Distance(randomPosition, zone.zoneTransform.position) < zone.zoneRadius)
                    {
                        inExclusionZone = true;
                        break;
                    }
                }

                if (inExclusionZone)
                {
                    attemptCount++;
                    continue;
                }

                positionFound = true;
                attemptCount++;
            }

            if (positionFound)
            {
                // Для редактора просто инстанцируем локально
                GameObject newCube = Instantiate(cubePrefab, randomPosition, Quaternion.identity);
                newCube.transform.parent = _cubesParent; // Для Editor-Only генерации родитель допустим
                newCube.name = $"Cube_{_generatedPositions.Count}";
                _generatedPositions.Add(randomPosition);
            }
            else
            {
                Debug.LogWarning($"Could not find a suitable position for cube {i + 1} after {maxAttemptsPerCube} attempts in editor. Consider increasing area size, decreasing number of cubes, reducing minSpacing, or adjusting exclusion zones.");
                break;
            }
        }
        Debug.Log($"Generated {_generatedPositions.Count} cubes in editor.");
    }

    // НОВЫЙ метод для очистки кубов ТОЛЬКО В РЕДАКТОРЕ
    private void ClearCubesLocalForEditor()
    {
        if (_cubesParent == null) return;

        foreach (Transform child in _cubesParent.Cast<Transform>().ToList())
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
            }
        }
        _generatedPositions.Clear();
        Debug.Log("Cleared existing cubes locally for editor.");
    }

    // Методы для Gizmos (для визуализации в редакторе)
    void OnDrawGizmos()
    {
        if (Application.isPlaying && !showGizmoInGame) return;

        // Draw area
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSizeX, 0.1f, areaSizeZ));

        // Draw generated positions
        foreach (Vector3 pos in _generatedPositions)
        {
            Gizmos.DrawSphere(pos, 0.5f);
        }

        // Draw exclusion zones
        Gizmos.color = exclusionGizmoColor;
        foreach (ExclusionZone zone in exclusionZones)
        {
            if (zone.zoneTransform != null)
            {
                Gizmos.DrawWireSphere(zone.zoneTransform.position, zone.zoneRadius);
            }
        }
    }
}