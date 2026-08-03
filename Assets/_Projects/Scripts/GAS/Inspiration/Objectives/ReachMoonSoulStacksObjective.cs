using UnityEngine;

/// <summary>
/// 月魂层数达到指定值（同时持有，非累计获得次数）。
/// </summary>
[System.Serializable]
public class ReachMoonSoulStacksObjective : InspirationObjective
{
    [Tooltip("需要达到的月魂层数")]
    public int requiredStacks = 8;

    public override int GetProgressTarget() => requiredStacks;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        return evt.type == CombatEventType.MoonSoulChanged && evt.target == owner;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner) => 0;

    public override bool TryReadAbsoluteProgress(CombatEvent evt, AbilitySystemComponent owner, out int value, out int target)
    {
        value = 0;
        target = requiredStacks;
        if (evt.type != CombatEventType.MoonSoulChanged || evt.target != owner)
            return false;

        value = Mathf.Clamp(evt.intValue, 0, requiredStacks);
        return true;
    }

    public int ReadCurrentStacks(AbilitySystemComponent owner)
    {
        if (owner == null || !owner.HasMoonSoul)
            return 0;
        return Mathf.Clamp(owner.MoonSoul.Stacks, 0, requiredStacks);
    }
}
