using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性 Buff — 修改器 + 实例 tag；持续外观见 BuffPresentationCatalog。
/// 一次性目标特效用基类 targetVfx。
/// </summary>
[System.Serializable]
public class BuffAbilityEffect : AbilityEffect
{
    public string attributeName = "Attack";
    public float multiplicativeBonus = 0.2f;
    public int durationTurns = 2;
    [Tooltip("技能实例 tag：Buff.<类别>.<技能名>；同 tag 刷新，不同 tag 可叠加")]
    public GameplayTag buffTag = new GameplayTag("Buff.AttackUp");

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;
        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;
            target.Attributes.AddModifier(new AttributeModifier(
                attributeName,
                multiplicativeBonus,
                ModifierOperation.Multiplicative,
                buffTag,
                durationTurns));
            caster?.ApplyBuffTo(target, buffTag, caster);
        }
    }
}
