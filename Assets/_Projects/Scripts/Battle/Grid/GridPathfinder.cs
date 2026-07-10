using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Legacy] 网格 A* 寻路 — 已由 NavStraightMovementPlanner 取代。
/// </summary>
[System.Obsolete("Use NavStraightMovementPlanner for BG3-style straight movement.")]
public static class GridPathfinder
{
    private static readonly Vector2Int[] Neighbors4 =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly Vector2Int[] Neighbors8 =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    /// <summary>
    /// 从 start 到 goal 寻路。goal 可被占用（终点除外自身占位校验由调用方负责）。
    /// </summary>
    public static bool TryFindPath(
        BattleGrid grid,
        Vector2Int start,
        Vector2Int goal,
        int maxCost,
        bool allowDiagonal,
        out List<Vector2Int> path)
    {
        path = null;
        if (grid == null || maxCost < 0) return false;
        if (start == goal)
        {
            path = new List<Vector2Int> { start };
            return true;
        }

        if (!grid.IsInBounds(start) || !grid.IsInBounds(goal)) return false;
        if (!grid.GetCell(goal).walkable) return false;

        var openSet = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

        var neighbors = allowDiagonal ? Neighbors8 : Neighbors4;

        while (openSet.Count > 0)
        {
            Vector2Int current = PopLowestF(openSet, gScore, goal);

            if (current == goal)
            {
                path = ReconstructPath(cameFrom, current);
                return GetPathCost(path, allowDiagonal) <= maxCost;
            }

            openSet.Remove(current);

            foreach (var offset in neighbors)
            {
                Vector2Int next = current + offset;
                if (!grid.IsInBounds(next)) continue;

                var cell = grid.GetCell(next);
                if (!cell.walkable) continue;

                bool isGoal = next == goal;
                if (cell.IsOccupied && !isGoal) continue;

                int stepCost = offset.x != 0 && offset.y != 0 ? 2 : 1;
                int tentative = gScore[current] + stepCost;
                if (tentative > maxCost) continue;

                if (gScore.TryGetValue(next, out int existing) && tentative >= existing)
                    continue;

                cameFrom[next] = current;
                gScore[next] = tentative;
                if (!openSet.Contains(next))
                    openSet.Add(next);
            }
        }

        return false;
    }

    /// <summary>
    /// BFS 可达格（用于移动范围高亮），返回从 start 出发 cost 以内的所有格。
    /// </summary>
    public static HashSet<Vector2Int> GetReachableCells(
        BattleGrid grid,
        Vector2Int start,
        int maxCost,
        bool allowDiagonal)
    {
        var result = new HashSet<Vector2Int>();
        if (grid == null || maxCost < 0 || !grid.IsInBounds(start)) return result;

        var neighbors = allowDiagonal ? Neighbors8 : Neighbors4;
        var queue = new Queue<(Vector2Int cell, int cost)>();
        var visited = new Dictionary<Vector2Int, int>();

        queue.Enqueue((start, 0));
        visited[start] = 0;
        result.Add(start);

        while (queue.Count > 0)
        {
            var (current, cost) = queue.Dequeue();
            if (cost >= maxCost) continue;

            foreach (var offset in neighbors)
            {
                Vector2Int next = current + offset;
                if (!grid.IsInBounds(next)) continue;

                var cell = grid.GetCell(next);
                if (!cell.walkable || cell.IsOccupied) continue;

                int stepCost = offset.x != 0 && offset.y != 0 ? 2 : 1;
                int newCost = cost + stepCost;
                if (newCost > maxCost) continue;

                if (visited.TryGetValue(next, out int prev) && prev <= newCost)
                    continue;

                visited[next] = newCost;
                result.Add(next);
                queue.Enqueue((next, newCost));
            }
        }

        return result;
    }

    private static Vector2Int PopLowestF(List<Vector2Int> openSet, Dictionary<Vector2Int, int> gScore, Vector2Int goal)
    {
        Vector2Int best = openSet[0];
        int bestF = int.MaxValue;

        foreach (var cell in openSet)
        {
            int g = gScore[cell];
            int f = g + Heuristic(cell, goal);
            if (f < bestF)
            {
                bestF = f;
                best = cell;
            }
        }

        return best;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public static int GetPathCost(List<Vector2Int> path, bool allowDiagonal)
    {
        if (path == null || path.Count < 2) return 0;

        int cost = 0;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int delta = path[i] - path[i - 1];
            cost += delta.x != 0 && delta.y != 0 ? 2 : 1;
        }

        return cost;
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    /// <summary>去掉同一直线上的中间格，只保留拐点（BG3 式路径视觉简化，逻辑代价不变）。</summary>
    public static List<Vector2Int> SimplifyCollinear(List<Vector2Int> path)
    {
        if (path == null || path.Count <= 2)
            return path;

        var result = new List<Vector2Int> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector2Int prevDir = path[i] - path[i - 1];
            Vector2Int nextDir = path[i + 1] - path[i];
            if (prevDir != nextDir)
                result.Add(path[i]);
        }

        result.Add(path[path.Count - 1]);
        return result;
    }
}
