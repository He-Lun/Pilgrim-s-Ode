using UnityEngine;

/// <summary>
/// 累计造成指定伤害量
/// </summary>
[System.Serializable]
public class DealDamageObjective : InspirationObjective
{
    public float minDamagePerHit;
    public GameplayTag damageTypeTag;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.DamageDealt || !IsInstigator(evt, owner))
            return false;

        if (evt.value < minDamagePerHit)
            return false;

        if (!string.IsNullOrEmpty(damageTypeTag.TagName) && !evt.tag.Matches(damageTypeTag))
            return false;

        return true;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner)
    {
        return Mathf.Max(1, Mathf.FloorToInt(evt.value));
    }
}
