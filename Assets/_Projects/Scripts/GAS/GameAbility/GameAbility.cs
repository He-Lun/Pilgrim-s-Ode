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
    DirectedRect,
    /// <summary>以施法者为起点、指定方向的扇形范围；需 context 提供 aimDirection。</summary>
    DirectedSector
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
    [Tooltip("范围半径（米）；DirectedRect 时表示向前长度；DirectedSector 为扇形半径")]
    public float areaRadiusMeters;
    [Tooltip("DirectedRect 横向宽度（米）")]
    public float areaWidthMeters = 2f;
    [Tooltip("DirectedSector 半角（度）；总张角 = 2×半角")]
    [Range(1f, 180f)]
    public float sectorHalfAngleDegrees = 45f;

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

    public float GetSectorHalfAngleDegrees() => Mathf.Clamp(sectorHalfAngleDegrees, 1f, 180f);

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

    /// <summary>
    /// 插入行动（追加攻击等）：不耗 AP，走动画相位；Immediate 即时，OnHit 等由动画事件驱动。
    /// </summary>
    public virtual AbilityActivationResult TryActivateAsInsert(AbilitySystemComponent owner, AbilityActivationContext context)
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

        var contextResult = ValidateContext(owner, context);
        if (contextResult != AbilityActivationResult.Success)
            return contextResult;

        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.Immediate);
        owner.BeginAbilityActivation(this, context);
        NotifyAbilityUsed(owner, context);
        return AbilityActivationResult.Success;
    }

    public virtual AbilityActivationResult TryActivateAsInsert(AbilitySystemComponent owner, List<AbilitySystemComponent> targets)
    {
        return TryActivateAsInsert(owner, AbilityActivationContext.FromTargets(targets));
    }

    /// <summary>开战被动注册：仅执行 Immediate 效果，不广播 AbilityUsed、不进入技能表现。</summary>
    public virtual AbilityActivationResult TryActivatePassiveSetup(AbilitySystemComponent owner)
    {
        foreach (var tag in blockTags)
        {
            if (owner.HasTag(tag))
                return AbilityActivationResult.HasBlockTags;
        }

        ExecuteEffectsByPhase(owner, AbilityActivationContext.Self(), AbilityEffectPhase.Immediate);
        return AbilityActivationResult.Success;
    }

    protected virtual AbilityActivationResult ValidateContext(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        switch (GetEffectiveTargetScope(owner))
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
            case TargetScope.DirectedSector:
                if (!context.HasAimDirection && !context.HasTargetPoint && !context.HasDirection)
                    return AbilityActivationResult.TargetInvalid;
                break;
        }

        return AbilityActivationResult.Success;
    }

    /// <summary>
    /// 施法/预览用的有效目标范围：优先取会改变选向/点地方式的 Override Effect。
    /// Override + Self / AreaAroundSelf / All* 只表示效果命中范围，不改变技能施法方式
    /// （避免永世怒火等 Area 点地技被脚边伤害盖成 AreaAroundSelf）。
    /// </summary>
    public TargetScope GetEffectiveTargetScope(AbilitySystemComponent caster)
    {
        if (TryGetCastModeOverrideEffect(caster, out var effect))
            return effect.targetScope;
        return targetScope;
    }

    /// <summary>当前生效范围的半径（米）；仅当 Override 真正改变施法方式时才用其半径覆盖。</summary>
    public float GetEffectiveAreaRadiusMeters(AbilitySystemComponent caster)
    {
        if (TryGetCastModeOverrideEffect(caster, out var effect) && effect.areaRadiusMetersOverride > 0f)
            return effect.areaRadiusMetersOverride;
        return GetAreaRadiusMeters();
    }

    private bool TryGetCastModeOverrideEffect(AbilitySystemComponent caster, out AbilityEffect effect)
    {
        effect = null;
        if (effects == null || caster == null) return false;

        for (int i = 0; i < effects.Count; i++)
        {
            var e = effects[i];
            if (e == null || e.targetSelection != EffectTargetSelection.Override) continue;
            if (!e.PassesCasterTagGates(caster)) continue;
            if (!OverridesCastMode(e.targetScope)) continue;
            effect = e;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 这些 scope 才会改变“怎么选目标/怎么点地”；其余仅描述效果结算范围。
    /// </summary>
    private static bool OverridesCastMode(TargetScope scope)
    {
        switch (scope)
        {
            case TargetScope.SingleEnemy:
            case TargetScope.SingleAlly:
            case TargetScope.Area:
            case TargetScope.DirectedRect:
            case TargetScope.DirectedSector:
                return true;
            default:
                return false;
        }
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
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnHit2);
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnHit3);
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnHit4);
        ExecuteEffectsByPhase(owner, context, AbilityEffectPhase.OnComplete);
    }

    /// <summary>供 Effect 与事件广播使用的目标解析。</summary>
    public List<AbilitySystemComponent> ResolveEffectTargets(
        AbilitySystemComponent owner,
        AbilityActivationContext context)
    {
        return GetTargets(owner, context);
    }

    /// <summary>按指定 scope 解析（Effect Override 时用）；半径/宽度 ≤0 则回退本 GA。</summary>
    public List<AbilitySystemComponent> ResolveEffectTargets(
        AbilitySystemComponent owner,
        AbilityActivationContext context,
        TargetScope scope,
        float radiusMetersOverride = 0f,
        float widthMetersOverride = 0f)
    {
        float radius = radiusMetersOverride > 0f ? radiusMetersOverride : GetAreaRadiusMeters();
        float width = widthMetersOverride > 0f ? widthMetersOverride : GetAreaWidthMeters();
        return BattleTargeting.ResolveByScope(
            owner, context, scope, radius, width, GetSectorHalfAngleDegrees());
    }

    /// <summary>
    /// FromAbility 效果用技能自身 targetScope，不用 GetEffectiveTargetScope。
    /// 后者只服务选向/预览；若混用，同技能上 Override Self 的治疗会把伤害也解析成打自己。
    /// </summary>
    protected virtual List<AbilitySystemComponent> GetTargets(AbilitySystemComponent owner, AbilityActivationContext context)
    {
        return BattleTargeting.ResolveByScope(
            owner,
            context,
            targetScope,
            GetAreaRadiusMeters(),
            GetAreaWidthMeters(),
            GetSectorHalfAngleDegrees());
    }
}

/// <summary>效果触发阶段 — 由动画事件驱动 OnHit~OnHit4 / OnComplete。</summary>
public enum AbilityEffectPhase
{
    Immediate = 0,
    OnHit = 1,
    OnComplete = 2,
    OnHit2 = 3,
    OnHit3 = 4,
    OnHit4 = 5
}

/// <summary>Effect 目标解析方式。</summary>
public enum EffectTargetSelection
{
    /// <summary>按所属 GA 的 targetScope 解析（AreaAroundSelf / Self / Area 等）。</summary>
    FromAbility,
    /// <summary>仅使用 context.explicitTargets（旧行为）。</summary>
    ExplicitOnly,
    /// <summary>使用本 Effect 的 targetScope（可另配半径覆盖）。</summary>
    Override
}

/// <summary>
/// 技能效果基类。默认按 sourceAbility 解析目标；突进/范围类 Effect 可重写 Execute(caster, context) 读取 direction / targetCell。
/// 条件分支（如狂暴加强）在 Effect 上配 requiredCasterTags / blockCasterTags，勿拆技能。
/// </summary>
[System.Serializable]
public abstract class AbilityEffect
{
    [TextArea(1, 3)] public string description;

    [Tooltip("Immediate=施法瞬间；OnHit~OnHit4=多段命中事件；OnComplete=收招事件")]
    public AbilityEffectPhase phase = AbilityEffectPhase.OnHit;

    [Tooltip("FromAbility=按 GA；ExplicitOnly=仅 explicitTargets；Override=用下方 targetScope")]
    public EffectTargetSelection targetSelection = EffectTargetSelection.FromAbility;

    [Tooltip("仅 targetSelection=Override 时生效")]
    public TargetScope targetScope = TargetScope.AreaAroundSelf;

    [Tooltip("Override 时 >0 覆盖 GA 半径（米）；≤0 用 GA。避免与突进长度共用半径")]
    public float areaRadiusMetersOverride;

    [Tooltip("触发概率 0~1；1=必触发。眩晕等概率效果在此配置")]
    [Range(0f, 1f)] public float chance = 1f;

    [Tooltip("施法者须持有（含 Buff 类别匹配，如 Buff.Anger）；空=不限制")]
    public List<GameplayTag> requiredCasterTags = new List<GameplayTag>();

    [Tooltip("施法者持有任一则跳过本效果；空=不限制")]
    public List<GameplayTag> blockCasterTags = new List<GameplayTag>();

    [Header("目标特效")]
    [Tooltip("对本效果实际命中的每个目标播放；范围/突进/拉取请配此项。锚点相对受击者时用 CasterRoot / NamedPoint")]
    public VfxSpawnEntry targetVfx;

    /// <summary>未通过概率检定则跳过本效果。</summary>
    protected bool RollChance() => chance >= 1f || UnityEngine.Random.value <= chance;

    /// <summary>标签门闩 + 概率。子类重写 Execute 时须在入口调用。</summary>
    protected bool ShouldExecute(AbilitySystemComponent caster)
    {
        if (caster == null) return false;
        if (!MeetsCasterTagGates(caster)) return false;
        return RollChance();
    }

    /// <summary>仅标签门闩（不含概率）。预览/有效范围解析也会调用。</summary>
    public bool PassesCasterTagGates(AbilitySystemComponent caster)
    {
        if (caster == null) return false;

        if (blockCasterTags != null)
        {
            for (int i = 0; i < blockCasterTags.Count; i++)
            {
                var tag = blockCasterTags[i];
                if (string.IsNullOrEmpty(tag.TagName)) continue;
                if (caster.HasActiveEffectCategory(tag)) return false;
            }
        }

        if (requiredCasterTags != null)
        {
            for (int i = 0; i < requiredCasterTags.Count; i++)
            {
                var tag = requiredCasterTags[i];
                if (string.IsNullOrEmpty(tag.TagName)) continue;
                if (!caster.HasActiveEffectCategory(tag)) return false;
            }
        }

        return true;
    }

    /// <summary>仅标签门闩（不含概率）。</summary>
    protected bool MeetsCasterTagGates(AbilitySystemComponent caster) => PassesCasterTagGates(caster);

    /// <summary>按本 Effect 的 targetSelection 解析目标。</summary>
    protected List<AbilitySystemComponent> ResolveTargets(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (targetSelection == EffectTargetSelection.ExplicitOnly || caster == null)
            return context.GetExplicitTargets();

        if (targetSelection == EffectTargetSelection.Override)
        {
            if (sourceAbility == null)
                return BattleTargeting.ResolveByScope(
                    caster, context, targetScope, areaRadiusMetersOverride, 0f);

            return sourceAbility.ResolveEffectTargets(
                caster, context, targetScope, areaRadiusMetersOverride);
        }

        return BattleTargeting.ResolveEffectTargets(
            caster, sourceAbility, context, EffectTargetSelection.FromAbility);
    }

    /// <summary>对单个目标播 targetVfx（未配置则跳过）。</summary>
    protected void PlayTargetVfx(AbilitySystemComponent caster, AbilitySystemComponent target)
    {
        if (target == null || targetVfx == null || !targetVfx.IsValid) return;
        caster?.PlayTargetEffect(target, targetVfx);
    }

    /// <summary>对目标列表逐个播 targetVfx。</summary>
    protected void PlayTargetVfx(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (targets == null || targetVfx == null || !targetVfx.IsValid) return;
        for (int i = 0; i < targets.Count; i++)
            PlayTargetVfx(caster, targets[i]);
    }

    public virtual void Execute(AbilitySystemComponent caster, AbilityActivationContext context)
    {
        Execute(caster, null, context);
    }

    public virtual void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster)) return;

        var targets = ResolveTargets(caster, sourceAbility, context);
        Execute(caster, targets);
        PlayTargetVfx(caster, targets);
    }

    public abstract void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets);
}
