using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    [Networked] public byte WinningTeam { get; set; }

    private bool matchStarted;
    private bool networkReady;
    private bool ShowPanel;

    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button teamSelectionButton;

    public override void Spawned()
    {
        networkReady = true;

        if (Object.HasStateAuthority)
        {
            WinningTeam = 0;
            matchStarted = false;
        }

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        ShowPanel = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || WinningTeam != 0)
            return;

        if (!AreAllPlayersReady())
            return;

        matchStarted = true;
        CheckWinner();
    }

    private void Update()
    {
        if (!networkReady || WinningTeam == 0 || ShowPanel)
            return;

        ShowEndGamePanel();
    }

    private bool AreAllPlayersReady()
    {
        bool foundPlayer = false;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            foundPlayer = true;

            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                return false;

            Player player = playerObject.GetComponent<Player>();

            if (player == null || player.Team == 0)
                return false;
        }

        return foundPlayer;
    }

    public void PlayerReachedFinish(Player player)
    {
        if (!Object.HasStateAuthority || WinningTeam != 0 || player == null)
            return;

        if (player.Team == 1)
        {
            WinningTeam = 1;
            Debug.Log("¡Blue Team llegó primero y ganó!");
        }
        else if (player.Team == 2)
        {
            WinningTeam = 2;
            Debug.Log("¡Red Team llegó primero y ganó!");
        }
    }

    private void CheckWinner()
    {
        bool team1Alive = false;
        bool team2Alive = false;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                continue;

            Player player = playerObject.GetComponent<Player>();

            if (player == null || !player.IsAlive)
                continue;

            if (player.Team == 1)
                team1Alive = true;
            else if (player.Team == 2)
                team2Alive = true;
        }

        if (!team1Alive && team2Alive)
        {
            WinningTeam = 2;
            Debug.Log("¡Gana el equipo 2!");
        }
        else if (team1Alive && !team2Alive)
        {
            WinningTeam = 1;
            Debug.Log("¡Gana el equipo 1!");
        }
        else if (!team1Alive && !team2Alive)
        {
            WinningTeam = 3;
            Debug.Log("¡Empate!");
        }
    }

    private void ShowEndGamePanel()
    {
        ShowPanel = true;

        if (endGamePanel == null)
            return;

        endGamePanel.SetActive(true);

        if (resultText == null)
            return;

        if (WinningTeam == 1)
            resultText.text = "¡BLUE TEAM WON!";
        else if (WinningTeam == 2)
            resultText.text = "¡RED TEAM WON!";
        else if (WinningTeam == 3)
            resultText.text = "¡DRAW!";
    }

    // Reiniciar niveles

    public void RestartLevel1()
    {
        RestartLevel1Rpc();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RestartLevel1Rpc()
    {
        RestartLevel(1);
    }

    public void RestartLevel2()
    {
        RestartLevel2Rpc();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RestartLevel2Rpc()
    {
        RestartLevel(2);
    }

    public void RestartLevel3()
    {
        RestartLevel3Rpc();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RestartLevel3Rpc()
    {
        RestartLevel(3);
    }

    private void RestartLevel(int sceneIndex)
    {
        Debug.Log($"Reiniciando nivel {sceneIndex}.");

        Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
    }

    // Selección de equipos

    public void ReturnToTeamSelection()
    {
        ReturnToTeamSelectionRpc();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void ReturnToTeamSelectionRpc()
    {
        TeamManager teamManager = TeamManager.Instance;

        if (teamManager != null)
            teamManager.ResetTeams();

        Debug.Log("Volviendo a selección de equipos.");

        Runner.LoadScene(SceneRef.FromIndex(0));
    }

    public bool HasMatchEnded()
    {
        return WinningTeam != 0;
    }

    // Menú principal

    public void ReturnToMainMenu()
    {
        NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager != null)
        {
            networkManager.ReturnToMainMenu();
        }
    }
}
