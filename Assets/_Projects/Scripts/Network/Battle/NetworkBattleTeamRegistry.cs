using System.Collections.Generic;

/// <summary>
/// connectionId → teamId（Host=Team1，Client=Team0）。
/// </summary>
public static class NetworkBattleTeamRegistry
{
    private static readonly Dictionary<int, int> ConnectionTeams = new Dictionary<int, int>();

    public static void Reset()
    {
        ConnectionTeams.Clear();
    }

    public static int RegisterConnection(int connectionId)
    {
        if (ConnectionTeams.TryGetValue(connectionId, out int existing))
            return existing;

        // connection 0 = Host → Team1；其余 Client → Team0
        int team = connectionId == 0 ? 1 : 0;
        ConnectionTeams[connectionId] = team;
        return team;
    }

    public static int GetTeam(int connectionId)
    {
        return ConnectionTeams.TryGetValue(connectionId, out int team) ? team : -1;
    }
}
