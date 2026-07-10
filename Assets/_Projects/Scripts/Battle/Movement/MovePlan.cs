using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NavMesh 移动规划结果（含绕障路径拐点）。
/// </summary>
public struct MovePlan
{
    public bool isValid;
    public Vector3 destination;
    public float costMeters;
    public MoveResult result;
    public List<Vector3> pathPoints;

    public static MovePlan Invalid(MoveResult reason)
    {
        return new MovePlan { isValid = false, result = reason };
    }

    public static MovePlan Ready(Vector3 destination, float costMeters, List<Vector3> pathPoints = null)
    {
        return new MovePlan
        {
            isValid = true,
            destination = destination,
            costMeters = costMeters,
            result = MoveResult.Success,
            pathPoints = pathPoints ?? new List<Vector3> { destination }
        };
    }
}
