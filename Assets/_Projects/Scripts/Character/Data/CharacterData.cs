using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "巡礼之诗/角色数据")]
public class CharacterDataSO : ScriptableObject
{
    [Header("========== 基本信息 ==========")]
    [Header("名字")]
    [SerializeField] public new string name;
    [Header("角色介绍")]
    [SerializeField] public string description = "这是一个角色";
    [Header("职业")]
    [SerializeField] public GameplayTag job;
    [Header("出身王国")]
    [SerializeField] public GameplayTag kingdom;

    [Header("========== 基础属性 ==========")]
    [Header("基础生命值")]
    [SerializeField] public float baseHealth = 100f;
    [Header("基础攻击力")]
    [SerializeField] public float baseAttack = 10f;
    [Header("基础防御力")]
    [SerializeField] public float baseDefense = 5f;
    [Header("基础敏捷值（决定行动频率）")]
    [SerializeField] public float baseAgility = 10f;
    [Header("速度（每回合移动力）")]
    [SerializeField] public float baseSpeed = 10f;

    [Header("========== 职业技能 ==========")]
    [Tooltip("该角色所属职业的可选技能池（构筑 / 手牌校验用）")]
    [SerializeField] public JobAbilityPoolSO jobAbilityPool;

    [Tooltip("该角色独占、不在职业池中的技能")]
    [SerializeField] public List<GameplayAbility> exclusiveAbilities = new List<GameplayAbility>();

    [Tooltip("本角色如何演绎各技能 — 同一张职业牌可与其他角色不同 SkillIndex")]
    [SerializeField] public List<AbilityPresentationEntry> abilityPresentations = new List<AbilityPresentationEntry>();

    [Header("========== 天赋技能 ==========")]
    [Header("天赋技能效果（圣骨被动等，开战自动生效）")]
    [SerializeField] public List<GameplayAbility> innateAbilities;

    [Header("========== 身份 ==========")]
    [Tooltip("可选角色身份 tag，如 Character.Luna")]
    [SerializeField] public GameplayTag characterTag;

    [Header("========== 激励系统 ==========")]
    [Header("激励技能效果")]
    [SerializeField] public GameplayAbility inspirationAbility;
    [Header("激励任务")]
    [SerializeField] public InspirationTaskSO inspirationTask;

    [Header("========== 月魂（露娜等）==========")]
    [Tooltip("非空则开战初始化月魂/月相系统")]
    [SerializeField] public MoonSoulConfigSO moonSoulConfig;

    /// <summary>职业池 + 角色独占技能（去重）。</summary>
    public IEnumerable<GameplayAbility> GetAllKnownAbilities()
    {
        var seen = new HashSet<GameplayAbility>();

        if (exclusiveAbilities != null)
        {
            foreach (var ability in exclusiveAbilities)
            {
                if (ability == null || !seen.Add(ability)) continue;
                yield return ability;
            }
        }

        if (jobAbilityPool?.abilities == null) yield break;

        foreach (var ability in jobAbilityPool.abilities)
        {
            if (ability == null || !seen.Add(ability)) continue;
            yield return ability;
        }
    }

    public bool KnowsAbility(GameplayAbility ability)
    {
        if (ability == null) return false;

        foreach (var known in GetAllKnownAbilities())
        {
            if (known == ability)
                return true;
        }

        return false;
    }

    /// <summary>解析该角色对某技能的表现；未配置则回退到 GameplayAbility 默认值。</summary>
    public AbilityPresentationEntry ResolvePresentation(GameplayAbility ability)
    {
        if (ability == null)
            return new AbilityPresentationEntry();

        if (abilityPresentations != null)
        {
            foreach (var entry in abilityPresentations)
            {
                if (entry != null && entry.ability == ability)
                    return entry;
            }
        }

        return AbilityPresentationEntry.FromAbilityDefaults(ability);
    }
}
