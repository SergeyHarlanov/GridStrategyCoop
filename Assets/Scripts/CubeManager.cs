using UnityEngine;

public class CubeManager : MonoBehaviour
{
    // Singleton instance
    public static CubeManager Instance { get; private set; }

    [Header("Cube Settings")]
    public GameObject cubePrefab; // Assign your Cube Prefab in the Inspector
    public int gridSizeX = 10;    // Number of cubes in X direction
    public int gridSizeY = 10;    // Number of cubes in Y direction
    public float spacing = 1.1f;  // Spacing between cubes

    private Transform _cubesParent; // To keep generated cubes organized under a dedicated parent

    /// <summary>
    /// Ensures only one instance of CubeManager exists and initializes it.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep the manager alive across scene changes (optional)
        }

        // Create a parent GameObject for all cubes for better hierarchy organization
        GameObject parentGo = new GameObject("GeneratedCubes");
        _cubesParent = parentGo.transform;
        _cubesParent.SetParent(this.transform); // Make the cubes parent a child of the manager for neatness
    }

    /// <summary>
    /// Generates a grid of cubes based on the defined settings.
    /// Can be called to regenerate the grid.
    /// </summary>
    public void GenerateCubes()
    {
        if (cubePrefab == null)
        {
            Debug.LogError("Cube Prefab is not assigned! Please assign a Cube Prefab in the Inspector.");
            return;
        }

        // Clear existing cubes before generating new ones (useful if you regenerate)
        ClearCubes();

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 position = new Vector3(x * spacing, 0, y * spacing);
                GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);

                // Parent the new cube to the dedicated _cubesParent transform
                newCube.transform.parent = _cubesParent;

                newCube.name = $"Cube_{x}_{y}";
            }
        }
        Debug.Log($"Generated {gridSizeX * gridSizeY} cubes.");
    }

    /// <summary>
    /// Clears all previously generated cubes.
    /// </summary>
    public void ClearCubes()
    {
        if (_cubesParent == null) return;

        // Destroy all children of the _cubesParent
        foreach (Transform child in _cubesParent)
        {
            Destroy(child.gameObject);
        }
        Debug.Log("Cleared existing cubes.");
    }

    // Example of how to call GenerateCubes from Start (you might call it from another script)
    void Start()
    {
        GenerateCubes();
    }
}