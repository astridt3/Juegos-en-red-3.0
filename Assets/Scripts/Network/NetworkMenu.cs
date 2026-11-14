using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkMenu : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private TMP_InputField input;

    [SerializeField] private CanvasGroup networkMenu;
    [SerializeField] private CanvasGroup selectLevels;
    [SerializeField] private CanvasGroup selectTeams;
    [SerializeField] private CanvasGroup serverCreating;
    [SerializeField] private CanvasGroup serverJoining;
    [SerializeField] private CanvasGroup quickmatchCreating;
    [SerializeField] private NetworkRunner runner;

    [Header("Selector de niveles")]
    [SerializeField] private TMP_Text waitingHostText;

    [Header("Botones de niveles")]
    [SerializeField] private Button[] levelButtons;
    [Header("Error de conexión")]
    [SerializeField] private CanvasGroup connectionError;

    [Header("Panel inicial")]
    [SerializeField] private CanvasGroup mainMenuPanel;

    public void ShowTeamSelectionScreen()
    {
        SetPanel(mainMenuPanel, false);
        Hide();

        SetPanel(serverCreating, false);
        SetPanel(serverJoining, false);
        SetPanel(quickmatchCreating, false);
        SetPanel(connectionError, false);

        ShowTeams();
    }

    public void SetActiveRunner(NetworkRunner activeRunner)
    {
        if (activeRunner != null)
            runner = activeRunner;
    }

    private void SetPanel(CanvasGroup panel, bool visible)
    {
        if (panel == null) return;
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }
    private void OnEnable()
    {
        NetworkManager.OnConnectedToGame += ConnectedToGame;
        NetworkManager.OnConnectionFailed += ConnectionFailed;
        NetworkManager.OnDisconnectedFromGame += DisconnectedFromGame;
        NetworkManager.OnDisconnectedFromGame += DisconnectedFromGame;
    }

    private void OnDisable()
    {
        NetworkManager.OnConnectedToGame -= ConnectedToGame;
        NetworkManager.OnConnectionFailed -= ConnectionFailed;
        NetworkManager.OnDisconnectedFromGame -= DisconnectedFromGame;
        NetworkManager.OnDisconnectedFromGame -= DisconnectedFromGame;
    }

    private void ConnectedToGame()
    {
        Hide();
        ShowTeams();
    }

    public void CreateGame()
    {
        if (string.IsNullOrEmpty(input.text))
        {
            Debug.LogWarning("Escribí un nombre para la sala.");
            return;
        }

        networkManager.StartGameHost(input.text);
    }

    public void JoinGame()
    {
        if (string.IsNullOrEmpty(input.text))
        {
            Debug.LogWarning("Escribí un nombre para la sala.");
            return;
        }

        networkManager.StartGameClient(input.text);
    }

    public void QuickPlay()
    {
        networkManager.QuickPlay();
        Hide();
        quickmatchCreating.alpha = 1;
        quickmatchCreating.interactable = true;
        quickmatchCreating.blocksRaycasts = true;
    }

    public void QuickPlayClose()
    {
        Show();
        quickmatchCreating.alpha = 0;
        quickmatchCreating.interactable = false;
        quickmatchCreating.blocksRaycasts = false;
    }

    private void ConnectionFailed()
    {
        SetPanel(serverCreating, false);
        SetPanel(serverJoining, false);
        SetPanel(quickmatchCreating, false);
        SetPanel(networkMenu, true);
        SetPanel(connectionError, true);
    }

    public void CloseConnectionError()
    {
        SetPanel(connectionError, false);
        SetPanel(serverCreating, false);
        SetPanel(serverJoining, false);
        SetPanel(quickmatchCreating, false);

        SetPanel(networkMenu, true);

        input.text = "";
        input.Select();
    }

    private void DisconnectedFromGame()
    {
        Debug.Log("Se perdió la conexión con la partida.");

        SetPanel(serverCreating, false);
        SetPanel(serverJoining, false);
        SetPanel(quickmatchCreating, false);

        SetPanel(connectionError, true);
        SetPanel(networkMenu, true);
    }

    private void ReconnectedToGame()
    {
        Debug.Log("Reconexión completada.");

        SetPanel(connectionError, false);
        SetPanel(networkMenu, false);
    }

    public void Reconnect()
    {
        Debug.Log("Solicitando reconexión...");

        SetPanel(connectionError, false);
        SetPanel(networkMenu, false);

        networkManager.ReconnectToGame();
    }

    public void CreatingServer()
    {
        Hide();
        serverCreating.alpha = 1;
        serverCreating.interactable = true;
        serverCreating.blocksRaycasts = true;
    }

    public void CreatingServerClose()
    {
        Show();
        serverCreating.alpha = 0;
        serverCreating.interactable = false;
        serverCreating.blocksRaycasts = false;
    }

    public void JoiningServer()
    {
        Hide();
        serverJoining.alpha = 1;
        serverJoining.interactable = true;
        serverJoining.blocksRaycasts = true;
    }

    public void JoiningServerClose()
    {
        Show();
        serverJoining.alpha = 0;
        serverJoining.interactable = false;
        serverJoining.blocksRaycasts = false;
    }

    public void Hide()
    {
        networkMenu.alpha = 0;
        networkMenu.interactable = false;
        networkMenu.blocksRaycasts = false;
    }

    public void Show()
    {
        networkMenu.alpha = 1;
        networkMenu.interactable = true;
        networkMenu.blocksRaycasts = true;
    }

    public void ShowTeams()
    {
        // Mostrar equipos
        selectTeams.alpha = 1;
        selectTeams.interactable = true;
        selectTeams.blocksRaycasts = true;

        // Ocultar niveles
        selectLevels.alpha = 0;
        selectLevels.interactable = false;
        selectLevels.blocksRaycasts = false;
    }

    public void ShowLevelSelector()
    {
        // Ocultar equipos
        selectTeams.alpha = 0;
        selectTeams.interactable = false;
        selectTeams.blocksRaycasts = false;

        // Mostrar niveles
        selectLevels.alpha = 1;
        selectLevels.interactable = true;
        selectLevels.blocksRaycasts = true;

        // Ver si soy Host
        bool isHost = runner != null && runner.IsServer;

        foreach (Button button in levelButtons)
        {
            if (button == null)
                continue;

            button.interactable = isHost;
        }

        if (waitingHostText != null)
        {
            waitingHostText.gameObject.SetActive(!isHost);
        }
    }

    public void ShowMainMenu()
    {
        SetPanel(mainMenuPanel, true);
        Hide();

        SetPanel(selectTeams, false);
        SetPanel(selectLevels, false);
        SetPanel(serverCreating, false);
        SetPanel(serverJoining, false);
        SetPanel(quickmatchCreating, false);
        SetPanel(connectionError, false);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}