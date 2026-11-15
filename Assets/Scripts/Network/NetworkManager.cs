using Fusion;
using Fusion.Sockets;
using Photon.Voice.Fusion;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetworkConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    private FusionVoiceClient voiceClient;
    private SafeSpawnManager safeSpawnManager;

    public static event Action OnConnectedToGame;
    public static event Action OnConnectionFailed;

    public static event Action OnDisconnectedFromGame;
    public static event Action OnReconnectedToGame;

    public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Disconnected;

    [Header("Network components")]
    [SerializeField] private NetworkSceneManagerDefault sceneManager;
    [SerializeField] private NetworkRunner runner;

    [Header("Player")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Input")]
    private InputSystemActions inputSystemActions;

    [Header("Spawns")]
    public Transform[] spawnPoints;

    [Header("Network menu")]
    public NetworkMenu netMenu;

    [Header("Network pool")]
    [SerializeField] private PooledNetworkObjectProvider objectProvider;

    [Header("Reconnection")]
    [SerializeField] private float reconnectTimeout = 120f;
    private string currentSessionName;
    private bool isReconnecting;
    private bool isStartingGame;

    private Dictionary<PlayerRef, string> connectedPlayers = new Dictionary<PlayerRef, string>();
    private Dictionary<string, PlayerReconnectData> disconnectedPlayers = new Dictionary<string, PlayerReconnectData>();

    private Dictionary<string, int> playerSpawnAssignments = new Dictionary<string, int>();

    private void Awake()
    {
        voiceClient = GetComponent<FusionVoiceClient>();

        if (runner == null)
        {
            Debug.LogError("[NetworkManager] No se ha asignado el NetworkRunner.");
        }

        runner.AddCallbacks(this);

        if (voiceClient == null)
        {
            Debug.LogError("[NetworkManager] No se ha encontrado FusionVoiceClient.");
        }

        runner.AddCallbacks(voiceClient);

        inputSystemActions = new InputSystemActions();
    }

    private void OnEnable()
    {
        inputSystemActions.Enable();
    }

    private void OnDisable()
    {
        inputSystemActions.Disable();
    }

    private void Update()
    {
        if (runner == null || !runner.IsRunning || !runner.IsServer)
            return;

        if (disconnectedPlayers.Count == 0)
            return;

        List<string> expiredPlayers = new List<string>();

        foreach (var entry in disconnectedPlayers)
        {
            float elapsedTime = Time.time - entry.Value.DisconnectTime;

            if (elapsedTime >= reconnectTimeout)
            {
                expiredPlayers.Add(entry.Key);
            }
        }

        foreach (string playerId in expiredPlayers)
        {
            ReleaseDisconnectedPlayer(runner, playerId);
        }
    }

    #region Start game modes
    public void StartGameHost(string sessionName)
    {
        if (netMenu != null)
            netMenu.CreatingServer();

        _ = StartGame(GameMode.Host, sessionName);
    }

    public void StartGameClient(string sessionName)
    {
        if (netMenu != null)
            netMenu.JoiningServer();

        _ = StartGame(GameMode.Client, sessionName);
    }

    public void QuickPlay()
    {
        if (netMenu != null)
            netMenu.JoiningServer();

        _ = StartGame(GameMode.AutoHostOrClient);
    }

    private async Task StartGame(GameMode gameMode, string sessionName = null)
    {
        if (runner == null || isStartingGame)
            return;

        isStartingGame = true;

        bool wasReconnecting = isReconnecting;

        SetConnectionState(wasReconnecting ? NetworkConnectionState.Reconnecting : NetworkConnectionState.Connecting);

        runner.ProvideInput = true;

        Debug.Log($"[NetworkManager] Iniciando conexión. Modo: {gameMode}");

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            PlayerCount = 4,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            SceneManager = sceneManager,
            ObjectProvider = objectProvider,
            ConnectionToken = PlayerReconnectIdentity.ConnectionToken
        });

        if (!result.Ok)
        {
            Debug.LogError($"[NetworkManager] Error de conexión: {result.ShutdownReason}");

            SetConnectionState(NetworkConnectionState.Failed);

            isReconnecting = false;

            await RecreateRunner();

            isStartingGame = false;

            OnConnectionFailed?.Invoke();

            return;
        }

        isStartingGame = false;

        // Guardar la sesión a la que estamos conectados.
        currentSessionName = runner.SessionInfo.Name;

        Debug.Log($"[NetworkManager] Conexión establecida con: {currentSessionName}");

        if (netMenu != null)
        {
            netMenu.CreatingServerClose();
            netMenu.JoiningServerClose();
        }

        isReconnecting = false;

        SetConnectionState(NetworkConnectionState.Connected);

        if (wasReconnecting)
        {
            OnReconnectedToGame?.Invoke();
        }
        else
        {
            OnConnectedToGame?.Invoke();
        }
    }
    #endregion

    private void SetConnectionState(NetworkConnectionState newState)
    {
        if (ConnectionState == newState)
            return;

        Debug.Log($"[NetworkManager] Connection state: {ConnectionState} -> {newState}");

        ConnectionState = newState;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        string playerId = GetPlayerId(runner, player);

        Debug.Log($"[JOIN] PlayerRef: {player} | Identity: {playerId}");

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogWarning("Jugador conectado sin ConnectionToken.");
            return;
        }

        connectedPlayers[player] = playerId;

        if (disconnectedPlayers.TryGetValue(playerId, out PlayerReconnectData data))
        {
            Debug.Log("[JOIN] Se detectó un jugador que está reconectando.");

            float elapsed = Time.time - data.DisconnectTime;

            if (elapsed < reconnectTimeout)
            {
                ReconnectPlayer(runner, player, data);
                return;
            }

            ReleaseDisconnectedPlayer(runner, playerId);
        }

        Debug.Log("[JOIN] Se creará un personaje nuevo.");

        SpawnPlayer(runner, player);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        NetworkObject playerObject = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        runner.SetPlayerObject(player, playerObject);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        ReserveDisconnectedPlayer(runner, player);
    }

    private void ReserveDisconnectedPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (!connectedPlayers.TryGetValue(player, out string playerId))
        {
            Debug.LogWarning($"No se encontró la identidad de {player}.");
            return;
        }

        connectedPlayers.Remove(player);

        NetworkObject playerObject = runner.GetPlayerObject(player);

        if (playerObject == null)
        {
            Debug.LogWarning($"No se encontró el objeto de {player}.");
            return;
        }

        Player playerComponent = playerObject.GetComponent<Player>();

        if (playerComponent == null)
        {
            Debug.LogError("El NetworkObject no contiene Player.");
            runner.Despawn(playerObject);
            return;
        }

        int spawnIndex = -1;

        if (playerSpawnAssignments.TryGetValue(playerId, out int assignedIndex))
        {
            spawnIndex = assignedIndex;
        }

        PlayerReconnectData reconnectData = new PlayerReconnectData
        {
            PlayerId = playerId,
            PlayerObject = playerObject,
            PreviousPlayerRef = player,
            SpawnIndex = spawnIndex,
            DisconnectTime = Time.time
        };

        disconnectedPlayers[playerId] = reconnectData;

        // Detener el procesamiento del jugador.
        playerComponent.IsDisconnected = true;

        // Retirar la autoridad del cliente anterior.
        playerObject.AssignInputAuthority(PlayerRef.None);

        Debug.Log($"Jugador reservado: {playerId}");
    }

    private void ReleaseDisconnectedPlayer(NetworkRunner runner, string playerId)
    {
        if (!disconnectedPlayers.TryGetValue(playerId, out PlayerReconnectData data))
            return;

        if (data.PlayerObject != null)
        {
            runner.Despawn(data.PlayerObject);
        }

        disconnectedPlayers.Remove(playerId);

        Debug.Log($"Reserva eliminada: {playerId}");
    }

    // Advertencias de OnConnectedToServer y OnDisconnectedFromServer silenciadas
#pragma warning disable UNT0006 // Incorrect message signature
    public void OnConnectedToServer(NetworkRunner runner)
    {
        if (this.runner != runner)
            return;

        Debug.Log("[NetworkManager] Conectado al servidor.");

        SetConnectionState(NetworkConnectionState.Connected);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
#pragma warning restore UNT0006 // Incorrect message signature
    {
        if (this.runner != runner)
            return;

        Debug.LogWarning($"[NetworkManager] Se perdió la conexión. Motivo: {reason}");

        SetConnectionState(NetworkConnectionState.Disconnected);

        OnDisconnectedFromGame?.Invoke();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (this.runner != runner)
            return;

        Debug.LogError($"[NetworkManager] Error de conexión: {reason}");
    }

    public async void ReturnToMainMenu()
    {
        if (runner != null)
        {
            await RecreateRunner();
        }

        currentSessionName = null;

        isReconnecting = false;
        isStartingGame = false;

        SetConnectionState(NetworkConnectionState.Disconnected);

        SceneManager.LoadScene(0);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // Ignorar callbacks de runners que ya no son el actual.
        if (this.runner != runner)
            return;

        Debug.Log($"[NetworkManager] Runner apagado. Motivo: {shutdownReason}");

        if (ConnectionState != NetworkConnectionState.Failed && ConnectionState != NetworkConnectionState.Reconnecting)
        {
            SetConnectionState(NetworkConnectionState.Disconnected);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        Vector2 direction = inputSystemActions.Gameplay.Move.ReadValue<Vector2>();

        NetworkInputData data = new NetworkInputData
        {
            Direction = direction
        };

        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed)
        {
            data.Buttons.Set(NetworkInputData.DashButton, true);
        }

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {

    }

    public void LoadGameScene(int sceneIndex)
    {
        if (runner == null)
        {
            return;
        }

        if (!runner.IsSceneAuthority)
        {
            return;
        }

        SceneRef scene = SceneRef.FromIndex(sceneIndex);
        runner.LoadScene(scene, LoadSceneMode.Single);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {

    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == 0)
        {
            safeSpawnManager = null;

            NetworkMenu menu = FindFirstObjectByType<NetworkMenu>();

            if (menu != null)
            {
                netMenu = menu;
                menu.SetActiveRunner(runner);
                menu.ShowTeamSelectionScreen();
            }

            MainMenu teamSelectPanel = FindFirstObjectByType<MainMenu>();

            if (teamSelectPanel != null)
            {
                teamSelectPanel.ResetLocalSelection();
            }

            if (runner.IsServer)
            {
                foreach (PlayerRef playerRef in runner.ActivePlayers)
                {
                    NetworkObject playerObject = runner.GetPlayerObject(playerRef);

                    if (playerObject == null)
                    {
                        playerObject = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, playerRef);

                        runner.SetPlayerObject(playerRef, playerObject);
                    }

                    Player playerComponent = playerObject.GetComponent<Player>();

                    if (playerComponent != null)
                    {
                        playerComponent.Team = 0;
                        playerComponent.IsAlive = true;
                    }
                }
            }

            return;
        }

        safeSpawnManager = FindFirstObjectByType<SafeSpawnManager>();

        if (safeSpawnManager == null)
        {
            Debug.LogError("SafeSpawnManager no encontrado.");
            return;
        }

        if (!runner.IsServer)
            return;

        if (safeSpawnManager.SpawnCount < 4)
        {
            Debug.LogError("No hay suficientes puntos de aparición.");
            return;
        }

        TeamManager teamManager = TeamManager.Instance;

        if (teamManager == null)
        {
            return;
        }

        int spawnIndex = 0;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (spawnIndex >= safeSpawnManager.SpawnCount)
            {
                Debug.LogWarning("No quedan spawn points disponibles.");
                break;
            }

            if (!safeSpawnManager.TryGetSpawnTransform(
                spawnIndex,
                out Transform spawnPoint))
            {
                Debug.LogWarning($"Spawn inválido en índice {spawnIndex}.");
                spawnIndex++;
                continue;
            }

            NetworkObject playerObject = runner.GetPlayerObject(player);

            if (playerObject == null)
            {
                playerObject = runner.Spawn(playerPrefab, spawnPoint.position, spawnPoint.rotation, player);

                runner.SetPlayerObject(player, playerObject);
            }

            playerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            byte team = (byte)teamManager.GetPlayerTeam(player);

            Player playerComponent = playerObject.GetComponent<Player>();

            if (playerComponent != null)
            {
                playerComponent.Team = team;
            }

            if (connectedPlayers.TryGetValue(player, out string playerId))
            {
                playerSpawnAssignments[playerId] = spawnIndex;

                Debug.Log($"Spawn asignado | Jugador: {playerId} | Índice: {spawnIndex}");
            }

            spawnIndex++;
        }
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj,PlayerRef player)
    {

    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {

    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {

    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {

    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        
    }

    private string GetPlayerId(NetworkRunner runner, PlayerRef player)
    {
        byte[] token = runner.GetPlayerConnectionToken(player);

        if (token == null || token.Length == 0)
            return null;

        return Convert.ToBase64String(token);
    }

    private void ReconnectPlayer(NetworkRunner runner, PlayerRef newPlayer, PlayerReconnectData reconnectData)
    {
        NetworkObject playerObject = reconnectData.PlayerObject;

        if (playerObject == null)
        {
            Debug.LogWarning("[Reconnect] El objeto reservado no existe. Creando uno nuevo.");

            disconnectedPlayers.Remove(reconnectData.PlayerId);

            SpawnPlayer(runner, newPlayer);

            return;
        }

        Player playerComponent = playerObject.GetComponent<Player>();

        if (playerComponent == null)
        {
            Debug.LogError("[Reconnect] El objeto no contiene Player.");
            return;
        }

        // Recuperar el equipo antes de actualizar las referencias.
        byte previousTeam = playerComponent.Team;

        // Recuperar posición en la escena actual.
        int currentScene = SceneManager.GetActiveScene().buildIndex;

        if (currentScene != 0)
        {
            if (safeSpawnManager == null)
                safeSpawnManager = FindFirstObjectByType<SafeSpawnManager>();

            if (safeSpawnManager != null && safeSpawnManager.TryGetSpawnTransform(reconnectData.SpawnIndex, out Transform spawnPoint))
            {
                playerObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

                Debug.Log($"[Reconnect] Spawn recuperado: {reconnectData.SpawnIndex}");
            }
            else
            {
                Debug.LogWarning($"[Reconnect] No se encontró el spawn {reconnectData.SpawnIndex}. " + "Se conserva la posición anterior.");
            }
        }

        // Recuperar el estado del personaje.
        playerComponent.IsDisconnected = false;

        // Recuperar autoridad de entrada.
        playerObject.AssignInputAuthority(newPlayer);

        // Asociar el personaje al nuevo PlayerRef.
        runner.SetPlayerObject(newPlayer, playerObject);

        // Actualizar referencias del sistema de equipos.
        TeamManager teamManager = TeamManager.Instance;

        if (teamManager != null)
        {
            teamManager.ReplacePlayerRef(reconnectData.PreviousPlayerRef, newPlayer);
        }

        // Recuperar identidad.
        connectedPlayers[newPlayer] = reconnectData.PlayerId;

        // Eliminar reserva.
        disconnectedPlayers.Remove(reconnectData.PlayerId);

        Debug.Log($"[Reconnect] Jugador recuperado: {reconnectData.PlayerId} | " + $"Nuevo PlayerRef: {newPlayer} | " + $"Equipo: {previousTeam}");
    }

    private async Task RecreateRunner()
    {
        NetworkRunner oldRunner = runner;

        if (oldRunner != null)
        {
            // Desvincular el Runner anterior.
            runner = null;

            if (oldRunner.IsRunning)
            {
                Debug.Log("[NetworkManager] Apagando Runner anterior.");
                await oldRunner.Shutdown();
            }

            Destroy(oldRunner);

            // Esperar al siguiente ciclo de Unity.
            await Task.Yield();
        }

        // Crear un Runner nuevo.
        runner = gameObject.AddComponent<NetworkRunner>();
        runner.AddCallbacks(this);

        Debug.Log("[NetworkManager] Nuevo NetworkRunner preparado.");
    }

    public async void ReconnectToGame()
    {
        if (isReconnecting || isStartingGame)
            return;

        if (string.IsNullOrEmpty(currentSessionName))
        {
            Debug.LogError("[NetworkManager] No existe una sesión guardada para reconectar.");

            OnConnectionFailed?.Invoke();
            return;
        }

        isReconnecting = true;

        SetConnectionState(NetworkConnectionState.Reconnecting);

        Debug.Log($"[NetworkManager] Reconectando a la sesión: {currentSessionName}");

        // Reemplazar el Runner anterior.
        await RecreateRunner();

        // Entrar nuevamente como cliente.
        await StartGame(GameMode.Client, currentSessionName);
    }

    private void ClearReconnectData()
    {
        connectedPlayers.Clear();
        disconnectedPlayers.Clear();
        playerSpawnAssignments.Clear();
    }
}