using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Cube : NetworkBehaviour
{
    private NetworkTransform _networkTransform; 

    private void Awake()
    {
        _networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        StartCoroutine(NetworkTransformDisabledCoroutine());
    }

    private IEnumerator NetworkTransformDisabledCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
            
        if (IsServer && _networkTransform != null) 
        {
            _networkTransform.enabled = false;
            Debug.Log($"Cube {NetworkObject.NetworkObjectId} NetworkTransform disabled on server.");
        }
        else if (_networkTransform != null) 
        {
            _networkTransform.enabled = false;
            Debug.Log($"Cube {NetworkObject.NetworkObjectId} NetworkTransform disabled on client {NetworkManager.Singleton.LocalClientId}.");
        }
    }
    
    public override void OnNetworkDespawn()
    {

    }
}