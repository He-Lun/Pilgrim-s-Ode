using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 将洪水填充可达点渲染为地面半透明面片。
/// </summary>
public static class MoveRangeMeshBuilder
{
    public static Mesh Build(IReadOnlyCollection<Vector3> points, float quadSize, float yOffset = 0.06f)
    {
        var mesh = new Mesh { name = "MoveRangeMesh" };
        if (points == null || points.Count == 0)
            return mesh;

        float half = quadSize * 0.48f;
        var vertices = new List<Vector3>(points.Count * 4);
        var triangles = new List<int>(points.Count * 6);

        foreach (var p in points)
        {
            Vector3 c = p + Vector3.up * yOffset;
            int i = vertices.Count;
            vertices.Add(c + new Vector3(-half, 0f, -half));
            vertices.Add(c + new Vector3(half, 0f, -half));
            vertices.Add(c + new Vector3(half, 0f, half));
            vertices.Add(c + new Vector3(-half, 0f, half));

            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
