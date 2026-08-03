using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗领域管理 — 生成、持续回合、世界特效；伤害：
/// 1) 回合开始仍在领域内；
/// 2) 从领域外进入领域内（含刚生成时已在内）。
/// </summary>
public sealed class BattleZoneManager
{
    private static BattleZoneManager instance;
    public static BattleZoneManager Instance => instance ??= new BattleZoneManager();

    private readonly List<BattleZoneInstance> zones = new List<BattleZoneInstance>();
    private BuffPresentationCatalog presentationCatalog;
    private bool subscribed;

    public IReadOnlyList<BattleZoneInstance> ActiveZones => zones;

    public void BindCatalog(BuffPresentationCatalog catalog) => presentationCatalog = catalog;

    public void EnsureSubscribed()
    {
        if (subscribed) return;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void ClearAll()
    {
        for (int i = 0; i < zones.Count; i++)
            zones[i].DestroyVfx(immediate: true);
        zones.Clear();
    }

    /// <summary>圆形领域（兼容旧调用）。</summary>
    public BattleZoneInstance SpawnZone(
        AbilitySystemComponent instigator,
        Vector3 center,
        float radiusMeters,
        int durationTurns,
        float damageScaler,
        GameplayTag damageType,
        GameplayTag zoneTag)
    {
        return SpawnZone(
            instigator,
            center,
            BattleZoneShape.Circle,
            radiusMeters,
            Vector3.forward,
            0f,
            0f,
            BattleZoneHitFilter.EnemiesOnly,
            durationTurns,
            damageScaler,
            damageType,
            zoneTag,
            null);
    }

    public BattleZoneInstance SpawnZone(
        AbilitySystemComponent instigator,
        Vector3 center,
        BattleZoneShape shape,
        float radiusMeters,
        Vector3 forward,
        float armHalfLengthMeters,
        float armWidthMeters,
        BattleZoneHitFilter hitFilter,
        int durationTurns,
        float damageScaler,
        GameplayTag damageType,
        GameplayTag zoneTag,
        GameObject persistentVfxOverride = null,
        VfxSpawnEntry hitVfx = null)
    {
        EnsureSubscribed();

        if (instigator == null || durationTurns <= 0)
            return null;

        if (shape == BattleZoneShape.Circle && radiusMeters <= 0f)
            return null;

        if (shape == BattleZoneShape.Cross
            && (armHalfLengthMeters <= 0f || armWidthMeters <= 0f))
            return null;

        var zone = new BattleZoneInstance(
            shape,
            center,
            radiusMeters,
            forward,
            armHalfLengthMeters,
            armWidthMeters,
            hitFilter,
            durationTurns,
            instigator,
            damageScaler,
            damageType,
            zoneTag,
            hitVfx);

        zones.Add(zone);
        TrySpawnZoneVfx(zone, persistentVfxOverride);
        ProcessInitialEnter(zone);
        return zone;
    }

    private void TrySpawnZoneVfx(BattleZoneInstance zone, GameObject persistentVfxOverride)
    {
        if (zone == null) return;

        GameObject instance = null;
        if (persistentVfxOverride != null)
        {
            instance = Object.Instantiate(
                persistentVfxOverride,
                zone.Center,
                Quaternion.LookRotation(zone.Forward, Vector3.up));
        }
        else if (presentationCatalog != null
                 && presentationCatalog.TryGet(zone.ZoneTag, out var entry))
        {
            instance = WorldVfxSpawner.SpawnPersistent(entry, zone.Center, zone.Forward);
        }

        if (instance != null)
            zone.AttachVfx(instance);
    }

    private void ProcessInitialEnter(BattleZoneInstance zone)
    {
        float query = zone.QueryRadiusMeters;
        if (query <= 0f) return;

        foreach (var actor in BattleTargeting.FindAbilitySystemsInRadius(zone.Center, query))
        {
            if (!zone.CanHit(actor) || !zone.ContainsActor(actor))
                continue;

            ApplyZoneDamage(zone, actor);
            zone.MarkInside(actor);
        }
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        switch (evt.type)
        {
            case CombatEventType.TurnStarted:
                if (evt.instigator != null)
                    ProcessTurnStartInZones(evt.instigator);
                break;

            case CombatEventType.TurnEnded:
                if (evt.instigator != null)
                    ProcessInstigatorTurnEnd(evt.instigator);
                break;

            case CombatEventType.CharacterMoved:
                if (evt.instigator != null)
                    ProcessActorMovedIntoZones(evt.instigator, evt.movePathPoints);
                break;

            case CombatEventType.CharacterKilled:
                if (evt.target != null)
                    UnregisterDeadActor(evt.target);
                break;
        }
    }

    private void ProcessTurnStartInZones(AbilitySystemComponent actor)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            if (!zone.CanHit(actor))
                continue;
            if (!zone.ContainsActor(actor))
                continue;

            ApplyZoneDamage(zone, actor);
        }
    }

    /// <summary>
    /// 移动中每一小段检测：从外穿入领域则立刻受伤（触发受击打断跑步）。
    /// </summary>
    /// <returns>是否因穿入造成了伤害。</returns>
    public bool TryProcessMovementSegment(AbilitySystemComponent actor, Vector3 from, Vector3 to)
    {
        if (actor == null || zones.Count == 0)
            return false;

        bool damaged = false;
        var segment = new List<Vector3>(2) { from, to };

        for (int i = 0; i < zones.Count; i++)
        {
            if (TryApplyEnterDamage(zones[i], actor, segment))
                damaged = true;
        }

        return damaged;
    }

    private void ProcessActorMovedIntoZones(AbilitySystemComponent actor, List<Vector3> movePathPoints)
    {
        for (int i = 0; i < zones.Count; i++)
            TryApplyEnterDamage(zones[i], actor, movePathPoints);
    }

    /// <returns>是否造成了进入伤害。</returns>
    private bool TryApplyEnterDamage(
        BattleZoneInstance zone,
        AbilitySystemComponent actor,
        List<Vector3> movePathPoints)
    {
        if (zone == null || actor == null)
            return false;
        if (!zone.CanHit(actor))
            return false;

        bool endInside = zone.ContainsActor(actor);
        bool startInside = ResolveStartInside(zone, actor, movePathPoints);
        bool wasOccupant = zone.IsOccupant(actor);

        // 起点在外且路径穿过；若途中已结算过则 wasOccupant=true，避免移动结束重复伤害
        bool pathCrossedFromOutside = !startInside
            && PathHitsZone(zone, actor, movePathPoints, endInside);

        bool damaged = false;
        if (!wasOccupant && pathCrossedFromOutside)
        {
            ApplyZoneDamage(zone, actor);
            damaged = true;
        }

        if (endInside)
            zone.MarkInside(actor);
        else
            zone.MarkOutside(actor);

        return damaged;
    }

    private static bool ResolveStartInside(
        BattleZoneInstance zone,
        AbilitySystemComponent actor,
        List<Vector3> movePathPoints)
    {
        if (movePathPoints != null && movePathPoints.Count > 0)
            return zone.ContainsPosition(movePathPoints[0]);

        // 无路径时退回占用表（旧行为）
        return zone.IsOccupant(actor);
    }

    private static bool PathHitsZone(
        BattleZoneInstance zone,
        AbilitySystemComponent actor,
        List<Vector3> movePathPoints,
        bool endInside)
    {
        if (movePathPoints != null && movePathPoints.Count > 0)
            return zone.PathIntersects(movePathPoints);

        return endInside;
    }

    private void ProcessInstigatorTurnEnd(AbilitySystemComponent instigator)
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var zone = zones[i];
            if (zone.Instigator != instigator)
                continue;

            if (zone.TickInstigatorTurnEnd())
            {
                // 回合耗尽：立刻拆特效（配合粒子 Duration=9999 的持续激光）
                zone.DestroyVfx(immediate: true);
                zones.RemoveAt(i);
            }
        }
    }

    private void UnregisterDeadActor(AbilitySystemComponent actor)
    {
        for (int i = 0; i < zones.Count; i++)
            zones[i].UnregisterActor(actor);
    }

    private static void ApplyZoneDamage(BattleZoneInstance zone, AbilitySystemComponent target)
    {
        if (zone.Instigator?.Attributes == null || target?.Attributes == null)
            return;

        float damage = zone.DamageScaler * zone.Instigator.Attributes.Attack;
        target.Attributes.TakeDamage(damage, zone.DamageType, zone.Instigator);

        if (zone.HitVfx != null && zone.HitVfx.IsValid)
            zone.Instigator.PlayTargetEffect(target, zone.HitVfx);
    }
}
