using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 表现层联机中继：Server 下发演出指令，Client 重放到本地表现系统。
/// 数值走 NetworkBattleState 快照，这里只管动画、特效、受击、死亡。
/// </summary>
public static class BattleNetworkPresentation
{
    /// <summary>Client 上有对应表现订阅者的事件才转发。</summary>
    public static bool ShouldRelay(CombatEventType type)
    {
        switch (type)
        {
            case CombatEventType.AbilityUsed:
            case CombatEventType.DamageTaken:
            case CombatEventType.HealApplied:
            case CombatEventType.HealthCostApplied:
            case CombatEventType.CharacterKilled:
            case CombatEventType.MoonSoulChanged:
            // Client 上 ApplyBuffTo 被结算门禁挡住，Buff 持续特效靠转发补。
            case CombatEventType.BuffApplied:
            // 领域/屏障倒数依赖回合事件，不转发 Client 上不会过期。
            case CombatEventType.TurnStarted:
            case CombatEventType.TurnEnded:
                return true;
            default:
                return false;
        }
    }

    public static void ServerBroadcastMove(
        AbilitySystemComponent asc,
        List<Vector3> waypoints,
        float costMeters)
    {
        if (!BattleNetworkGate.IsNetworkBattleActive || asc == null || waypoints == null)
            return;

        int slot = NetworkBattleActor.GetSlotIndex(asc);
        if (slot < 0)
            return;

        NetworkBattleState.Instance?.ServerSendMovePresentation(slot, waypoints.ToArray(), costMeters);
    }

    public static void ServerBroadcastCombatEvent(in CombatEvent evt)
    {
        if (!BattleNetworkGate.IsNetworkBattleActive || !ShouldRelay(evt.type))
            return;

        // 带 VfxSpawnEntry 的目标特效 Client 本地已播，且引用传不过去；只转发带 tag 的 Buff。
        if (evt.type == CombatEventType.BuffApplied && string.IsNullOrEmpty(evt.tag.TagName))
            return;

        var net = NetCombatEvent.From(evt);
        if (net.instigatorSlot < 0 && net.targetSlot < 0)
            return;

        NetworkBattleState.Instance?.ServerSendCombatEvent(net);
    }

    public static void ClientPlayMove(int slot, Vector3[] waypoints, float costMeters)
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        var asc = NetworkBattleActor.GetBySlot(slot);
        var movement = asc != null ? asc.GetComponent<CharacterMovementController>() : null;
        if (movement == null)
            return;

        // MoveState 会持有列表直到走完，不能复用缓冲。
        movement.PlayNetworkMove(new List<Vector3>(waypoints), costMeters);
    }

    public static void ClientReplayCombatEvent(NetCombatEvent net)
    {
        if (!net.TryToCombatEvent(out var evt))
            return;

        if (evt.type == CombatEventType.AbilityUsed)
        {
            // 施法表现进不了 Move 状态，先收掉位移动画；受击/死亡自行抢占。
            SettleLocomotion(evt.instigator);
            BeginClientAbility(ref evt);
        }
        else if (evt.type == CombatEventType.TurnEnded)
        {
            TickTurnEnd(evt.instigator);
        }

        CombatEventBus.Instance.Raise(evt);

        if (evt.type == CombatEventType.AbilityUsed)
            ClearAbandonedAbility(evt.instigator);
    }

    /// <summary>
    /// 按 Server TryActivate 顺序建 pending：先 Immediate 效果，再置 pending。
    /// </summary>
    static void BeginClientAbility(ref CombatEvent evt)
    {
        var caster = evt.instigator;
        if (caster == null || evt.ability == null)
            return;

        // 与 CharacterMotor.HandleCombatEvent 同一套回退，避免目标解析不一致。
        var ctx = evt.abilityContext;
        if (!ctx.HasExplicitTargets && !ctx.HasTargetPoint && !ctx.HasAimDirection && !ctx.HasDirection)
        {
            ctx = evt.target != null
                ? AbilityActivationContext.SingleTarget(evt.target)
                : AbilityActivationContext.Self();
            evt.abilityContext = ctx;
        }

        evt.ability.ExecuteEffectsByPhase(caster, ctx, AbilityEffectPhase.Immediate);
        caster.BeginAbilityActivation(evt.ability, ctx);
    }

    /// <summary>
    /// Client 不跑回合流程，TurnEnded 时在这里补 Buff 倒数。
    /// 仪式引导不在此推进——到期触发技能是 Server 的事。
    /// </summary>
    static void TickTurnEnd(AbilitySystemComponent actor)
    {
        if (actor == null || actor.Attributes == null)
            return;

        bool pauseBuffs = actor.HasTag(GameplayTag.Buff.BlessingWard);
        actor.Attributes.TickModifiers(1, pauseBuffs);
    }

    /// <summary>技能表现没启动时清 pending，否则 abilityBlocksTurnHandoff 会一直挂着。</summary>
    static void ClearAbandonedAbility(AbilitySystemComponent caster)
    {
        if (caster == null || !caster.HasPendingAbility)
            return;

        var motor = caster.GetComponent<CharacterMotor>();
        if (motor != null)
        {
            var state = motor.StateMachine.CurrentType;
            if (state == CharacterStateType.Ability || state == CharacterStateType.DashCharge)
                return;
        }

        caster.ClearPendingAbility();
    }

    static void SettleLocomotion(AbilitySystemComponent asc)
    {
        if (asc == null)
            return;

        var motor = asc.GetComponent<CharacterMotor>();
        if (motor == null || !motor.IsMoving)
            return;

        motor.NotifyMovementInterrupted();
        motor.ReturnToIdle();
    }
}
