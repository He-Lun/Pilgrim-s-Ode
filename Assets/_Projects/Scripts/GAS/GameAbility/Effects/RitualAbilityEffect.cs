using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 祈福仪式 — 锁定一名友方，冻结其 Buff.* 修改器回合；类别特效见 BuffPresentationCatalog。
/// </summary>
[System.Serializable]
public class RitualAbilityEffect : AbilityEffect
{
    public GameplayTag channelTag = new GameplayTag("State.Channeling");
    public GameplayTag wardTag = new GameplayTag("Buff.BlessingWard");

    [Tooltip("施法者回合数；0 = 直到打断或取消")]
    public int durationTurns = 0;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!RollChance() || caster == null) return;

        var targets = BattleTargeting.ResolveEffectTargets(caster, sourceAbility, context, targetSelection);
        if (targets == null) return;

        foreach (var t in targets)
        {
            if (t == null || t == caster) continue;

            caster.RitualTracker.Begin(caster, t, channelTag, wardTag, durationTurns);
            return;
        }
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets) { }
}
