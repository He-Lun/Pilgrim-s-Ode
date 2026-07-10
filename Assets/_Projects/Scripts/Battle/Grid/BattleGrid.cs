using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Legacy] 战斗网格 — 移动已改由 NavMesh 直线规划。仅保留供旧工具/编辑器参考。
/// </summary>
public class BattleGrid : MonoBehaviour
{
    public static BattleGrid Instance { get; private set; }

    [Header("网格配置")]
    [SerializeField] private Vector3 origin = Vector3.zero;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int gridWidth = 20;
    [SerializeField] private int gridHeight = 20;
    [SerializeField] private bool allowDiagonal = false;

    [Header("射线采样（贴地高度）")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float raycastHeight = 50f;
    [Tooltip("射线未命中时回退到此 Y；应与场景地面基准一致")]
    [SerializeField] private float fallbackGroundY;
    [Tooltip("忽略角色碰撞体，避免互相采样到对方身上")]
    [SerializeField] private bool ignoreCharacterColliders = true;
    [Tooltip("只接受法线朝上的表面，避免打到天花板/底面")]
    [SerializeField] private float minGroundNormalY = 0.5f;

    [Header("编辑器（默认关闭）")]
    [SerializeField] private bool showGridGizmosInEditor = false;

    private readonly Dictionary<Vector2Int, GridCell> cells = new Dictionary<Vector2Int, GridCell>();

    public float CellSize => cellSize;
    public bool AllowDiagonal => allowDiagonal;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeCells();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void InitializeCells()
    {
        cells.Clear();
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var coord = new Vector2Int(x, y);
                cells[coord] = new GridCell { coordinate = coord, walkable = true, elevation = 0 };
            }
        }
    }

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight;
    }

    public GridCell GetCell(Vector2Int cell)
    {
        if (!cells.TryGetValue(cell, out var data))
        {
            data = new GridCell { coordinate = cell, walkable = false };
            cells[cell] = data;
        }

        return data;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        var local = worldPosition - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 CellToWorld(Vector2Int cell, bool center = true)
    {
        float x = cell.x * cellSize;
        float z = cell.y * cellSize;
        if (center)
        {
            x += cellSize * 0.5f;
            z += cellSize * 0.5f;
        }

        var gridCell = GetCell(cell);
        float elevationOffset = gridCell.elevation * cellSize * 0.5f;
        var xz = origin + new Vector3(x, 0f, z);

        float groundY = SampleGroundY(xz) + elevationOffset;
        return new Vector3(xz.x, groundY, xz.z);
    }

    /// <summary>从指定 XZ 向下射线取样地面高度。</summary>
    public float SampleGroundY(Vector3 worldXZ)
    {
        float rayStartY = origin.y + raycastHeight;
        var rayOrigin = new Vector3(worldXZ.x, rayStartY, worldXZ.z);
        float maxDistance = raycastHeight * 2f;

        var hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxDistance, groundLayer, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (ignoreCharacterColliders && IsCharacterCollider(hit.collider))
                continue;
            if (hit.normal.y < minGroundNormalY)
                continue;

            return hit.point.y;
        }

        return fallbackGroundY != 0f ? fallbackGroundY : origin.y;
    }

    private static bool IsCharacterCollider(Collider collider)
    {
        return collider != null
               && collider.GetComponentInParent<CharacterMovementController>() != null;
    }

    void OnValidate()
    {
        if (fallbackGroundY == 0f)
            fallbackGroundY = origin.y;
    }

    public void SetWalkable(Vector2Int cell, bool walkable)
    {
        if (!IsInBounds(cell)) return;
        GetCell(cell).walkable = walkable;
    }

    public void SetElevation(Vector2Int cell, int elevation)
    {
        if (!IsInBounds(cell)) return;
        GetCell(cell).elevation = elevation;
    }

    public int GetElevation(Vector2Int cell)
    {
        return IsInBounds(cell) ? GetCell(cell).elevation : 0;
    }

    [System.Obsolete("Use NavStraightMovementPlanner.")]
    public void RegisterOccupant(Vector2Int cell, CharacterMovementController controller)
    {
        if (!IsInBounds(cell)) return;
        GetCell(cell).occupant = controller;
    }

    [System.Obsolete("Use NavStraightMovementPlanner.")]
    public void ClearOccupant(Vector2Int cell, CharacterMovementController controller)
    {
        if (!IsInBounds(cell)) return;
        var data = GetCell(cell);
        if (data.occupant == controller)
            data.occupant = null;
    }

    [System.Obsolete("Use NavStraightMovementPlanner.")]
    public bool TryFindPath(Vector2Int start, Vector2Int goal, int maxCost, out List<Vector2Int> path)
    {
        return GridPathfinder.TryFindPath(this, start, goal, maxCost, allowDiagonal, out path);
    }

    [System.Obsolete("Use NavStraightMovementPlanner.")]
    public HashSet<Vector2Int> GetReachableCells(Vector2Int start, int maxCost)
    {
        return GridPathfinder.GetReachableCells(this, start, maxCost, allowDiagonal);
    }

    void OnDrawGizmosSelected()
    {
        if (!showGridGizmosInEditor || cellSize <= 0f) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var cell = new Vector2Int(x, y);
                var center = CellToWorld(cell);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.05f, cellSize));
            }
        }
    }
}
