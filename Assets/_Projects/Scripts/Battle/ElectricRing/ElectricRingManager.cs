using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 电环管理 — 跟随宿主移动；敌人进入环带时造成伤害并击退。
/// </summary>
public sealed class ElectricRingManager
{
    private static ElectricRingManager instance;
    public static ElectricRingManager Instance => instance ??= new ElectricRingManager();

    private readonly List<ElectricRingInstance> rings = new List<ElectricRingInstance>();
    private bool subscribed;

    public IReadOnlyList<ElectricRingInstance> ActiveRings => rings;

    public void EnsureSubscribed()
    {
        if (subscribed) return;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void ClearAll()
    {
        for (int i = 0; i < rings.Count; i++)
            rings[i].DestroyVfx();
        rings.Clear();
    }

    public ElectricRingInstance SpawnRing(
        AbilitySystemComponent instigator,
        AbilitySystemComponent host,
        float radiusMeters,
        int durationTurns,
        float damageScaler,
        GameplayTag damageType,
        float knockbackDistanceMeters,
        float knockbackDurationSeconds,
        GameObject ringVfxPrefab,
        VfxSpawnEntry hitVfx)
    {
        EnsureSubscribed();

        if (instigator == null || host == null || radiusMeters <= 0f || durationTurns <= 0)
            return null;

        RemoveRingsOnHost(host);

        var ring = new ElectricRingInstance(
            host,
            instigator,
            radiusMeters,
            durationTurns,
            damageScaler,
            damageType,
            knockbackDistanceMeters,
            knockbackDurationSeconds,
            hitVfx);

        rings.Add(ring);
        TrySpawnRingVfx(ring, ringVfxPrefab);
        ProcessInitialContact(ring);
        return ring;
    }

    private void RemoveRingsOnHost(AbilitySystemComponent host)
    {
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            if (rings[i].Host != host) continue;
            rings[i].DestroyVfx();
            rings.RemoveAt(i);
        }
    }

    private static void TrySpawnRingVfx(ElectricRingInstance ring, GameObject prefab)
    {
        if (ring?.Host == null || prefab == null) return;

        var instance = Object.Instantiate(prefab, ring.Host.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        ring.AttachVfx(instance);
    }

    private void ProcessInitialContact(ElectricRingInstance ring)
    {
        foreach (var actor in BattleTargeting.FindAllBattleActors())
            TryApplyContact(ring, actor, null);
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        switch (evt.type)
        {
            case CombatEventType.CharacterMoved:
                if (evt.instigator != null)
                    ProcessActorMoved(evt.instigator, evt.movePathPoints);
                break;

            case CombatEventType.TurnEnded:
                if (evt.instigator != null)
                    ProcessHostTurnEnd(evt.instigator);
                break;

            case CombatEventType.CharacterKilled:
                if (evt.target != null)
                    UnregisterDeadActor(evt.target);
                break;
        }
    }

    private void ProcessActorMoved(AbilitySystemComponent actor, List<Vector3> movePathPoints)
    {
        for (int i = 0; i < rings.Count; i++)
        {
            var ring = rings[i];
            TryApplyContact(ring, actor, movePathPoints);

            if (ring.Host == actor)
                ProcessHostMoved(ring);
        }
    }

    private void ProcessHostMoved(ElectricRingInstance ring)
    {
        foreach (var actor in BattleTargeting.FindAllBattleActors())
        {
            if (actor == null || actor == ring.Host) continue;
            TryApplyContact(ring, actor, null);
        }
    }

    private void ProcessHostTurnEnd(AbilitySystemComponent actor)
    {
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var ring = rings[i];
            if (ring.Host != actor)
                continue;

            if (ring.TickHostTurnEnd())
            {
                ring.DestroyVfx();
                rings.RemoveAt(i);
            }
        }
    }

    private void UnregisterDeadActor(AbilitySystemComponent actor)
    {
        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var ring = rings[i];
            if (ring.Host == actor)
            {
                ring.DestroyVfx();
                rings.RemoveAt(i);
                continue;
            }

            ring.UnregisterActor(actor);
        }
    }

    private static void TryApplyContact(
        ElectricRingInstance ring,
        AbilitySystemComponent actor,
        List<Vector3> movePathPoints)
    {
        if (ring == null || actor == null || actor == ring.Host)
            return;
        if (!ring.CanHit(actor))
            return;

        bool endInside = ring.ContainsActor(actor);
        bool startInside = movePathPoints != null && movePathPoints.Count > 0
            ? ring.ContainsPosition(movePathPoints[0])
            : ring.IsOccupant(actor);

        bool crossedFromOutside = !startInside
            && (movePathPoints != null && movePathPoints.Count > 0
                ? ring.PathIntersects(movePathPoints)
                : endInside);

        if (!ring.IsOccupant(actor) && crossedFromOutside)
            ApplyRingHit(ring, actor);

        if (endInside)
            ring.MarkInside(actor);
        else
            ring.MarkOutside(actor);
    }

    private static void ApplyRingHit(ElectricRingInstance ring, AbilitySystemComponent target)
    {
        if (ring.Instigator?.Attributes == null || target?.Attributes == null)
            return;

        float damage = ring.DamageScaler * ring.Instigator.Attributes.Attack;
        target.Attributes.TakeDamage(damage, ring.DamageType, ring.Instigator);

        if (ring.HitVfx != null && ring.HitVfx.IsValid)
            ring.Instigator.PlayTargetEffect(target, ring.HitVfx);

        if (ring.KnockbackDistanceMeters <= 0f) return;

        var movement = target.GetComponent<CharacterMovementController>();
        movement?.TryApplyKnockback(ring.Center, ring.KnockbackDistanceMeters, ring.KnockbackDurationSeconds);
    }
}
