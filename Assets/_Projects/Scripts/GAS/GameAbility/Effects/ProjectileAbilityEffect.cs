using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弹体效果 —— 从枪口发射一枚弹体(魔法飞弹/光剑等)，命中判定与命中时机由弹体飞行决定。
/// 真正的伤害/buff 等写进 onImpactEffects，弹体到达时才结算。
/// 建议 phase = OnHit（动画出手瞬间发射）；伤害不要再放同相位兄弟效果，改放载荷里。
/// </summary>
[System.Serializable]
public class ProjectileAbilityEffect : AbilityEffect
{
    [Header("========== 弹体 ==========")]
    [Tooltip("飞行特效预制体（可含拖尾）；运行时自动挂 AbilityProjectile")]
    public GameObject projectilePrefab;

    [Tooltip("发射点（枪口）：复用 VfxAnchor，如 NamedPoint(WeaponTip/Muzzle) 或 CasterRoot")]
    public VfxAnchor muzzleAnchor;

    [Tooltip("Homing=追踪锁定目标；Straight=直线飞行沿途命中")]
    public ProjectileMode mode = ProjectileMode.Homing;

    [Tooltip("飞行速度（米/秒）")]
    public float speedMetersPerSecond = 12f;

    [Tooltip("命中判定半径（米）")]
    public float hitRadiusMeters = 0.6f;

    [Tooltip("Straight 最大飞行距离（米）；<=0 时用技能 range")]
    public float maxRangeMeters = 0f;

    [Tooltip("存活上限（秒）安全兜底")]
    public float maxLifetimeSeconds = 5f;

    [Header("========== 命中表现 ==========")]
    [Tooltip("命中/落点特效")]
    public GameObject impactVfx;
    public float impactVfxAutoDestroy = 3f;

    [Header("========== 命中载荷（弹体到达时结算） ==========")]
    [Tooltip("弹体命中时执行的效果（伤害/buff/击退…），目标以命中单位/落点解析")]
    [SerializeReference, SubclassSelector]
    public List<AbilityEffect> onImpactEffects = new List<AbilityEffect>();

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (caster == null || projectilePrefab == null) return;

        // 发射点解析（复用角色的 VFX 挂点系统）
        Vector3 muzzlePos;
        Quaternion muzzleRot;
        var player = caster.GetComponent<AbilityVfxPlayer>();
        if (player != null)
        {
            player.TryGetAnchorWorld(muzzleAnchor, context, out muzzlePos, out muzzleRot);
        }
        else
        {
            muzzlePos = caster.transform.position + Vector3.up;
            muzzleRot = caster.transform.rotation;
        }

        var go = Object.Instantiate(projectilePrefab, muzzlePos, muzzleRot);
        var projectile = go.GetComponent<AbilityProjectile>();
        if (projectile == null)
            projectile = go.AddComponent<AbilityProjectile>();

        // 目标/方向解析
        AbilitySystemComponent homing = null;
        var explicitTargets = context.GetExplicitTargets();
        if (explicitTargets != null && explicitTargets.Count > 0)
            homing = explicitTargets[0];

        Vector3 direction = caster.transform.forward;
        bool hasPoint = false;
        Vector3 point = Vector3.zero;

        if (context.hasAimDirection)
        {
            direction = context.aimDirectionWorld;
        }
        else if (context.hasTargetPoint)
        {
            point = context.targetWorldPoint;
            hasPoint = true;
            direction = point - muzzlePos;
        }
        else if (homing != null)
        {
            direction = homing.transform.position - muzzlePos;
        }

        float range = maxRangeMeters > 0f
            ? maxRangeMeters
            : BattleTargeting.GetCastRangeMeters(sourceAbility);
        if (range <= 0f) range = 10f;

        projectile.Launch(new ProjectileSpec
        {
            caster = caster,
            sourceAbility = sourceAbility,
            mode = mode,
            homingTarget = homing,
            direction = direction,
            hasTargetPoint = hasPoint,
            targetPoint = point,
            speed = speedMetersPerSecond,
            hitRadius = hitRadiusMeters,
            maxRange = range,
            maxLifetime = maxLifetimeSeconds,
            onImpactEffects = onImpactEffects,
            impactVfx = impactVfx,
            impactVfxAutoDestroy = impactVfxAutoDestroy
        });
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        // 弹体走三参数 Execute(caster, sourceAbility, context)；此处不会被调用。
    }
}
