using System;

/// <summary>
/// 战斗事件类型
/// </summary>
public enum CombatEventType
{
    AbilityUsed,
    DamageDealt,
    DamageTaken,
    HealApplied,
    CharacterKilled,
    CharacterMoved,
    BuffApplied,
    TurnStarted,
    TurnEnded,
    /// <summary>角色进入受击表现（霸体可阻止）。</summary>
    HitReacted,
    /// <summary>角色进入眩晕表现（霸体可阻止）。</summary>
    StunEntered
}

/// <summary>
/// 统一战斗事件上下文
/// </summary>
public struct CombatEvent
{
    public CombatEventType type;
    public AbilitySystemComponent instigator;
    public AbilitySystemComponent target;
    public GameplayAbility ability;
    public AbilityActivationContext abilityContext;
    public float value;
    public GameplayTag tag;
    public int intValue;
    /// <summary>一次性目标特效（仅 PlayTargetEffect 使用）。</summary>
    public VfxSpawnEntry effectVfx;
}

/// <summary>
/// 战斗事件总线 — 供激励任务、记牌框等系统订阅
/// </summary>
public class CombatEventBus
{
    private static CombatEventBus instance;
    public static CombatEventBus Instance => instance ??= new CombatEventBus();

    public event Action<CombatEvent> OnEvent;

    public void Raise(CombatEvent evt)
    {
        OnEvent?.Invoke(evt);
    }

    public void ClearAllListeners()
    {
        OnEvent = null;
    }
}
