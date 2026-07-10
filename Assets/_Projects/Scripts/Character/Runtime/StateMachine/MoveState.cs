using System.Collections.Generic;
using UnityEngine;

public class MoveState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Move;
    public int Priority => CharacterStatePriority.Get(Type);

    private List<Vector2Int> path;
    private int stepIndex;
    private Vector3 stepStart;
    private Vector3 stepTarget;
    private float stepProgress;
    private float stepDuration;
    private float moveCostMeters;

    private bool worldMove;
    private List<Vector3> worldWaypoints;
    private int worldWaypointIndex;
    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    private float segmentProgress;
    private float segmentDuration;

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        if (payload.isWorldMove)
        {
            BeginWorldPath(ctx, payload);
            return;
        }

        path = payload.path;
        stepIndex = Mathf.Max(1, payload.pathIndex);
        if (path == null || path.Count < 2)
        {
            ctx.CompleteMovement();
            return;
        }

        BeginStep(ctx);
        ctx.AnimatorDriver?.SetMoving(true);
    }

    public void Tick(CharacterMotor ctx, float dt)
    {
        if (worldMove)
        {
            TickWorldPath(ctx, dt);
            return;
        }

        if (path == null || stepIndex >= path.Count)
        {
            ctx.CompleteMovement();
            return;
        }

        stepProgress += dt / stepDuration;
        if (stepProgress >= 1f)
        {
            ctx.transform.position = stepTarget;

            stepIndex++;
            if (stepIndex >= path.Count)
            {
                ctx.CompleteMovement();
                return;
            }

            BeginStep(ctx);
            return;
        }

        ctx.transform.position = Vector3.Lerp(stepStart, stepTarget, stepProgress);
        ctx.Facing?.FaceMoveDirection(stepTarget - stepStart);
        ctx.AnimatorDriver?.SetSpeed(Mathf.Lerp(0.5f, 1f, stepProgress));
    }

    public void Exit(CharacterMotor ctx)
    {
        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);

        if (ctx.IsMoving)
            ctx.NotifyMovementInterrupted();

        path = null;
        worldMove = false;
        worldWaypoints = null;
    }

    public bool CanBeInterruptedBy(CharacterStateType other)
    {
        return other == CharacterStateType.Death
               || other == CharacterStateType.Hit
               || other == CharacterStateType.Ability;
    }

    private void BeginWorldPath(CharacterMotor ctx, CharacterStatePayload payload)
    {
        worldWaypoints = payload.worldWaypoints;
        moveCostMeters = payload.moveCostMeters;

        if (worldWaypoints == null || worldWaypoints.Count == 0)
        {
            ctx.CompleteMovement();
            return;
        }

        worldMove = true;
        worldWaypointIndex = 0;
        segmentStart = ctx.transform.position;
        segmentEnd = worldWaypoints[0];
        segmentProgress = 0f;
        segmentDuration = SegmentDuration(ctx, segmentStart, segmentEnd);

        ctx.AnimatorDriver?.SetMoving(true);
        ctx.Facing?.FaceMoveDirection(segmentEnd - segmentStart);
    }

    private void TickWorldPath(CharacterMotor ctx, float dt)
    {
        if (worldWaypoints == null || worldWaypoints.Count == 0)
        {
            ctx.CompleteMovement(moveCostMeters);
            return;
        }

        segmentProgress += dt / segmentDuration;
        float t = Mathf.Clamp01(segmentProgress);

        ctx.transform.position = Vector3.Lerp(segmentStart, segmentEnd, t);
        ctx.Facing?.FaceMoveDirection(segmentEnd - segmentStart);
        ctx.AnimatorDriver?.SetSpeed(Mathf.Lerp(0.5f, 1f, t));

        if (t < 1f) return;

        ctx.transform.position = segmentEnd;
        worldWaypointIndex++;

        if (worldWaypointIndex >= worldWaypoints.Count)
        {
            ctx.CompleteMovement(moveCostMeters);
            return;
        }

        segmentStart = segmentEnd;
        segmentEnd = worldWaypoints[worldWaypointIndex];
        segmentProgress = 0f;
        segmentDuration = SegmentDuration(ctx, segmentStart, segmentEnd);
        ctx.Facing?.FaceMoveDirection(segmentEnd - segmentStart);
    }

    private static float SegmentDuration(CharacterMotor ctx, Vector3 from, Vector3 to)
    {
        float distance = BattleOccupancy.HorizontalDistance(from, to);
        float segments = Mathf.Max(1f, distance / Mathf.Max(0.01f, ctx.MetersPerStepDuration));
        return Mathf.Max(0.08f, ctx.MoveStepDuration * segments);
    }

    private void BeginStep(CharacterMotor ctx)
    {
#pragma warning disable 618
        var grid = BattleGrid.Instance;
#pragma warning restore 618
        if (grid == null)
        {
            ctx.CompleteMovement();
            return;
        }

        stepStart = ctx.transform.position;
        stepTarget = ctx.GetCellWorldPosition(path[stepIndex]);
        stepProgress = 0f;
        stepDuration = ctx.MoveStepDuration;
        ctx.Facing?.FaceMoveDirection(stepTarget - stepStart);
    }
}
