using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 圣骨被动运行时追踪 — 释放技能后 N 个自身回合内，友方攻击敌人时在自身普攻范围内追加一次普攻。
/// </summary>
public class SacredBonePassiveTracker
{
    private AbilitySystemComponent owner;
    private GameplayAbility basicAttack;
    private int windowTurnsTotal;
    private int windowTurnsRemaining;
    private bool isSubscribed;
    private bool suppressOwnerAbilityWindowRefresh;

    private AbilitySystemComponent pendingAlly;
    private readonly HashSet<AbilitySystemComponent> pendingHitEnemies = new HashSet<AbilitySystemComponent>();

    public int WindowTurnsRemaining => windowTurnsRemaining;
    public bool IsWindowActive => windowTurnsRemaining > 0;

    public void Initialize(
        AbilitySystemComponent asc,
        GameplayAbility followUpBasicAttack,
        int windowTurns = 3)
    {
        Dispose();

        owner = asc;
        basicAttack = followUpBasicAttack ?? ResolveBasicAttack(asc);
        windowTurnsTotal = Mathf.Max(1, windowTurns);
        windowTurnsRemaining = 0;

        if (owner == null || basicAttack == null)
            return;

        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        isSubscribed = true;
    }

    public void Dispose()
    {
        EndAllyActionWatch();

        if (isSubscribed)
        {
            CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
            isSubscribed = false;
        }

        owner = null;
        basicAttack = null;
        windowTurnsRemaining = 0;
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (owner == null || basicAttack == null)
            return;

        switch (evt.type)
        {
            case CombatEventType.AbilityUsed:
                HandleAbilityUsed(evt);
                break;
            case CombatEventType.DamageDealt:
                HandleDamageDealt(evt);
                break;
            case CombatEventType.TurnEnded:
                HandleTurnEnded(evt);
                break;
        }
    }

    private void HandleAbilityUsed(CombatEvent evt)
    {
        if (evt.instigator == owner)
        {
            if (suppressOwnerAbilityWindowRefresh)
                return;

            if (!RefreshesSacredBoneWindow(evt.ability))
                return;

            windowTurnsRemaining = windowTurnsTotal;
            return;
        }

        if (!IsWindowActive)
            return;

        if (evt.instigator == null || !owner.IsAlly(evt.instigator))
            return;

        BeginAllyActionWatch(evt.instigator);
    }

    private void HandleDamageDealt(CombatEvent evt)
    {
        if (!IsWindowActive || pendingAlly == null)
            return;

        if (evt.instigator != pendingAlly)
            return;

        var target = evt.target;
        if (target == null || !owner.IsEnemy(target))
            return;

        if (!IsValidFollowUpTarget(target))
            return;

        pendingHitEnemies.Add(target);
    }

    private void HandleTurnEnded(CombatEvent evt)
    {
        if (evt.target != owner)
            return;

        if (windowTurnsRemaining > 0)
            windowTurnsRemaining--;
    }

    private void BeginAllyActionWatch(AbilitySystemComponent ally)
    {
        if (ally == null)
            return;

        if (pendingAlly == ally)
        {
            pendingHitEnemies.Clear();
            return;
        }

        EndAllyActionWatch();
        pendingAlly = ally;
        pendingHitEnemies.Clear();
        pendingAlly.OnAbilityActivationEnded += OnPendingAllyAbilityEnded;
    }

    private void EndAllyActionWatch()
    {
        if (pendingAlly != null)
            pendingAlly.OnAbilityActivationEnded -= OnPendingAllyAbilityEnded;

        pendingAlly = null;
        pendingHitEnemies.Clear();
    }

    private void OnPendingAllyAbilityEnded(GameplayAbility _)
    {
        TryEnqueueFollowUp();
        EndAllyActionWatch();
    }

    private void TryEnqueueFollowUp()
    {
        if (!IsWindowActive || owner == null || basicAttack == null || pendingHitEnemies.Count == 0)
            return;

        if (owner.Attributes != null && owner.Attributes.IsDead())
            return;

        var target = PickNearestEnemy(pendingHitEnemies);
        if (target == null)
            return;

        var context = AbilityActivationContext.SingleTarget(target);
        TurnManager.Instance?.PushInsert(new PendingAction(
            owner,
            basicAttack,
            context,
            InsertPriority.FollowUp));

        suppressOwnerAbilityWindowRefresh = true;
        TurnManager.Instance?.NotifyActionResolved();
        suppressOwnerAbilityWindowRefresh = false;
    }

    private AbilitySystemComponent PickNearestEnemy(IEnumerable<AbilitySystemComponent> candidates)
    {
        AbilitySystemComponent best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = owner.transform.position;

        foreach (var candidate in candidates)
        {
            if (!IsValidFollowUpTarget(candidate))
                continue;

            float distance = BattleTargeting.HorizontalDistance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    private bool IsValidFollowUpTarget(AbilitySystemComponent target)
    {
        if (target == null || target == owner)
            return false;

        if (!owner.IsEnemy(target))
            return false;

        if (target.Attributes == null || target.Attributes.IsDead())
            return false;

        return BattleTargeting.IsValidAbilityTarget(owner, target, basicAttack);
    }

    private bool RefreshesSacredBoneWindow(GameplayAbility ability)
    {
        if (ability == null || owner == null)
            return false;

        if (IsOwnerPassiveAbility(ability))
            return false;

        if (ability == owner.InspirationAbility)
            return false;

        if (ability.abilityName == "普通攻击")
            return false;

        return true;
    }

    private bool IsOwnerPassiveAbility(GameplayAbility ability)
    {
        if (ability == null || owner == null)
            return false;

        var passives = owner.PassiveAbilities;
        if (passives == null)
            return false;

        for (int i = 0; i < passives.Count; i++)
        {
            if (passives[i] == ability)
                return true;
        }

        return false;
    }

    private static GameplayAbility ResolveBasicAttack(AbilitySystemComponent asc)
    {
        if (asc?.CharacterData == null)
            return null;

        var pool = asc.CharacterData.jobAbilityPool;
        if (pool?.abilities == null)
            return null;

        for (int i = 0; i < pool.abilities.Count; i++)
        {
            var ability = pool.abilities[i];
            if (ability != null && ability.abilityName == "普通攻击")
                return ability;
        }

        return null;
    }
}
