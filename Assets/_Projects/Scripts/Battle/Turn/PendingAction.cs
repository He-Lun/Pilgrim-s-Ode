/// <summary>
/// 插入行动优先级 — 同一时刻批量触发时的第一排序关键字（越大越先结算）。
/// </summary>
public enum InsertPriority
{
    Reaction = 30,   // 其他反应（预留）
    FollowUp = 50,   // 追加攻击
    OnDeath = 100,   // 死亡自爆
}

/// <summary>
/// 一次“插入行动”（终结技/追击/自爆等）的调度数据。
/// 由触发源构造并压入 ActionQueue 的插入栈，走深度优先结算。
/// 注意：插入行动只跑技能，不触发“回合”事件（回合 vs 行动的区分由 TurnManager 处理）。
/// </summary>
public struct PendingAction
{
    /// <summary>动作归属的角色实例（自爆算它的行动）。</summary>
    public AbilitySystemComponent actor;

    /// <summary>要释放的技能定义。</summary>
    public GameplayAbility ability;

    /// <summary>释放参数（目标/方向/格子等）。</summary>
    public AbilityActivationContext context;

    /// <summary>批内第一排序关键字；第二关键字为 actor 的敏捷（速度）。</summary>
    public InsertPriority priority;

    public PendingAction(
        AbilitySystemComponent actor,
        GameplayAbility ability,
        AbilityActivationContext context,
        InsertPriority priority = InsertPriority.Reaction)
    {
        this.actor = actor;
        this.ability = ability;
        this.context = context;
        this.priority = priority;
    }
}
