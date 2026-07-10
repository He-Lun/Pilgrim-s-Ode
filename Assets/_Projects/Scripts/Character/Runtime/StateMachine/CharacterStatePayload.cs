using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态切换时携带的上下文数据。
/// </summary>
public struct CharacterStatePayload
{
    public List<Vector2Int> path;
    public int pathIndex;
    public List<Vector3> worldWaypoints;
    public Vector3 worldDestination;
    public float moveCostMeters;
    public bool isWorldMove;
    public GameplayAbility ability;
    public AbilityActivationContext abilityContext;

    public Vector3 knockbackFromCenter;
    public float knockbackDistance;
    public float knockbackDuration;

    public static CharacterStatePayload ForMove(List<Vector2Int> movePath, int startIndex = 0)
    {
        return new CharacterStatePayload { path = movePath, pathIndex = startIndex };
    }

    public static CharacterStatePayload ForWorldMove(Vector3 destination, float costMeters)
    {
        return new CharacterStatePayload
        {
            worldDestination = destination,
            moveCostMeters = costMeters,
            isWorldMove = true,
            worldWaypoints = new List<Vector3> { destination }
        };
    }

    public static CharacterStatePayload ForWorldPath(List<Vector3> waypoints, float costMeters)
    {
        return new CharacterStatePayload
        {
            worldWaypoints = waypoints,
            worldDestination = waypoints != null && waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : default,
            moveCostMeters = costMeters,
            isWorldMove = true
        };
    }

    public static CharacterStatePayload ForAbility(GameplayAbility ab, AbilityActivationContext ctx)
    {
        return new CharacterStatePayload
        {
            ability = ab,
            abilityContext = ctx
        };
    }

    public static CharacterStatePayload ForKnockback(Vector3 fromCenter, float distanceMeters, float durationSeconds)
    {
        return new CharacterStatePayload
        {
            knockbackFromCenter = fromCenter,
            knockbackDistance = distanceMeters,
            knockbackDuration = durationSeconds
        };
    }
}
