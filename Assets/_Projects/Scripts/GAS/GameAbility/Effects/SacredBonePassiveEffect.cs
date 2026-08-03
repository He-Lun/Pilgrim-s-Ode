using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 圣骨被动 — 开战注册追踪器：释放技能后 N 个自身回合内，友方攻击敌人时在自身普攻范围内追加一次普攻。
/// </summary>
[System.Serializable]
public class SacredBonePassiveEffect : AbilityEffect
{
    public int windowTurns = 3;

    [Tooltip("留空则自动从职业池查找「普通攻击」")]
    public GameplayAbility basicAttack;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (!ShouldExecute(caster))
            return;

        caster.SacredBone.Initialize(caster, basicAttack, windowTurns);
    }

    public override void Execute(AbilitySystemComponent caster, GameplayAbility sourceAbility, AbilityActivationContext context)
    {
        Execute(caster, context.GetExplicitTargets());
    }
}
