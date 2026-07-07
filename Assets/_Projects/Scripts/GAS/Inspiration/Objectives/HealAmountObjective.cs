using UnityEngine;

/// <summary>
/// 累计治疗指定血量
/// </summary>
[System.Serializable]
public class HealAmountObjective : InspirationObjective
{
    public bool targetMustBeAlly = true;
    public bool includeSelf = true;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.HealApplied || !IsInstigator(evt, owner))
            return false;

        if (evt.target == null)
            return false;

        if (evt.target == owner)
            return includeSelf;

        if (targetMustBeAlly)
            return owner.IsAlly(evt.target);

        return true;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner)
    {
        return Mathf.Max(1, Mathf.FloorToInt(evt.value));
    }
}
