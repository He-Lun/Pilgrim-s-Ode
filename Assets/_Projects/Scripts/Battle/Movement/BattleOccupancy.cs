using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗单位占用检测 — 连续坐标下的个人空间半径 + 地形召唤物（晶石等）。
/// </summary>
public static class BattleOccupancy
{
    private static readonly List<CharacterMovementController> Registered = new List<CharacterMovementController>();
    private static readonly List<DestructibleBattleProp> Props = new List<DestructibleBattleProp>();

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

    public static void RegisterProp(DestructibleBattleProp prop)
    {
        if (prop == null || Props.Contains(prop)) return;
        Props.Add(prop);
        InvalidateAllReachableCaches();
    }

    public static void UnregisterProp(DestructibleBattleProp prop)
    {
        if (prop == null) return;
        if (Props.Remove(prop))
            InvalidateAllReachableCaches();
    }

    public static void InvalidateAllReachableCaches()
    {
        for (int i = Registered.Count - 1; i >= 0; i--)
        {
            var c = Registered[i];
            if (c == null)
            {
                Registered.RemoveAt(i);
                continue;
            }

            c.InvalidateReachableCache();
        }
    }

    public static bool IsPositionFree(Vector3 position, float radius, CharacterMovementController ignore = null)
    {
        if (!IsClearOfProps(position, radius))
            return false;

        foreach (var other in Registered)
        {
            if (other == null || other == ignore) continue;

            float minDist = radius + other.PersonalSpaceRadius;
            if (HorizontalDistance(position, other.transform.position) < minDist)
                return false;
        }

        return true;
    }

    /// <summary>忽略一组单位（如同时被拉取的目标）后的占用检测。</summary>
    public static bool IsPositionFree(
        Vector3 position,
        float radius,
        ICollection<CharacterMovementController> ignoreSet)
    {
        if (!IsClearOfProps(position, radius))
            return false;

        foreach (var other in Registered)
        {
            if (other == null) continue;
            if (ignoreSet != null && ignoreSet.Contains(other)) continue;

            float minDist = radius + other.PersonalSpaceRadius;
            if (HorizontalDistance(position, other.transform.position) < minDist)
                return false;
        }

        return true;
    }

    public static bool IsClearOfProps(Vector3 position, float radius)
    {
        for (int i = Props.Count - 1; i >= 0; i--)
        {
            var prop = Props[i];
            if (prop == null)
            {
                Props.RemoveAt(i);
                continue;
            }

            float minDist = radius + prop.ObstacleRadius;
            if (HorizontalDistance(position, prop.transform.position) < minDist)
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

    /// <summary>枚举当前登记的移动单位（供召唤物挤开占用）。</summary>
    public static IReadOnlyList<CharacterMovementController> GetRegisteredMovers() => Registered;
}
