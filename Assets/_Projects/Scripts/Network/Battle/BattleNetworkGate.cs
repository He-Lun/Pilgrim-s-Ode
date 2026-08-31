using Mirror;

/// <summary>
/// 联机战斗权限：谁跑仿真、谁能操控、谁能看手牌。
/// </summary>
public static class BattleNetworkGate
{
    public static bool IsNetworkBattleActive => NetworkClient.active || NetworkServer.active;

    /// <summary>本机是否执行 TurnManager / 抽牌 / 伤害结算。</summary>
    public static bool IsSimulationServer => !IsNetworkBattleActive || NetworkServer.active;

    /// <summary>Host 单人测试：仅一名玩家时可操控当前行动角色。</summary>
    public static bool IsSoloHostTest =>
        NetworkServer.active && NetworkClient.active && NetworkServer.connections.Count <= 1;

    private static int? explicitLocalTeamId;

    /// <summary>本机阵营。Host=1，纯 Client=0（1v1 固定映射）。</summary>
    public static int LocalTeamId => ResolveLocalTeamId();

    public static void SetLocalTeamId(int teamId) => explicitLocalTeamId = teamId;

    public static void ClearLocalTeamId() => explicitLocalTeamId = null;

    public static int ResolveLocalTeamId()
    {
        if (!IsNetworkBattleActive)
            return -1;

        if (explicitLocalTeamId.HasValue)
            return explicitLocalTeamId.Value;

        // 1v1：Host = Team1，纯 Client = Team0
        if (NetworkServer.active && NetworkClient.active)
            return 1;

        if (NetworkClient.active && !NetworkServer.active)
            return 0;

        return -1;
    }

    public static bool CanLocalControlActor(AbilitySystemComponent asc)
    {
        if (asc == null) return false;
        if (!IsNetworkBattleActive) return true;
        if (!IsBattleSimulationLive())
            return false;

        var tm = TurnManager.Instance;
        if (tm == null || tm.CurrentActor != asc) return false;
        if (tm.Phase != TurnPhase.TurnAction) return false;

        if (IsSoloHostTest) return true;

        int localTeam = ResolveLocalTeamId();
        if (localTeam < 0) return IsSimulationServer;
        return asc.TeamId == localTeam;
    }

    /// <summary>Server 已跑起 TurnManager，或 Client 已收到开战 SyncVar。</summary>
    public static bool IsBattleSimulationLive()
    {
        var state = BattleNetworkRuntimeSpawner.ResolveState() ?? NetworkBattleState.Instance;
        if (state != null && state.battleStarted)
            return true;

        if (!IsSimulationServer)
            return false;

        var tm = TurnManager.Instance;
        return tm != null && tm.Phase != TurnPhase.BattleEnd && tm.CurrentActor != null;
    }

    /// <summary>联机时只能看本队手牌；Host 跑仿真也不能看对手底牌。</summary>
    public static bool CanLocalViewHand(AbilitySystemComponent asc)
    {
        if (asc == null)
            return false;

        if (!IsNetworkBattleActive)
            return true;

        if (IsSoloHostTest)
            return true;

        int localTeam = ResolveLocalTeamId();
        if (localTeam < 0)
            return false;

        return asc.TeamId == localTeam;
    }

    public static int ResolveTeamForConnection(int connectionId)
    {
        int registered = NetworkBattleTeamRegistry.GetTeam(connectionId);
        if (registered >= 0)
            return registered;

        // 与 RegisterConnection 一致：0=Host→Team1，其余→Team0
        return connectionId == 0 ? 1 : 0;
    }
}
