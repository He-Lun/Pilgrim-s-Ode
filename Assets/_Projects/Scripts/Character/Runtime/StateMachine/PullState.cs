using UnityEngine;

/// <summary>
/// 重力拉取 — 沿二次缓动曲线直接插值位移（不挡单位/不中途停），过程中播眩晕表现。
/// </summary>
public class PullState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Pull;
    public int Priority => CharacterStatePriority.Get(Type);

    private Vector3 startPosition;
    private Vector3 destination;
    private float duration;
    private float elapsed;
    private float traveled;
    private Vector3 lastPosition;
    private CharacterMovementController movement;

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        movement = ctx.GetComponent<CharacterMovementController>();
        startPosition = Flatten(ctx.transform.position);
        destination = Flatten(payload.pullDestination);
        if (NavPathMovementPlanner.TrySampleNavMesh(payload.pullDestination, out Vector3 destSnap))
            destination = Flatten(destSnap);

        duration = Mathf.Max(0.08f, payload.pullDuration);
        elapsed = 0f;
        traveled = 0f;
        lastPosition = ctx.transform.position;

        ctx.AnimatorDriver?.SetStunned(true);
        ctx.AnimatorDriver?.SetMoving(true);

        Vector3 flat = destination - startPosition;
        if (flat.sqrMagnitude > 0.0001f)
            ctx.Facing?.FaceMoveDirection(flat.normalized);
    }

    public void Tick(CharacterMotor ctx, float dt)
    {
        elapsed += dt;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = EaseInOutQuad(t);

        // 直接插值到缓动位置，避免 SimulateStep 被墙/距离失败导致原地不动或结束瞬移
        Vector3 desired = Vector3.Lerp(startPosition, destination, eased);
        Vector3 worldPos = desired;
        if (NavPathMovementPlanner.TrySampleNavMesh(desired, out Vector3 snapped))
            worldPos = movement != null ? movement.ApplyFootOffset(snapped) : snapped;
        else if (movement != null)
            worldPos = movement.ApplyFootOffset(desired);

        traveled += BattleOccupancy.HorizontalDistance(lastPosition, worldPos);
        lastPosition = worldPos;
        ctx.transform.position = worldPos;

        Vector3 face = destination - startPosition;
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            ctx.Facing?.FaceMoveDirection(face.normalized);

        float speedNorm = 4f * t * (1f - t);
        ctx.AnimatorDriver?.SetSpeed(Mathf.Lerp(0.35f, 1.1f, speedNorm));
        ctx.AnimatorDriver?.SetStunned(true);

        if (t >= 1f)
            ctx.CompletePull(traveled);
    }

    public void Exit(CharacterMotor ctx)
    {
        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);
        ctx.AnimatorDriver?.SetStunned(false);
        movement = null;
    }

    public bool CanBeInterruptedBy(CharacterStateType other) => other == CharacterStateType.Death;

    private static float EaseInOutQuad(float t)
    {
        return t < 0.5f
            ? 2f * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
