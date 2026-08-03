/// <summary>
/// 第一排优先队列条目类型。
/// </summary>
public enum PriorityEntryKind
{
    /// <summary>追加攻击、自爆等插入行动。</summary>
    Insert,
    /// <summary>角色正常回合（从右侧入队）。</summary>
    Turn
}

/// <summary>
/// 行动条第一排 — 双端优先队列元素。
/// 插入行动从左侧入队；正常回合从右侧入队；始终从队首（左侧）结算。
/// </summary>
public struct PriorityQueueEntry
{
    public PriorityEntryKind kind;
    public AbilitySystemComponent actor;
    public PendingAction pending;

    public bool IsValid =>
        kind == PriorityEntryKind.Turn
            ? actor != null
            : pending.actor != null && pending.ability != null;

    public static PriorityQueueEntry FromInsert(PendingAction action)
    {
        return new PriorityQueueEntry
        {
            kind = PriorityEntryKind.Insert,
            actor = action.actor,
            pending = action
        };
    }

    public static PriorityQueueEntry FromTurn(AbilitySystemComponent actor)
    {
        return new PriorityQueueEntry
        {
            kind = PriorityEntryKind.Turn,
            actor = actor
        };
    }
}
