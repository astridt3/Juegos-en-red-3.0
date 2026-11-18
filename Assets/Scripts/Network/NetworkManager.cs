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

    public static event Action OnHostDisconnected;

    public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Disconnected;
    public bool IsConnected => runner != null && runner.IsRunning;
    public NetworkRunner Runner => runner;

    [Header("Network components")]
    [SerializeField] private NetworkSceneManagerDefault sceneManager;
    [SerializeField] private NetworkRunner runner;

    [Header("Player")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("Input")]
    private InputSystemActions inputSystemActions;

    [Header("Spawns")]
    public Transform[] spawnPoints;

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

    private bool isReturningToMainMenu;

    private void Awake()
    {
        voiceClient = GetComponent<FusionVoiceClient>();

        if (runner == null)
        {
            Debug.LogError("[NetworkManager] No se ha asignado el NetworkRunner.");
            enabled = false;
            return;
        }

        if (voiceClient == null)
        {
            Debug.LogError("[NetworkManager] No se ha encontrado FusionVoiceClient.");
            enabled = false;
            return;
        }

        runner.ProvideInput = true;

        runner.AddCallbacks(this);
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

        _ = StartGame(GameMode.Host, sessionName);
    }

    public void StartGameClient(string sessionName)
    {

        _ = StartGame(GameMode.Client, sessionName);
    }

    public void QuickPlay()
    {

        _ = StartGame(GameMode.AutoHostOrClient);
    }

    private async Task StartGame(GameMode gameMode, string sessionName = null)
    {
        if (runner == null || isStartingGame)
            return;

        isStartingGame = true;
        isReturningToMainMenu = false;

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

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogWarning($"Jugador {player} conectado sin ConnectionToken.");
            return;
        }

        connectedPlayers[player] = playerId;

        // ¿Es un jugador que está regresando?
        if (disconnectedPlayers.TryGetValue(playerId, out PlayerReconnectData reconnectData))
        {
            float elapsedTime = Time.time - reconnectData.DisconnectTime;

            if (elapsedTime < reconnectTimeout)
            {
                ReconnectPlayer(runner, player, reconnectData);
                return;
            }

            Debug.Log($"Tiempo de reconexión agotado: {playerId}");

            ReleaseDisconnectedPlayer(runner, playerId);
        }

        Debug.Log($"[DEBUG] Creando Player para {player}");

        // Jugador nuevo: comprobar si ya tiene un objeto asociado.
        NetworkObject existingPlayer = runner.GetPlayerObject(player);

        if (existingPlayer == null)
        {
            Debug.Log($"[NetworkManager] Creando Player para {player}.");

            SpawnPlayer(runner, player);
        }
        else
        {
            Debug.LogWarning($"[NetworkManager] El jugador {player} ya tiene un Player asociado. " + "Se evita crear un duplicado.");
        }
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
#pragma warning restore UNT0006
    {
        Debug.LogError(
            $"[DEBUG DISCONNECT] Callback recibido. " +
            $"IsServer: {runner.IsServer} | " +
            $"IsRunning: {runner.IsRunning} | " +
            $"Reason: {reason}"
        );

        if (this.runner != runner)
        {
            Debug.LogWarning("[DEBUG DISCONNECT] Runner diferente al actual.");
            return;
        }

        SetConnectionState(NetworkConnectionState.Disconnected);

        OnDisconnectedFromGame?.Invoke();

        OnHostDisconnected?.Invoke();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (this.runner != runner)
            return;

        Debug.LogError($"[NetworkManager] Error de conexión: {reason}");
    }

    public async void ReturnToMainMenu()
    {
        if (isStartingGame)
            return;

        isReturningToMainMenu = true;

        await ShutdownRunner();

        if (this == null)
            return;

        currentSessionName = null;

        isReconnecting = false;
        isStartingGame = false;

        connectedPlayers.Clear();
        disconnectedPlayers.Clear();
        playerSpawnAssignments.Clear();

        SetConnectionState(NetworkConnectionState.Disconnected);

        SceneManager.LoadScene(0);
    }

    private async Task ShutdownRunner()
    {
        NetworkRunner oldRunner = runner;

        // Desvincular el Runner actual.
        runner = null;

        if (oldRunner == null)
            return;

        if (oldRunner.IsRunning)
        {
            Debug.Log("[NetworkManager] Apagando Runner anterior.");
            await oldRunner.Shutdown();
        }

        if (oldRunner != null)
        {
            Destroy(oldRunner);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (this.runner != runner)
            return;

        Debug.Log($"[NetworkManager] Runner apagado. Motivo: {shutdownReason}");

        // Comprobar si el cliente perdió al host inesperadamente.
        if (runner.IsClient &&
            ConnectionState == NetworkConnectionState.Connected &&
            !isReturningToMainMenu)
        {
            Debug.LogWarning("[NetworkManager] El host abandonó la partida.");

            OnHostDisconnected?.Invoke();
        }

        if (ConnectionState != NetworkConnectionState.Failed &&
            ConnectionState != NetworkConnectionState.Reconnecting)
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
        if (this.runner != runner)
            return;

        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == 0)
        {
            NetworkMenu menu = FindFirstObjectByType<NetworkMenu>();

            if (menu != null)
            {
                menu.SetActiveRunner(runner);
                Debug.Log("[NetworkManager] Runner asignado al nuevo NetworkMenu.");
            }

            safeSpawnManager = null;

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

        GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag("PlayerSpawn");

        if (spawnObjects.Length < 4)
        {
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
            if (spawnIndex >= spawnObjects.Length)
            {
                Debug.LogWarning("No quedan spawn points disponibles.");
                break;
            }

            NetworkObject playerObject = runner.GetPlayerObject(player);

            Transform spawnPoint = spawnObjects[spawnIndex].transform;

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

            // Registrar la identidad y su spawn asignado.
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
            Debug.LogWarning("El objeto reservado ya no existe.");

            disconnectedPlayers.Remove(reconnectData.PlayerId);

            SpawnPlayer(runner, newPlayer);
            return;
        }

        Player playerComponent = playerObject.GetComponent<Player>();

        if (playerComponent == null)
        {
            Debug.LogError("El objeto recuperado no contiene Player.");
            return;
        }

        int currentScene = SceneManager.GetActiveScene().buildIndex;

        // En escenas de partida intentamos recuperar el spawn.
        if (currentScene != 0)
        {
            if (safeSpawnManager == null)
            {
                safeSpawnManager = FindFirstObjectByType<SafeSpawnManager>();
            }

            if (safeSpawnManager != null && safeSpawnManager.TryGetSpawnPosition(reconnectData.SpawnIndex,out Vector3 spawnPosition))
            {
                playerObject.transform.position = spawnPosition;
            }
            else
            {
                Debug.LogWarning("No se encontró un spawn válido. Se conserva la posición anterior.");
            }
        }

        // Recuperar el procesamiento del jugador.
        playerComponent.IsDisconnected = false;

        // Recuperar autoridad.
        playerObject.AssignInputAuthority(newPlayer);

        // Asociar el objeto al nuevo PlayerRef.
        runner.SetPlayerObject(newPlayer, playerObject);

        // Registrar nuevamente su identidad.
        connectedPlayers[newPlayer] = reconnectData.PlayerId;

        // Eliminar la reserva.
        disconnectedPlayers.Remove(reconnectData.PlayerId);

        Debug.Log($"Jugador recuperado: {reconnectData.PlayerId} | " + $"Equipo: {playerComponent.Team} | " + $"Vivo: {playerComponent.IsAlive}");
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

        if (voiceClient != null)
        {
            runner.AddCallbacks(voiceClient);
        }

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
}