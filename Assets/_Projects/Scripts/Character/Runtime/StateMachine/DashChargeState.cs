using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 突进冲锋 — 蓄力（等 OnDashChargeStart）→ 平滑位移 → 等 OnAbilityComplete 收招。
/// </summary>
public class DashChargeState : ICharacterState
{
    private enum Phase
    {
        Windup,
        Moving,
        AwaitingComplete
    }

    public CharacterStateType Type => CharacterStateType.DashCharge;
    public int Priority => CharacterStatePriority.Get(Type);

    private Phase phase = Phase.Windup;
    private Vector3 direction;
    private float maxDistance;
    private float speed;
    private float traveled;
    private Vector3 segmentStart;
    private float pathHalfWidth;
    private float damageScaler;
    private GameplayTag damageType;
    private float knockbackDistance;
    private float knockbackDuration;
    private VfxSpawnEntry hitVfx;
    private CharacterMovementController movement;
    private AbilitySystemComponent caster;
    private bool grantedHyperArmor;
    private readonly HashSet<AbilitySystemComponent> hitTargets = new HashSet<AbilitySystemComponent>();

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        phase = Phase.Windup;

        var spec = payload.dashChargeSpec;
        direction = spec.direction;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = ctx.transform.forward;
        direction.Normalize();

        maxDistance = Mathf.Max(0f, spec.distanceMeters);
        speed = Mathf.Max(0.1f, spec.speedMetersPerSecond);
        pathHalfWidth = Mathf.Max(0.1f, spec.pathHalfWidthMeters);
        damageScaler = spec.damageScaler;
        damageType = spec.damageType;
        knockbackDistance = spec.knockbackDistanceMeters;
        knockbackDuration = spec.knockbackDurationSeconds;
        hitVfx = spec.hitVfx;
        traveled = 0f;
        segmentStart = ctx.transform.position;
        hitTargets.Clear();

        movement = ctx.GetComponent<CharacterMovementController>();
        caster = ctx.Asc;
        grantedHyperArmor = spec.grantCasterHyperArmor;
        if (grantedHyperArmor && caster != null)
            HyperArmor.Grant(caster, GameplayTag.Buff.HyperArmor_DashCharge);

        if (payload.ability != null)
        {
            var presentation = caster != null
                ? caster.GetPresentation(payload.ability)
                : AbilityPresentationEntry.FromAbilityDefaults(payload.ability);
            ctx.AnimatorDriver?.PlaySkill(presentation);
        }

        ctx.Facing?.FaceMoveDirection(direction);
    }

    public void Tick(CharacterMotor ctx, float dt)
    {
        switch (phase)
        {
            case Phase.Windup:
                TickWindup(ctx);
                break;
            case Phase.Moving:
                TickMoving(ctx, dt);
                break;
            case Phase.AwaitingComplete:
                break;
        }
    }

    public void Exit(CharacterMotor ctx)
    {
        if (grantedHyperArmor && caster != null)
            HyperArmor.Revoke(caster, GameplayTag.Buff.HyperArmor_DashCharge);

        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);
        movement = null;
        caster = null;
        grantedHyperArmor = false;
        hitTargets.Clear();
        phase = Phase.Windup;
    }

    public bool CanBeInterruptedBy(CharacterStateType other) =>
        other == CharacterStateType.Death || other == CharacterStateType.Hit;

    private void TickWindup(CharacterMotor ctx)
    {
        if (!ctx.DashChargeMovementAuthorized)
            return;
            

        if (maxDistance <= 0f)
        {
            FinishMovement(ctx);
            return;
        }

        phase = Phase.Moving;
    }

    private void TickMoving(CharacterMotor ctx, float dt)
    {
        float step = speed * dt;
        step = Mathf.Min(step, maxDistance - traveled);
        if (step < 0.01f)
        {
            FinishMovement(ctx);
            return;
        }

        var result = DashMovementSimulator.SimulateStep(
            ctx.transform.position,
            direction,
            step,
            movement);

        if (!result.moved)
        {
            FinishMovement(ctx);
            return;
        }

        Vector3 prev = ctx.transform.position;
        ctx.transform.position = result.position;
        traveled += result.distanceMeters;

        TryHitAlongSegment(ctx, prev, ctx.transform.position);
        TryZoneAlongSegment(ctx, prev, ctx.transform.position);
        segmentStart = ctx.transform.position;

        ctx.Facing?.FaceMoveDirection(direction);

        if (traveled >= maxDistance - 0.02f || result.blockedByWall)
            FinishMovement(ctx);
    }

    private void TryHitAlongSegment(CharacterMotor ctx, Vector3 from, Vector3 to)
    {
        if (caster == null) return;

        float hitRadius = pathHalfWidth;
        foreach (var target in BattleTargeting.FindAllBattleActors())
        {
            if (target == null || target == caster) continue;
            if (!BattleTargeting.IsAlive(target)) continue;
            if (!caster.IsEnemy(target)) continue;
            if (hitTargets.Contains(target)) continue;

            var targetMove = target.GetComponent<CharacterMovementController>();
            float targetRadius = targetMove != null ? targetMove.PersonalSpaceRadius : 0.6f;
            if (DashPathUtility.DistancePointToSegmentXZ(target.transform.position, from, to) >
                hitRadius + targetRadius)
                continue;

            hitTargets.Add(target);
            ApplyHit(ctx, target);
        }
    }

    private void TryZoneAlongSegment(CharacterMotor ctx, Vector3 from, Vector3 to)
    {
        if (caster == null) return;
        BattleZoneManager.Instance.TryProcessMovementSegment(caster, from, to);
    }

    private void ApplyHit(CharacterMotor ctx, AbilitySystemComponent target)
    {
        if (caster?.Attributes == null || target?.Attributes == null) return;

        float damage = damageScaler * caster.Attributes.Attack;
        target.Attributes.TakeDamage(damage, damageType, caster);
        caster.PlayTargetEffect(target, hitVfx);

        if (knockbackDistance <= 0f) return;

        Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 offset = target.transform.position - ctx.transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.0001f && Vector3.Dot(offset, lateral) < 0f)
            lateral = -lateral;

        var targetMove = target.GetComponent<CharacterMovementController>();
        targetMove?.TryApplyKnockbackDirection(lateral, knockbackDistance, knockbackDuration);
    }

    /// <summary>位移结束；收招由动画 OnAbilityComplete 驱动。</summary>
    private void FinishMovement(CharacterMotor ctx)
    {
        phase = Phase.AwaitingComplete;
        ctx.NotifyMovementInterrupted();
    }
}
