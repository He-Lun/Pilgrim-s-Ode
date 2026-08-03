using System;
using UnityEngine;

/// <summary>
/// Animator 参数桥接 — 与 FSM 状态同步。
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterAnimatorDriver : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int HitRecoverHash = Animator.StringToHash("HitRecover");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int SkillIndexHash = Animator.StringToHash("SkillIndex");
    private static readonly int IsStunnedHash = Animator.StringToHash("IsStunned");

    [SerializeField] private Animator animator;

    public Animator Animator => animator;

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void SetSpeed(float speed)
    {
        if (animator == null) return;
        if (HasParameter(SpeedHash))
            animator.SetFloat(SpeedHash, speed);
    }

    public void SetMoving(bool moving)
    {
        if (animator == null) return;
        if (HasParameter(IsMovingHash))
            animator.SetBool(IsMovingHash, moving);
    }

    public void SetStunned(bool stunned)
    {
        if (animator == null) return;
        if (HasParameter(IsStunnedHash))
            animator.SetBool(IsStunnedHash, stunned);
    }

    public void TriggerHit()
    {
        if (animator == null) return;
        TrySetTrigger(HitHash);
    }

    /// <summary>动画事件 OnHitComplete 时触发，驱动 Hit→Idle 过渡。</summary>
    public void EndHitPresentation()
    {
        if (animator == null) return;
        TrySetTrigger(HitRecoverHash);
    }

    public void TriggerDeath()
    {
        if (animator == null) return;
        TrySetTrigger(DeathHash);
    }

    public void PlaySkill(AbilityPresentationEntry presentation)
    {
        if (animator == null || presentation == null || !presentation.IsConfigured) return;

        // animTrigger == SkillIndex 表示走整型 skillAnimIndex，不是 Trigger。
        if (!string.IsNullOrEmpty(presentation.animTrigger)
            && !string.Equals(presentation.animTrigger, CharacterAnimationParameters.SkillIndex, StringComparison.Ordinal))
        {
            TrySetTrigger(Animator.StringToHash(presentation.animTrigger));
        }

        if (HasIntegerParameter(SkillIndexHash))
        {
            // 先归零再设置，避免同值重复写入时 Any State 不触发。
            animator.SetInteger(SkillIndexHash, 0);
            animator.SetInteger(SkillIndexHash, presentation.skillAnimIndex);
        }
    }

    [System.Obsolete("Use PlaySkill(AbilityPresentationEntry) resolved from CharacterDataSO.")]
    public void PlaySkill(GameplayAbility ability)
    {
        PlaySkill(AbilityPresentationEntry.FromAbilityDefaults(ability));
    }

    public void StopSkill()
    {
        if (animator == null) return;
        if (HasIntegerParameter(SkillIndexHash))
            animator.SetInteger(SkillIndexHash, 0);
    }

    private bool TrySetTrigger(int hash)
    {
        if (animator == null || !TryGetParameter(hash, out var param)) return false;
        if (param.type != AnimatorControllerParameterType.Trigger) return false;

        animator.SetTrigger(hash);
        return true;
    }

    private bool HasIntegerParameter(int hash)
    {
        return TryGetParameter(hash, out var param)
               && param.type == AnimatorControllerParameterType.Int;
    }

    private bool HasParameter(int hash)
    {
        return TryGetParameter(hash, out _);
    }

    private bool TryGetParameter(int hash, out AnimatorControllerParameter param)
    {
        param = default;
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (var p in animator.parameters)
        {
            if (p.nameHash != hash) continue;
            param = p;
            return true;
        }

        return false;
    }
}
