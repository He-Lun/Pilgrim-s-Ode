using UnityEngine;

/// <summary>
/// 从技能效果中读取拉条预演参数。
/// </summary>
public static class ActionBarAbilityPreviewUtility
{
    public static bool TryGetAdvancePercent(GameplayAbility ability, out float percent)
    {
        percent = 0f;
        if (ability?.effects == null)
            return false;

        for (int i = 0; i < ability.effects.Count; i++)
        {
            if (ability.effects[i] is AdvanceActionAbilityEffect advance && advance.advancePercent > 0f)
            {
                percent = advance.advancePercent;
                return true;
            }
        }

        return false;
    }
}
