using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗事件类型
/// </summary>
public enum CombatEventType
{
    AbilityUsed,
    DamageDealt,
    DamageTaken,
    /// <summary>献祭/自残扣血（不进 Hit；与 DamageTaken 分离）。</summary>
    HealthCostApplied,
    HealApplied,
    CharacterKilled,
    CharacterMoved,
    BuffApplied,
    TurnStarted,
    TurnEnded,
    /// <summary>角色进入受击表现（霸体可阻止）。</summary>
    HitReacted,
    /// <summary>角色进入眩晕表现（霸体可阻止）。</summary>
    StunEntered,
    /// <summary>月魂层数变化（intValue = 当前层数）。</summary>
    MoonSoulChanged
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
    /// <summary>CharacterMoved：本次位移折线（含起点），供领域穿行判定。</summary>
    public List<Vector3> movePathPoints;
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
