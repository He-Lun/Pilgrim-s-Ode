using System;
using System.Collections.Generic;
using UnityEngine;

    /// <summary>
    /// 能力系统组件 - 每个角色的"大脑"
    /// </summary>
    public class AbilitySystemComponent : MonoBehaviour
    {
        [Header("========== 核心引用 ==========")]
        [SerializeField] private AttributeSet attributes;
        [SerializeField] private List<GameplayTag> appliedTags = new List<GameplayTag>();
        private InspirationTaskTracker inspirationTracker;

        [Header("========== 角色配置 ==========")]
        [SerializeField] private CharacterDataSO characterData;

        [Header("========== 能力配置 ==========")]
        [SerializeField] private GameplayAbility inspirationAbility;
        [SerializeField] private List<GameplayAbility> passiveAbilities = new List<GameplayAbility>();

        private readonly List<GameplayAbility> knownAbilities = new List<GameplayAbility>();
        private GameplayAbility pendingAbility;
        private AbilityActivationContext pendingContext;
        private bool pendingHitResolved;
        private bool pendingCompleteResolved;

        [Header("========== 阵营 ==========")]
        [SerializeField] private int teamId;

        [Header("========== 阵营资源 ==========")]
        [SerializeField] private TeamResourceManager teamResource;

        public TeamResourceManager TeamResource => teamResource;
        public int TeamId => teamId;
        public CharacterDataSO CharacterData => characterData;
        public IReadOnlyList<GameplayAbility> KnownAbilities => knownAbilities;
        public GameplayAbility InspirationAbility => inspirationAbility;
        public InspirationTaskTracker InspirationTracker => inspirationTracker;
        public bool HasPendingAbility => pendingAbility != null;

        // ---------- 事件 ----------
        public Action<GameplayAbility, List<AbilitySystemComponent>> OnAbilityUsed;
        public Action<GameplayTag> OnTagAdded;
        public Action<GameplayTag> OnTagRemoved;
        public Action<AbilitySystemComponent> OnDeath;

        // ---------- 属性访问 ----------
        public AttributeSet Attributes => attributes;
        public List<GameplayAbility> PassiveAbilities => passiveAbilities;

        void Awake()
        {
            inspirationTracker ??= new InspirationTaskTracker();
        }

        void OnDestroy()
        {
            inspirationTracker?.Dispose();
        }

        // ---------- 初始化 ----------
        public void Initialize(AttributeSet attrs, TeamResourceManager resource, int team = 0)
        {
            attributes = attrs ?? GetComponent<AttributeSet>();
            teamResource = resource;
            teamId = team;
            appliedTags.Clear();
        }

        public void SetupFromCharacterData(CharacterDataSO data, TeamResourceManager resource, int team = 0)
        {
            if (data == null) return;

            characterData = data;
            Initialize(attributes ?? GetComponent<AttributeSet>(), resource, team);
            attributes?.Initialize(data);

            ApplyIdentityTags(data);
            RebuildKnownAbilities(data);

            inspirationAbility = data.inspirationAbility;

            passiveAbilities = data.innateAbilities != null
                ? new List<GameplayAbility>(data.innateAbilities)
                : new List<GameplayAbility>();

            SetupPassives();

            inspirationTracker.Initialize(data.inspirationTask, data.inspirationAbility, this);
        }

        private void ApplyIdentityTags(CharacterDataSO data)
        {
            if (!string.IsNullOrEmpty(data.job.TagName))
                AddTag(data.job);
            if (!string.IsNullOrEmpty(data.kingdom.TagName))
                AddTag(data.kingdom);
        }

        private void RebuildKnownAbilities(CharacterDataSO data)
        {
            knownAbilities.Clear();
            foreach (var ability in data.GetAllKnownAbilities())
                knownAbilities.Add(ability);
        }

        public AbilityPresentationEntry GetPresentation(GameplayAbility ability)
        {
            if (characterData != null)
                return characterData.ResolvePresentation(ability);

            return AbilityPresentationEntry.FromAbilityDefaults(ability);
        }

        public bool KnowsAbility(GameplayAbility ability)
        {
            return ability != null && knownAbilities.Contains(ability);
        }

        public void BeginAbilityActivation(GameplayAbility ability, AbilityActivationContext context)
        {
            pendingAbility = ability;
            pendingContext = context;
            pendingHitResolved = false;
            pendingCompleteResolved = false;
        }

        public void ResolvePendingAbilityPhase(AbilityEffectPhase phase)
        {
            if (pendingAbility == null) return;

            if (phase == AbilityEffectPhase.OnHit)
            {
                if (pendingHitResolved) return;
                pendingHitResolved = true;
            }
            else if (phase == AbilityEffectPhase.OnComplete)
            {
                if (pendingCompleteResolved) return;
                pendingCompleteResolved = true;
            }

            pendingAbility.ExecuteEffectsByPhase(this, pendingContext, phase);
        }

        public void ClearPendingAbility()
        {
            pendingAbility = null;
            pendingHitResolved = false;
            pendingCompleteResolved = false;
        }

        public bool HasEnoughActionPoints(int cost)
        {
            return teamResource != null && teamResource.CurrentActionPoints >= cost;
        }

        public void SetupPassives(List<GameplayAbility> passives)
        {
            passiveAbilities = passives ?? new List<GameplayAbility>();
            SetupPassives();
        }

        private void SetupPassives()
        {
            foreach (var passive in passiveAbilities)
            {
                if (passive == null) continue;
                var dummy = new List<AbilitySystemComponent> { this };
                passive.TryActivateAsInspiration(this, dummy);
            }
        }

        // ---------- 激活技能 ----------
        /// <summary>释放技能（Facade / HandCardManager 出牌后调用）。</summary>
        public AbilityActivationResult ActivateAbility(GameplayAbility ability, AbilityActivationContext context)
        {
            if (ability == null)
                return AbilityActivationResult.UnknownError;

            return ability.TryActivate(this, context);
        }

        /// <summary>兼容：仅传目标列表。</summary>
        public AbilityActivationResult ActivateAbility(GameplayAbility ability, List<AbilitySystemComponent> targets = null)
        {
            if (ability == null)
                return AbilityActivationResult.UnknownError;

            return ability.TryActivate(this, AbilityActivationContext.FromTargets(
                targets ?? new List<AbilitySystemComponent> { this }));
        }

        // TODO: HandCardManager — 按手牌索引出牌，由 Facade 取 ability 后调用 ActivateAbility(ability, targets)
        // public AbilityActivationResult ActivateAbility(int abilityIndex, List<AbilitySystemComponent> targets = null)

        // ---------- 阵营关系 ----------
        public bool IsAlly(AbilitySystemComponent other)
        {
            return other != null && other != this && teamId == other.teamId;
        }

        public bool IsEnemy(AbilitySystemComponent other)
        {
            return other != null && teamId != other.teamId;
        }

        // TODO: HandCardManager — 判断是否为当前手牌
        // public bool IsHandCard(GameplayAbility ability)

        // ---------- 标签管理 ----------
        public bool HasTag(GameplayTag tag) => appliedTags.HasTag(tag);

        public void AddTag(GameplayTag tag)
        {
            appliedTags.AddTag(tag);
            OnTagAdded?.Invoke(tag);
        }

        public void RemoveTag(GameplayTag tag)
        {
            appliedTags.RemoveTag(tag);
            OnTagRemoved?.Invoke(tag);
        }

        public bool HasAnyTag(List<GameplayTag> tags)
        {
            foreach (var tag in tags)
                if (HasTag(tag)) return true;
            return false;
        }

        public bool HasAllTags(List<GameplayTag> tags)
        {
            foreach (var tag in tags)
                if (!HasTag(tag)) return false;
            return true;
        }

        /// <summary>
        /// 施加 Buff 并广播事件（供 AbilityEffect 调用）
        /// </summary>
        public void ApplyBuffTo(AbilitySystemComponent target, GameplayTag buffTag, AbilitySystemComponent instigator = null)
        {
            target?.AddTag(buffTag);

            CombatEventBus.Instance.Raise(new CombatEvent
            {
                type = CombatEventType.BuffApplied,
                instigator = instigator ?? this,
                target = target,
                tag = buffTag
            });
        }

        /// <summary>
        /// 广播移动事件（供移动系统调用），distanceMeters 为本次移动米数。
        /// </summary>
        public void NotifyMoved(float distanceMeters)
        {
            CombatEventBus.Instance.Raise(new CombatEvent
            {
                type = CombatEventType.CharacterMoved,
                instigator = this,
                value = distanceMeters,
                intValue = Mathf.Max(1, Mathf.RoundToInt(distanceMeters))
            });
        }

        // TODO: HandCardManager — 供 UI 展示当前手牌
        // public List<GameplayAbility> GetHandCards()

        public void NotifyDeath(AbilitySystemComponent killer)
        {
            OnDeath?.Invoke(this);

            CombatEventBus.Instance.Raise(new CombatEvent
            {
                type = CombatEventType.CharacterKilled,
                instigator = killer,
                target = this
            });
        }

        // ---------- 调试信息 ----------
        public string GetDebugInfo()
        {
            string tagStr = "";
            foreach (var tag in appliedTags)
                tagStr += tag.ToString() + ", ";

            return $"{gameObject.name} | Tags: [{tagStr}] | {attributes?.GetDebugInfo()}";
        }
    }
