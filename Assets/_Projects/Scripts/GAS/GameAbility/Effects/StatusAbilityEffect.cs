using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态效果 — 修改器计时 + 状态 tag；类别特效见 BuffPresentationCatalog。
/// </summary>
[System.Serializable]
public class StatusAbilityEffect : AbilityEffect
{
    [Tooltip("状态标签，如 Debuff.Stun")]
    public GameplayTag statusTag = new GameplayTag("Debuff.Stun");

    [Tooltip("持续回合数（按目标自身回合 Tick；1=跳过其下一回合后解除）")]
    public int durationTurns = 1;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!MeetsCasterTagGates(caster)) return;

        var targets = ResolveTargets(caster, sourceAbility, context);
        if (targets == null) return;

        var applied = new List<AbilitySystemComponent>();
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (!RollChance()) continue;
            if (TryApplyStatus(caster, target))
                applied.Add(target);
        }

        PlayTargetVfx(caster, applied);
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || string.IsNullOrEmpty(statusTag.TagName)) return;

        for (int i = 0; i < targets.Count; i++)
            TryApplyStatus(caster, targets[i]);
    }

    private bool TryApplyStatus(AbilitySystemComponent caster, AbilitySystemComponent target)
    {
        if (target?.Attributes == null) return false;
        if (statusTag.Matches(GameplayTag.Debuff.Stun) && HyperArmor.IsActive(target))
            return false;

        int duration = Mathf.Max(0, durationTurns);
        target.Attributes.AddModifier(new AttributeModifier(
            "Status",
            0f,
            ModifierOperation.Additive,
            statusTag,
            duration));

        caster?.ApplyBuffTo(target, statusTag, caster);
        return true;
    }
}
