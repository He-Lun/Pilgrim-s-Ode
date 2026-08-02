using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 激励任务运行时追踪器 — 由 ASC 持有，无需单独挂载
/// </summary>
public class InspirationTaskTracker
{
    private InspirationTaskSO taskDef;
    private GameplayAbility inspirationAbility;
    private AbilitySystemComponent owner;

    private readonly Dictionary<InspirationObjective, int> progress = new Dictionary<InspirationObjective, int>();
    private bool isCompleted;
    private bool isSubscribed;
    private bool inspirationSpendPending;

    public InspirationTaskSO TaskDef => taskDef;
    public bool IsCompleted => isCompleted;

    public event Action<InspirationObjective, int, int> OnProgressChanged;
    public event Action OnTaskCompleted;

    public void Initialize(InspirationTaskSO task, GameplayAbility ability, AbilitySystemComponent asc)
    {
        Unsubscribe();

        taskDef = task;
        inspirationAbility = ability;
        owner = asc;
        isCompleted = false;
        inspirationSpendPending = false;
        ResetProgress();

        if (taskDef != null)
        {
            SyncMoonSoulProgress();
            CombatEventBus.Instance.OnEvent += HandleCombatEvent;
            isSubscribed = true;
        }
    }

    public void Dispose()
    {
        Unsubscribe();
        taskDef = null;
        inspirationAbility = null;
        owner = null;
        progress.Clear();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
        isSubscribed = false;
    }

    public int GetProgress(InspirationObjective objective)
    {
        if (objective is ReachMoonSoulStacksObjective moon)
            return moon.ReadCurrentStacks(owner);

        return progress.TryGetValue(objective, out int value) ? value : 0;
    }

    public float GetProgressRatio()
    {
        if (taskDef?.objectives == null || taskDef.objectives.Count == 0)
            return 0f;

        if (taskDef.requireAllObjectives)
        {
            int totalCurrent = 0;
            int totalTarget = 0;
            foreach (var obj in taskDef.objectives)
            {
                if (obj == null) continue;
                totalCurrent += GetProgress(obj);
                totalTarget += obj.GetProgressTarget();
            }

            return totalTarget > 0 ? Mathf.Clamp01((float)totalCurrent / totalTarget) : 0f;
        }

        float best = 0f;
        foreach (var obj in taskDef.objectives)
        {
            if (obj == null) continue;
            int target = obj.GetProgressTarget();
            if (target <= 0) continue;
            best = Mathf.Max(best, (float)GetProgress(obj) / target);
        }

        return Mathf.Clamp01(best);
    }

    public void ResetProgress()
    {
        progress.Clear();
        if (taskDef?.objectives == null) return;

        foreach (var obj in taskDef.objectives)
        {
            if (obj != null)
                progress[obj] = 0;
        }
    }

    private void SyncMoonSoulProgress()
    {
        if (owner == null || taskDef?.objectives == null) return;

        foreach (var objective in taskDef.objectives)
        {
            if (objective is not ReachMoonSoulStacksObjective moon)
                continue;

            int current = moon.ReadCurrentStacks(owner);
            progress[objective] = current;
        }
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (taskDef == null || owner == null || isCompleted && !taskDef.repeatable)
            return;

        if (TryConsumeInspirationSpend(evt))
            return;

        foreach (var objective in taskDef.objectives)
        {
            if (objective == null) continue;
            if (!progress.ContainsKey(objective))
                progress[objective] = 0;

            if (objective.TryReadAbsoluteProgress(evt, owner, out int absoluteValue, out int absoluteTarget))
            {
                if (inspirationSpendPending && absoluteValue > 0)
                    continue;

                if (absoluteValue == progress[objective])
                    continue;

                progress[objective] = absoluteValue;
                if (absoluteValue == 0)
                    inspirationSpendPending = false;

                OnProgressChanged?.Invoke(objective, absoluteValue, absoluteTarget);
                continue;
            }

            if (inspirationSpendPending)
                continue;

            int target = objective.GetProgressTarget();
            if (progress[objective] >= target)
                continue;

            if (!objective.MatchesEvent(evt, owner))
                continue;

            int delta = objective.GetProgressDelta(evt, owner);
            if (delta <= 0)
                continue;

            int current = Mathf.Min(progress[objective] + delta, target);
            progress[objective] = current;
            OnProgressChanged?.Invoke(objective, current, target);
        }

        if (inspirationSpendPending)
            return;

        if (IsTaskFulfilled())
            CompleteTask();
    }

    private bool TryConsumeInspirationSpend(CombatEvent evt)
    {
        if (!inspirationSpendPending
            || evt.type != CombatEventType.AbilityUsed
            || evt.instigator != owner
            || inspirationAbility == null
            || evt.ability != inspirationAbility)
            return false;

        ResetProgress();
        inspirationSpendPending = false;
        NotifyAllProgressChanged();
        return true;
    }

    private void NotifyAllProgressChanged()
    {
        if (taskDef?.objectives == null) return;

        foreach (var objective in taskDef.objectives)
        {
            if (objective == null) continue;
            OnProgressChanged?.Invoke(objective, GetProgress(objective), objective.GetProgressTarget());
        }
    }

    private bool IsTaskFulfilled()
    {
        if (taskDef?.objectives == null || taskDef.objectives.Count == 0)
            return false;

        if (taskDef.requireAllObjectives)
        {
            foreach (var obj in taskDef.objectives)
            {
                if (obj == null) continue;
                if (GetProgress(obj) < obj.GetProgressTarget())
                    return false;
            }
            return true;
        }

        foreach (var obj in taskDef.objectives)
        {
            if (obj == null) continue;
            if (GetProgress(obj) >= obj.GetProgressTarget())
                return true;
        }

        return false;
    }

    private void CompleteTask()
    {
        if (!taskDef.repeatable)
            isCompleted = true;

        TurnManager.Instance?.OnInspirationCompleted(owner, inspirationAbility, taskDef);
        OnTaskCompleted?.Invoke();
        Debug.Log($"[InspirationTask] {owner.gameObject.name} 完成激励任务: {taskDef.taskName}");

        if (taskDef.repeatable)
        {
            isCompleted = false;
            inspirationSpendPending = true;
        }
    }
}
