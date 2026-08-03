using UnityEngine;

/// <summary>
/// 平滑击退 — 初速 + 线性衰减，NavMesh 射线挡墙，占用检测挡其他单位。
/// </summary>
public class KnockbackState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Knockback;
    public int Priority => CharacterStatePriority.Get(Type);

    private Vector3 direction;
    private float maxDistance;
    private float duration;
    private float initialSpeed;
    private float elapsed;
    private float traveled;
    private CharacterMovementController movement;

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        movement = ctx.GetComponent<CharacterMovementController>();

        Vector3 fromCenter = payload.knockbackFromCenter;
        maxDistance = Mathf.Max(0f, payload.knockbackDistance);
        duration = Mathf.Max(0.08f, payload.knockbackDuration);

        Vector3 flatDir = payload.hasKnockbackDirection && payload.knockbackDirection.sqrMagnitude > 0.0001f
            ? payload.knockbackDirection
            : ctx.transform.position - fromCenter;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 0.0001f)
            flatDir = ctx.transform.forward;
        direction = flatDir.normalized;

        // 三角速度曲线：初速 = 2D/T，在 T 时刻衰减到 0
        initialSpeed = maxDistance > 0f ? (2f * maxDistance / duration) : 0f;
        elapsed = 0f;
        traveled = 0f;

        ctx.AnimatorDriver?.TriggerHit();
        ctx.AnimatorDriver?.SetMoving(true);
        ctx.Facing?.FaceMoveDirection(direction);
    }

    public void Tick(CharacterMotor ctx, float dt)
    {
        if (maxDistance <= 0f || duration <= 0f)
        {
            ctx.CompleteKnockback(0f);
            return;
        }

        elapsed += dt;
        float t = Mathf.Clamp01(elapsed / duration);

        if (t >= 1f || traveled >= maxDistance - 0.02f)
        {
            ctx.CompleteKnockback(traveled);
            return;
        }

        float speed = initialSpeed * (1f - t);
        float stepDist = speed * dt;
        stepDist = Mathf.Min(stepDist, maxDistance - traveled);

        if (stepDist < 0.01f)
        {
            ctx.CompleteKnockback(traveled);
            return;
        }

        var result = KnockbackSimulator.SimulateStep(
            ctx.transform.position,
            direction,
            stepDist,
            movement);

        if (!result.moved)
        {
            ctx.CompleteKnockback(traveled);
            return;
        }

        ctx.transform.position = result.position;
        traveled += result.distanceMeters;

        ctx.Facing?.FaceMoveDirection(direction);
        ctx.AnimatorDriver?.SetSpeed(Mathf.Lerp(0.4f, 1f, 1f - t));

        if (result.blockedByWall || result.blockedByUnit)
            ctx.CompleteKnockback(traveled);
    }

    public void Exit(CharacterMotor ctx)
    {
        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);
        ctx.AnimatorDriver?.EndHitPresentation();
        movement = null;
    }

    public bool CanBeInterruptedBy(CharacterStateType other) => other == CharacterStateType.Death;
}
