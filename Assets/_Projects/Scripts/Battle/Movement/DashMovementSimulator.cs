using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 突进步进 — 只挡墙/边界，不挡其他单位（贴身碰撞在 DashChargeState 里结算）。
/// </summary>
public static class DashMovementSimulator
{
    private const float WallBackoff = 0.12f;
    private const float MinStepMeters = 0.02f;

    public static KnockbackSimulator.StepResult SimulateStep(
        Vector3 currentPosition,
        Vector3 direction,
        float stepMeters,
        CharacterMovementController self)
    {
        var fail = new KnockbackSimulator.StepResult { position = currentPosition, moved = false };

        if (stepMeters < MinStepMeters)
            return fail;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return fail;

        direction.Normalize();

        Vector3 origin = Flatten(currentPosition);
        if (NavPathMovementPlanner.TrySampleNavMesh(currentPosition, out Vector3 sampledOrigin))
            origin = Flatten(sampledOrigin);

        Vector3 desired = origin + direction * stepMeters;

        if (BattleBounds.Instance != null && !BattleBounds.Instance.ContainsPoint(desired))
            return fail;

        int areaMask = BattleSpaceSettings.GetNavMeshAreaMask();
        Vector3 landPoint = desired;
        bool blockedByWall = false;

        if (NavMesh.Raycast(origin, desired, out NavMeshHit navHit, areaMask))
        {
            landPoint = navHit.position - direction * WallBackoff;
            blockedByWall = true;

            if (BattleOccupancy.HorizontalDistance(origin, landPoint) < MinStepMeters)
            {
                return new KnockbackSimulator.StepResult
                {
                    position = currentPosition,
                    moved = false,
                    blockedByWall = true
                };
            }
        }

        if (!NavPathMovementPlanner.TrySampleNavMesh(landPoint, out Vector3 snapped))
            return fail;

        float moved = BattleOccupancy.HorizontalDistance(origin, snapped);
        if (moved < MinStepMeters)
            return fail;

        Vector3 finalPos = self != null ? self.ApplyFootOffset(snapped) : snapped;
        return new KnockbackSimulator.StepResult
        {
            moved = true,
            distanceMeters = moved,
            position = finalPos,
            blockedByWall = blockedByWall
        };
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
