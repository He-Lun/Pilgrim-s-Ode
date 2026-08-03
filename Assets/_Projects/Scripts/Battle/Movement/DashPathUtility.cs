using UnityEngine;

/// <summary>
/// 突进路径几何 — XZ 平面线段距离。
/// </summary>
public static class DashPathUtility
{
    public static float DistancePointToSegmentXZ(Vector3 point, Vector3 segA, Vector3 segB)
    {
        point.y = 0f;
        segA.y = 0f;
        segB.y = 0f;

        Vector3 ab = segB - segA;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
            return Vector3.Distance(point, segA);

        float t = Mathf.Clamp01(Vector3.Dot(point - segA, ab) / lenSq);
        Vector3 closest = segA + ab * t;
        return Vector3.Distance(point, closest);
    }
}
