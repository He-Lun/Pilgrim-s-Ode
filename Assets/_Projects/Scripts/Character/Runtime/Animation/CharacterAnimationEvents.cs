using UnityEngine;

/// <summary>
/// Animator 动画事件入口。Clip 上配置：
///   攻击 — OnAbilityHit~OnAbilityHit4（或 OnHit~OnHit4）
///         / OnAbilityCastVfx~OnAbilityCastVfx3（或 OnCastVfx…）
///         / OnAbilityComplete
///         / OnExit（允许其他角色行动；与 OnAbilityComplete 同帧，引导技仅 OnExit）
///   突进 — OnDashChargeStart（蓄力结束、开始位移）/ OnAbilityComplete（收招）
///   受击 — OnHitComplete
/// 表现过渡由各 Controller 状态连线负责；OnHitComplete 同时驱动逻辑 FSM 与 HitRecover。
/// </summary>
public class CharacterAnimationEvents : MonoBehaviour
{
    [SerializeField] private CharacterMotor motor;

    void Awake()
    {
        if (motor == null)
            motor = GetComponentInParent<CharacterMotor>();
    }

    public void OnAbilityHit() => motor?.OnAbilityHitEvent(AbilityEffectPhase.OnHit);
    public void OnAbilityHit2() => motor?.OnAbilityHitEvent(AbilityEffectPhase.OnHit2);
    public void OnAbilityHit3() => motor?.OnAbilityHitEvent(AbilityEffectPhase.OnHit3);
    public void OnAbilityHit4() => motor?.OnAbilityHitEvent(AbilityEffectPhase.OnHit4);

    /// <summary>与 OnAbilityHit 相同，便于在 Clip 上写短名。</summary>
    public void OnHit() => OnAbilityHit();
    public void OnHit2() => OnAbilityHit2();
    public void OnHit3() => OnAbilityHit3();
    public void OnHit4() => OnAbilityHit4();

    public void OnAbilityComplete() => motor?.OnAbilityCompleteEvent();

    /// <summary>技能对其他人“占回合”结束 — 引导技可仅配置此项以在起手后放行其他角色。</summary>
    public void OnExit() => motor?.OnAbilityExitEvent();

    /// <summary>突进蓄力结束 — 从此刻起 DashChargeState 才开始向前位移。</summary>
    public void OnDashChargeStart() => motor?.OnDashChargeStartEvent();

    public void OnAbilityCastVfx() => motor?.PlayActiveAbilityCastVfx(VfxTiming.OnCast);
    public void OnAbilityCastVfx2() => motor?.PlayActiveAbilityCastVfx(VfxTiming.OnCast2);
    public void OnAbilityCastVfx3() => motor?.PlayActiveAbilityCastVfx(VfxTiming.OnCast3);

    public void OnCastVfx() => OnAbilityCastVfx();
    public void OnCastVfx2() => OnAbilityCastVfx2();
    public void OnCastVfx3() => OnAbilityCastVfx3();

    public void OnHitComplete() => motor?.OnHitCompleteEvent();
}
