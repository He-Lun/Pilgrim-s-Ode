using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh 洪水填充 — BFS 计算移动力范围内所有可达点（自动绕开障碍）。
/// </summary>
public static class NavReachabilityFloodFill
{
    private static readonly Vector3[] NeighborOffsets =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.left,
        Vector3.right,
        new Vector3(1f, 0f, 1f).normalized,
        new Vector3(1f, 0f, -1f).normalized,
        new Vector3(-1f, 0f, 1f).normalized,
        new Vector3(-1f, 0f, -1f).normalized
    };

    public static HashSet<Vector3> ComputeReachable(
        Vector3 start,
        float maxMeters,
        float cellSize,
        float agentRadius,
        CharacterMovementController ignoreOccupant = null)
    {
        var reachable = new HashSet<Vector3>();

        if (maxMeters <= 0f || cellSize <= 0f)
            return reachable;

        if (!NavPathMovementPlanner.TrySampleNavMesh(start, out Vector3 startSnapped))
            return reachable;

        var bestCost = new Dictionary<Vector2Int, float>();
        var queue = new Queue<(Vector3 pos, float cost)>();

        reachable.Add(startSnapped);
        bestCost[Quantize(startSnapped, cellSize)] = 0f;
        queue.Enqueue((startSnapped, 0f));

        int areaMask = BattleSpaceSettings.GetNavMeshAreaMask();
        float sampleRadius = BattleSpaceSettings.GetNavMeshSampleRadius();

        while (queue.Count > 0)
        {
            var (current, cost) = queue.Dequeue();

            foreach (var offset in NeighborOffsets)
            {
                Vector3 candidate = current + offset * cellSize;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, areaMask))
                    continue;

                Vector3 next = hit.position;

                if (!IsNavConnected(current, next, areaMask))
                    continue;

                if (!BattleOccupancy.IsPositionFree(next, agentRadius, ignoreOccupant))
                    continue;

                float edgeCost = offset.sqrMagnitude > 1.1f ? cellSize * 1.4142135f : cellSize;
                float newCost = cost + edgeCost;
                if (newCost > maxMeters + 0.01f)
                    continue;

                Vector2Int key = Quantize(next, cellSize);
                if (bestCost.TryGetValue(key, out float existing) && existing <= newCost + 0.001f)
                    continue;

                bestCost[key] = newCost;
                reachable.Add(next);
                queue.Enqueue((next, newCost));
            }
        }

        return reachable;
    }

    public static bool IsPointReachable(Vector3 worldPoint, HashSet<Vector3> reachable, float cellSize)
    {
        if (reachable == null || reachable.Count == 0) return false;

        Vector2Int key = Quantize(worldPoint, cellSize);
        foreach (var p in reachable)
        {
            if (Quantize(p, cellSize) == key)
                return true;
        }

        return false;
    }

    private static bool IsNavConnected(Vector3 from, Vector3 to, int areaMask)
    {
        return !NavMesh.Raycast(from, to, out _, areaMask);
    }

    private static Vector2Int Quantize(Vector3 world, float cellSize)
    {
        return new Vector2Int(
            Mathf.RoundToInt(world.x / cellSize),
            Mathf.RoundToInt(world.z / cellSize));
    }
}
