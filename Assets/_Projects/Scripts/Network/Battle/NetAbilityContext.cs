using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可网络传输的出牌上下文 — 目标用 slotIndex，客户端/服务端各自还原 ASC。
/// Mirror Weaver 按 public 字段自动序列化。
/// </summary>
public struct NetAbilityContext
{
    public const int MaxTargets = 8;

    public int handIndex;
    public byte targetCount;
    public int targetSlot0;
    public int targetSlot1;
    public int targetSlot2;
    public int targetSlot3;
    public Vector3 targetWorldPoint;
    public bool hasTargetPoint;
    public Vector3 aimDirectionWorld;
    public bool hasAimDirection;

    public AbilityActivationContext ToActivationContext()
    {
        var targets = new List<AbilitySystemComponent>(targetCount);
        if (targetCount > 0) AppendTargetSlot(targets, targetSlot0);
        if (targetCount > 1) AppendTargetSlot(targets, targetSlot1);
        if (targetCount > 2) AppendTargetSlot(targets, targetSlot2);
        if (targetCount > 3) AppendTargetSlot(targets, targetSlot3);

        if (hasAimDirection)
            return AbilityActivationContext.WithAimDirection(aimDirectionWorld);

        if (hasTargetPoint)
        {
            var ctx = AbilityActivationContext.WithTargetPoint(targetWorldPoint);
            ctx.explicitTargets = targets;
            return ctx;
        }

        if (targets.Count > 0)
            return AbilityActivationContext.FromTargets(targets);

        return AbilityActivationContext.Self();
    }

    public static NetAbilityContext From(int handIndex, AbilityActivationContext ctx)
    {
        var net = new NetAbilityContext
        {
            handIndex = handIndex,
            targetSlot0 = -1,
            targetSlot1 = -1,
            targetSlot2 = -1,
            targetSlot3 = -1
        };

        if (ctx.hasAimDirection)
        {
            net.hasAimDirection = true;
            net.aimDirectionWorld = ctx.aimDirectionWorld;
        }

        if (ctx.hasTargetPoint)
        {
            net.hasTargetPoint = true;
            net.targetWorldPoint = ctx.targetWorldPoint;
        }

        if (ctx.explicitTargets == null || ctx.explicitTargets.Count == 0)
            return net;

        int count = Mathf.Min(ctx.explicitTargets.Count, MaxTargets);
        net.targetCount = (byte)count;
        if (count > 0) net.targetSlot0 = NetworkBattleActor.GetSlotIndex(ctx.explicitTargets[0]);
        if (count > 1) net.targetSlot1 = NetworkBattleActor.GetSlotIndex(ctx.explicitTargets[1]);
        if (count > 2) net.targetSlot2 = NetworkBattleActor.GetSlotIndex(ctx.explicitTargets[2]);
        if (count > 3) net.targetSlot3 = NetworkBattleActor.GetSlotIndex(ctx.explicitTargets[3]);
        return net;
    }

    private static void AppendTargetSlot(List<AbilitySystemComponent> targets, int slot)
    {
        if (slot < 0) return;
        var asc = NetworkBattleActor.GetBySlot(slot);
        if (asc != null)
            targets.Add(asc);
    }
}
