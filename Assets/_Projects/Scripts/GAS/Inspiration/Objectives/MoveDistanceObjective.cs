using UnityEngine;

/// <summary>
/// 累计移动指定格数
/// </summary>
[System.Serializable]
public class MoveDistanceObjective : InspirationObjective
{
    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        return evt.type == CombatEventType.CharacterMoved && IsInstigator(evt, owner);
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner)
    {
        return Mathf.Max(1, evt.intValue);
    }
}
