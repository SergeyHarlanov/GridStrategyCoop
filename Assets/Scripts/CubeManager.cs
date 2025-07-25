using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq;
using Zenject;

public class CubeManager : NetworkBehaviour
{
    [System.Serializable]
    public class ExclusionZone
    {
        public Transform zoneTransform;
        public float zoneRadius = 1.0f;
    }

    [Header("Cube Settings")]
   
    public float minSpacing = 1.5f;

    [Header("Exclusion Zones")]
    public List<ExclusionZone> exclusionZones = new List<ExclusionZone>();

    [Header("Gizmo Settings")]
    public Color gizmoColor = Color.yellow;
    public Color exclusionGizmoColor = Color.red;
    public bool showGizmoInGame = false;

    [Inject] private GameSettings _gameSettings;
    
    private Transform _cubesParent; 
    private List<Vector3> _generatedPositions = new List<Vector3>(); 

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

    [ServerRpc(RequireOwnership = false)] 
    public void GenerateAndSpawnCubesNetworkServerRpc()
    {
        if (!IsServer) return; 

        ClearSpawnedNetworkCubes(); 

        _generatedPositions.Clear(); // Очищаем список позиций для новой генерации

        Debug.Log("CubeManager: Generating and Spawning cubes for network...");

        int maxAttemptsPerCube = 100; 

        for (int i = 0; i < _gameSettings.NumberOfObstacles; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            int attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                randomPosition = new Vector3(
                    Random.Range(-_gameSettings.AreaSizeX / 2f, _gameSettings.AreaSizeX / 2f),
                    0f, // Высота кубов
                    Random.Range(-_gameSettings.AreaSizeZ / 2f, _gameSettings.AreaSizeZ / 2f)
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
                GameObject randPrefab = _gameSettings.PrefabObstacles[Random.Range(0, _gameSettings.PrefabObstacles.Length)];
                GameObject newCubeInstance = Instantiate(randPrefab, randomPosition, Quaternion.identity);
                newCubeInstance.name = $"NetworkCube_{i}";

                NetworkObject netObj = newCubeInstance.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"CubeManager: Prefab {randPrefab.name} is missing NetworkObject component! Cannot spawn network cube.");
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
                   
                    Destroy(cube);
                    Debug.LogWarning($"CubeManager: Destroyed non-network-spawned cube {cube.name} locally.");
                }
            }
        }
        _spawnedNetworkCubes.Clear();
        _generatedPositions.Clear();
        Debug.Log("CubeManager: Cleared all spawned network cubes.");
    }

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

        for (int i = 0; i < _gameSettings.NumberOfObstacles; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            int attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                randomPosition = new Vector3(
                    Random.Range(-_gameSettings.AreaSizeX / 2f, _gameSettings.AreaSizeX / 2f),
                    0f, // Высота кубов
                    Random.Range(-_gameSettings.AreaSizeZ / 2f, _gameSettings.AreaSizeZ / 2f)
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
                GameObject randPrefab = _gameSettings.PrefabObstacles[Random.Range(0, _gameSettings.PrefabObstacles.Length)];
                GameObject newCube = Instantiate(randPrefab, randomPosition, Quaternion.identity);
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

    [SerializeField] private GameSettings _gameSettingsForEditor;
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmoInGame)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(_gameSettingsForEditor.AreaSizeX, 0.1f, _gameSettingsForEditor.AreaSizeZ));

        foreach (Vector3 pos in _generatedPositions)
        {
            Gizmos.DrawSphere(pos, 0.5f);
        }

        Gizmos.color = exclusionGizmoColor;
        foreach (ExclusionZone zone in exclusionZones)
        {
            if (zone.zoneTransform != null)
            {
                Gizmos.DrawWireSphere(zone.zoneTransform.position, zone.zoneRadius);
            }
        }
    }
#endif
}