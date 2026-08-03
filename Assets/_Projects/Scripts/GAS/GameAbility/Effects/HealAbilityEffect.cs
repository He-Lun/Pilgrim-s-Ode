using System.Collections.Generic;
using UnityEngine;

/// <summary>治疗量计算方式。</summary>
public enum HealAmountMode
{
    /// <summary>固定数值。</summary>
    Fixed,
    /// <summary>目标当前 MaxHealth × 百分比。</summary>
    PercentOfMaxHealth,
    /// <summary>scaler × 施法者 Attack（与 DamageEffect 同构）。</summary>
    ScaledByCasterAttack
}

/// <summary>
/// 治疗效果 — 支持固定值、上限百分比、施法者攻击力缩放三种模式。
/// </summary>
[System.Serializable]
public class HealAbilityEffect : AbilityEffect
{
    public HealAmountMode healMode = HealAmountMode.Fixed;

    [Tooltip("Fixed 模式：直接治疗量")]
    public float healAmount = 10f;

    [Tooltip("PercentOfMaxHealth 模式：目标 MaxHealth 的百分比，0.2 = 20%")]
    public float healPercentOfMaxHealth = 0.2f;

    [Tooltip("ScaledByCasterAttack 模式：治疗量 = scaler × 施法者 Attack")]
    public float scaler = 1f;

    [Tooltip("勾选则无视 Debuff.HealBlock（如狂暴下的吸血/自疗）")]
    public bool bypassHealBlock = false;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;

        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;

            float amount = ResolveHealAmount(caster, target);
            if (amount <= 0f) continue;

            target.Attributes.Heal(amount, caster, target, bypassHealBlock);
        }
    }

    float ResolveHealAmount(AbilitySystemComponent caster, AbilitySystemComponent target)
    {
        switch (healMode)
        {
            case HealAmountMode.Fixed:
                return healAmount;

            case HealAmountMode.PercentOfMaxHealth:
                return target.Attributes.MaxHealth * healPercentOfMaxHealth;

            case HealAmountMode.ScaledByCasterAttack:
                if (caster?.Attributes == null) return 0f;
                return scaler * caster.Attributes.Attack;

            default:
                return healAmount;
        }
    }
}
