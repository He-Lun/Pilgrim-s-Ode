/// <summary>
/// 禁疗 — 持有 Debuff.HealBlock（或子 tag）时拒绝一切治疗。
/// </summary>
public static class HealBlock
{
    public static bool IsActive(AbilitySystemComponent asc)
    {
        if (asc == null) return false;
        return asc.HasActiveEffectCategory(GameplayTag.Debuff.HealBlock);
    }

    public static bool CanReceiveHeal(AbilitySystemComponent asc) => !IsActive(asc);
}
