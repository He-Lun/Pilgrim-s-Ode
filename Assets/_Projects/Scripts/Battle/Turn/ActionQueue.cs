using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 行动条 — 双层结构：
///   · AV 时间轴（其他排）：按行动值稳定排序的未来出手序列；
///   · 插入栈（第一排/确认区）：终结技/追击/自爆等“行动”，深度优先结算。
/// 第一排 = 插入栈非空时的栈顶；否则为时间轴最前。
///
/// 排序规则：
///   · 批内插入：优先级降序 → 敏捷降序；
///   · 时间轴平局：稳定排序（AV 相等时保持当前条上的顺序）。
///
/// 本类只负责“排序与增删”，不涉及行动点/抽牌/事件广播（由 TurnManager 编排）。
/// </summary>
public class ActionQueue : MonoBehaviour
{
    public const int BASE_ACTION_VALUE = 10000;

    public static ActionQueue Instance { get; private set; }

    /// <summary>时间轴单元：角色实例 + 当前剩余行动值。</summary>
    private class TimelineEntry
    {
        public AbilitySystemComponent unit;
        public float currentAV;
    }

    // 时间轴：始终按 currentAV 稳定升序（[0] 为最前）
    private readonly List<TimelineEntry> timeline = new List<TimelineEntry>();

    // 插入栈：深度优先
    private readonly Stack<PendingAction> insertStack = new Stack<PendingAction>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================================================================
    //  时间轴（其他排）
    // ==================================================================

    /// <summary>入场。initialAdvancePercent 用于“战斗开始/召唤时行动提前 X%”。</summary>
    public void Register(AbilitySystemComponent unit, float initialAdvancePercent = 0f)
    {
        if (unit == null || Find(unit) != null) return;
        if (!unit.ParticipatesInActionQueue) return;

        float full = FullAV(unit);
        float av = full;
        if (initialAdvancePercent > 0f)
            av = Mathf.Max(0f, av - full * initialAdvancePercent);

        timeline.Add(new TimelineEntry { unit = unit, currentAV = av });
        StableSort();
    }

    /// <summary>退场（死亡/离场）。</summary>
    public void Unregister(AbilitySystemComponent unit)
    {
        if (unit == null) return;
        timeline.RemoveAll(e => e.unit == unit);
    }

    /// <summary>时间轴最前（AV 最小，平局按当前条序）。</summary>
    public AbilitySystemComponent PeekTimeline()
    {
        return timeline.Count > 0 ? timeline[0].unit : null;
    }

    /// <summary>弹出最前：全体减去 minAV 后移出并返回它（供“角色回合”）。</summary>
    public AbilitySystemComponent PopTimeline()
    {
        if (timeline.Count == 0) return null;

        StableSort();
        var first = timeline[0];
        float minAV = first.currentAV;

        if (minAV > 0f)
        {
            foreach (var e in timeline)
                e.currentAV -= minAV;
        }

        timeline.RemoveAt(0);
        return first.unit;
    }

    /// <summary>回合结算后把角色以满 AV 重新插回条上。</summary>
    public void Reinsert(AbilitySystemComponent unit)
    {
        if (unit == null) return;

        timeline.RemoveAll(e => e.unit == unit);
        timeline.Add(new TimelineEntry { unit = unit, currentAV = FullAV(unit) });
        StableSort();
    }

    // ==================================================================
    //  行动提前 / 延后（时间轴 AV 操作，不进插入栈）
    // ==================================================================

    /// <summary>行动提前：currentAV -= 满AV × percent（夹到 0）。percent=1 即提前 100%。</summary>
    public void AdvanceForward(AbilitySystemComponent unit, float percent)
    {
        var e = Find(unit);
        if (e == null) return;

        e.currentAV = Mathf.Max(0f, e.currentAV - FullAV(unit) * percent);
        StableSort();
    }

    /// <summary>行动延后：currentAV += 满AV × percent。</summary>
    public void DelayAction(AbilitySystemComponent unit, float percent)
    {
        var e = Find(unit);
        if (e == null) return;

        e.currentAV += FullAV(unit) * percent;
        StableSort();
    }

    /// <summary>速度(敏捷)变化时按已走进度比例重算：AV_new = AV_old × (oldAgility / newAgility)。</summary>
    public void OnAgilityChanged(AbilitySystemComponent unit, float oldAgility, float newAgility)
    {
        var e = Find(unit);
        if (e == null || newAgility <= 0f) return;

        e.currentAV *= oldAgility / newAgility;
        StableSort();
    }

    // ==================================================================
    //  插入栈（第一排：深度优先）
    // ==================================================================

    /// <summary>压入单个插入行动（连锁中新触发的反应用它）。</summary>
    public void PushInsert(PendingAction action)
    {
        insertStack.Push(action);
    }

    /// <summary>
    /// 压入同一时刻批量触发的插入行动。
    /// 内部按“优先级降序 → 敏捷降序”排出期望结算顺序，再【逆序压栈】使弹出即为正序。
    /// </summary>
    public void PushInsertBatch(List<PendingAction> batch)
    {
        if (batch == null || batch.Count == 0) return;

        var ordered = new List<PendingAction>(batch);
        ordered.Sort(CompareInsert); // ordered[0] = 最先结算
        for (int i = ordered.Count - 1; i >= 0; i--)
            insertStack.Push(ordered[i]);
    }

    /// <summary>弹出栈顶插入行动（供“角色行动”）。</summary>
    public PendingAction PopInsert()
    {
        return insertStack.Pop();
    }

    public bool HasInsert => insertStack.Count > 0;

    // 优先级降序 → 敏捷(速度)降序
    private static int CompareInsert(PendingAction a, PendingAction b)
    {
        int byPriority = ((int)b.priority).CompareTo((int)a.priority);
        if (byPriority != 0) return byPriority;

        float agiA = a.actor != null && a.actor.Attributes != null ? a.actor.Attributes.Agility : 0f;
        float agiB = b.actor != null && b.actor.Attributes != null ? b.actor.Attributes.Agility : 0f;
        return agiB.CompareTo(agiA);
    }

    // ==================================================================
    //  第一排 & UI 预览
    // ==================================================================

    /// <summary>下一个要结算的条目：插入栈非空 → Action(栈顶)；否则 → Turn(时间轴最前)。</summary>
    public NextEntry PeekNext()
    {
        if (HasInsert)
            return NextEntry.Action(insertStack.Peek());

        var u = PeekTimeline();
        return u != null ? NextEntry.Turn(u) : NextEntry.None;
    }

    /// <summary>预测未来出手序列（先插入栈按弹出序，再模拟时间轴），供行动条 UI。</summary>
    public List<AbilitySystemComponent> PreviewOrder(int count)
    {
        var result = new List<AbilitySystemComponent>(count);

        // Stack 的枚举顺序即栈顶到栈底 = 弹出顺序
        foreach (var pending in insertStack)
        {
            if (result.Count >= count) return result;
            if (pending.actor != null) result.Add(pending.actor);
        }

        // 模拟时间轴推进（副本，不改真实数据）
        var sim = timeline
            .OrderBy(e => e.currentAV)
            .Select(e => new TimelineEntry { unit = e.unit, currentAV = e.currentAV })
            .ToList();

        int guard = 0;
        int maxIter = count * 4 + 8;
        while (result.Count < count && sim.Count > 0 && guard++ < maxIter)
        {
            sim.Sort((x, y) => x.currentAV.CompareTo(y.currentAV));
            var first = sim[0];
            float minAV = first.currentAV;
            foreach (var e in sim) e.currentAV -= minAV;
            first.currentAV = FullAV(first.unit); // 行动后重置，模拟其下一次出手
            result.Add(first.unit);
        }

        return result;
    }

    // ==================================================================
    //  兼容旧接口（InspirationTaskTracker 仍在调用）
    // ==================================================================

    /// <summary>[兼容] 旧的激励插队入口，重定向到行动提前。</summary>
    public void ForceImmediateTurn(AbilitySystemComponent asc, float priorityBoost)
    {
        if (asc == null) return;
        AdvanceForward(asc, priorityBoost);
    }

    // ==================================================================
    //  辅助
    // ==================================================================

    /// <summary>清空整条行动条（战斗重置用）。</summary>
    public void Clear()
    {
        timeline.Clear();
        insertStack.Clear();
    }

    private TimelineEntry Find(AbilitySystemComponent unit)
    {
        if (unit == null) return null;
        for (int i = 0; i < timeline.Count; i++)
            if (timeline[i].unit == unit) return timeline[i];
        return null;
    }

    /// <summary>满行动值 = BASE / 敏捷（敏捷保底，避免除零）。</summary>
    private static float FullAV(AbilitySystemComponent unit)
    {
        float agi = unit != null && unit.Attributes != null ? unit.Attributes.Agility : 0f;
        agi = Mathf.Max(0.0001f, agi);
        return BASE_ACTION_VALUE / agi;
    }

    // 稳定升序：LINQ OrderBy 为稳定排序，AV 相等时保持当前相对顺序（= 当前条上的顺序）
    private void StableSort()
    {
        if (timeline.Count <= 1) return;
        var sorted = timeline.OrderBy(e => e.currentAV).ToList();
        timeline.Clear();
        timeline.AddRange(sorted);
    }
}