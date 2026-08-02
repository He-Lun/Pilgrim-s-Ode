using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 行动条 — 双层结构：
///   · 第一排：双端优先队列（左入插入、右入回合、队首结算）；
///   · 第二排：AV 时间轴（每单位下一次回合位置）。
/// </summary>
public class ActionQueue : MonoBehaviour
{
    public const int BASE_ACTION_VALUE = 10000;

    public static ActionQueue Instance { get; private set; }

    public event Action Changed;

    public readonly struct TimelineRow
    {
        public readonly AbilitySystemComponent unit;
        public readonly float av;

        public TimelineRow(AbilitySystemComponent unit, float av)
        {
            this.unit = unit;
            this.av = av;
        }
    }

    private class TimelineEntry
    {
        public AbilitySystemComponent unit;
        public float currentAV;
    }

    private readonly List<TimelineEntry> timeline = new List<TimelineEntry>();
    private readonly LinkedList<PriorityQueueEntry> priorityDeque = new LinkedList<PriorityQueueEntry>();
    private readonly Queue<PendingAction> insertStaging = new Queue<PendingAction>();
    private readonly List<PendingAction> activeConfirmDisplay = new List<PendingAction>();
    private bool stagingDeferUntilTurnEnd;

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
    //  时间轴（第二排）
    // ==================================================================

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
        NotifyChanged();
    }

    public void Unregister(AbilitySystemComponent unit)
    {
        if (unit == null) return;
        timeline.RemoveAll(e => e.unit == unit);
        NotifyChanged();
    }

    public AbilitySystemComponent PeekTimeline()
    {
        return timeline.Count > 0 ? timeline[0].unit : null;
    }

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
        NotifyChanged();
        return first.unit;
    }

    public void Reinsert(AbilitySystemComponent unit)
    {
        if (unit == null) return;

        timeline.RemoveAll(e => e.unit == unit);
        timeline.Add(new TimelineEntry { unit = unit, currentAV = FullAV(unit) });
        StableSort();
        NotifyChanged();
    }

    public void AdvanceForward(AbilitySystemComponent unit, float percent)
    {
        var e = Find(unit);
        if (e == null) return;

        e.currentAV = Mathf.Max(0f, e.currentAV - FullAV(unit) * percent);
        StableSort();
        NotifyChanged();
    }

    public void DelayAction(AbilitySystemComponent unit, float percent)
    {
        var e = Find(unit);
        if (e == null) return;

        e.currentAV += FullAV(unit) * percent;
        StableSort();
        NotifyChanged();
    }

    public void OnAgilityChanged(AbilitySystemComponent unit, float oldAgility, float newAgility)
    {
        var e = Find(unit);
        if (e == null || newAgility <= 0f) return;

        e.currentAV *= oldAgility / newAgility;
        StableSort();
        NotifyChanged();
    }

    // ==================================================================
    //  第一排 — 双端优先队列
    // ==================================================================

    /// <summary>正常回合从右侧入队。</summary>
    public void EnqueueTurnBack(AbilitySystemComponent actor)
    {
        if (actor == null) return;
        priorityDeque.AddLast(PriorityQueueEntry.FromTurn(actor));
        NotifyChanged();
    }

    /// <summary>
    /// 当前回合内触发的插入 — 先进暂存区。
    /// deferUntilTurnEnd=true：等回合令牌弹出后再灌入（如本回合内批量追加）；
    /// false：下一次 NotifyActionResolved 时灌入（如友方攻击触发的追加）。
    /// </summary>
    public void StageInsert(PendingAction action, bool deferUntilTurnEnd = false)
    {
        if (action.actor == null || action.ability == null) return;
        insertStaging.Enqueue(action);
        if (deferUntilTurnEnd)
            stagingDeferUntilTurnEnd = true;
        NotifyChanged();
    }

    public bool CanFlushStagingOnActionResolved =>
        insertStaging.Count > 0 && !stagingDeferUntilTurnEnd;

    /// <summary>立即从左侧入队（回合外连锁、死亡自爆等）。</summary>
    public void EnqueueInsertFront(PendingAction action)
    {
        if (action.actor == null || action.ability == null) return;
        priorityDeque.AddFirst(PriorityQueueEntry.FromInsert(action));
        NotifyChanged();
    }

    /// <summary>批量从左侧入队（已按结算顺序排好，最先结算的在列表最前）。</summary>
    public void EnqueueInsertFrontBatch(List<PendingAction> batch)
    {
        if (batch == null || batch.Count == 0) return;

        var ordered = new List<PendingAction>(batch);
        ordered.Sort(CompareInsert);
        for (int i = ordered.Count - 1; i >= 0; i--)
            priorityDeque.AddFirst(PriorityQueueEntry.FromInsert(ordered[i]));

        NotifyChanged();
    }

    /// <summary>暂存区依次灌入队首左侧（保持 FIFO）。</summary>
    public void FlushStagingToFront()
    {
        if (insertStaging.Count == 0)
            return;

        var buffered = insertStaging.ToArray();
        insertStaging.Clear();
        stagingDeferUntilTurnEnd = false;
        for (int i = buffered.Length - 1; i >= 0; i--)
            priorityDeque.AddFirst(PriorityQueueEntry.FromInsert(buffered[i]));

        NotifyChanged();
    }

    /// <summary>弹出队首插入行动；队首为回合令牌或队列为空时返回 false。弹出后仍保留在第一排 UI 直至 CompleteFrontConfirmInsert。</summary>
    public bool TryPopFrontInsert(out PendingAction action)
    {
        action = default;
        if (priorityDeque.Count == 0 || priorityDeque.First.Value.kind != PriorityEntryKind.Insert)
            return false;

        action = priorityDeque.First.Value.pending;
        priorityDeque.RemoveFirst();
        activeConfirmDisplay.Add(action);
        NotifyChanged();
        return true;
    }

    /// <summary>队首插入行动表现结束后从第一排 UI 移除。</summary>
    public void CompleteFrontConfirmInsert()
    {
        if (activeConfirmDisplay.Count == 0)
            return;

        activeConfirmDisplay.RemoveAt(0);
        NotifyChanged();
    }

    /// <summary>若队首展示项已不再阻塞，则移除。</summary>
    public void TryCompleteActiveConfirmInsert()
    {
        if (activeConfirmDisplay.Count == 0)
            return;

        var pending = activeConfirmDisplay[0];
        if (pending.actor != null && pending.actor.AbilityBlocksTurnHandoff)
            return;

        CompleteFrontConfirmInsert();
    }

    public bool HasActiveConfirmDisplay => activeConfirmDisplay.Count > 0;

    /// <summary>移除队尾的回合令牌（角色回合结束时调用）。</summary>
    public void PopTurnToken(AbilitySystemComponent actor)
    {
        if (actor == null || priorityDeque.Count == 0)
            return;

        var node = priorityDeque.Last;
        if (node.Value.kind == PriorityEntryKind.Turn && node.Value.actor == actor)
            priorityDeque.RemoveLast();

        NotifyChanged();
    }

    public bool HasFrontInsert =>
        priorityDeque.Count > 0 && priorityDeque.First.Value.kind == PriorityEntryKind.Insert;

    public bool HasPendingWork =>
        HasFrontInsert || insertStaging.Count > 0 || HasTurnToken;

    private bool HasTurnToken
    {
        get
        {
            return priorityDeque.Count > 0
                   && priorityDeque.Last.Value.kind == PriorityEntryKind.Turn;
        }
    }

    /// <summary>第一排 UI：执行中 + 暂存区 + 队首插入段（从左到右 = 即将结算顺序）。</summary>
    public IEnumerable<PendingAction> EnumerateConfirmRowInserts()
    {
        for (int i = 0; i < activeConfirmDisplay.Count; i++)
            yield return activeConfirmDisplay[i];

        foreach (var pending in insertStaging)
            yield return pending;

        foreach (var entry in priorityDeque)
        {
            if (entry.kind != PriorityEntryKind.Insert)
                break;
            yield return entry.pending;
        }
    }

    /// <summary>兼容旧名。</summary>
    public IEnumerable<PendingAction> EnumerateInsertsTopFirst() => EnumerateConfirmRowInserts();

    // ==================================================================
    //  第一排 & UI 预览
    // ==================================================================

    public NextEntry PeekNext()
    {
        if (insertStaging.Count > 0)
            return NextEntry.Action(insertStaging.Peek());

        if (priorityDeque.Count > 0)
        {
            var front = priorityDeque.First.Value;
            if (front.kind == PriorityEntryKind.Insert)
                return NextEntry.Action(front.pending);
        }

        var u = PeekTimeline();
        return u != null ? NextEntry.Turn(u) : NextEntry.None;
    }

    public List<AbilitySystemComponent> PreviewOrder(int count)
    {
        var result = new List<AbilitySystemComponent>(count);

        foreach (var pending in EnumerateConfirmRowInserts())
        {
            if (result.Count >= count) return result;
            if (pending.actor != null) result.Add(pending.actor);
        }

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
            first.currentAV = FullAV(first.unit);
            result.Add(first.unit);
        }

        return result;
    }

    public List<TimelineRow> GetTimelineSnapshot()
    {
        StableSort();
        var rows = new List<TimelineRow>(timeline.Count);
        for (int i = 0; i < timeline.Count; i++)
        {
            var e = timeline[i];
            if (e.unit != null)
                rows.Add(new TimelineRow(e.unit, e.currentAV));
        }

        return rows;
    }

    public float PreviewAvAfterAdvance(AbilitySystemComponent unit, float percent)
    {
        var e = Find(unit);
        if (e == null || percent <= 0f)
            return float.MaxValue;

        return Mathf.Max(0f, e.currentAV - FullAV(unit) * percent);
    }

    /// <summary>预演角色回合结束 Reinsert 后的 AV（不在条上则按满 AV 计算）。</summary>
    public float PreviewNextTurnAv(AbilitySystemComponent unit)
    {
        if (unit == null)
            return float.MaxValue;

        var e = Find(unit);
        return e != null ? e.currentAV : FullAV(unit);
    }

    public bool IsOnTimeline(AbilitySystemComponent unit) => Find(unit) != null;

    private void NotifyChanged() => Changed?.Invoke();

    public void ForceImmediateTurn(AbilitySystemComponent asc, float priorityBoost)
    {
        if (asc == null) return;
        AdvanceForward(asc, priorityBoost);
    }

    public void Clear()
    {
        timeline.Clear();
        priorityDeque.Clear();
        insertStaging.Clear();
        activeConfirmDisplay.Clear();
        stagingDeferUntilTurnEnd = false;
        NotifyChanged();
    }

    private static int CompareInsert(PendingAction a, PendingAction b)
    {
        int byPriority = ((int)b.priority).CompareTo((int)a.priority);
        if (byPriority != 0) return byPriority;

        float agiA = a.actor != null && a.actor.Attributes != null ? a.actor.Attributes.Agility : 0f;
        float agiB = b.actor != null && b.actor.Attributes != null ? b.actor.Attributes.Agility : 0f;
        return agiB.CompareTo(agiA);
    }

    private TimelineEntry Find(AbilitySystemComponent unit)
    {
        if (unit == null) return null;
        for (int i = 0; i < timeline.Count; i++)
            if (timeline[i].unit == unit) return timeline[i];
        return null;
    }

    private static float FullAV(AbilitySystemComponent unit)
    {
        float agi = unit != null && unit.Attributes != null ? unit.Attributes.Agility : 0f;
        agi = Mathf.Max(0.0001f, agi);
        return BASE_ACTION_VALUE / agi;
    }

    private void StableSort()
    {
        if (timeline.Count <= 1) return;
        var sorted = timeline.OrderBy(e => e.currentAV).ToList();
        timeline.Clear();
        timeline.AddRange(sorted);
    }

    // ==================================================================
    //  兼容旧 API（TurnManager 逐步迁移后可删）
    // ==================================================================

    public void PushInsert(PendingAction action) => EnqueueInsertFront(action);

    public void PushInsertBatch(List<PendingAction> batch) => EnqueueInsertFrontBatch(batch);

    public PendingAction PopInsert()
    {
        TryPopFrontInsert(out var action);
        return action;
    }

    public bool HasInsert => HasFrontInsert;
}
