using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗单位占用检测 — 连续坐标下的个人空间半径。
/// </summary>
public static class BattleOccupancy
{
    private static readonly List<CharacterMovementController> Registered = new List<CharacterMovementController>();

    public static void Register(CharacterMovementController controller)
    {
        if (controller == null || Registered.Contains(controller)) return;
        Registered.Add(controller);
    }

    public static void Unregister(CharacterMovementController controller)
    {
        if (controller == null) return;
        Registered.Remove(controller);
    }

    public static bool IsPositionFree(Vector3 position, float radius, CharacterMovementController ignore = null)
    {
        foreach (var other in Registered)
        {
            if (other == null || other == ignore) continue;

            Vector3 otherPos = other.transform.position;
            float minDist = radius + other.PersonalSpaceRadius;
            if (HorizontalDistance(position, otherPos) < minDist)
                return false;
        }

        return true;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
