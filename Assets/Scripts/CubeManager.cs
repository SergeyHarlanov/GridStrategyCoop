using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq; 

public class CubeManager : NetworkBehaviour
{
    public static CubeManager Instance { get; private set; }

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
    private List<NetworkObject> _spawnedCubeNetworkObjects = new List<NetworkObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        // Создаем родительский GameObject для визуальной организации в редакторе
        // Он не будет сетевым объектом.
        GameObject parentGo = new GameObject("GeneratedCubes");
        _cubesParent = parentGo.transform;
        _cubesParent.SetParent(this.transform); // Прикрепляем к CubeManager GameObject
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"CubeManager: OnNetworkSpawn called. IsServer: {IsServer}, IsHost: {IsHost}, IsClient: {IsClient}");

        if (IsServer) 
        {
            Debug.Log("CubeManager: Server is spawning. Generating cubes...");
            GenerateCubes();
        }
    }

    public override void OnNetworkDespawn()
    {
        // При деспавне CubeManager, деспавним все его кубы
        if (IsServer)
        {
            ClearCubes();
        }
        base.OnNetworkDespawn();
    }

    private void GenerateCubes()
    {
        if (!IsServer)
        {
            Debug.LogWarning("GenerateCubes can only be called on the server.");
            return;
        }

        if (cubePrefab == null)
        {
            Debug.LogError("Cube Prefab is not assigned! Please assign a Cube Prefab in the Inspector.");
            return;
        }

        ClearCubes(); // Очищаем и деспавним существующие сетевые кубы
        _generatedPositions.Clear(); // Очищаем локальный список позиций
        _spawnedCubeNetworkObjects.Clear(); // НОВОЕ: Очищаем список сетевых объектов

        int attemptCount = 0;
        int maxAttemptsPerCube = 200;

        for (int i = 0; i < numberOfCubes; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                float randomX = transform.position.x + Random.Range(-areaSizeX / 2f, areaSizeX / 2f);
                float randomZ = transform.position.z + Random.Range(-areaSizeZ / 2f, areaSizeZ / 2f);

                randomPosition = new Vector3(randomX, transform.position.y, randomZ);

                bool tooCloseToExistingCubes = false;
                foreach (Vector3 existingPos in _generatedPositions) 
                {
                    if (Vector3.Distance(randomPosition, existingPos) < minSpacing)
                    {
                        tooCloseToExistingCubes = true;
                        break;
                    }
                }
                if (tooCloseToExistingCubes)
                {
                    attemptCount++;
                    continue;
                }

                bool inExclusionZone = false;
                foreach (ExclusionZone zone in exclusionZones)
                {
                    if (zone.zoneTransform == null) continue;

                    Vector3 flatRandomPos = new Vector3(randomPosition.x, 0, randomPosition.z);
                    Vector3 flatExcludedPos = new Vector3(zone.zoneTransform.position.x, 0, zone.zoneTransform.position.z);

                    if (Vector3.Distance(flatRandomPos, flatExcludedPos) < zone.zoneRadius / 2f + 0.01f)
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
                _generatedPositions.Add(randomPosition); 

                GameObject newCubeInstance = Instantiate(cubePrefab, randomPosition, Quaternion.identity);
                // newCubeInstance.transform.parent = _cubesParent; // УДАЛЕНО: Больше не делаем родителя для сетевых объектов
                newCubeInstance.name = $"Cube_{_generatedPositions.Count}";

                NetworkObject networkObject = newCubeInstance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError($"Cube Prefab '{cubePrefab.name}' is missing a NetworkObject component! Cannot spawn network object.");
                    Destroy(newCubeInstance);
                    continue;
                }
                
                networkObject.Spawn(true); // Сервер спавнит объект по сети для всех клиентов
                
                // НОВОЕ: Добавляем заспавненный сетевой объект в наш список
                _spawnedCubeNetworkObjects.Add(networkObject);
            }
            else
            {
                Debug.LogWarning($"Could not find a suitable position for cube {i + 1} after {maxAttemptsPerCube} attempts. Consider increasing area size, decreasing number of cubes, reducing minSpacing, or adjusting exclusion zones.");
                break;
            }
        }
        Debug.Log($"Generated {_generatedPositions.Count} networked cubes.");
    }

    /// <summary>
    /// Очищает все заспавненные сетевые кубы (только на сервере).
    /// </summary>
    public void ClearCubes()
    {
        if (!IsServer)
        {
            Debug.LogWarning("ClearCubes should only be called on the server.");
            return;
        }

        // НОВОЕ: Деспавним кубы из списка _spawnedCubeNetworkObjects
        // Используем ToList() для обхода коллекции, так как Despawn может модифицировать оригинальный список,
        // если бы мы напрямую итерировали NetworkManager.Singleton.SpawnManager.SpawnedObjects
        foreach (NetworkObject netObj in _spawnedCubeNetworkObjects.ToList()) 
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true); // Деспавним со всех клиентов
            }
        }
        _spawnedCubeNetworkObjects.Clear(); // Очищаем список после деспавна
        _generatedPositions.Clear(); // Очищаем локальный список позиций
        Debug.Log("Cleared existing networked cubes.");
    }
    
    void OnDrawGizmos()
    {
        if (Application.isPlaying && !showGizmoInGame)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Vector3 gizmoCenter = transform.position;
        Vector3 gizmoSize = new Vector3(areaSizeX, 0.1f, areaSizeZ);
        Gizmos.DrawWireCube(gizmoCenter, gizmoSize);

        Gizmos.color = exclusionGizmoColor;
        foreach (ExclusionZone zone in exclusionZones)
        {
            if (zone.zoneTransform == null) continue;

            Gizmos.DrawCube(new Vector3(zone.zoneTransform.position.x, transform.position.y, zone.zoneTransform.position.z), Vector3.one * zone.zoneRadius);
        }
    }

    [ContextMenu("Generate Cubes Now (Editor Only)")]
    private void EditorGenerateCubes()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Use GenerateCubes via NetworkManager on server during play mode.");
            return;
        }
        // Этот метод НЕ СЕТЕВОЙ, он просто генерирует кубы локально в редакторе
        ClearCubesLocalForEditor();
        _generatedPositions.Clear(); // Для редактора используем локальный список
        GenerateCubesEditorOnly(); // Отдельный метод для редактора
    }

    [ContextMenu("Clear Cubes Now (Editor Only)")]
    private void EditorClearCubes()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Use ClearCubes via NetworkManager on server during play mode.");
            return;
        }
        ClearCubesLocalForEditor();
        _generatedPositions.Clear();
    }

    // НОВЫЙ метод для генерации кубов ТОЛЬКО В РЕДАКТОРЕ
    private void GenerateCubesEditorOnly()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("Cube Prefab is not assigned! Please assign a Cube Prefab in the Inspector.");
            return;
        }

        int attemptCount = 0;
        int maxAttemptsPerCube = 200;

        for (int i = 0; i < numberOfCubes; i++)
        {
            Vector3 randomPosition = Vector3.zero;
            bool positionFound = false;
            attemptCount = 0;

            while (!positionFound && attemptCount < maxAttemptsPerCube)
            {
                float randomX = transform.position.x + Random.Range(-areaSizeX / 2f, areaSizeX / 2f);
                float randomZ = transform.position.z + Random.Range(-areaSizeZ / 2f, areaSizeZ / 2f);

                randomPosition = new Vector3(randomX, transform.position.y, randomZ);

                bool tooCloseToExistingCubes = false;
                foreach (Vector3 existingPos in _generatedPositions)
                {
                    if (Vector3.Distance(randomPosition, existingPos) < minSpacing)
                    {
                        tooCloseToExistingCubes = true;
                        break;
                    }
                }
                if (tooCloseToExistingCubes)
                {
                    attemptCount++;
                    continue;
                }

                bool inExclusionZone = false;
                foreach (ExclusionZone zone in exclusionZones)
                {
                    if (zone.zoneTransform == null) continue;

                    Vector3 flatRandomPos = new Vector3(randomPosition.x, 0, randomPosition.z);
                    Vector3 flatExcludedPos = new Vector3(zone.zoneTransform.position.x, 0, zone.zoneTransform.position.z);

                    if (Vector3.Distance(flatRandomPos, flatExcludedPos) < zone.zoneRadius / 2f + 0.01f)
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
}