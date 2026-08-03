using System.Collections.Generic;
using UnityEngine;

/// <summary>拉取落点参照。</summary>
public enum PullDestinationMode
{
    /// <summary>拉到施法者面前（重力魔法）。</summary>
    CasterFront = 0,
    /// <summary>拉到点地中心周围（黑洞等）。</summary>
    TargetPoint = 1
}

/// <summary>
/// 重力拉取 — 范围内敌人拉向落点；二次缓动。
/// CasterFront=面前；TargetPoint=Area 点击点（小型黑洞）。
/// </summary>
[System.Serializable]
public class PullAbilityEffect : AbilityEffect
{
    [Tooltip("CasterFront=拉到身前；TargetPoint=拉到点地/黑洞中心")]
    public PullDestinationMode destinationMode = PullDestinationMode.CasterFront;

    [Tooltip("落点距中心/身前的距离（米）；黑洞可填 0.6~1.2")]
    public float landingDistanceMeters = 1.6f;

    [Tooltip("多名敌人横向基础间距（米）")]
    public float landingSpacingMeters = 1.4f;

    [Tooltip("拉取持续时间（秒）")]
    public float durationSeconds = 0.55f;

    [Tooltip("勾选则额外施加 Debuff.Stun；不勾选则仅拉取过程播眩晕表现，落地回 Idle")]
    public bool applyStunTag = false;

    [Tooltip("applyStunTag 时眩晕持续回合数")]
    public int stunDurationTurns = 1;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null) return;

        var targets = ResolveTargets(caster, sourceAbility, context);
        if (targets == null || targets.Count == 0) return;

        if (!TryResolvePullFrame(caster, context, out Vector3 origin, out Vector3 aim, out Vector3 right))
            return;

        var pullable = new List<AbilitySystemComponent>();
        var ignoreOccupancy = new HashSet<CharacterMovementController>();

        var casterMove = caster.GetComponent<CharacterMovementController>();
        if (casterMove != null)
            ignoreOccupancy.Add(casterMove);

        foreach (var t in targets)
        {
            if (t == null || t == caster) continue;
            if (!BattleTargeting.IsAlive(t)) continue;
            if (caster != null && !caster.IsEnemy(t)) continue;
            if (HyperArmor.IsActive(t)) continue;

            pullable.Add(t);
            var move = t.GetComponent<CharacterMovementController>();
            if (move != null)
                ignoreOccupancy.Add(move);
        }

        if (pullable.Count == 0) return;

        // 远的先排槽，落点预先占位，避免多人抢同一点
        pullable.Sort((a, b) =>
        {
            float da = BattleOccupancy.HorizontalDistance(origin, a.transform.position);
            float db = BattleOccupancy.HorizontalDistance(origin, b.transform.position);
            return db.CompareTo(da);
        });

        var reservedLandings = new List<Vector3>();
        int count = pullable.Count;
        float spacing = Mathf.Max(1.2f, landingSpacingMeters);

        for (int i = 0; i < count; i++)
        {
            var target = pullable[i];
            var movement = target.GetComponent<CharacterMovementController>();
            if (movement == null) continue;

            Vector3 ideal = BuildIdealLanding(origin, aim, right, target, i, count, spacing);
            Vector3 landing = ResolveFreeLanding(
                ideal, aim, right, movement.PersonalSpaceRadius, ignoreOccupancy, reservedLandings);

            reservedLandings.Add(landing);

            if (applyStunTag)
                ApplyStun(caster, target);

            PlayTargetVfx(caster, target);
            movement.TryApplyPullToPoint(landing, durationSeconds);
        }
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets) { }

    private bool TryResolvePullFrame(
        AbilitySystemComponent caster,
        AbilityActivationContext context,
        out Vector3 origin,
        out Vector3 aim,
        out Vector3 right)
    {
        origin = default;
        aim = Vector3.forward;
        right = Vector3.right;

        if (destinationMode == PullDestinationMode.TargetPoint && context.HasTargetPoint)
        {
            origin = context.targetWorldPoint;
            origin.y = 0f;

            // 排布轴向：优先用施法者→黑洞方向；否则用世界前方
            if (caster != null)
            {
                aim = origin - caster.transform.position;
                aim.y = 0f;
            }

            if (aim.sqrMagnitude < 0.0001f)
                aim = Vector3.forward;
            else
                aim.Normalize();
        }
        else
        {
            if (caster == null) return false;
            origin = caster.transform.position;
            aim = DirectedRectUtility.ResolveAimDirection(context, origin);
        }

        right = Vector3.Cross(Vector3.up, aim).normalized;
        return true;
    }

    private Vector3 BuildIdealLanding(
        Vector3 origin,
        Vector3 aim,
        Vector3 right,
        AbilitySystemComponent target,
        int index,
        int count,
        float spacing)
    {
        if (destinationMode == PullDestinationMode.TargetPoint)
        {
            // 环绕黑洞中心：保留每人径向来向，避免全挤成一点
            Vector3 from = target.transform.position - origin;
            from.y = 0f;
            Vector3 radial = from.sqrMagnitude > 0.0001f ? from.normalized : aim;
            Vector3 side = Vector3.Cross(Vector3.up, radial).normalized;
            float lateralIndex = index - (count - 1) * 0.5f;
            return origin + radial * landingDistanceMeters + side * (lateralIndex * spacing * 0.35f);
        }

        float lateral = index - (count - 1) * 0.5f;
        return origin + aim * landingDistanceMeters + right * (lateral * spacing);
    }

    private void ApplyStun(AbilitySystemComponent caster, AbilitySystemComponent target)
    {
        if (target?.Attributes == null || stunDurationTurns <= 0) return;
        if (HyperArmor.IsActive(target)) return;

        var stunTag = GameplayTag.Debuff.Stun;
        int duration = Mathf.Max(1, stunDurationTurns);

        target.Attributes.AddModifier(new AttributeModifier(
            "Status",
            0f,
            ModifierOperation.Additive,
            stunTag,
            duration));

        caster?.ApplyBuffTo(target, stunTag, caster);
    }

    private static Vector3 ResolveFreeLanding(
        Vector3 ideal,
        Vector3 aim,
        Vector3 right,
        float personalRadius,
        HashSet<CharacterMovementController> ignoreSet,
        List<Vector3> reserved)
    {
        float clearance = Mathf.Max(0.5f, personalRadius);

        if (TryAcceptLanding(ideal, clearance, ignoreSet, reserved, out Vector3 accepted))
            return accepted;

        for (int ring = 1; ring <= 6; ring++)
        {
            float dist = ring * clearance * 1.15f;
            Vector3[] candidates =
            {
                ideal + right * dist,
                ideal - right * dist,
                ideal + aim * dist,
                ideal - aim * (dist * 0.5f),
                ideal + right * dist + aim * (dist * 0.5f),
                ideal - right * dist + aim * (dist * 0.5f),
            };

            foreach (var c in candidates)
            {
                if (TryAcceptLanding(c, clearance, ignoreSet, reserved, out accepted))
                    return accepted;
            }
        }

        return NavPathMovementPlanner.TrySampleNavMesh(ideal, out Vector3 fallback)
            ? fallback
            : ideal;
    }

    private static bool TryAcceptLanding(
        Vector3 candidate,
        float clearance,
        HashSet<CharacterMovementController> ignoreSet,
        List<Vector3> reserved,
        out Vector3 accepted)
    {
        accepted = candidate;

        if (!NavPathMovementPlanner.TrySampleNavMesh(candidate, out Vector3 snapped))
            return false;

        if (!BattleOccupancy.IsPositionFree(snapped, clearance, ignoreSet))
            return false;

        for (int i = 0; i < reserved.Count; i++)
        {
            if (BattleOccupancy.HorizontalDistance(snapped, reserved[i]) < clearance * 2f)
                return false;
        }

        accepted = snapped;
        return true;
    }
}
