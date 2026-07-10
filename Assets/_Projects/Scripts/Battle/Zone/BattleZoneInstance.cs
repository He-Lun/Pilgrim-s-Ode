using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时战斗领域 — 固定圆心 + 半径，追踪圈内占用用于“进入”判定。
/// </summary>
public class BattleZoneInstance
{
    public Vector3 Center { get; }
    public float RadiusMeters { get; }
    public int RemainingTurns { get; private set; }
    public AbilitySystemComponent Instigator { get; }
    public float DamageScaler { get; }
    public GameplayTag DamageType { get; }
    public GameplayTag ZoneTag { get; }

    private readonly HashSet<AbilitySystemComponent> occupantsInside = new HashSet<AbilitySystemComponent>();

    public BattleZoneInstance(
        Vector3 center,
        float radiusMeters,
        int durationTurns,
        AbilitySystemComponent instigator,
        float damageScaler,
        GameplayTag damageType,
        GameplayTag zoneTag)
    {
        Center = center;
        RadiusMeters = radiusMeters;
        RemainingTurns = durationTurns;
        Instigator = instigator;
        DamageScaler = damageScaler;
        DamageType = damageType;
        ZoneTag = zoneTag;
    }

    public bool ContainsPosition(Vector3 worldPosition)
    {
        return BattleOccupancy.HorizontalDistance(Center, worldPosition) <= RadiusMeters;
    }

    public bool ContainsActor(AbilitySystemComponent asc)
    {
        return asc != null && ContainsPosition(asc.transform.position);
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
}
