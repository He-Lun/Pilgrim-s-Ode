using UnityEngine;

/// <summary>
/// 单个格子的运行时数据。
/// </summary>
public class GridCell
{
    public Vector2Int coordinate;
    public bool walkable = true;
    public int elevation;
    public CharacterMovementController occupant;

    public bool IsOccupied => occupant != null;
    public bool IsWalkable => walkable && !IsOccupied;
}
