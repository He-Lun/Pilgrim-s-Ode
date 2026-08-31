using Mirror;
using UnityEngine;

/// <summary>
/// 可网络传输的战斗表现事件 — Client 重放后由 CharacterMotor / 相机等既有订阅者驱动表现。
/// 与 NetCharacterSnapshot 一样使用显式 Read/Write，避免 Weaver 在 Editor/Build 间生成不一致。
/// </summary>
public struct NetCombatEvent
{
    public byte type;
    public int instigatorSlot;
    public int targetSlot;
    public int abilityId;
    public float value;
    public int intValue;
    public string tagName;
    public byte hasContext;
    public NetAbilityContext context;

    public CombatEventType Type => (CombatEventType)type;

    public static NetCombatEvent From(in CombatEvent evt)
    {
        var net = new NetCombatEvent
        {
            type = (byte)evt.type,
            instigatorSlot = NetworkBattleActor.GetSlotIndex(evt.instigator),
            targetSlot = NetworkBattleActor.GetSlotIndex(evt.target),
            abilityId = NetAbilityRegistry.GetId(evt.ability),
            value = evt.value,
            intValue = evt.intValue,
            tagName = evt.tag.TagName ?? string.Empty
        };

        if (evt.type == CombatEventType.AbilityUsed)
        {
            net.hasContext = 1;
            net.context = NetAbilityContext.From(-1, evt.abilityContext);
        }

        return net;
    }

    /// <summary>还原为本地事件；施术者与目标 slot 都无法解析时返回 false。</summary>
    public bool TryToCombatEvent(out CombatEvent evt)
    {
        var instigator = NetworkBattleActor.GetBySlot(instigatorSlot);
        var target = NetworkBattleActor.GetBySlot(targetSlot);

        evt = new CombatEvent
        {
            type = Type,
            instigator = instigator,
            target = target,
            ability = NetAbilityRegistry.GetById(abilityId),
            value = value,
            intValue = intValue,
            tag = string.IsNullOrEmpty(tagName) ? default : new GameplayTag(tagName)
        };

        if (hasContext != 0)
            evt.abilityContext = context.ToActivationContext();

        return instigator != null || target != null;
    }
}

public static class NetCombatEventSerialization
{
    public static void WriteNetCombatEvent(this NetworkWriter writer, NetCombatEvent value)
    {
        writer.WriteByte(value.type);
        writer.WriteInt(value.instigatorSlot);
        writer.WriteInt(value.targetSlot);
        writer.WriteInt(value.abilityId);
        writer.WriteFloat(value.value);
        writer.WriteInt(value.intValue);
        writer.WriteString(value.tagName);
        writer.WriteByte(value.hasContext);
        WriteContext(writer, value.context);
    }

    public static NetCombatEvent ReadNetCombatEvent(this NetworkReader reader)
    {
        return new NetCombatEvent
        {
            type = reader.ReadByte(),
            instigatorSlot = reader.ReadInt(),
            targetSlot = reader.ReadInt(),
            abilityId = reader.ReadInt(),
            value = reader.ReadFloat(),
            intValue = reader.ReadInt(),
            tagName = reader.ReadString(),
            hasContext = reader.ReadByte(),
            context = ReadContext(reader)
        };
    }

    static void WriteContext(NetworkWriter writer, NetAbilityContext ctx)
    {
        writer.WriteInt(ctx.handIndex);
        writer.WriteByte(ctx.targetCount);
        writer.WriteInt(ctx.targetSlot0);
        writer.WriteInt(ctx.targetSlot1);
        writer.WriteInt(ctx.targetSlot2);
        writer.WriteInt(ctx.targetSlot3);
        writer.WriteVector3(ctx.targetWorldPoint);
        writer.WriteBool(ctx.hasTargetPoint);
        writer.WriteVector3(ctx.aimDirectionWorld);
        writer.WriteBool(ctx.hasAimDirection);
    }

    static NetAbilityContext ReadContext(NetworkReader reader)
    {
        return new NetAbilityContext
        {
            handIndex = reader.ReadInt(),
            targetCount = reader.ReadByte(),
            targetSlot0 = reader.ReadInt(),
            targetSlot1 = reader.ReadInt(),
            targetSlot2 = reader.ReadInt(),
            targetSlot3 = reader.ReadInt(),
            targetWorldPoint = reader.ReadVector3(),
            hasTargetPoint = reader.ReadBool(),
            aimDirectionWorld = reader.ReadVector3(),
            hasAimDirection = reader.ReadBool()
        };
    }
}
