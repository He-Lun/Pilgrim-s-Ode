using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 消耗全部月魂 — 记录 LastConsumedStacks / LastConsumedPhase，供同技能后续倍率读取。
/// 建议 phase 早于依赖月相倍率的伤害，或伤害用 requiredCasterTags 读消耗前的月相 tag。
/// </summary>
[System.Serializable]
public class MoonSoulConsumeAbilityEffect : AbilityEffect
{
    [Tooltip("勾选则对施法者消耗")]
    public bool applyToCaster = true;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster)) return;

        if (applyToCaster)
        {
            Consume(caster);
            return;
        }

        var targets = ResolveTargets(caster, sourceAbility, context);
        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
            Consume(targets[i]);
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (!ShouldExecute(caster)) return;

        if (applyToCaster)
        {
            Consume(caster);
            return;
        }

        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
            Consume(targets[i]);
    }

    private static void Consume(AbilitySystemComponent asc)
    {
        var tracker = asc?.MoonSoul;
        if (tracker == null || !tracker.IsBound) return;
        tracker.ConsumeAll();
    }
}
