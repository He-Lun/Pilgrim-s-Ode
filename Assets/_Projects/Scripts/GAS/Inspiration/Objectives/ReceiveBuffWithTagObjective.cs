using UnityEngine;

/// <summary>
/// 自己获得带有指定 Tag 的 Buff N 次（targetCount），不论 Buff 来源。
/// </summary>
[System.Serializable]
public class ReceiveBuffWithTagObjective : InspirationObjective
{
    public GameplayTag requiredBuffTag;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.BuffApplied)
            return false;

        if (evt.target != owner)
            return false;

        if (!string.IsNullOrEmpty(requiredBuffTag.TagName) && !evt.tag.Matches(requiredBuffTag))
            return false;

        return true;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner) => 1;
}
