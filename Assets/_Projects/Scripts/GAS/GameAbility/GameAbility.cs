using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能激活结果
/// </summary>
public enum AbilityActivationResult
{
    Success,
    NotEnoughActionPoints,
    CooldownNotReady,
    MissingRequiredTags,
    HasBlockTags,
    TargetInvalid,
    UnknownError
}

/// <summary>
/// 技能目标选择类型
/// </summary>
public enum TargetScope
{
    Self,
    SingleEnemy,
    SingleAlly,
    AllEnemies,
    AllAllies,
    Area
}

/// <summary>
/// 技能基类 - 使用ScriptableObject实现数据驱动
/// </summary>
public abstract class GameplayAbility : ScriptableObject
{
    [Header("========== 基础信息 ==========")]
    public string abilityName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("========== 消耗 ==========")]
    public int actionPointCost = 1;
    public bool isInstant = true;

    [Header("========== 目标选择 ==========")]
    public TargetScope targetScope = TargetScope.SingleEnemy;
    public int areaRadius = 1;

    [Header("========== 标签条件 ==========")]
    public List<GameplayTag> requiredTags = new List<GameplayTag>();
    public List<GameplayTag> blockTags = new List<GameplayTag>();
    public List<GameplayTag> abilityTags = new List<GameplayTag>();

    [Header("========== 技能效果列表 ==========")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    public virtual AbilityActivationResult CanActivate(AbilitySystemComponent owner)
    {
        foreach (var tag in blockTags)
        {
            if (owner.HasTag(tag))
                return AbilityActivationResult.HasBlockTags;
        }

        foreach (var tag in requiredTags)
        {
            if (!owner.HasTag(tag))
                return AbilityActivationResult.MissingRequiredTags;
        }

        if (actionPointCost > 0)
        {
            if (owner.TeamResource == null)
                return AbilityActivationResult.UnknownError;

            if (owner.TeamResource.CurrentActionPoints < actionPointCost)
                return AbilityActivationResult.NotEnoughActionPoints;
        }

        return AbilityActivationResult.Success;
    }

    public virtual AbilityActivationResult CanActivate(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        var result = CanActivate(owner);
        if (result != AbilityActivationResult.Success)
            return result;

        return ValidateContext(owner, context);
    }

    /// <summary>激活技能（推荐使用，带完整 Context）</summary>
    public virtual AbilityActivationResult TryActivate(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        var result = CanActivate(owner, context);
        if (result != AbilityActivationResult.Success)
            return result;

        if (actionPointCost > 0)
        {
            if (owner.TeamResource == null)
                return AbilityActivationResult.UnknownError;

            if (!owner.TeamResource.TryConsumeActionPoints(actionPointCost))
                return AbilityActivationResult.NotEnoughActionPoints;
        }

        ExecuteEffects(owner, context);
        NotifyAbilityUsed(owner, context);
        return AbilityActivationResult.Success;
    }

    /// <summary>兼容旧调用：仅传目标列表时自动包装为 Context。</summary>
    public virtual AbilityActivationResult TryActivate(AbilitySystemComponent owner, List<AbilitySystemComponent> targets)
    {
        return TryActivate(owner, AbilityActivationContext.FromTargets(targets));
    }

    public virtual AbilityActivationResult TryActivateAsInspiration(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        foreach (var tag in blockTags)
        {
            if (owner.HasTag(tag))
                return AbilityActivationResult.HasBlockTags;
        }

        ExecuteEffects(owner, context);
        NotifyAbilityUsed(owner, context);
        return AbilityActivationResult.Success;
    }

    public virtual AbilityActivationResult TryActivateAsInspiration(AbilitySystemComponent owner, List<AbilitySystemComponent> targets)
    {
        return TryActivateAsInspiration(owner, AbilityActivationContext.FromTargets(targets));
    }

    protected virtual AbilityActivationResult ValidateContext(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        switch (targetScope)
        {
            case TargetScope.SingleEnemy:
            case TargetScope.SingleAlly:
                if (!context.HasExplicitTargets)
                    return AbilityActivationResult.TargetInvalid;
                break;

            case TargetScope.Area:
                if (!context.HasTargetCell && !context.HasExplicitTargets)
                    return AbilityActivationResult.TargetInvalid;
                break;

            case TargetScope.Self:
            case TargetScope.AllEnemies:
            case TargetScope.AllAllies:
                break;
        }

        return AbilityActivationResult.Success;
    }

    private void NotifyAbilityUsed(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        var targets = GetTargets(owner, context);
        owner.OnAbilityUsed?.Invoke(this, targets);

        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = CombatEventType.AbilityUsed,
            instigator = owner,
            ability = this,
            target = targets.Count > 0 ? targets[0] : null
        });
    }

    protected virtual void ExecuteEffects(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        foreach (var effect in effects)
        {
            if (effect != null)
                effect.Execute(owner, context);
        }
    }

    protected virtual List<AbilitySystemComponent> GetTargets(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        if (targetScope == TargetScope.Self)
            return new List<AbilitySystemComponent> { owner };

        return context.GetExplicitTargets();
    }
}

/// <summary>
/// 技能效果基类。默认从 Context 取 explicitTargets；突进/范围类 Effect 可重写 Execute(caster, context) 读取 direction / targetCell。
/// </summary>
[System.Serializable]
public abstract class AbilityEffect
{
    [TextArea(1, 3)] public string description;

    public virtual void Execute(AbilitySystemComponent caster, AbilityActivationContext context)
    {
        Execute(caster, context.GetExplicitTargets());
    }

    public abstract void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets);
}
