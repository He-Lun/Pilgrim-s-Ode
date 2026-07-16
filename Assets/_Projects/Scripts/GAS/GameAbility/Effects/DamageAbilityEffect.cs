using System.Collections.Generic;

/// <summary>
/// 伤害效果。命中特效配基类 targetVfx（范围技勿依赖 defaultVfx 的 Target 锚点）。
/// </summary>
[System.Serializable]
public class DamageEffect : AbilityEffect
{
    public float scaler = 1f;
    public GameplayTag damageType = new GameplayTag("DamageType.Physical");

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null) return;

        foreach (var target in targets)
        {
            if (target?.Attributes == null) continue;
            target.Attributes.TakeDamage(scaler * caster.Attributes.Attack, damageType, caster);
        }
    }
}
