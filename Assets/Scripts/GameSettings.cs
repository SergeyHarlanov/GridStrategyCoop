using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public GameObject[] PrefabObstacles => _prefabObstacles;
    public GameObject[] PrefabUnits => _prefabUnits;
    public float AreaSizeX => _areaSizeX;
    public float AreaSizeZ => _areaSizeZ;
    public int NumberOfObstacles => _numberOfObstacles;
    
    public Transform[] PointsSpawnPlayer1 => _pointsSpawnPlayer1;
    public Transform[] PointsSpawnPlayer2 => _pointsSpawnPlayer2;
    public int NumberOfUnits => _numberOfUnits;
    
    [Header("Спавн препятствий")]
    [SerializeField] private GameObject[] _prefabObstacles;
    [SerializeField] private float _areaSizeX, _areaSizeZ;
    [SerializeField] private int _numberOfObstacles;
    
    [Header("Спавн юнитов")]
    [SerializeField] private GameObject[] _prefabUnits;
    [SerializeField] private Transform[] _pointsSpawnPlayer1;
    [SerializeField] private Transform[] _pointsSpawnPlayer2;
    [SerializeField] private int _numberOfUnits;
}