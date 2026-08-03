/// <summary>
/// 下一个即将结算的东西的类型：
/// Turn   = 角色回合（来自 AV 时间轴，含被行动提前到 0 的），走完整回合流程；
/// Action = 角色行动（来自插入栈），只跑技能、不触发回合事件。
/// </summary>
public enum NextKind
{
    None,
    Turn,
    Action
}

/// <summary>
/// “第一排”——行动条下一个要结算的条目。
/// PeekNext 返回：插入栈非空 → Action(栈顶)；否则 → Turn(时间轴最前)。
/// </summary>
public struct NextEntry
{
    public NextKind kind;
    public AbilitySystemComponent actor;

    /// <summary>kind == Action 时有效。</summary>
    public PendingAction action;

    public bool IsValid => kind != NextKind.None && actor != null;

    public static NextEntry None => new NextEntry { kind = NextKind.None };

    public static NextEntry Turn(AbilitySystemComponent actor)
    {
        return new NextEntry { kind = NextKind.Turn, actor = actor };
    }

    public static NextEntry Action(PendingAction action)
    {
        return new NextEntry { kind = NextKind.Action, actor = action.actor, action = action };
    }
}
