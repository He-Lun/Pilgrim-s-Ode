using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 禁疗 — 持续回合内目标无法被治疗；实例 tag 默认 Debuff.HealBlock，类别特效见 BuffPresentationCatalog。
/// </summary>
[System.Serializable]
public class HealBlockAbilityEffect : AbilityEffect
{
    [Tooltip("持续回合数（按目标自身回合 Tick）")]
    public int durationTurns = 2;

    [Tooltip("实例 tag，如 Debuff.HealBlock.Wound；类别为 Debuff.HealBlock")]
    public GameplayTag debuffTag = new GameplayTag("Debuff.HealBlock");

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || string.IsNullOrEmpty(debuffTag.TagName)) return;

        int duration = Mathf.Max(0, durationTurns);
        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;

            target.Attributes.AddModifier(new AttributeModifier(
                "Status",
                0f,
                ModifierOperation.Additive,
                debuffTag,
                duration));

            caster?.ApplyBuffTo(target, debuffTag, caster);
        }
    }
}
