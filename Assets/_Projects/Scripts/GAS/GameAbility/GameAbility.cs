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
    Area,
    /// <summary>以施法者为圆心，无需点选目标；Effect 默认命中半径内敌人。</summary>
    AreaAroundSelf,
    /// <summary>以施法者为起点、指定方向的矩形范围；需 context 提供 aimDirection。</summary>
    DirectedRect
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
    [Tooltip("旧格数半径，areaRadiusMeters<=0 时按 1.5m/格换算")]
    public int areaRadius = 1;
    [Tooltip("范围半径（米）；DirectedRect 时表示向前长度")]
    public float areaRadiusMeters;
    [Tooltip("DirectedRect 横向宽度（米）")]
    public float areaWidthMeters = 2f;

    [Header("========== 标签条件 ==========")]
    public List<GameplayTag> requiredTags = new List<GameplayTag>();
    public List<GameplayTag> blockTags = new List<GameplayTag>();
    public List<GameplayTag> abilityTags = new List<GameplayTag>();

    [Header("========== 表现默认值（可被 CharacterDataSO 覆盖）==========")]
    [Tooltip("Animator Trigger 名称，可选")]
    public string animTrigger;
    [Tooltip("Animator SkillIndex 整型参数 — 角色未配表现表时的回退值")]
    public int skillAnimIndex;

    [Header("特效默认值（可被角色表现表覆盖）")]
    [Tooltip("技能默认特效列表 — 每条独立配置位置/时机/跟随")]
    public List<VfxSpawnEntry> defaultVfx = new List<VfxSpawnEntry>();

    [Header("========== 技能效果列表 ==========")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    public float GetAreaRadiusMeters()
    {
        if (areaRadiusMeters > 0f) return areaRadiusMeters;
        return areaRadius * BattleSpaceSettings.GetMetersPerSpeedPoint();
    }

    public float GetAreaWidthMeters()
    {
        if (areaWidthMeters > 0f) return areaWidthMeters;
        return GetAreaRadiusMeters() * 0.5f;
    }

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

        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.Immediate);
        owner.BeginAbilityActivation(this, context);
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

        ExecuteAllEffectPhases(owner, context);
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
#pragma warning disable 618
                if (!context.HasTargetPoint && !context.HasTargetCell && !context.HasExplicitTargets)
#pragma warning restore 618
                    return AbilityActivationResult.TargetInvalid;
                break;

            case TargetScope.Self:
            case TargetScope.AreaAroundSelf:
            case TargetScope.AllEnemies:
            case TargetScope.AllAllies:
                break;

            case TargetScope.DirectedRect:
                if (!context.HasAimDirection && !context.HasTargetPoint && !context.HasDirection)
                    return AbilityActivationResult.TargetInvalid;
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
            target = targets.Count > 0 ? targets[0] : null,
            abilityContext = context
        });
    }

    public void ExecuteEffectsByPhase(AbilitySystemComponent owner, AbilityActivationContext context, AbilityEffectPhase phase)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect != null && effect.phase == phase)
                effect.Execute(owner, this, context);
        }
    }

    public void ExecuteAllEffectPhases(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.Immediate);
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnHit);
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnComplete);
    }

    /// <summary>供 Effect 与事件广播使用的目标解析。</summary>
    public List<AbilitySystemComponent> ResolveEffectTargets(
        AbilitySystemComponent owner,
        AbilityActivationContext context)
    {
        return GetTargets(owner, context);
    }

    protected virtual List<AbilitySystemComponent> GetTargets(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        if (targetScope == TargetScope.Self)
            return new List<AbilitySystemComponent> { owner };

        if (targetScope == TargetScope.AreaAroundSelf)
            return BattleTargeting.FilterEnemiesInRadius(owner, owner.transform.position, GetAreaRadiusMeters());

        if (targetScope == TargetScope.AllAllies)
            return BattleTargeting.FilterAllies(owner, includeCaster: true);

        if (targetScope == TargetScope.AllEnemies)
            return BattleTargeting.FilterEnemies(owner);

        if (targetScope == TargetScope.DirectedRect)
        {
            Vector3 origin = owner.transform.position;
            Vector3 aim = DirectedRectUtility.ResolveAimDirection(context, origin);
            return BattleTargeting.FilterEnemiesInDirectedRect(
                owner, origin, aim, GetAreaRadiusMeters(), GetAreaWidthMeters());
        }

        if (targetScope == TargetScope.Area && context.HasTargetPoint)
            return BattleTargeting.FindAbilitySystemsInRadius(context.targetWorldPoint, GetAreaRadiusMeters());

        return context.GetExplicitTargets();
    }
}

/// <summary>效果触发阶段 — 由动画事件驱动 OnHit / OnComplete。</summary>
public enum AbilityEffectPhase
{
    Immediate,
    OnHit,
    OnComplete
}

/// <summary>Effect 目标解析方式。</summary>
public enum EffectTargetSelection
{
    /// <summary>按所属 GA 的 targetScope 解析（AreaAroundSelf / Self / Area 等）。</summary>
    FromAbility,
    /// <summary>仅使用 context.explicitTargets（旧行为）。</summary>
    ExplicitOnly
}

/// <summary>
/// 技能效果基类。默认按 sourceAbility 解析目标；突进/范围类 Effect 可重写 Execute(caster, context) 读取 direction / targetCell。
/// </summary>
[System.Serializable]
public abstract class AbilityEffect
{
    [TextArea(1, 3)] public string description;

    [Tooltip("Immediate=施法瞬间；OnHit=动画命中事件；OnComplete=收招事件")]
    public AbilityEffectPhase phase = AbilityEffectPhase.OnHit;

    [Tooltip("FromAbility=按技能 targetScope 解析；ExplicitOnly=仅 explicitTargets")]
    public EffectTargetSelection targetSelection = EffectTargetSelection.FromAbility;

    [Tooltip("触发概率 0~1；1=必触发。眩晕等概率效果在此配置")]
    [Range(0f, 1f)] public float chance = 1f;

    /// <summary>未通过概率检定则跳过本效果。</summary>
    protected bool RollChance() => chance >= 1f || UnityEngine.Random.value <= chance;

    public virtual void Execute(AbilitySystemComponent caster, AbilityActivationContext context)
    {
        Execute(caster, null, context);
    }

    public virtual void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!RollChance()) return;

        var targets = BattleTargeting.ResolveEffectTargets(caster, sourceAbility, context, targetSelection);
        Execute(caster, targets);
    }

    public abstract void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets);
}
