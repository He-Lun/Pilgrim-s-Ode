/// <summary>
/// 霸体 — 受击仍扣血，但不进受击/眩晕表现。
/// </summary>
public static class HyperArmor
{
    public static bool IsActive(AbilitySystemComponent asc)
    {
        if (asc == null) return false;
        return asc.HasActiveEffectCategory(GameplayTag.Buff.HyperArmor);
    }
}
