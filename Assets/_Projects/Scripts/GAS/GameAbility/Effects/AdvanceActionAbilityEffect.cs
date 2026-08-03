using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行动提前 — currentAV -= 满AV × percent。目标特效用基类 targetVfx。
/// </summary>
[System.Serializable]
public class AdvanceActionAbilityEffect : AbilityEffect
{
    [Tooltip("提前比例：1 = 提前 100%（AV 减一个满条），0.5 = 提前 50%")]
    [Min(0f)]
    public float advancePercent = 1f;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || advancePercent <= 0f) return;

        var queue = ActionQueue.Instance;
        if (queue == null) return;

        foreach (var target in targets)
        {
            if (target == null) continue;
            queue.AdvanceForward(target, advancePercent);
        }
    }
}
