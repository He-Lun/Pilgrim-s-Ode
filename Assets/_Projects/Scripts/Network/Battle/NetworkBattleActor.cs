using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 参战角色 slot 注册 — 联机指令用 slotIndex 标识 ASC。
/// </summary>
[DisallowMultipleComponent]
public class NetworkBattleActor : MonoBehaviour
{
    [SerializeField] private int slotIndex = -1;

    private AbilitySystemComponent asc;

    private static readonly Dictionary<int, NetworkBattleActor> Slots = new Dictionary<int, NetworkBattleActor>();
    private static readonly Dictionary<AbilitySystemComponent, int> AscToSlot = new Dictionary<AbilitySystemComponent, int>();

    public int SlotIndex => slotIndex;
    public AbilitySystemComponent Asc => asc;

    public static IReadOnlyDictionary<int, NetworkBattleActor> AllSlots => Slots;

    public static void ClearRegistry()
    {
        Slots.Clear();
        AscToSlot.Clear();
    }

    public static void RegisterRoster(IReadOnlyList<AbilitySystemComponent> roster)
    {
        ClearRegistry();
        for (int i = 0; i < roster.Count; i++)
        {
            var actor = roster[i];
            if (actor == null) continue;

            var marker = actor.GetComponent<NetworkBattleActor>();
            if (marker == null)
                marker = actor.gameObject.AddComponent<NetworkBattleActor>();

            marker.asc = actor;
            marker.slotIndex = i;
            Slots[i] = marker;
            AscToSlot[actor] = i;
        }

        NetAbilityRegistry.RegisterRoster(roster);
    }

    public static AbilitySystemComponent GetBySlot(int slot)
    {
        return Slots.TryGetValue(slot, out var marker) ? marker.asc : null;
    }

    public static int GetSlotIndex(AbilitySystemComponent actor)
    {
        if (actor == null) return -1;
        return AscToSlot.TryGetValue(actor, out int slot) ? slot : -1;
    }
}
