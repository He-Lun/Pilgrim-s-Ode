using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按最近一次 MoonSoulConsume 的层数追加伤害倍率。
/// 公式：(baseScaler + bonusPerStack × LastConsumedStacks) × 攻击力。
/// 须与 MoonSoulConsumeAbilityEffect 同技能，且消耗效果先执行。
/// </summary>
[System.Serializable]
public class MoonSoulScaledDamageEffect : AbilityEffect
{
    [Tooltip("基础伤害倍率（相对攻击力）")]
    public float baseScaler = 2f;

    [Tooltip("每层消耗月魂追加的倍率，1.5 = +150%")]
    public float bonusPerStack = 1.5f;

    public GameplayTag damageType = new GameplayTag("DamageType.AP");

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || caster?.Attributes == null) return;

        int stacks = caster.HasMoonSoul ? caster.MoonSoul.LastConsumedStacks : 0;
        float scaler = baseScaler + bonusPerStack * stacks;

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target?.Attributes == null) continue;
            target.Attributes.TakeDamage(scaler * caster.Attributes.Attack, damageType, caster);
        }
    }
}
