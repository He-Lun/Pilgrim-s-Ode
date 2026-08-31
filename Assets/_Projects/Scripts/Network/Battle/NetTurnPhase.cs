/// <summary>
/// 网络同步用回合阶段 — 与 <see cref="TurnPhase"/> 一一对应。
/// </summary>
public enum NetTurnPhase : byte
{
    BattleStart = 0,
    TurnStart = 1,
    TurnDraw = 2,
    TurnAction = 3,
    TurnSettle = 4,
    BattleEnd = 5
}

public static class NetTurnPhaseUtility
{
    public static NetTurnPhase ToNet(TurnPhase phase) => (NetTurnPhase)(int)phase;

    public static TurnPhase ToLocal(NetTurnPhase phase) => (TurnPhase)(int)phase;
}
