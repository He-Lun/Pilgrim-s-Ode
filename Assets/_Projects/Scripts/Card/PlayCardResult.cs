/// <summary>
/// HandCardManager 出牌准备结果 — 供 Facade 接收后调用 ASC。
/// Manager 只负责校验手牌与填充 ability；不调用 ASC。
/// </summary>
public struct PlayCardResult
{
    /// <summary>手牌校验是否通过（索引合法、该位有牌等）。</summary>
    public bool isValid;

    /// <summary>对应手牌索引，便于失败回手或 UI 高亮。</summary>
    public int handIndex;

    /// <summary>要释放的技能定义。</summary>
    public GameplayAbility ability;

    /// <summary>玩家已选好的释放参数（目标、方向、格子等）。</summary>
    public AbilityActivationContext context;

    public static PlayCardResult Invalid(int handIndex = -1)
    {
        return new PlayCardResult { isValid = false, handIndex = handIndex };
    }

    public static PlayCardResult Ready(int handIndex, GameplayAbility ability, AbilityActivationContext context)
    {
        return new PlayCardResult
        {
            isValid = true,
            handIndex = handIndex,
            ability = ability,
            context = context
        };
    }
}
