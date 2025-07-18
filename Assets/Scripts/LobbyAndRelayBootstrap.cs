using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Lobby;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1. Авторизуемся в UGS.
/// 2. Создаём или присоединяемся к лобби.
/// 3. В лобби хост запрашивает Relay-код, клиенты забирают его.
/// 4. После того как 2 игрока в лобби – автоматически загружаем игровую сцену и запускаем Netcode.
/// </summary>
public class LobbyAndRelayBootstrap : MonoBehaviour
{
    public Button         quickJoinBtn;  // перетащи кнопку «Join»
    [Header("Ввод кода лобби")]
    public TMP_InputField lobbyCodeInput;   // перетащи InputField сюда
    public Button         joinWithCodeBtn;  // перетащи кнопку «Join»
    [Header("UI (необязательно)")]
    public Button createButton;
    public Button joinButton;
    public Text   statusText;

    private const int MaxPlayers = 2;
    private const string LobbyCodeKey = "RelayJoinCode";

    private void Awake()
    {
        Application.targetFrameRate = 60;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        createButton?.onClick.AddListener(() => _ = StartHostAsync());
        joinButton?.onClick.AddListener(() => _ = StartClientAsync());
        joinWithCodeBtn.onClick.AddListener(() =>
        {
            if (!string.IsNullOrWhiteSpace(lobbyCodeInput.text))
                _ = StartClientAsync(lobbyCodeInput.text.ToUpper());
        });
        quickJoinBtn.onClick.AddListener(() => _ = QuickJoinAsync());
        await UnityServices.InitializeAsync();
        var profile = "Player_" + Guid.NewGuid().ToString("N")[..8];
        AuthenticationService.Instance.SwitchProfile(profile);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Status("Авторизован: " + AuthenticationService.Instance.PlayerId);
    }
    public async Task QuickJoinAsync()
    {
        try
        {
            Status("Ищем свободное лобби...");

            var options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.AvailableSlots, "1", QueryFilter.OpOptions.GE)
                }
            };

  
            var response = await LobbyService.Instance.QueryLobbiesAsync(options);

            if (response.Results.Count == 0)
            {
                Status("Нет открытых лобби.");
                return;
            }

            var lobby = response.Results[0];

            // Проверяем, есть ли нужный ключ в данных лобби
            if (!lobby.Data.TryGetValue(LobbyCodeKey, out var dataObject))
            {
                Status("Лобби не содержит Relay-код.");
                return;
            }

            string relayJoinCode = dataObject.Value;

            Status($"Получен Relay-код: {relayJoinCode}");

            // Подключаемся к Relay
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            var relayServerData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(relayServerData);

            // Стартуем клиента
            NetworkManager.Singleton.StartClient();
            Loader.LoadNetwork("Game");
        }
        catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyNotFound)
        {
            Status("Лобби уже закрылось.");
        }
        catch (Exception ex)
        {
            Status("Ошибка quick-join: " + ex.Message);
            Debug.LogException(ex);
        }
    }
    #region Host
    public async Task StartHostAsync()
    {
        try
        {
            Status("Создаём лобби...");
            var lobby = await LobbyService.Instance.CreateLobbyAsync(
                "Room_" + Guid.NewGuid().ToString("N")[..4], MaxPlayers);

            Status("Создаём Relay...");
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
            var joinCode   = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            await LobbyService.Instance.UpdateLobbyAsync(
                lobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new System.Collections.Generic.Dictionary<string, DataObject>
                    {
                        { LobbyCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                    }
                });

            Status("Ждём второго игрока в лобби " + lobby.LobbyCode + "...");
            StartCoroutine(PollLobbyUntilReady(lobby.Id, allocation));
        }
        catch (Exception ex)
        {
            Status("Ошибка хоста: " + ex.Message);
        }
    }

    private System.Collections.IEnumerator PollLobbyUntilReady(string lobbyId, Allocation allocation)
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            Task<Lobby> poll = LobbyService.Instance.GetLobbyAsync(lobbyId);
            yield return new WaitUntil(() => poll.IsCompleted);

            if (poll.Result.Players.Count == MaxPlayers)
            {
                // Запускаем сцену и стартуем Netcode
                NetworkManager.Singleton.GetComponent<UnityTransport>()
                    .SetRelayServerData(new RelayServerData(allocation, "dtls"));
                NetworkManager.Singleton.StartHost();
                Loader.LoadNetwork("Game"); // см. ниже
                yield break;
            }
        }
    }
    #endregion

    public async Task StartClientAsync(string lobbyCode = null)
    {
        // Если код не передали — берём из UI
        if (string.IsNullOrEmpty(lobbyCode))
            lobbyCode = lobbyCodeInput?.text?.Trim().ToUpper();

        if (string.IsNullOrEmpty(lobbyCode))
        {
            Status("Введите код лобби!");
            return;
        }

        try
        {
            Status($"Подключение к лобби {lobbyCode}...");

            // 1. Присоединяемся к лобби
            var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            if (!lobby.Data.TryGetValue(LobbyCodeKey, out var dataObject))
            {
                Status("В лобби нет Relay-кода. Возможно, хост ещё не создал реле.");
                return;
            }
            Status($"Лобби найдено: {lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})");

            // 2. Получаем Relay-код из данных лобби
            if (!lobby.Data.TryGetValue(LobbyCodeKey, out var relayData))
            {
                Status("В лобби нет Relay-кода. Возможно, хост ещё не создал реле.");
                return;
            }

            string relayJoinCode = relayData.Value;
            Status($"Получен Relay-код: {relayJoinCode}");

            // 3. Подключаемся к Relay
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            var relayServerData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(relayServerData);

            // 4. Запускаем Netcode-клиент
            NetworkManager.Singleton.StartClient();
            Status("Клиент запущен. Загрузка сцены...");
            Loader.LoadNetwork("Game");
        }
        catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyNotFound)
        {
            Status("Лобби не найдено. Проверьте код.");
        }
        catch (Exception ex)
        {
            Status("Ошибка клиента: " + ex.Message);
        }
    }

    private async Task<string> GetLobbyCodeFromInputAsync()
    {
#if UNITY_EDITOR
        return Debug.isDebugBuild ? "AAAA" : null; // для быстрого теста
#else
        return await GetLobbyCodeFromUI(); // ваш UI
#endif
    }

    #region Utils
    private void Status(string s) => Debug.Log(s); // statusText?.text = s;
    #endregion
}