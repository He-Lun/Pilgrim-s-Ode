using System.Collections.Generic;
using UnityEngine;

/// <summary>跟随友方的电环 — 以宿主为圆心，进入时造成伤害并击退。</summary>
public sealed class ElectricRingInstance
{
    public AbilitySystemComponent Host { get; }
    public AbilitySystemComponent Instigator { get; }
    public float RadiusMeters { get; }
    public int RemainingTurns { get; private set; }
    public float DamageScaler { get; }
    public GameplayTag DamageType { get; }
    public float KnockbackDistanceMeters { get; }
    public float KnockbackDurationSeconds { get; }
    public VfxSpawnEntry HitVfx { get; }
    public GameObject VfxInstance { get; private set; }

    private readonly HashSet<AbilitySystemComponent> occupantsInside = new HashSet<AbilitySystemComponent>();

    public ElectricRingInstance(
        AbilitySystemComponent host,
        AbilitySystemComponent instigator,
        float radiusMeters,
        int durationTurns,
        float damageScaler,
        GameplayTag damageType,
        float knockbackDistanceMeters,
        float knockbackDurationSeconds,
        VfxSpawnEntry hitVfx)
    {
        Host = host;
        Instigator = instigator;
        RadiusMeters = radiusMeters;
        RemainingTurns = durationTurns;
        DamageScaler = damageScaler;
        DamageType = damageType;
        KnockbackDistanceMeters = knockbackDistanceMeters;
        KnockbackDurationSeconds = knockbackDurationSeconds;
        HitVfx = hitVfx;
    }

    public Vector3 Center => Host != null ? Host.transform.position : Vector3.zero;

    public bool ContainsActor(AbilitySystemComponent actor)
    {
        return actor != null && ContainsPosition(actor.transform.position);
    }

    public bool ContainsPosition(Vector3 worldPosition)
    {
        if (Host == null) return false;
        return BattleOccupancy.HorizontalDistance(Center, worldPosition) <= RadiusMeters;
    }

    public bool PathIntersects(IList<Vector3> pathPoints)
    {
        return CrossZoneUtility.PathIntersectsCircle(pathPoints, Center, RadiusMeters);
    }

    public bool CanHit(AbilitySystemComponent actor)
    {
        if (actor == null || Instigator == null || Host == null) return false;
        if (!BattleTargeting.IsAlive(actor)) return false;
        return Instigator.IsEnemy(actor);
    }

    public bool IsOccupant(AbilitySystemComponent actor)
    {
        return actor != null && occupantsInside.Contains(actor);
    }

    public void MarkInside(AbilitySystemComponent actor)
    {
        if (actor != null)
            occupantsInside.Add(actor);
    }

    public void MarkOutside(AbilitySystemComponent actor)
    {
        if (actor != null)
            occupantsInside.Remove(actor);
    }

    public void UnregisterActor(AbilitySystemComponent actor)
    {
        occupantsInside.Remove(actor);
    }

    public bool TickHostTurnEnd()
    {
        if (RemainingTurns <= 0) return true;
        RemainingTurns--;
        return RemainingTurns <= 0;
    }

    internal void AttachVfx(GameObject instance) => VfxInstance = instance;

    internal void DestroyVfx()
    {
        if (VfxInstance == null) return;
        Object.Destroy(VfxInstance);
        VfxInstance = null;
    }
}
