using System.Collections.Generic;

/// <summary>
/// 治疗效果 — 供技能配置与示例资产使用
/// </summary>
[System.Serializable]
public class HealAbilityEffect : AbilityEffect
{
    public float healAmount = 10f;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;

        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;
            target.Attributes.Heal(healAmount, caster, target);
        }
    }
}
