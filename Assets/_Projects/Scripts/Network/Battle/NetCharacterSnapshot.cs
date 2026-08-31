using Mirror;
using UnityEngine;

/// <summary>
/// 角色快照 — 使用显式 Read/Write，避免 SyncList 自定义 struct 在 Editor/Build 间序列化不一致。
/// </summary>
[System.Serializable]
public struct NetCharacterSnapshot
{
    public int slotIndex;
    public int teamId;
    public float currentHealth;
    public float maxHealth;
    public int handCount;
    public float posX;
    public float posY;
    public float posZ;
    public byte isAlive;
    /// <summary>本回合剩余移动力；Client 的路径预览与可达范围全靠它。</summary>
    public float remainingMoveMeters;

    public Vector3 WorldPosition
    {
        get => new Vector3(posX, posY, posZ);
        set
        {
            posX = value.x;
            posY = value.y;
            posZ = value.z;
        }
    }

    public bool IsAlive
    {
        get => isAlive != 0;
        set => isAlive = (byte)(value ? 1 : 0);
    }

    public static NetCharacterSnapshot FromActor(AbilitySystemComponent asc, int slot)
    {
        var attrs = asc?.Attributes;
        var movement = asc != null ? asc.GetComponent<CharacterMovementController>() : null;
        return new NetCharacterSnapshot
        {
            slotIndex = slot,
            teamId = asc != null ? asc.TeamId : 0,
            currentHealth = attrs != null ? attrs.CurrentHealth : 0f,
            maxHealth = attrs != null ? attrs.MaxHealth : 0f,
            handCount = asc?.HandCards?.HandCount ?? 0,
            WorldPosition = asc != null ? asc.transform.position : Vector3.zero,
            IsAlive = attrs == null || !attrs.IsDead(),
            remainingMoveMeters = movement != null ? movement.RemainingMoveMeters : 0f
        };
    }
}

public static class NetCharacterSnapshotSerialization
{
    public static void WriteNetCharacterSnapshot(this NetworkWriter writer, NetCharacterSnapshot value)
    {
        writer.WriteInt(value.slotIndex);
        writer.WriteInt(value.teamId);
        writer.WriteFloat(value.currentHealth);
        writer.WriteFloat(value.maxHealth);
        writer.WriteInt(value.handCount);
        writer.WriteFloat(value.posX);
        writer.WriteFloat(value.posY);
        writer.WriteFloat(value.posZ);
        writer.WriteByte(value.isAlive);
        writer.WriteFloat(value.remainingMoveMeters);
    }

    public static NetCharacterSnapshot ReadNetCharacterSnapshot(this NetworkReader reader)
    {
        return new NetCharacterSnapshot
        {
            slotIndex = reader.ReadInt(),
            teamId = reader.ReadInt(),
            currentHealth = reader.ReadFloat(),
            maxHealth = reader.ReadFloat(),
            handCount = reader.ReadInt(),
            posX = reader.ReadFloat(),
            posY = reader.ReadFloat(),
            posZ = reader.ReadFloat(),
            isAlive = reader.ReadByte(),
            remainingMoveMeters = reader.ReadFloat()
        };
    }
}
