using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在指定位置生成持续领域 — 配合 BattleZoneManager 结算进入/回合开始伤害。
/// </summary>
[System.Serializable]
public class SpawnZoneAbilityEffect : AbilityEffect
{
    [Tooltip("每跳伤害 = scaler × 施法者攻击力")]
    public float scaler = 1.25f;

    public GameplayTag damageType = new GameplayTag("DamageType.Divine");

    [Tooltip("持续回合数（施法者回合结束递减）")]
    public int durationTurns = 2;

    [Tooltip("0 = 使用 GA 的 areaRadiusMeters")]
    public float radiusMetersOverride;

    public GameplayTag zoneTag = new GameplayTag("Zone.HolyField");

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (caster == null || sourceAbility == null)
            return;

        if (!TryResolveCenter(caster, context, out Vector3 center))
            return;

        float radius = radiusMetersOverride > 0f
            ? radiusMetersOverride
            : sourceAbility.GetAreaRadiusMeters();

        BattleZoneManager.Instance.SpawnZone(
            caster,
            center,
            radius,
            durationTurns,
            scaler,
            damageType,
            zoneTag);
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        // 领域生成走三参数 Execute(caster, sourceAbility, context)；此处不会被调用。
    }

    private static bool TryResolveCenter(
        AbilitySystemComponent caster,
        AbilityActivationContext context,
        out Vector3 center)
    {
        center = default;

        if (context.HasTargetPoint)
        {
            center = context.targetWorldPoint;
            return true;
        }

#pragma warning disable 618
        if (context.HasTargetCell && BattleGrid.Instance != null)
        {
            center = BattleGrid.Instance.CellToWorld(context.targetCell);
            return true;
        }
#pragma warning restore 618

        if (caster != null)
        {
            center = caster.transform.position;
            return true;
        }

        return false;
    }
}
