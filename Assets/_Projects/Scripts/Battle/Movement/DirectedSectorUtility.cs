using UnityEngine;

/// <summary>
/// 前方扇形范围 — 命中判定与指示器几何（XZ 平面）。
/// </summary>
public static class DirectedSectorUtility
{
    public struct DirectedSector
    {
        public Vector3 origin;
        public Vector3 forward;
        public float radiusMeters;
        public float halfAngleDegrees;
    }

    public static DirectedSector Build(
        Vector3 origin,
        Vector3 aimDirection,
        float radiusMeters,
        float halfAngleDegrees)
    {
        return new DirectedSector
        {
            origin = Flatten(origin),
            forward = FlattenNormalize(aimDirection),
            radiusMeters = Mathf.Max(0f, radiusMeters),
            halfAngleDegrees = Mathf.Clamp(halfAngleDegrees, 1f, 180f)
        };
    }

    public static bool ContainsPoint(in DirectedSector sector, Vector3 worldPoint)
    {
        if (sector.radiusMeters <= 0f) return false;

        Vector3 offset = Flatten(worldPoint) - sector.origin;
        float dist = offset.magnitude;
        if (dist < 0.01f || dist > sector.radiusMeters)
            return false;

        float angle = Vector3.Angle(sector.forward, offset);
        return angle <= sector.halfAngleDegrees;
    }

    /// <summary>扇形填充网格（原点 + 弧线采样）。</summary>
    public static Mesh BuildFillMesh(in DirectedSector sector, float yOffset = 0.06f, int arcSegments = 24)
    {
        int segs = Mathf.Max(4, arcSegments);
        float half = sector.halfAngleDegrees;
        float y = yOffset;

        var vertices = new Vector3[segs + 2];
        vertices[0] = sector.origin + Vector3.up * y;

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float angle = Mathf.Lerp(-half, half, t);
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 dir = rot * sector.forward;
            vertices[i + 1] = sector.origin + dir * sector.radiusMeters + Vector3.up * y;
        }

        var triangles = new int[segs * 3];
        for (int i = 0; i < segs; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = "DirectedSectorMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Vector3[] GetOutlinePoints(in DirectedSector sector, float yOffset = 0.08f, int arcSegments = 24)
    {
        int segs = Mathf.Max(4, arcSegments);
        float half = sector.halfAngleDegrees;
        var points = new Vector3[segs + 3];
        Vector3 y = Vector3.up * yOffset;

        points[0] = sector.origin + y;
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float angle = Mathf.Lerp(-half, half, t);
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
            points[i + 1] = sector.origin + (rot * sector.forward) * sector.radiusMeters + y;
        }

        points[segs + 2] = sector.origin + y;
        return points;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static Vector3 FlattenNormalize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
