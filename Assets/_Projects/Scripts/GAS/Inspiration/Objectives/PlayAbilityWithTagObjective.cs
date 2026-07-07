using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 打出带有指定标签的手牌/技能 N 次
/// </summary>
[System.Serializable]
public class PlayAbilityWithTagObjective : InspirationObjective
{
    public List<GameplayTag> requiredAbilityTags = new List<GameplayTag>();
    public bool countHandCardsOnly = true;

    public override bool MatchesEvent(CombatEvent evt, AbilitySystemComponent owner)
    {
        if (evt.type != CombatEventType.AbilityUsed || !IsInstigator(evt, owner))
            return false;

        // TODO: HandCardManager — countHandCardsOnly 时校验 evt.ability 是否在当前手牌中
        // if (countHandCardsOnly && !handCardManager.IsInHand(owner, evt.ability))
        //     return false;

        return AbilityHasAnyTag(evt.ability, requiredAbilityTags);
    }

    public override int GetProgressDelta(CombatEvent evt, AbilitySystemComponent owner) => 1;

    private static bool AbilityHasAnyTag(GameplayAbility ability, List<GameplayTag> tags)
    {
        if (ability == null || tags == null || tags.Count == 0)
            return true;

        foreach (var tag in tags)
        {
            if (ability.abilityTags != null && ability.abilityTags.Contains(tag))
                return true;
        }

        return false;
    }
}
