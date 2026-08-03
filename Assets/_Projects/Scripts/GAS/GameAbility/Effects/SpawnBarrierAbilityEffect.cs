using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成平面屏障 — 攻击源与受击者路径穿过墙段时，受击方受到减伤。
/// </summary>
[System.Serializable]
public class SpawnBarrierAbilityEffect : AbilityEffect
{
    [Tooltip("减伤比例 0~1；0.3 = 伤害降低 30%")]
    [Range(0f, 1f)]
    public float damageReduction = 0.3f;

    [Tooltip("持续回合数（施法者回合结束递减）")]
    public int durationTurns = 2;

    [Tooltip("0 = 使用 GA 的 areaWidthMeters")]
    public float widthMetersOverride;

    [Tooltip("墙段厚度（米），用于路径贴近判定")]
    public float thicknessMeters = 0.5f;

    [Tooltip("仅对屏障施放者友方受击生效")]
    public bool protectAlliesOnly = true;

    public GameplayTag barrierTag = new GameplayTag("Zone.Barrier");

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null)
            return;

        if (!TryResolvePlacement(caster, context, out Vector3 center, out Vector3 forward))
            return;

        float width = widthMetersOverride > 0f
            ? widthMetersOverride
            : sourceAbility.GetAreaWidthMeters();

        BattleBarrierManager.Instance.SpawnBarrier(
            caster,
            center,
            forward,
            width,
            thicknessMeters,
            damageReduction,
            protectAlliesOnly,
            durationTurns,
            barrierTag);

        PlayTargetVfx(caster, ResolveTargets(caster, sourceAbility, context));
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets) { }

    private static bool TryResolvePlacement(
        AbilitySystemComponent caster,
        AbilityActivationContext context,
        out Vector3 center,
        out Vector3 forward)
    {
        center = default;
        forward = default;
        if (caster == null)
            return false;

        forward = context.hasAimDirection ? context.aimDirectionWorld : caster.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

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

        center = caster.transform.position;
        return true;
    }
}
