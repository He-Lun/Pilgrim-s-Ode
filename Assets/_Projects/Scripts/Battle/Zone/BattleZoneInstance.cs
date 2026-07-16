using System.Collections.Generic;
using UnityEngine;

/// <summary>持续领域几何形状。</summary>
public enum BattleZoneShape
{
    Circle = 0,
    /// <summary>有限长度十字（两臂正交矩形）。</summary>
    Cross = 1
}

/// <summary>领域伤害对象筛选。</summary>
public enum BattleZoneHitFilter
{
    EnemiesOnly = 0,
    /// <summary>除施法者外所有存活单位（含友方）。</summary>
    AllExceptInstigator = 1,
    Everyone = 2
}

/// <summary>
/// 运行时战斗领域 — Circle / Cross；追踪占用用于“进入”判定。
/// </summary>
public class BattleZoneInstance
{
    public BattleZoneShape Shape { get; }
    public Vector3 Center { get; }
    public float RadiusMeters { get; }
    public Vector3 Forward { get; }
    public float ArmHalfLengthMeters { get; }
    public float ArmWidthMeters { get; }
    public BattleZoneHitFilter HitFilter { get; }
    public int RemainingTurns { get; private set; }
    public AbilitySystemComponent Instigator { get; }
    public float DamageScaler { get; }
    public GameplayTag DamageType { get; }
    public GameplayTag ZoneTag { get; }
    public VfxSpawnEntry HitVfx { get; }
    public GameObject VfxInstance { get; private set; }

    private readonly HashSet<AbilitySystemComponent> occupantsInside = new HashSet<AbilitySystemComponent>();

    public BattleZoneInstance(
        BattleZoneShape shape,
        Vector3 center,
        float radiusMeters,
        Vector3 forward,
        float armHalfLengthMeters,
        float armWidthMeters,
        BattleZoneHitFilter hitFilter,
        int durationTurns,
        AbilitySystemComponent instigator,
        float damageScaler,
        GameplayTag damageType,
        GameplayTag zoneTag,
        VfxSpawnEntry hitVfx = null)
    {
        Shape = shape;
        Center = center;
        RadiusMeters = radiusMeters;
        Forward = Flatten(forward);
        ArmHalfLengthMeters = armHalfLengthMeters;
        ArmWidthMeters = armWidthMeters;
        HitFilter = hitFilter;
        RemainingTurns = durationTurns;
        Instigator = instigator;
        DamageScaler = damageScaler;
        DamageType = damageType;
        ZoneTag = zoneTag;
        HitVfx = hitVfx;
    }

    public float QueryRadiusMeters
    {
        get
        {
            if (Shape == BattleZoneShape.Cross)
                return CrossZoneUtility.BoundingRadiusMeters(ArmHalfLengthMeters, ArmWidthMeters);
            return RadiusMeters;
        }
    }

    public bool ContainsPosition(Vector3 worldPosition)
    {
        if (Shape == BattleZoneShape.Cross)
        {
            return CrossZoneUtility.ContainsPoint(
                Center, Forward, ArmHalfLengthMeters, ArmWidthMeters, worldPosition);
        }

        return BattleOccupancy.HorizontalDistance(Center, worldPosition) <= RadiusMeters;
    }

    public bool ContainsActor(AbilitySystemComponent asc)
    {
        return asc != null && ContainsPosition(asc.transform.position);
    }

    /// <summary>折线路径是否穿过本领域（不要求终点落在内）。</summary>
    public bool PathIntersects(IList<Vector3> pathPoints)
    {
        if (pathPoints == null || pathPoints.Count == 0)
            return false;

        if (Shape == BattleZoneShape.Cross)
        {
            return CrossZoneUtility.PathIntersects(
                pathPoints, Center, Forward, ArmHalfLengthMeters, ArmWidthMeters);
        }

        return CrossZoneUtility.PathIntersectsCircle(pathPoints, Center, RadiusMeters);
    }

    public bool CanHit(AbilitySystemComponent actor)
    {
        if (actor == null || Instigator == null)
            return false;
        if (!BattleTargeting.IsAlive(actor))
            return false;

        switch (HitFilter)
        {
            case BattleZoneHitFilter.Everyone:
                return true;
            case BattleZoneHitFilter.AllExceptInstigator:
                return actor != Instigator;
            default:
                return Instigator.IsEnemy(actor);
        }
    }

    public bool IsOccupant(AbilitySystemComponent asc)
    {
        return asc != null && occupantsInside.Contains(asc);
    }

    public void MarkInside(AbilitySystemComponent asc)
    {
        if (asc != null)
            occupantsInside.Add(asc);
    }

    public void MarkOutside(AbilitySystemComponent asc)
    {
        if (asc != null)
            occupantsInside.Remove(asc);
    }

    public void UnregisterActor(AbilitySystemComponent asc)
    {
        occupantsInside.Remove(asc);
    }

    public bool TickInstigatorTurnEnd()
    {
        if (RemainingTurns <= 0)
            return true;

        RemainingTurns--;
        return RemainingTurns <= 0;
    }

    internal void AttachVfx(GameObject instance) => VfxInstance = instance;

    /// <summary>无 Animator 领域：到期立即销毁（粒子 Duration 可设很大）。</summary>
    internal void DestroyVfx(bool immediate = true)
    {
        if (VfxInstance == null) return;

        if (immediate)
            WorldVfxSpawner.DestroyInstance(VfxInstance);
        else
            WorldVfxSpawner.BeginExpire(VfxInstance);

        VfxInstance = null;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }
}
