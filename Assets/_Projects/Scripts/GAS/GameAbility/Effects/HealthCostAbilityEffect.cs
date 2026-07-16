using System.Collections.Generic;
using UnityEngine;

/// <summary>生命消耗计算方式（自残/献祭，非战斗伤害）。</summary>
public enum HealthCostMode
{
    Fixed,
    PercentOfMaxHealth,
    PercentOfCurrentHealth
}

/// <summary>
/// 生命消耗 — 按固定值或百分比扣除 HP；无视防御、不受禁疗影响。
/// 用于原始狂怒等「以血换 buff」技能，勿用负向 HealAbilityEffect。
/// </summary>
[System.Serializable]
public class HealthCostAbilityEffect : AbilityEffect
{
    public HealthCostMode costMode = HealthCostMode.PercentOfMaxHealth;

    [Tooltip("Fixed 模式：直接扣除量")]
    public float costAmount = 10f;

    [Tooltip("百分比模式：0.4 = 失去 40%")]
    [Range(0f, 1f)]
    public float costPercent = 0.4f;

    [Tooltip("勾选则至少保留 1 点生命，避免献祭致死")]
    public bool leaveAtLeastOneHp = true;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;

        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;

            float cost = ResolveCost(target);
            if (cost <= 0f) continue;

            target.Attributes.LoseHealth(cost, caster, leaveAtLeastOneHp);
        }
    }

    float ResolveCost(AbilitySystemComponent target)
    {
        switch (costMode)
        {
            case HealthCostMode.Fixed:
                return Mathf.Max(0f, costAmount);

            case HealthCostMode.PercentOfMaxHealth:
                return target.Attributes.MaxHealth * Mathf.Clamp01(costPercent);

            case HealthCostMode.PercentOfCurrentHealth:
                return target.Attributes.CurrentHealth * Mathf.Clamp01(costPercent);

            default:
                return 0f;
        }
    }
}
