using UnityEngine;

/// <summary>
/// Animator 动画事件入口。Clip 上配置：
///   攻击 — OnAbilityHit / OnAbilityComplete / OnAbilityCastVfx（可选）
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

    public void OnAbilityHit() => motor?.OnAbilityHitEvent();

    public void OnAbilityComplete() => motor?.OnAbilityCompleteEvent();

    public void OnAbilityCastVfx() => motor?.PlayActiveAbilityCastVfx();

    public void OnHitComplete() => motor?.OnHitCompleteEvent();
}
