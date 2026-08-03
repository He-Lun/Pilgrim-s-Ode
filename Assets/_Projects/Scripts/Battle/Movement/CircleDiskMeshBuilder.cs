using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 构建贴地圆形填充 Mesh。
/// </summary>
public static class CircleDiskMeshBuilder
{
    public const float DefaultYOffset = 0.08f;

    /// <summary>以原点为圆心、XZ 平面上的实心圆盘（供预览根节点定位到落点）。</summary>
    public static Mesh BuildLocal(float radiusMeters, int segments, float yOffset = DefaultYOffset)
    {
        var mesh = new Mesh { name = "CircleDiskMeshLocal" };
        if (radiusMeters <= 0f || segments < 3)
            return mesh;

        var vertices = new List<Vector3>(segments + 2);
        var triangles = new List<int>(segments * 3);

        vertices.Add(new Vector3(0f, yOffset, 0f));

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            vertices.Add(new Vector3(
                Mathf.Cos(angle) * radiusMeters,
                yOffset,
                Mathf.Sin(angle) * radiusMeters));
        }

        for (int i = 1; i <= segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>世界坐标圆盘（兼容旧调用）。</summary>
    public static Mesh Build(Vector3 center, float radiusMeters, int segments, float yOffset = DefaultYOffset)
    {
        center = BattleTargeting.ProjectToGround(center);
        var mesh = BuildLocal(radiusMeters, segments, yOffset);

        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] += center;

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        return mesh;
    }
}
