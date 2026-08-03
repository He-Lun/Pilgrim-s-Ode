using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在指定位置生成持续领域 — Circle / Cross；进入伤害 + 回合开始伤害。
/// 十字为有限臂长，非无限射线。世界特效无 Animator 时用粒子长 Duration，回合结束立刻 Destroy。
/// </summary>
[System.Serializable]
public class SpawnZoneAbilityEffect : AbilityEffect
{
    [Tooltip("每跳伤害 = scaler × 施法者攻击力")]
    public float scaler = 1.25f;

    public GameplayTag damageType = new GameplayTag("DamageType.Divine");

    [Tooltip("持续回合数（施法者回合结束递减；耗尽立刻销毁世界特效）")]
    public int durationTurns = 2;

    public BattleZoneShape shape = BattleZoneShape.Circle;

    [Tooltip("EnemiesOnly / AllExceptInstigator（碰激光的友方也会受伤）/ Everyone")]
    public BattleZoneHitFilter hitFilter = BattleZoneHitFilter.EnemiesOnly;

    [Tooltip("圆形：0 = 使用 GA 的 areaRadiusMeters")]
    public float radiusMetersOverride;

    [Tooltip("十字臂半长（米）；0 = 用 GA areaRadiusMeters")]
    public float armHalfLengthMeters;

    [Tooltip("十字臂全宽（米）；0 = 用 GA areaWidthMeters")]
    public float armWidthMeters;

    [Tooltip("勾选则十字对齐世界轴；否则用施法者朝向 / 瞄准方向")]
    public bool axisAlignedCross;

    public GameplayTag zoneTag = new GameplayTag("Zone.HolyField");

    [Tooltip("可选：领域持续世界特效（优先于 BuffPresentationCatalog）。粒子 Duration 可设很大，回合结束立刻 Destroy")]
    public GameObject persistentWorldVfxPrefab;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null)
            return;

        if (!TryResolveCenter(caster, context, out Vector3 center))
            return;

        float radius = radiusMetersOverride > 0f
            ? radiusMetersOverride
            : sourceAbility.GetAreaRadiusMeters();

        float halfLen = armHalfLengthMeters > 0f
            ? armHalfLengthMeters
            : sourceAbility.GetAreaRadiusMeters();

        float width = armWidthMeters > 0f
            ? armWidthMeters
            : sourceAbility.GetAreaWidthMeters();

        Vector3 forward = ResolveForward(caster, context);

        BattleZoneManager.Instance.SpawnZone(
            caster,
            center,
            shape,
            radius,
            forward,
            halfLen,
            width,
            hitFilter,
            durationTurns,
            scaler,
            damageType,
            zoneTag,
            persistentWorldVfxPrefab,
            targetVfx != null && targetVfx.IsValid ? targetVfx : null);
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        // 领域生成走三参数 Execute(caster, sourceAbility, context)；此处不会被调用。
    }

    private Vector3 ResolveForward(AbilitySystemComponent caster, AbilityActivationContext context)
    {
        if (axisAlignedCross)
            return Vector3.forward;

        if (caster != null)
        {
            Vector3 face = caster.transform.forward;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
                return face.normalized;
        }

        return DirectedRectUtility.ResolveAimDirection(
            context,
            caster != null ? caster.transform.position : Vector3.zero);
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
