using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh 路径移动规划 — CalculatePath 绕障 + 米制预算。
/// </summary>
public static class NavPathMovementPlanner
{
    private const float MinMoveMeters = 0.05f;
    private const float PathEpsilon = 0.05f;

    private static readonly NavMeshPath SharedPath = new NavMeshPath();

    public static MovePlan TryPlan(
        Vector3 start,
        Vector3 desiredTarget,
        float maxMeters,
        float agentRadius,
        CharacterMovementController ignoreOccupant = null,
        HashSet<Vector3> reachableCache = null)
    {
        if (maxMeters <= 0f)
            return MovePlan.Invalid(MoveResult.OutOfRange);

        if (!TrySampleNavMesh(desiredTarget, out Vector3 sampledTarget))
            return MovePlan.Invalid(MoveResult.InvalidTarget);

        if (BattleBounds.Instance != null && !BattleBounds.Instance.ContainsPoint(sampledTarget))
            return MovePlan.Invalid(MoveResult.InvalidTarget);

        float cellSize = BattleSpaceSettings.GetFloodFillCellSize();
        if (reachableCache != null && !NavReachabilityFloodFill.IsPointReachable(sampledTarget, reachableCache, cellSize))
            return MovePlan.Invalid(MoveResult.OutOfRange);

        if (!TrySampleNavMesh(start, out Vector3 startSnapped))
            return MovePlan.Invalid(MoveResult.InvalidTarget);

        SharedPath.ClearCorners();
        if (!NavMesh.CalculatePath(startSnapped, sampledTarget, BattleSpaceSettings.GetNavMeshAreaMask(), SharedPath))
            return MovePlan.Invalid(MoveResult.Blocked);

        if (SharedPath.status != NavMeshPathStatus.PathComplete && SharedPath.status != NavMeshPathStatus.PathPartial)
            return MovePlan.Invalid(MoveResult.Blocked);

        var corners = SharedPath.corners;
        if (corners == null || corners.Length == 0)
            return MovePlan.Invalid(MoveResult.Blocked);

        float pathLength = CalculatePathLength(corners);
        List<Vector3> pathPoints = new List<Vector3>(corners);

        if (pathLength > maxMeters + PathEpsilon)
        {
            pathPoints = TrimPath(pathPoints, maxMeters, out pathLength);
            if (pathPoints == null || pathLength < MinMoveMeters)
                return MovePlan.Invalid(MoveResult.OutOfRange);
        }

        Vector3 destination = pathPoints[pathPoints.Count - 1];

        if (BattleOccupancy.HorizontalDistance(startSnapped, destination) < MinMoveMeters)
            return MovePlan.Invalid(MoveResult.InvalidTarget);

        if (!BattleOccupancy.IsPositionFree(destination, agentRadius, ignoreOccupant))
            return MovePlan.Invalid(MoveResult.Blocked);

        return MovePlan.Ready(destination, pathLength, pathPoints);
    }

    public static bool TrySampleNavMesh(Vector3 worldPoint, out Vector3 snapped)
    {
        if (NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, BattleSpaceSettings.GetNavMeshSampleRadius(),
                BattleSpaceSettings.GetNavMeshAreaMask()))
        {
            snapped = hit.position;
            return true;
        }

        snapped = default;
        return false;
    }

    public static Vector3 SnapToNavMesh(Vector3 worldPoint)
    {
        return TrySampleNavMesh(worldPoint, out Vector3 snapped) ? snapped : worldPoint;
    }

    public static float CalculatePathLength(IList<Vector3> corners)
    {
        if (corners == null || corners.Count < 2) return 0f;

        float total = 0f;
        for (int i = 1; i < corners.Count; i++)
            total += BattleOccupancy.HorizontalDistance(corners[i - 1], corners[i]);
        return total;
    }

    public static List<Vector3> TrimPath(List<Vector3> corners, float maxMeters, out float outLength)
    {
        outLength = 0f;
        if (corners == null || corners.Count == 0)
            return null;

        var result = new List<Vector3> { corners[0] };

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 prev = result[result.Count - 1];
            Vector3 next = corners[i];
            float seg = BattleOccupancy.HorizontalDistance(prev, next);

            if (outLength + seg <= maxMeters + PathEpsilon)
            {
                outLength += seg;
                result.Add(next);
                continue;
            }

            float remain = maxMeters - outLength;
            if (remain >= MinMoveMeters)
            {
                Vector3 dir = next - prev;
                dir.y = 0f;
                dir.Normalize();
                result.Add(prev + dir * remain);
                outLength = maxMeters;
            }
            break;
        }

        return result.Count >= 1 ? result : null;
    }
}
