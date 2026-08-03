using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色对某一技能的动画/表现覆盖 — 同职业共享 GameplayAbility，各角色独立配置 SkillIndex 等。
/// </summary>
[System.Serializable]
public class AbilityPresentationEntry
{
    [Tooltip("对应的技能逻辑资产")]
    public GameplayAbility ability;

    [Tooltip("Animator SkillIndex 参数（Haru: baseAttack = 1）")]
    public int skillAnimIndex = 1;

    [Tooltip("可选 Animator Trigger，与 SkillIndex 二选一或同时使用")]
    public string animTrigger;

    [Header("特效（角色覆盖）")]
    [Tooltip("角色专属特效列表 — 非空则覆盖技能 defaultVfx；每条独立配置位置/时机/跟随")]
    public List<VfxSpawnEntry> vfxOverrides = new List<VfxSpawnEntry>();

    public bool IsConfigured => ability != null;

    /// <summary>
    /// 解析本次实际使用的特效列表：角色 vfxOverrides（非空）优先，否则用技能 ability.defaultVfx。
    /// </summary>
    public List<VfxSpawnEntry> GetEffectiveVfx()
    {
        if (vfxOverrides != null && vfxOverrides.Count > 0)
            return vfxOverrides;

        return ability != null ? ability.defaultVfx : null;
    }

    public static AbilityPresentationEntry FromAbilityDefaults(GameplayAbility ability)
    {
        if (ability == null)
            return new AbilityPresentationEntry();

        return new AbilityPresentationEntry
        {
            ability = ability,
            skillAnimIndex = ability.skillAnimIndex,
            animTrigger = ability.animTrigger ?? string.Empty
        };
    }
}
