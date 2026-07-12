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

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || string.IsNullOrEmpty(statusTag.TagName)) return;

        int duration = Mathf.Max(0, durationTurns);
        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;
            if (statusTag.Matches(GameplayTag.Debuff.Stun) && HyperArmor.IsActive(target))
                continue;

            target.Attributes.AddModifier(new AttributeModifier(
                "Status",
                0f,
                ModifierOperation.Additive,
                statusTag,
                duration));

            caster?.ApplyBuffTo(target, statusTag, caster);
        }
    }
}
