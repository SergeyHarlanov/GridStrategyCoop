using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Loader
{
    public static void LoadNetwork(string sceneName)
    {
        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}