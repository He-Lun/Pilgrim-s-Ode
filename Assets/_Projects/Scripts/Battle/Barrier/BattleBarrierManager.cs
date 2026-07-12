using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 屏障管理 — 生成、持续回合、世界特效；攻击路径穿过屏障时对受击方减伤。
/// </summary>
public sealed class BattleBarrierManager
{
    private static BattleBarrierManager instance;
    public static BattleBarrierManager Instance => instance ??= new BattleBarrierManager();

    private readonly List<BattleBarrierInstance> barriers = new List<BattleBarrierInstance>();
    private BuffPresentationCatalog presentationCatalog;
    private bool subscribed;

    public IReadOnlyList<BattleBarrierInstance> ActiveBarriers => barriers;

    public void BindCatalog(BuffPresentationCatalog catalog) => presentationCatalog = catalog;

    public void EnsureSubscribed()
    {
        if (subscribed) return;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void ClearAll()
    {
        for (int i = 0; i < barriers.Count; i++)
            barriers[i].DestroyVfx(immediate: true);
        barriers.Clear();
    }

    public BattleBarrierInstance SpawnBarrier(
        AbilitySystemComponent instigator,
        Vector3 center,
        Vector3 forward,
        float widthMeters,
        float thicknessMeters,
        float damageReduction,
        bool protectAlliesOnly,
        int durationTurns,
        GameplayTag barrierTag)
    {
        EnsureSubscribed();

        if (instigator == null || widthMeters <= 0f || durationTurns <= 0)
            return null;

        var barrier = new BattleBarrierInstance(
            center,
            forward,
            widthMeters,
            thicknessMeters,
            damageReduction,
            protectAlliesOnly,
            durationTurns,
            instigator,
            barrierTag);

        barriers.Add(barrier);
        TrySpawnBarrierVfx(barrier);
        return barrier;
    }

    public float MitigateDamage(AbilitySystemComponent attacker, AbilitySystemComponent victim, float damage)
    {
        if (damage <= 0f || attacker == null || victim == null)
            return damage;

        float bestReduction = 0f;
        for (int i = 0; i < barriers.Count; i++)
        {
            var barrier = barriers[i];
            if (!barrier.AppliesTo(attacker, victim))
                continue;
            bestReduction = Mathf.Max(bestReduction, barrier.DamageReduction);
        }

        return damage * (1f - bestReduction);
    }

    private void TrySpawnBarrierVfx(BattleBarrierInstance barrier)
    {
        if (presentationCatalog == null || barrier == null)
            return;
        if (!presentationCatalog.TryGet(barrier.BarrierTag, out var entry))
            return;

        var instance = WorldVfxSpawner.SpawnPersistent(entry, barrier.Center, barrier.Forward);
        if (instance != null)
            barrier.AttachVfx(instance);
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (evt.type == CombatEventType.TurnEnded && evt.instigator != null)
            ProcessInstigatorTurnEnd(evt.instigator);
    }

    private void ProcessInstigatorTurnEnd(AbilitySystemComponent instigator)
    {
        for (int i = barriers.Count - 1; i >= 0; i--)
        {
            var barrier = barriers[i];
            if (barrier.Instigator != instigator)
                continue;

            if (barrier.TickInstigatorTurnEnd())
            {
                barrier.DestroyVfx();
                barriers.RemoveAt(i);
            }
        }
    }
}
