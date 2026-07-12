using UnityEngine;

/// <summary>攻击路径与屏障墙段的 XZ 平面相交检测。</summary>
public static class BarrierLineMath
{
    private const float Eps = 1e-4f;

    public static bool PathCrossesWall(Vector3 pathStart, Vector3 pathEnd, BattleBarrierInstance barrier)
    {
        if (barrier == null) return false;

        barrier.GetWallSegment(out Vector3 wa, out Vector3 wb);
        return SegmentsCross(
            ToXZ(pathStart),
            ToXZ(pathEnd),
            ToXZ(wa),
            ToXZ(wb),
            barrier.ThicknessMeters);
    }

    private static Vector2 ToXZ(Vector3 v) => new Vector2(v.x, v.z);

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float thickness)
    {
        if (SegmentIntersect(a, b, c, d))
            return true;

        if (thickness <= 0f)
            return false;

        float min = PointSegmentDistance(a, c, d);
        min = Mathf.Min(min, PointSegmentDistance(b, c, d));
        min = Mathf.Min(min, PointSegmentDistance(c, a, b));
        min = Mathf.Min(min, PointSegmentDistance(d, a, b));
        return min <= thickness;
    }

    private static bool SegmentIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p3, p4, p1);
        float d2 = Cross(p3, p4, p2);
        float d3 = Cross(p1, p2, p3);
        float d4 = Cross(p1, p2, p4);

        if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
            && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            return true;

        if (Mathf.Abs(d1) < Eps && OnSegment(p3, p4, p1)) return true;
        if (Mathf.Abs(d2) < Eps && OnSegment(p3, p4, p2)) return true;
        if (Mathf.Abs(d3) < Eps && OnSegment(p1, p2, p3)) return true;
        if (Mathf.Abs(d4) < Eps && OnSegment(p1, p2, p4)) return true;
        return false;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        => (c.x - a.x) * (b.y - a.y) - (b.x - a.x) * (c.y - a.y);

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - Eps && p.x <= Mathf.Max(a.x, b.x) + Eps
            && p.y >= Mathf.Min(a.y, b.y) - Eps && p.y <= Mathf.Max(a.y, b.y) + Eps;
    }

    private static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }
}
