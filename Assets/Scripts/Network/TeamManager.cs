using Fusion;

public class TeamManager : NetworkBehaviour
{
    private static TeamManager instance;
    public static TeamManager Instance => instance;

    [Networked] private PlayerRef Team1Slot1 { get; set; }
    [Networked] private PlayerRef Team1Slot2 { get; set; }
    [Networked] private PlayerRef Team2Slot1 { get; set; }
    [Networked] private PlayerRef Team2Slot2 { get; set; }

    [Networked] private NetworkBool AllTeamsFull { get; set; }

    public bool IsNetworkReady { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public override void Spawned()
    {
        IsNetworkReady = true;
        Runner.MakeDontDestroyOnLoad(gameObject);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsNetworkReady = false;
    }

    public void SelectTeam(byte team)
    {
        if (Runner == null)
        {
            return;
        }

        if (team != 1 && team != 2)
        {
            return;
        }

        if (IsTeamFull(team))
        {
            return;
        }

        RequestTeamRpc(team, Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestTeamRpc(byte team, PlayerRef player)
    {
        if (IsTeamFull(team))
        {
            return;
        }

        RemovePlayer(player);

        if (team == 1)
        {
            if (Team1Slot1 == PlayerRef.None)
            {
                Team1Slot1 = player;
            }
            else if (Team1Slot2 == PlayerRef.None)
            {
                Team1Slot2 = player;
            }
        }
        else
        {
            if (Team2Slot1 == PlayerRef.None)
            {
                Team2Slot1 = player;
            }
            else if (Team2Slot2 == PlayerRef.None)
            {
                Team2Slot2 = player;
            }
        }

        NetworkObject playerObject = Runner.GetPlayerObject(player);

        if (playerObject != null)
        {
            Player playerComponent = playerObject.GetComponent<Player>();

            if (playerComponent != null)
            {
                playerComponent.Team = team;
            }
        }

        CheckIfTeamsAreFull();
    }

    public void RemoveDisconnectedPlayer(PlayerRef player)
    {
        if (Runner == null)
            return;

        if (!Runner.IsServer)
            return;

        RemovePlayer(player);
        CheckIfTeamsAreFull();
    }

    private void RemovePlayer(PlayerRef player)
    {
        if (Team1Slot1 == player)
        {
            Team1Slot1 = PlayerRef.None;
        }

        if (Team1Slot2 == player)
        {
            Team1Slot2 = PlayerRef.None;
        }

        if (Team2Slot1 == player)
        {
            Team2Slot1 = PlayerRef.None;
        }

        if (Team2Slot2 == player)
        {
            Team2Slot2 = PlayerRef.None;
        }
    }

    public int GetTeam1Count()
    {
        int cantidad = 0;

        if (Team1Slot1 != PlayerRef.None)
        {
            cantidad++;
        }

        if (Team1Slot2 != PlayerRef.None)
        {
            cantidad++;
        }

        return cantidad;
    }

    public int GetTeam2Count()
    {
        int cantidad = 0;

        if (Team2Slot1 != PlayerRef.None)
        {
            cantidad++;
        }

        if (Team2Slot2 != PlayerRef.None)
        {
            cantidad++;
        }

        return cantidad;
    }

    public bool AreAllTeamsFull()
    {
        return AllTeamsFull;
    }

    private void CheckIfTeamsAreFull()
    {
        if (Runner == null)
            return;

        if (!Runner.IsServer)
            return;

        bool team1Full = Team1Slot1 != PlayerRef.None && Team1Slot2 != PlayerRef.None;
        bool team2Full = Team2Slot1 != PlayerRef.None && Team2Slot2 != PlayerRef.None;

        AllTeamsFull = team1Full && team2Full;
    }

    public bool IsTeamFull(int team)
    {
        if (!IsNetworkReady)
            return false;

        if (team == 1)
        {
            return Team1Slot1 != PlayerRef.None && Team1Slot2 != PlayerRef.None;
        }

        if (team == 2)
        {
            return Team2Slot1 != PlayerRef.None && Team2Slot2 != PlayerRef.None;
        }

        return true;
    }

    public int GetPlayerTeam(PlayerRef player)
    {
        if (Team1Slot1 == player || Team1Slot2 == player)
        {
            return 1;
        }

        if (Team2Slot1 == player || Team2Slot2 == player)
        {
            return 2;
        }

        return 0;
    }

    public void ResetTeams()
    {
        if (!Object.HasStateAuthority)
            return;

        Team1Slot1 = PlayerRef.None;
        Team1Slot2 = PlayerRef.None;
        Team2Slot1 = PlayerRef.None;
        Team2Slot2 = PlayerRef.None;
        AllTeamsFull = false;

        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);
            if (playerObject == null) continue;

            Player player = playerObject.GetComponent<Player>();
            if (player == null) continue;

            player.Team = 0;
            player.IsAlive = true;
        }
    }
}