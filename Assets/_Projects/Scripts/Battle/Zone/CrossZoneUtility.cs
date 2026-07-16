using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 有限长度十字激光几何 — 两臂正交矩形（半长 × 全宽），非无限射线。
/// </summary>
public static class CrossZoneUtility
{
    private const float Eps = 1e-4f;

    /// <param name="armHalfLengthMeters">从中心沿一臂到末端的半长</param>
    /// <param name="armWidthMeters">臂的全宽（横向厚度）</param>
    public static bool ContainsPoint(
        Vector3 center,
        Vector3 forward,
        float armHalfLengthMeters,
        float armWidthMeters,
        Vector3 worldPoint)
    {
        if (armHalfLengthMeters <= 0f || armWidthMeters <= 0f)
            return false;

        ToLocal(center, forward, worldPoint, out float u, out float v);
        float halfWidth = armWidthMeters * 0.5f;

        bool inForwardArm = Mathf.Abs(u) <= armHalfLengthMeters && Mathf.Abs(v) <= halfWidth;
        bool inRightArm = Mathf.Abs(v) <= armHalfLengthMeters && Mathf.Abs(u) <= halfWidth;
        return inForwardArm || inRightArm;
    }

    /// <summary>折线路径是否与十字相交（穿过即算，不要求终点在内）。</summary>
    public static bool PathIntersects(
        IList<Vector3> pathPoints,
        Vector3 center,
        Vector3 forward,
        float armHalfLengthMeters,
        float armWidthMeters)
    {
        if (pathPoints == null || pathPoints.Count == 0)
            return false;
        if (armHalfLengthMeters <= 0f || armWidthMeters <= 0f)
            return false;

        if (pathPoints.Count == 1)
            return ContainsPoint(center, forward, armHalfLengthMeters, armWidthMeters, pathPoints[0]);

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            if (SegmentIntersects(
                    pathPoints[i],
                    pathPoints[i + 1],
                    center,
                    forward,
                    armHalfLengthMeters,
                    armWidthMeters))
                return true;
        }

        return false;
    }

    public static bool SegmentIntersects(
        Vector3 worldA,
        Vector3 worldB,
        Vector3 center,
        Vector3 forward,
        float armHalfLengthMeters,
        float armWidthMeters)
    {
        ToLocal(center, forward, worldA, out float au, out float av);
        ToLocal(center, forward, worldB, out float bu, out float bv);

        float halfWidth = armWidthMeters * 0.5f;
        var a = new Vector2(au, av);
        var b = new Vector2(bu, bv);

        // 前向臂：u∈[-halfLen, halfLen], v∈[-halfW, halfW]
        if (SegmentIntersectsAabb(
                a, b,
                -armHalfLengthMeters, armHalfLengthMeters,
                -halfWidth, halfWidth))
            return true;

        // 横向臂：u∈[-halfW, halfW], v∈[-halfLen, halfLen]
        return SegmentIntersectsAabb(
            a, b,
            -halfWidth, halfWidth,
            -armHalfLengthMeters, armHalfLengthMeters);
    }

    public static float BoundingRadiusMeters(float armHalfLengthMeters, float armWidthMeters)
    {
        float halfWidth = Mathf.Max(0f, armWidthMeters) * 0.5f;
        float halfLen = Mathf.Max(0f, armHalfLengthMeters);
        return Mathf.Sqrt(halfLen * halfLen + halfWidth * halfWidth);
    }

    /// <summary>折线是否与圆盘相交。</summary>
    public static bool PathIntersectsCircle(IList<Vector3> pathPoints, Vector3 center, float radiusMeters)
    {
        if (pathPoints == null || pathPoints.Count == 0 || radiusMeters <= 0f)
            return false;

        if (pathPoints.Count == 1)
            return BattleOccupancy.HorizontalDistance(center, pathPoints[0]) <= radiusMeters;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            if (SegmentIntersectsCircle(pathPoints[i], pathPoints[i + 1], center, radiusMeters))
                return true;
        }

        return false;
    }

    private static bool SegmentIntersectsCircle(Vector3 a, Vector3 b, Vector3 center, float radius)
    {
        Vector2 ca = new Vector2(a.x - center.x, a.z - center.z);
        Vector2 cb = new Vector2(b.x - center.x, b.z - center.z);
        if (ca.magnitude <= radius || cb.magnitude <= radius)
            return true;

        Vector2 ab = cb - ca;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f)
            return ca.magnitude <= radius;

        float t = Mathf.Clamp01(Vector2.Dot(-ca, ab) / lenSq);
        Vector2 closest = ca + ab * t;
        return closest.magnitude <= radius;
    }

    private static void ToLocal(
        Vector3 center,
        Vector3 forward,
        Vector3 worldPoint,
        out float alongFwd,
        out float alongRight)
    {
        Vector3 fwd = FlattenNormalize(forward);
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        Vector3 delta = worldPoint - center;
        delta.y = 0f;
        alongFwd = Vector3.Dot(delta, fwd);
        alongRight = Vector3.Dot(delta, right);
    }

    /// <summary>2D 线段与轴对齐矩形相交（含端点在内 / 擦边）。</summary>
    private static bool SegmentIntersectsAabb(
        Vector2 a,
        Vector2 b,
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        if (PointInAabb(a, minX, maxX, minY, maxY) || PointInAabb(b, minX, maxX, minY, maxY))
            return true;

        // Liang-Barsky
        float dx = b.x - a.x;
        float dy = b.y - a.y;
        float t0 = 0f;
        float t1 = 1f;

        if (!ClipEdge(-dx, a.x - minX, ref t0, ref t1)) return false;
        if (!ClipEdge(dx, maxX - a.x, ref t0, ref t1)) return false;
        if (!ClipEdge(-dy, a.y - minY, ref t0, ref t1)) return false;
        if (!ClipEdge(dy, maxY - a.y, ref t0, ref t1)) return false;

        return true;
    }

    private static bool ClipEdge(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < Eps)
            return q >= 0f;

        float r = q / p;
        if (p < 0f)
        {
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else
        {
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }

        return true;
    }

    private static bool PointInAabb(Vector2 p, float minX, float maxX, float minY, float maxY)
    {
        return p.x >= minX - Eps && p.x <= maxX + Eps
            && p.y >= minY - Eps && p.y <= maxY + Eps;
    }

    private static Vector3 FlattenNormalize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }
}
