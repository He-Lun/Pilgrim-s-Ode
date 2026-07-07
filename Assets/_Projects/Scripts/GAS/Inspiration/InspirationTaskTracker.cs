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
        ResetProgress();

        if (taskDef != null)
        {
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
        return progress.TryGetValue(objective, out int value) ? value : 0;
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

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (taskDef == null || owner == null || isCompleted && !taskDef.repeatable)
            return;

        foreach (var objective in taskDef.objectives)
        {
            if (objective == null) continue;
            if (!progress.ContainsKey(objective))
                progress[objective] = 0;

            if (progress[objective] >= objective.targetCount)
                continue;

            if (!objective.MatchesEvent(evt, owner))
                continue;

            int delta = objective.GetProgressDelta(evt, owner);
            int current = Mathf.Min(progress[objective] + delta, objective.targetCount);
            progress[objective] = current;
            OnProgressChanged?.Invoke(objective, current, objective.targetCount);
        }

        if (IsTaskFulfilled())
            CompleteTask();
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
                if (!progress.TryGetValue(obj, out int val) || val < obj.targetCount)
                    return false;
            }
            return true;
        }

        foreach (var obj in taskDef.objectives)
        {
            if (obj == null) continue;
            if (progress.TryGetValue(obj, out int val) && val >= obj.targetCount)
                return true;
        }
        return false;
    }

    private void CompleteTask()
    {
        if (!taskDef.repeatable)
            isCompleted = true;

        if (inspirationAbility != null)
        {
            var targets = new List<AbilitySystemComponent> { owner };
            inspirationAbility.TryActivateAsInspiration(owner, targets);
        }

        if (owner.TeamResource != null)
            owner.TeamResource.AddActionPoints(taskDef.actionPointReward);

        if (ActionQueue.Instance != null)
            ActionQueue.Instance.ForceImmediateTurn(owner, taskDef.actionPriorityBoost);

        OnTaskCompleted?.Invoke();
        Debug.Log($"[InspirationTask] {owner.gameObject.name} 完成激励任务: {taskDef.taskName}");

        if (taskDef.repeatable)
        {
            isCompleted = false;
            ResetProgress();
        }
    }
}
