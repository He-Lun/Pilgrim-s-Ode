using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [Legacy 别名] 请使用 NavPathMovementPlanner / NavReachabilityFloodFill。
/// </summary>
public static class NavStraightMovementPlanner
{
    public static MovePlan TryPlan(
        Vector3 start,
        Vector3 desiredTarget,
        float maxMeters,
        float agentRadius,
        CharacterMovementController ignoreOccupant = null)
    {
        return NavPathMovementPlanner.TryPlan(start, desiredTarget, maxMeters, agentRadius, ignoreOccupant);
    }

    public static bool TrySampleNavMesh(Vector3 worldPoint, out Vector3 snapped)
        => NavPathMovementPlanner.TrySampleNavMesh(worldPoint, out snapped);

    public static Vector3 SnapToNavMesh(Vector3 worldPoint)
        => NavPathMovementPlanner.SnapToNavMesh(worldPoint);
}
