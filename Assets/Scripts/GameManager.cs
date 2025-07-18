using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject[] _unitsPrefabForSpawn; // Ensure this prefab has the UnitController attached

    [Header("Точки спавна")]
    [SerializeField] private Transform[] player1SpawnPoints;
    [SerializeField] private Transform[] player2SpawnPoints;

    public override void OnNetworkSpawn()
    {
        // Check if this object has a NetworkObject. Without it, the method won't be called.
        if (IsServer)
        {
            Debug.Log("GameManager: OnNetworkSpawn. Server started. Waiting for clients...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // If the server is already running and there's at least one client, spawn units.
            // This handles cases where a client might connect before the server's OnNetworkSpawn fully completes,
            // or if the server starts without any clients initially.
            if (NetworkManager.Singleton.ConnectedClients.Count >= 1)
            {
                SpawnGroupUnits();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Log every time someone connects
        Debug.Log($"GameManager: Client {clientId} connected. Total clients: {NetworkManager.Singleton.ConnectedClients.Count}");

        // Only try to spawn units if the game hasn't started yet and we have enough players
        // This prevents re-spawning units if a client disconnects and reconnects later in a running game.
            SpawnGroupUnits();
    }

    private void SpawnGroupUnits()
    {
        // Ensure this is the server, we have at least one client, and the game hasn't officially started spawning units yet.
        if (IsServer)
        {
            Debug.Log("!!! GameManager: START CONDITION MET. Beginning spawn. !!!");

            // Spawn for Player 1 (first connected client)
            if (NetworkManager.Singleton.ConnectedClients.Count >= 1)
            {
                ulong player1Id = NetworkManager.Singleton.ConnectedClientsIds[0];
                SpawnUnitsForPlayer(player1Id, player1SpawnPoints);
            }

            // Spawn for Player 2 (second connected client, if available)
            if (NetworkManager.Singleton.ConnectedClients.Count >= 2)
            {
                ulong player2Id = NetworkManager.Singleton.ConnectedClientsIds[1];
                SpawnUnitsForPlayer(player2Id, player2SpawnPoints);
            }
        }
        else
        {
            Debug.Log($"!!! GameManager: START CONDITION NOT MET. Current state - IsServer: {IsServer}, ConnectedClients: {NetworkManager.Singleton.ConnectedClients.Count}");
        }
    }

    private void SpawnUnitsForPlayer(ulong ownerId, Transform[] spawnPoints)
    {
        Debug.Log($"-- Spawning 5 units for player {ownerId} --");
        if (spawnPoints.Length < 5)
        {
            Debug.LogError($"!!! ERROR: Not enough spawn points for player {ownerId}. Need 5. Found: {spawnPoints.Length}");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject unitInstance = Instantiate(_unitsPrefabForSpawn[i], spawnPoints[i].position, spawnPoints[i].rotation);
            NetworkObject networkObject = unitInstance.GetComponent<NetworkObject>();
            UnitController unitController = unitInstance.GetComponent<UnitController>(); // Get the UnitController

            networkObject.SpawnWithOwnership(ownerId); // Spawn with ownership
            Debug.Log($"Unit {i+1} for player {ownerId} spawned.");
        }
    }
}