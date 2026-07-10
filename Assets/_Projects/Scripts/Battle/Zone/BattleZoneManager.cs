using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗领域管理 — 生成、持续回合、两类伤害结算：
/// 1) 敌人在自身回合开始时站在领域内受伤；
/// 2) 敌人从领域外进入领域内受伤（含领域刚生成时）。
/// </summary>
public sealed class BattleZoneManager
{
    private static BattleZoneManager instance;
    public static BattleZoneManager Instance => instance ??= new BattleZoneManager();

    private readonly List<BattleZoneInstance> zones = new List<BattleZoneInstance>();
    private bool subscribed;

    public IReadOnlyList<BattleZoneInstance> ActiveZones => zones;

    public void EnsureSubscribed()
    {
        if (subscribed) return;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void ClearAll()
    {
        zones.Clear();
    }

    public BattleZoneInstance SpawnZone(
        AbilitySystemComponent instigator,
        Vector3 center,
        float radiusMeters,
        int durationTurns,
        float damageScaler,
        GameplayTag damageType,
        GameplayTag zoneTag)
    {
        EnsureSubscribed();

        if (instigator == null || radiusMeters <= 0f || durationTurns <= 0)
            return null;

        var zone = new BattleZoneInstance(
            center,
            radiusMeters,
            durationTurns,
            instigator,
            damageScaler,
            damageType,
            zoneTag);

        zones.Add(zone);
        ProcessInitialEnter(zone);
        return zone;
    }

    private void ProcessInitialEnter(BattleZoneInstance zone)
    {
        foreach (var enemy in BattleTargeting.FilterEnemiesInRadius(zone.Instigator, zone.Center, zone.RadiusMeters))
            TryApplyEnterDamage(zone, enemy);
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        switch (evt.type)
        {
            case CombatEventType.TurnStarted:
                if (evt.instigator != null)
                    ProcessEnemyTurnStartInZones(evt.instigator);
                break;

            case CombatEventType.TurnEnded:
                if (evt.instigator != null)
                    ProcessInstigatorTurnEnd(evt.instigator);
                break;

            case CombatEventType.CharacterMoved:
                if (evt.instigator != null)
                    ProcessActorMovedIntoZones(evt.instigator);
                break;

            case CombatEventType.CharacterKilled:
                if (evt.target != null)
                    UnregisterDeadActor(evt.target);
                break;
        }
    }

    private void ProcessEnemyTurnStartInZones(AbilitySystemComponent actor)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            if (zone.Instigator == null || !zone.Instigator.IsEnemy(actor))
                continue;
            if (!BattleTargeting.IsAlive(actor))
                continue;
            if (!zone.ContainsActor(actor))
                continue;

            ApplyZoneDamage(zone, actor);
        }
    }

    private void ProcessActorMovedIntoZones(AbilitySystemComponent actor)
    {
        for (int i = 0; i < zones.Count; i++)
            TryApplyEnterDamage(zones[i], actor);
    }

    private void TryApplyEnterDamage(BattleZoneInstance zone, AbilitySystemComponent actor)
    {
        if (zone.Instigator == null || actor == null)
            return;
        if (!zone.Instigator.IsEnemy(actor))
            return;
        if (!BattleTargeting.IsAlive(actor))
            return;

        bool inside = zone.ContainsActor(actor);
        bool wasInside = zone.IsOccupant(actor);

        if (inside && !wasInside)
            ApplyZoneDamage(zone, actor);

        if (inside)
            zone.MarkInside(actor);
        else
            zone.MarkOutside(actor);
    }

    private void ProcessInstigatorTurnEnd(AbilitySystemComponent instigator)
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var zone = zones[i];
            if (zone.Instigator != instigator)
                continue;

            if (zone.TickInstigatorTurnEnd())
                zones.RemoveAt(i);
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
    }
}
