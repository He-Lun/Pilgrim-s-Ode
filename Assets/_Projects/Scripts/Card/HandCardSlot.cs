/// <summary>
/// 手牌中的一张牌 — 运行时槽位，不含 UI 表现。
/// </summary>
public struct HandCardSlot
{
    public GameplayAbility ability;
    /// <summary>激励卡：可叠加，打出后消耗；满手时无法获得。</summary>
    public bool isInspiration;

    public bool IsEmpty => ability == null;

    public static HandCardSlot FromAbility(GameplayAbility ability, bool inspiration = false)
    {
        return new HandCardSlot { ability = ability, isInspiration = inspiration };
    }
}
