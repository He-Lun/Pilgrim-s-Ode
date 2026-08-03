using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行动条上一格的类型（仿星铁）。
/// </summary>
public enum ActionBarEntryKind
{
    /// <summary>确认区 — 当前正在行动的角色（放大，靠右）。</summary>
    CurrentTurn,
    /// <summary>确认区 — 追击/追加攻击等（靠左，队首优先）。</summary>
    Insert,
    /// <summary>时间轴 — 下一次角色回合。</summary>
    TimelineTurn,
    /// <summary>时间轴 — 拉条预演。</summary>
    AdvancePreview,
    /// <summary>时间轴 — 当前行动角色下一次回合位置预演。</summary>
    NextTurnPreview
}

/// <summary>
/// 行动条 UI 单格数据。
/// </summary>
public struct ActionBarDisplayEntry
{
    public AbilitySystemComponent actor;
    public ActionBarEntryKind kind;
    public bool blink;
    public bool dimmed;
    public bool isCurrentActor;

    public bool IsValid => actor != null;
}

/// <summary>
/// 星铁式行动条快照 — 顶部横向确认区 + 下方纵向时间轴。
/// </summary>
public struct ActionBarSnapshot
{
    public List<ActionBarDisplayEntry> confirmRow;
    public List<ActionBarDisplayEntry> timeline;
}

/// <summary>
/// 从 ActionQueue + 预演上下文构建行动条。
/// 第一排：左 = 暂存/队首插入（优先结算），右 = 当前行动角色（放大）。
/// </summary>
public static class ActionBarSnapshotBuilder
{
    public static ActionBarSnapshot Build(
        ActionQueue queue,
        AbilitySystemComponent currentActor = null)
    {
        var snapshot = new ActionBarSnapshot
        {
            confirmRow = new List<ActionBarDisplayEntry>(),
            timeline = new List<ActionBarDisplayEntry>()
        };

        if (queue == null)
            return snapshot;

        foreach (var pending in queue.EnumerateConfirmRowInserts())
        {
            if (pending.actor == null)
                continue;

            snapshot.confirmRow.Add(new ActionBarDisplayEntry
            {
                actor = pending.actor,
                kind = ActionBarEntryKind.Insert,
                blink = true,
                isCurrentActor = pending.actor == currentActor
            });
        }

        if (currentActor != null)
        {
            snapshot.confirmRow.Add(new ActionBarDisplayEntry
            {
                actor = currentActor,
                kind = ActionBarEntryKind.CurrentTurn,
                blink = false,
                isCurrentActor = true
            });
        }

        var timelineRows = queue.GetTimelineSnapshot();
        for (int i = 0; i < timelineRows.Count; i++)
        {
            var row = timelineRows[i];
            if (row.unit == null)
                continue;

            snapshot.timeline.Add(new ActionBarDisplayEntry
            {
                actor = row.unit,
                kind = ActionBarEntryKind.TimelineTurn,
                blink = false,
                dimmed = IsAdvancePreviewTarget(row.unit),
                isCurrentActor = row.unit == currentActor
            });
        }

        if (ActionBarPreviewContext.HasAdvancePreview)
        {
            var target = ActionBarPreviewContext.AdvanceTarget;
            snapshot.timeline.Add(new ActionBarDisplayEntry
            {
                actor = target,
                kind = ActionBarEntryKind.AdvancePreview,
                blink = true,
                dimmed = false,
                isCurrentActor = target == currentActor
            });
        }
        else if (currentActor != null && !queue.IsOnTimeline(currentActor))
        {
            snapshot.timeline.Add(new ActionBarDisplayEntry
            {
                actor = currentActor,
                kind = ActionBarEntryKind.NextTurnPreview,
                blink = true,
                dimmed = false,
                isCurrentActor = true
            });
        }

        if (snapshot.timeline.Count > 1)
            snapshot.timeline.Sort((a, b) => CompareTimelineOrder(a, b, queue, timelineRows));

        return snapshot;
    }

    private static int CompareTimelineOrder(
        ActionBarDisplayEntry a,
        ActionBarDisplayEntry b,
        ActionQueue queue,
        List<ActionQueue.TimelineRow> timelineRows)
    {
        float avA = GetSortAv(a, queue, timelineRows);
        float avB = GetSortAv(b, queue, timelineRows);
        return avA.CompareTo(avB);
    }

    private static float GetSortAv(
        ActionBarDisplayEntry entry,
        ActionQueue queue,
        List<ActionQueue.TimelineRow> timelineRows)
    {
        if (entry.kind == ActionBarEntryKind.AdvancePreview)
        {
            return queue.PreviewAvAfterAdvance(
                entry.actor,
                ActionBarPreviewContext.AdvancePercent);
        }

        if (entry.kind == ActionBarEntryKind.NextTurnPreview)
            return queue.PreviewNextTurnAv(entry.actor);

        for (int i = 0; i < timelineRows.Count; i++)
        {
            if (timelineRows[i].unit == entry.actor)
                return timelineRows[i].av;
        }

        return float.MaxValue;
    }

    private static bool IsAdvancePreviewTarget(AbilitySystemComponent unit)
    {
        return ActionBarPreviewContext.HasAdvancePreview
               && ActionBarPreviewContext.AdvanceTarget == unit;
    }
}
