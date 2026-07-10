using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// buff效果
/// </summary>
[System.Serializable]
public class BuffAbilityEffect : AbilityEffect
{
    public string attributeName = "Attack";
    public float multiplicativeBonus = 0.2f;
    public int durationTurns = 2;
    public GameplayTag buffTag = new GameplayTag("Buff.AttackUp");

    [Header("Buff 特效（持续型，跟随 buff 存续）")]
    [Tooltip("挂在被 buff 角色身上、随 buff 生命周期存续的特效；位置/朝向/跟随用 VfxSpawnEntry 配置，timing 与 autoDestroy 对 buff 无效")]
    public VfxSpawnEntry buffVfx;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;
        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;
            var modifier = new AttributeModifier(
                attributeName,
                multiplicativeBonus,
                ModifierOperation.Multiplicative,
                buffTag,
                durationTurns);
            target.Attributes.AddModifier(modifier);
            caster?.ApplyBuffTo(target, buffTag, caster);

            if (buffVfx != null && buffVfx.IsValid)
                target.GetComponent<AbilityVfxPlayer>()?.PlayBuffVfx(buffTag, buffVfx);
        }
    }
}
