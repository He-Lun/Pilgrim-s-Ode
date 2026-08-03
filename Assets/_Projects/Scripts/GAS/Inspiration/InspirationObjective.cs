using System;
using UnityEngine;

/// <summary>
/// 激励任务目标基类 — 与 AbilityEffect 同构的多态配置
/// </summary>
[Serializable]
public abstract class InspirationObjective
{
    public string displayName;
    public int targetCount = 1;

    public abstract bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner);
    public abstract int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner);

    /// <summary>若返回 true，value/target 为当前绝对进度（如月魂层数），而非增量。</summary>
    public virtual bool TryReadAbsoluteProgress(CombatEvent evt, AbilitySystemComponent owner, out int value, out int target)
    {
        value = 0;
        target = 0;
        return false;
    }

    public virtual int GetProgressTarget() => targetCount;

    protected static bool IsInstigator(CombatEvent evt, AbilitySystemComponent owner)
    {
        return evt.instigator != null && evt.instigator == owner;
    }
}
