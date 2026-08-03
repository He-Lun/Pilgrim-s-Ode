using UnityEngine;

/// <summary>
/// 累计损失指定生命值（受到的伤害；可选计入献祭/自残消耗）。
/// </summary>
[System.Serializable]
public class DamageTakenObjective : InspirationObjective
{
    public float minDamagePerHit;
    [Tooltip("是否计入 HealthCostApplied（献祭/自残，非受击伤害）")]
    public bool includeHealthCost;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.target != owner)
            return false;

        if (evt.type == CombatEventType.DamageTaken)
            return evt.value >= minDamagePerHit;

        if (includeHealthCost && evt.type == CombatEventType.HealthCostApplied)
            return evt.value >= minDamagePerHit;

        return false;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner)
    {
        return Mathf.Max(1, Mathf.FloorToInt(evt.value));
    }
}
