using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 获得月魂层数 — 通常配 Self；仅对已绑定 MoonSoulTracker 的角色生效。
/// 基类 targetVfx 在成功叠层后播放。
/// </summary>
[System.Serializable]
public class MoonSoulGrantAbilityEffect : AbilityEffect
{
    [Tooltip("增加的月魂层数（可为负）")]
    public int stacks = 1;

    [Tooltip("勾选则对施法者生效")]
    public bool applyToCaster = true;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || stacks == 0) return;

        if (applyToCaster)
        {
            if (Grant(caster, stacks))
                PlayTargetVfx(caster, caster);
            return;
        }

        var targets = ResolveTargets(caster, sourceAbility, context);
        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (Grant(targets[i], stacks))
                PlayTargetVfx(caster, targets[i]);
        }
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (!ShouldExecute(caster) || stacks == 0) return;

        if (applyToCaster)
        {
            if (Grant(caster, stacks))
                PlayTargetVfx(caster, caster);
            return;
        }

        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (Grant(targets[i], stacks))
                PlayTargetVfx(caster, targets[i]);
        }
    }

    private static bool Grant(AbilitySystemComponent asc, int amount)
    {
        var tracker = asc?.MoonSoul;
        if (tracker == null || !tracker.IsBound) return false;
        tracker.Add(amount);
        return true;
    }
}
