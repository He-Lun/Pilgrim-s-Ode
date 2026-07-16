/// <summary>
/// 霸体 — 受击仍扣血，但不进受击/眩晕表现、不打断突进等位移。
/// </summary>
public static class HyperArmor
{
    public static bool IsActive(AbilitySystemComponent asc)
    {
        if (asc == null) return false;
        return asc.HasActiveEffectCategory(GameplayTag.Buff.HyperArmor);
    }

    public static void Grant(AbilitySystemComponent asc, GameplayTag instanceTag)
    {
        if (asc?.Attributes == null || string.IsNullOrEmpty(instanceTag.TagName))
            return;

        asc.Attributes.AddModifier(new AttributeModifier(
            "Status",
            0f,
            ModifierOperation.Additive,
            instanceTag,
            0));

        asc.ApplyBuffTo(asc, instanceTag, asc);
    }

    public static void Revoke(AbilitySystemComponent asc, GameplayTag instanceTag)
    {
        if (asc == null || string.IsNullOrEmpty(instanceTag.TagName))
            return;

        asc.Attributes?.RemoveModifier(instanceTag);
        asc.RemoveTag(instanceTag);
    }
}
