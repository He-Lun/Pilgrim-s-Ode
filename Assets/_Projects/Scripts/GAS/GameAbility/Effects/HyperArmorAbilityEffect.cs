using System.Collections.Generic;
using UnityEngine;

public enum HyperArmorAreaShape
{
    Circle,
    DirectedRect,
    FromAbility
}

public enum AreaAffiliationFilter
{
    All,
    AlliesOnly,
    EnemiesOnly
}

/// <summary>
/// 区域内施加霸体 — 受击仍扣血，但不进受击/眩晕表现。
/// </summary>
[System.Serializable]
public class HyperArmorAbilityEffect : AbilityEffect
{
    public HyperArmorAreaShape areaShape = HyperArmorAreaShape.Circle;

    public AreaAffiliationFilter affiliationFilter = AreaAffiliationFilter.AlliesOnly;

    [Tooltip("持续回合数（按目标自身回合 Tick）")]
    public int durationTurns = 2;

    [Tooltip("0 = 使用 GA 的 areaRadiusMeters（圆）或长度（矩形）")]
    public float radiusMetersOverride;

    [Tooltip("0 = 使用 GA 的 areaWidthMeters（仅 DirectedRect）")]
    public float widthMetersOverride;

    [Tooltip("技能实例 tag，如 Buff.HyperArmor.RingWave")]
    public GameplayTag buffTag = new GameplayTag("Buff.HyperArmor.RingWave");

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null)
            return;

        var targets = ResolveTargets(caster, sourceAbility, context);
        ApplyHyperArmor(caster, targets);
        PlayTargetVfx(caster, targets);
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        ApplyHyperArmor(caster, targets);
    }

    private void ApplyHyperArmor(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || string.IsNullOrEmpty(buffTag.TagName)) return;

        int duration = Mathf.Max(0, durationTurns);
        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;

            target.Attributes.AddModifier(new AttributeModifier(
                "Status",
                0f,
                ModifierOperation.Additive,
                buffTag,
                duration));

            caster?.ApplyBuffTo(target, buffTag, caster);
        }
    }

    private List<AbilitySystemComponent> ResolveTargets(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        var shape = areaShape == HyperArmorAreaShape.FromAbility
            ? ResolveShapeFromAbility(sourceAbility)
            : areaShape;

        if (shape == HyperArmorAreaShape.DirectedRect)
        {
            Vector3 origin = caster.transform.position;
            Vector3 aim = DirectedRectUtility.ResolveAimDirection(context, origin);
            float length = radiusMetersOverride > 0f
                ? radiusMetersOverride
                : sourceAbility.GetAreaRadiusMeters();
            float width = widthMetersOverride > 0f
                ? widthMetersOverride
                : sourceAbility.GetAreaWidthMeters();

            return BattleTargeting.FilterActorsInDirectedRect(
                caster, origin, aim, length, width, affiliationFilter);
        }

        if (!TryResolveCircleCenter(caster, context, out Vector3 center))
            return new List<AbilitySystemComponent>();

        float radius = radiusMetersOverride > 0f
            ? radiusMetersOverride
            : sourceAbility.GetAreaRadiusMeters();

        return BattleTargeting.FilterActorsInRadius(caster, center, radius, affiliationFilter);
    }

    private static HyperArmorAreaShape ResolveShapeFromAbility(GameplayAbility ability)
    {
        return ability.targetScope == TargetScope.DirectedRect
            ? HyperArmorAreaShape.DirectedRect
            : HyperArmorAreaShape.Circle;
    }

    private static bool TryResolveCircleCenter(
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
