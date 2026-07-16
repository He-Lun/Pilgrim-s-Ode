using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能释放上下文 — 由 UI / HandCardManager 在出牌时构造，经 Facade 传入 ASC。
/// </summary>
public struct AbilityActivationContext
{
    public List<AbilitySystemComponent> explicitTargets;
    public Vector2Int direction;

    [System.Obsolete("Use targetWorldPoint for meter-based combat.")]
    public Vector2Int targetCell;

    [System.Obsolete("Use moveDistanceMeters.")]
    public int moveDistance;

    /// <summary>玩家点击的世界坐标（范围技、位移落点等）。</summary>
    public Vector3 targetWorldPoint;
    public bool hasTargetPoint;

    /// <summary>移动/突进距离（米）。0 表示由技能配置决定。</summary>
    public float moveDistanceMeters;

    /// <summary>360° 自由指向 — 水平归一化方向（DirectedRect 等）。</summary>
    public Vector3 aimDirectionWorld;
    public bool hasAimDirection;

    public static AbilityActivationContext Self()
    {
        return new AbilityActivationContext
        {
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    public static AbilityActivationContext SingleTarget(AbilitySystemComponent target)
    {
        return new AbilityActivationContext
        {
            explicitTargets = target != null
                ? new List<AbilitySystemComponent> { target }
                : new List<AbilitySystemComponent>()
        };
    }

    public static AbilityActivationContext FromTargets(List<AbilitySystemComponent> targets)
    {
        return new AbilityActivationContext
        {
            explicitTargets = targets ?? new List<AbilitySystemComponent>()
        };
    }

    public static AbilityActivationContext WithDirection(Vector2Int dir, float distanceMeters = 0f)
    {
        return new AbilityActivationContext
        {
            direction = dir,
            moveDistanceMeters = distanceMeters,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    public static AbilityActivationContext WithAimDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
            worldDirection = Vector3.forward;

        return new AbilityActivationContext
        {
            aimDirectionWorld = worldDirection.normalized,
            hasAimDirection = true,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    public static AbilityActivationContext WithTargetPoint(Vector3 worldPoint)
    {
        worldPoint = BattleTargeting.ProjectToGround(worldPoint);

        return new AbilityActivationContext
        {
            targetWorldPoint = worldPoint,
            hasTargetPoint = true,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    [System.Obsolete("Use WithTargetPoint.")]
    public static AbilityActivationContext WithTargetCell(Vector2Int cell)
    {
        return new AbilityActivationContext
        {
            targetCell = cell,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    public bool HasExplicitTargets =>
        explicitTargets != null && explicitTargets.Count > 0;

    public bool HasDirection => direction != Vector2Int.zero;

    [System.Obsolete("Use HasTargetPoint.")]
    public bool HasTargetCell => targetCell != Vector2Int.zero;

    public bool HasTargetPoint => hasTargetPoint;

    public bool HasAimDirection => hasAimDirection;

    public List<AbilitySystemComponent> GetExplicitTargets()
    {
        return explicitTargets ?? new List<AbilitySystemComponent>();
    }
}
