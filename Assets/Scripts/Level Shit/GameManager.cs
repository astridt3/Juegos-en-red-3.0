using Fusion;

public class GameManager : NetworkBehaviour
{
    public static event System.Action<byte> OnMatchEnded;

    [Networked]public byte WinningTeam { get; set; }

    private bool matchStarted;
    private bool networkReady;
    private byte lastDisplayedWinner;

    public override void Spawned()
    {
        networkReady = true;

        if (Object.HasStateAuthority)
        {
            WinningTeam = 0;
            matchStarted = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (WinningTeam != 0)
            return;

        if (!AreAllPlayersReady())
            return;

        matchStarted = true;
        CheckWinner();
    }

    private void Update()
    {
        if (!networkReady)
            return;

        if (WinningTeam == 0)
            return;

        if (lastDisplayedWinner == WinningTeam)
            return;

        lastDisplayedWinner = WinningTeam;

        OnMatchEnded?.Invoke(WinningTeam);
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

            if (player == null)
                return false;

            if (player.Team == 0)
                return false;
        }
        return foundPlayer;
    }

    public void PlayerReachedFinish(Player player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (WinningTeam != 0)
            return;

        if (player == null)
            return;

        if (player.Team == 1)
        {
            WinningTeam = 1;
        }
        else if (player.Team == 2)
        {
            WinningTeam = 2;
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

            if (player == null)
                continue;

            if (!player.IsAlive)
                continue;

            if (player.Team == 1)
            {
                team1Alive = true;
            }
            else if (player.Team == 2)
            {
                team2Alive = true;
            }
        }

        if (!team1Alive && team2Alive)
        {
            WinningTeam = 2;
        }
        else if (team1Alive && !team2Alive)
        {
            WinningTeam = 1;
        }
        else if (!team1Alive && !team2Alive)
        {
            WinningTeam = 3;
        }
    }

    private void RestartLevel(int sceneIndex)
    {
        Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RestartLevel1()
    {
        RestartLevel(1);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RestartLevel2()
    {
        RestartLevel(2);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RestartLevel3()
    {
        RestartLevel(3);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ReturnToTeamSelection()
    {
        TeamManager teamManager = TeamManager.Instance;

        if (teamManager != null)
        {
            teamManager.ResetTeams();
        }

        Runner.LoadScene(SceneRef.FromIndex(0));
    }

    public bool HasMatchEnded()
    {
        return WinningTeam != 0;
    }

    public void ReturnToMainMenu()
    {
        NetworkManager networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager != null)
        {
            networkManager.ReturnToMainMenu();
        }
    }
}