using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameplayAbility 的跨端稳定 ID 映射。
/// Host 与 Client 加载同一批资产，按资产名的稳定哈希取得一致 ID。
/// </summary>
public static class NetAbilityRegistry
{
    static readonly Dictionary<int, GameplayAbility> IdToAbility = new Dictionary<int, GameplayAbility>();
    static readonly Dictionary<GameplayAbility, int> AbilityToId = new Dictionary<GameplayAbility, int>();

    public static void Clear()
    {
        IdToAbility.Clear();
        AbilityToId.Clear();
    }

    public static void RegisterRoster(IReadOnlyList<AbilitySystemComponent> roster)
    {
        Clear();

        if (roster == null)
            return;

        for (int i = 0; i < roster.Count; i++)
            RegisterActor(roster[i]);
    }

    public static void RegisterActor(AbilitySystemComponent actor)
    {
        if (actor == null)
            return;

        Register(actor.InspirationAbility);
        RegisterAll(actor.KnownAbilities);
        RegisterAll(actor.PassiveAbilities);
        RegisterAll(actor.CharacterData?.battleDeck);
    }

    static void RegisterAll(IReadOnlyList<GameplayAbility> abilities)
    {
        if (abilities == null)
            return;

        for (int i = 0; i < abilities.Count; i++)
            Register(abilities[i]);
    }

    public static int Register(GameplayAbility ability)
    {
        if (ability == null)
            return 0;

        if (AbilityToId.TryGetValue(ability, out int existing))
            return existing;

        int id = StableHash(ability.name);
        if (id == 0)
            id = 1;

        if (IdToAbility.TryGetValue(id, out var clash) && clash != ability)
        {
            Debug.LogError(
                $"[NetAbilityRegistry] 技能资产名哈希冲突：'{ability.name}' 与 '{clash.name}' 得到相同 ID {id}。" +
                "请重命名其中一个资产，否则联机时会打出错误的技能。");
        }

        IdToAbility[id] = ability;
        AbilityToId[ability] = id;
        return id;
    }

    public static int GetId(GameplayAbility ability)
    {
        if (ability == null)
            return 0;

        return AbilityToId.TryGetValue(ability, out int id) ? id : Register(ability);
    }

    public static GameplayAbility GetById(int id)
    {
        if (id == 0)
            return null;

        return IdToAbility.TryGetValue(id, out var ability) ? ability : null;
    }

    /// <summary>FNV-1a：跨平台、跨运行时稳定，不能用 string.GetHashCode()。</summary>
    static int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
