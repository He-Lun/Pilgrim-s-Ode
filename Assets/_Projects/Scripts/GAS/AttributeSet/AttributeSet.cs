using System;
using System.Collections.Generic;
using UnityEngine;

    /// <summary>
    /// 属性修改器类型
    /// </summary>
    public enum ModifierOperation
    {
        Additive,      // 加法：最终值 = 基础值 + 总值
        Multiplicative // 乘法：最终值 = 基础值 * (1 + 总值)
    }

    /// <summary>
    /// 属性修改器（用于圣骨、Buff动态修改属性）
    /// </summary>
    [Serializable]
    public struct AttributeModifier
    {
        public string attributeName;         // 属性名称
        public float value;                  // 修改值
        public ModifierOperation operation;  // 操作类型
        public GameplayTag sourceTag;        // 来源标签（用于追踪）
        public int durationTurns;          // 持续时间（0为永久）
        public int turnsRemaining;         // 剩余时间

        public AttributeModifier(string name, float val, ModifierOperation op, GameplayTag src, int dur = 0)
        {
            attributeName = name;
            value = val;
            operation = op;
            sourceTag = src;
            durationTurns = dur;
            turnsRemaining = dur;
        }

        //是否过期
        public bool IsExpired() => durationTurns > 0 && turnsRemaining <= 0;

        //结算一次
        public void Tick(int turn) { if (durationTurns > 0) turnsRemaining -= turn; }
    }

    /// <summary>
    /// 属性集合，挂载在角色身上管理所有数值
    /// </summary>
    public class AttributeSet : MonoBehaviour
    {
        [Header("========== 基础属性 ==========")]
        [Header("基础生命值")]
        [SerializeField] private float baseHealth = 100f;
        [Header("基础攻击力")]
        [SerializeField] private float baseAttack = 10f;
        [Header("基础防御力")]
        [SerializeField] private float baseDefense = 5f;
        [Header("基础敏捷值（决定行动频率）")]
        [SerializeField] private float baseAgility = 10f;
        [Header("速度（每回合移动力点数，×1.5m 即为移动米数，BG3 约 5–8 点）")]
        [SerializeField] private float baseSpeed = 6f;

        [Header("========== 运行时当前值 ==========")]
        [Header("当前生命值")]
        [SerializeField] private float currentHealth=100f;

        [Header("========== 修改器列表 ==========")]
        [SerializeField] private List<AttributeModifier> modifiers = new List<AttributeModifier>();

        // ---------- 属性变更事件 ----------
        public Action<float> OnHealthChanged;
        public Action<float> OnActionValueChanged;
        public Action<string, float, float> OnAttributeChanged; // 属性名, 旧值, 新值

        // ---------- 修改器(Buff)生命周期事件 ----------
        /// <summary>某 sourceTag 的修改器新增（含刷新）时触发。</summary>
        public Action<GameplayTag> OnModifierAdded;
        /// <summary>某 sourceTag 的修改器全部移除（过期/被驱散）时触发 —— buff 特效据此销毁。</summary>
        public Action<GameplayTag> OnModifierRemoved;

        // ---------- 属性访问器 ----------
        public float CurrentHealth
        {
            get => currentHealth;
            private set
            {
                float old = currentHealth;
                currentHealth = Mathf.Max(0, value);
                OnHealthChanged?.Invoke(currentHealth);
                OnAttributeChanged?.Invoke("Health", old, currentHealth);
            }
        }

        public float MaxHealth => GetFinalValue("Health", baseHealth);
        public float Attack => GetFinalValue("Attack", baseAttack);
        public float Defense => GetFinalValue("Defense", baseDefense);
        public float Agility => GetFinalValue("Agility", baseAgility);
        public float Speed => GetFinalValue("Speed", baseSpeed);

        // ---------- 初始化 ----------
        public void Initialize(CharacterDataSO data)
        {
            baseHealth = data.baseHealth;
            baseAttack = data.baseAttack;
            baseDefense = data.baseDefense;
            baseSpeed = data.baseSpeed;
            baseAgility=data.baseAgility;

            CurrentHealth = baseHealth;
            modifiers.Clear();
        }

        // ---------- 计算最终属性值 ----------
        private float GetFinalValue(string attributeName, float baseValue)
        {
            float additive = 0f;
            float multiplicative = 0f;

            foreach (var mod in modifiers)
            {
                if (mod.attributeName != attributeName || mod.IsExpired()) continue;

                switch (mod.operation)
                {
                    case ModifierOperation.Additive:
                        additive += mod.value;
                        break;
                    case ModifierOperation.Multiplicative:
                        multiplicative += mod.value;
                        break;
                }
            }

            return Mathf.Max(0, (baseValue + additive) * (1f + multiplicative));
        }

        // ---------- 修改器管理 ----------
        public void AddModifier(AttributeModifier modifier)
        {
            // 如果已有同源同属性的修改器，覆盖它
            for (int i = 0; i < modifiers.Count; i++)
            {
                var existing = modifiers[i];
                if (existing.attributeName == modifier.attributeName && 
                    existing.sourceTag.Matches(modifier.sourceTag))
                {
                    modifiers[i] = modifier;
                    OnModifierAdded?.Invoke(modifier.sourceTag);
                    return;
                }
            }
            modifiers.Add(modifier);
            OnModifierAdded?.Invoke(modifier.sourceTag);
        }

        public void RemoveModifier(GameplayTag sourceTag, string attributeName = null)
        {
            bool removedAny = false;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                var mod = modifiers[i];
                if (mod.sourceTag.Matches(sourceTag))
                {
                    if (string.IsNullOrEmpty(attributeName) || mod.attributeName == attributeName)
                    {
                        modifiers.RemoveAt(i);
                        removedAny = true;
                    }
                }
            }

            if (removedAny && !HasModifierWithTag(sourceTag))
                OnModifierRemoved?.Invoke(sourceTag);
        }

        /// <summary>是否仍有该来源标签的活跃(未过期)修改器。</summary>
        public bool HasModifierWithTag(GameplayTag sourceTag)
        {
            foreach (var mod in modifiers)
            {
                if (mod.sourceTag.Matches(sourceTag) && !mod.IsExpired())
                    return true;
            }
            return false;
        }

        public void RemoveAllModifiers()
        {
            modifiers.Clear();
        }

        // ---------- 生命周期更新 ----------
        //由TurnManager调用
        public void TickModifiers(int turn)
        {
            List<GameplayTag> expiredTags = null;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                mod.Tick(turn);
                modifiers[i] = mod;
                if (mod.IsExpired())
                {
                    expiredTags ??= new List<GameplayTag>();
                    if (!expiredTags.Contains(mod.sourceTag))
                        expiredTags.Add(mod.sourceTag);
                }
            }

            if (expiredTags == null) return;

            modifiers.RemoveAll(m => m.IsExpired());

            // 仅在该来源标签不再有活跃修改器时广播移除（buff 特效据此销毁）。
            foreach (var tag in expiredTags)
            {
                if (!HasModifierWithTag(tag))
                    OnModifierRemoved?.Invoke(tag);
            }
        }

        private AbilitySystemComponent cachedAsc;

        void Awake()
        {
            cachedAsc = GetComponent<AbilitySystemComponent>();
        }

        // ---------- 伤害/治疗快捷方法 ----------测试用
        public void TakeDamage(float damage, GameplayTag damageType, AbilitySystemComponent instigator = null)
        {
            float finalDamage = Mathf.Max(1, damage - Defense);
            float healthBefore = CurrentHealth;
            CurrentHealth -= finalDamage;
            Debug.Log($"[AttributeSet] 受到 {finalDamage} 点 {damageType} 伤害，剩余血量: {CurrentHealth}");

            var targetAsc = cachedAsc ?? GetComponent<AbilitySystemComponent>();

            if (instigator != null)
            {
                CombatEventBus.Instance.Raise(new CombatEvent
                {
                    type = CombatEventType.DamageDealt,
                    instigator = instigator,
                    target = targetAsc,
                    value = finalDamage,
                    tag = damageType
                });
            }

            if (targetAsc != null)
            {
                CombatEventBus.Instance.Raise(new CombatEvent
                {
                    type = CombatEventType.DamageTaken,
                    instigator = instigator,
                    target = targetAsc,
                    value = finalDamage,
                    tag = damageType
                });
            }

            if (healthBefore > 0 && CurrentHealth <= 0 && targetAsc != null)
                targetAsc.NotifyDeath(instigator);
        }

        public void Heal(float amount, AbilitySystemComponent instigator = null, AbilitySystemComponent target = null)
        {
            float actualHeal = Mathf.Min(amount, MaxHealth - CurrentHealth);
            if (actualHeal <= 0) return;

            float newHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            CurrentHealth = newHealth;
            Debug.Log($"[AttributeSet] 治疗 {actualHeal} 点，当前血量: {CurrentHealth}");

            var targetAsc = target ?? cachedAsc ?? GetComponent<AbilitySystemComponent>();
            if (instigator != null && targetAsc != null)
            {
                CombatEventBus.Instance.Raise(new CombatEvent
                {
                    type = CombatEventType.HealApplied,
                    instigator = instigator,
                    target = targetAsc,
                    value = actualHeal
                });
            }
        }

        public bool IsDead() => CurrentHealth <= 0;

        // ---------- 调试信息 ----------
        public string GetDebugInfo()
        {
            return $"HP: {CurrentHealth}/{MaxHealth} | ATK: {Attack} | DEF: {Defense} | AGI: {Agility} | SP: {Speed}";
        }
    }
