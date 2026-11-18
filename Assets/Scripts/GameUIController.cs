using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("End Game")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;

    [Header("Connection")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject reconnectingPanel;
    [SerializeField] private Button reconnectButton;

    [Header("Host disconnection")]
    [SerializeField] private GameObject hostDisconnectPanel;

    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();

        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (reconnectingPanel != null)
            reconnectingPanel.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.OnMatchEnded += ShowEndGame;

        NetworkManager.OnDisconnectedFromGame += ShowDisconnected;
        NetworkManager.OnReconnectedToGame += HideConnectionPanels;
        NetworkManager.OnHostDisconnected += ShowHostDisconnectionPanel;

        if (reconnectButton != null)
            reconnectButton.onClick.AddListener(Reconnect);
    }

    private void OnDisable()
    {
        GameManager.OnMatchEnded -= ShowEndGame;

        NetworkManager.OnDisconnectedFromGame -= ShowDisconnected;
        NetworkManager.OnReconnectedToGame -= HideConnectionPanels;
        NetworkManager.OnHostDisconnected -= ShowHostDisconnectionPanel;

        if (reconnectButton != null)
            reconnectButton.onClick.RemoveListener(Reconnect);
    }

    private void ShowEndGame(byte winningTeam)
    {
        if (endGamePanel == null)
            return;

        endGamePanel.SetActive(true);

        if (resultText == null)
            return;

        switch (winningTeam)
        {
            case 1:
                resultText.text = "¡BLUE TEAM WON!";
                break;

            case 2:
                resultText.text = "¡RED TEAM WON!";
                break;

            case 3:
                resultText.text = "¡DRAW!";
                break;
        }
    }

    private void ShowDisconnected()
    {
        if (connectionPanel != null)
            connectionPanel.SetActive(true);

        if (reconnectingPanel != null)
            reconnectingPanel.SetActive(false);
    }

    private void Reconnect()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError("NetworkManager no encontrado.");
            return;
        }

        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (reconnectingPanel != null)
            reconnectingPanel.SetActive(true);

        if (reconnectButton != null)
            reconnectButton.interactable = false;

        networkManager.ReconnectToGame();
    }

    private void HideConnectionPanels()
    {
        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (reconnectingPanel != null)
            reconnectingPanel.SetActive(false);

        if (reconnectButton != null)
            reconnectButton.interactable = true;
    }

    private void ShowHostDisconnectionPanel()
    {
        if (hostDisconnectPanel != null)
            hostDisconnectPanel.SetActive(true);
    }

    public void ReturnToMenuAfterHostDisconnect()
    {
        SceneManager.LoadScene(0);
    }
}
