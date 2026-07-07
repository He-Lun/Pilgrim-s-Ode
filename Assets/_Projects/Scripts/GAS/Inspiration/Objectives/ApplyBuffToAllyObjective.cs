using UnityEngine;

/// <summary>
/// 给友方施加指定 Buff N 次
/// </summary>
[System.Serializable]
public class ApplyBuffToAllyObjective : InspirationObjective
{
    public GameplayTag requiredBuffTag;
    public bool includeSelf;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.BuffApplied || !IsInstigator(evt, owner))
            return false;

        if (evt.target == null)
            return false;

        if (evt.target == owner)
        {
            if (!includeSelf) return false;
        }
        else if (!owner.IsAlly(evt.target))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(requiredBuffTag.TagName) && !evt.tag.Matches(requiredBuffTag))
            return false;

        return true;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner) => 1;
}
