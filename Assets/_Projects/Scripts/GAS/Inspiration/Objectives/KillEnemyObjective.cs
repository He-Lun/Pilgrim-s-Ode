using UnityEngine;

/// <summary>
/// 击杀敌人 N 次
/// </summary>
[System.Serializable]
public class KillEnemyObjective : InspirationObjective
{
    public bool killerMustBeOwner = true;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.CharacterKilled)
            return false;

        if (killerMustBeOwner && !IsInstigator(evt, owner))
            return false;

        if (evt.target == null || !owner.IsEnemy(evt.target))
            return false;

        return true;
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner) => 1;
}
