using System;
using System.Collections.Generic;
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

    protected static bool IsInstigator(CombatEvent evt, AbilitySystemComponent owner)
    {
        return evt.instigator != null && evt.instigator == owner;
    }
}
