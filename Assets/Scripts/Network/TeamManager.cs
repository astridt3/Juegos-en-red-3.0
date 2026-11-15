using UnityEngine;
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

    // Singleton
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

    // Network lifecycle
    public override void Spawned()
    {
        IsNetworkReady = true;
        Runner.MakeDontDestroyOnLoad(gameObject);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsNetworkReady = false;
    }

    // Team selection
    public void SelectTeam(byte team)
    {
        if (Runner == null || team < 1 || team > 2)
            return;

        if (IsTeamFull(team))
            return;

        RequestTeamRpc(team, Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestTeamRpc(byte team, PlayerRef player)
    {
        if (IsTeamFull(team))
            return;

        RemovePlayer(player);
        AssignPlayerToTeam(player, team);

        UpdatePlayerTeam(player, team);
        CheckIfTeamsAreFull();
    }

    private void AssignPlayerToTeam(PlayerRef player, byte team)
    {
        if (team == 1)
        {
            if (Team1Slot1 == PlayerRef.None)
                Team1Slot1 = player;
            else if (Team1Slot2 == PlayerRef.None)
                Team1Slot2 = player;
        }
        else
        {
            if (Team2Slot1 == PlayerRef.None)
                Team2Slot1 = player;
            else if (Team2Slot2 == PlayerRef.None)
                Team2Slot2 = player;
        }
    }

    private void UpdatePlayerTeam(PlayerRef player, byte team)
    {
        NetworkObject playerObject = Runner.GetPlayerObject(player);

        if (playerObject == null)
            return;

        Player playerComponent = playerObject.GetComponent<Player>();

        if (playerComponent == null)
            return;

        playerComponent.Team = team;
    }

    // Player removal
    public void RemoveDisconnectedPlayer(PlayerRef player)
    {
        if (Runner == null || !Runner.IsServer)
            return;

        RemovePlayer(player);
        CheckIfTeamsAreFull();
    }

    private void RemovePlayer(PlayerRef player)
    {
        if (Team1Slot1 == player)
            Team1Slot1 = PlayerRef.None;

        if (Team1Slot2 == player)
            Team1Slot2 = PlayerRef.None;

        if (Team2Slot1 == player)
            Team2Slot1 = PlayerRef.None;

        if (Team2Slot2 == player)
            Team2Slot2 = PlayerRef.None;
    }

    // Team information
    public int GetTeam1Count()
    {
        return GetTeamCount(Team1Slot1, Team1Slot2);
    }

    public int GetTeam2Count()
    {
        return GetTeamCount(Team2Slot1, Team2Slot2);
    }

    private int GetTeamCount(PlayerRef slot1, PlayerRef slot2)
    {
        int count = 0;

        if (slot1 != PlayerRef.None)
            count++;

        if (slot2 != PlayerRef.None)
            count++;

        return count;
    }

    public bool AreAllTeamsFull()
    {
        return AllTeamsFull;
    }

    public bool IsTeamFull(int team)
    {
        if (!IsNetworkReady)
            return false;

        if (team == 1)
            return IsSlotOccupied(Team1Slot1, Team1Slot2);

        if (team == 2)
            return IsSlotOccupied(Team2Slot1, Team2Slot2);

        return true;
    }

    private bool IsSlotOccupied(PlayerRef slot1, PlayerRef slot2)
    {
        return slot1 != PlayerRef.None && slot2 != PlayerRef.None;
    }

    public int GetPlayerTeam(PlayerRef player)
    {
        if (Team1Slot1 == player || Team1Slot2 == player)
            return 1;

        if (Team2Slot1 == player || Team2Slot2 == player)
            return 2;

        return 0;
    }

    // Team synchronization
    private void CheckIfTeamsAreFull()
    {
        if (Runner == null || !Runner.IsServer)
            return;

        bool team1Full = IsSlotOccupied(Team1Slot1, Team1Slot2);
        bool team2Full = IsSlotOccupied(Team2Slot1, Team2Slot2);

        AllTeamsFull = team1Full && team2Full;
    }

    // Reset
    public void ResetTeams()
    {
        if (!Object.HasStateAuthority)
            return;

        Team1Slot1 = PlayerRef.None;
        Team1Slot2 = PlayerRef.None;
        Team2Slot1 = PlayerRef.None;
        Team2Slot2 = PlayerRef.None;

        AllTeamsFull = false;

        ResetPlayers();
    }

    private void ResetPlayers()
    {
        foreach (PlayerRef playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject == null)
                continue;

            Player player = playerObject.GetComponent<Player>();

            if (player == null)
                continue;

            player.Team = 0;
            player.IsAlive = true;
        }
    }

    public void ReplacePlayerRef(PlayerRef oldPlayer, PlayerRef newPlayer)
    {
        if (!Object.HasStateAuthority)
            return;

        if (Team1Slot1 == oldPlayer)
            Team1Slot1 = newPlayer;

        if (Team1Slot2 == oldPlayer)
            Team1Slot2 = newPlayer;

        if (Team2Slot1 == oldPlayer)
            Team2Slot1 = newPlayer;

        if (Team2Slot2 == oldPlayer)
            Team2Slot2 = newPlayer;

        CheckIfTeamsAreFull();

        Debug.Log($"TeamManager actualizado: {oldPlayer} -> {newPlayer}");
    }
}
