using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 指定方向矩形范围 — 命中判定与指示器几何（XZ 平面）。
/// </summary>
public static class DirectedRectUtility
{
    public struct DirectedRect
    {
        public Vector3 origin;
        public Vector3 forward;
        public float lengthMeters;
        public float widthMeters;

        public Vector3 Right => Vector3.Cross(Vector3.up, forward).normalized;
    }

    public static DirectedRect Build(
        Vector3 origin,
        Vector3 aimDirection,
        float lengthMeters,
        float widthMeters)
    {
        return new DirectedRect
        {
            origin = Flatten(origin),
            forward = FlattenNormalize(aimDirection),
            lengthMeters = Mathf.Max(0f, lengthMeters),
            widthMeters = Mathf.Max(0f, widthMeters)
        };
    }

    public static bool ContainsPoint(in DirectedRect rect, Vector3 worldPoint)
    {
        if (rect.lengthMeters <= 0f || rect.widthMeters <= 0f)
            return false;

        Vector3 offset = Flatten(worldPoint) - rect.origin;
        float forward = Vector3.Dot(offset, rect.forward);
        if (forward < 0f || forward > rect.lengthMeters)
            return false;

        float side = Mathf.Abs(Vector3.Dot(offset, rect.Right));
        return side <= rect.widthMeters * 0.5f;
    }

    public static Vector3[] GetCorners(in DirectedRect rect, float yOffset = 0.08f)
    {
        float half = rect.widthMeters * 0.5f;
        Vector3 y = Vector3.up * yOffset;
        Vector3 fwd = rect.forward * rect.lengthMeters;
        Vector3 right = rect.Right * half;

        return new[]
        {
            rect.origin - right + y,
            rect.origin - right + fwd + y,
            rect.origin + right + fwd + y,
            rect.origin + right + y
        };
    }

    public static Mesh BuildFillMesh(in DirectedRect rect, float yOffset = 0.06f)
    {
        var corners = GetCorners(rect, yOffset);
        var mesh = new Mesh { name = "DirectedRectMesh" };

        mesh.vertices = new[]
        {
            corners[0], corners[1], corners[2],
            corners[0], corners[2], corners[3]
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Vector3 ResolveAimDirection(AbilityActivationContext context, Vector3 origin)
    {
        if (context.hasAimDirection && context.aimDirectionWorld.sqrMagnitude > 0.0001f)
            return FlattenNormalize(context.aimDirectionWorld);

        if (context.hasTargetPoint)
        {
            Vector3 toPoint = context.targetWorldPoint - origin;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude > 0.0001f)
                return toPoint.normalized;
        }

#pragma warning disable 618
        if (context.HasDirection)
        {
            var gridDir = new Vector3(context.direction.x, 0f, context.direction.y);
            if (gridDir.sqrMagnitude > 0.0001f)
                return gridDir.normalized;
        }
#pragma warning restore 618

        return Vector3.forward;
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
