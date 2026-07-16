using UnityEngine;

/// <summary>
/// 月魂层数达到指定值（同时持有，非累计获得次数）。
/// targetCount 保持 1；requiredStacks 为门槛层数。
/// </summary>
[System.Serializable]
public class ReachMoonSoulStacksObjective : InspirationObjective
{
    [Tooltip("需要达到的月魂层数")]
    public int requiredStacks = 8;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.MoonSoulChanged || evt.target != owner)
            return false;

        return owner.HasMoonSoul;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner)
    {
        return evt.intValue >= requiredStacks ? 1 : 0;
    }
}
