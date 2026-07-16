using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色 Motor — 逻辑 FSM 宿主。
/// 规则：Hit / 技能收招仅由动画事件驱动；逻辑进 Idle 时同步 Animator 参数。
/// 运行时标记（isMoving 等）只在 FSM 切换成功后再置位，失败不产生残留状态。
/// </summary>
[RequireComponent(typeof(AbilitySystemComponent))]
public class CharacterMotor : MonoBehaviour
{
    [Header("移动表现")]
    [SerializeField] private float moveStepDuration = 0.25f;
    [SerializeField] private float metersPerStepDuration = 1.5f;

    private AbilitySystemComponent asc;
    private CharacterMovementController movement;
    private CharacterAnimatorDriver animatorDriver;
    private CharacterFacing facing;
    private AbilityVfxPlayer vfxPlayer;
    private CharacterStateMachine stateMachine;
    private float pendingMoveMeters;
    private bool isMoving;
    private GameplayAbility activeAbility;
    private AbilityActivationContext activeAbilityContext;
    private DashChargeSpec? pendingDashCharge;
    private bool dashChargeMovementAuthorized;

    public CharacterStateMachine StateMachine => stateMachine;
    public CharacterAnimatorDriver AnimatorDriver => animatorDriver;
    public CharacterFacing Facing => facing;
    public AbilitySystemComponent Asc => asc;
    public bool IsDead { get; set; }
    public bool IsMoving => isMoving;

    public float MoveStepDuration => moveStepDuration;
    public float MetersPerStepDuration => metersPerStepDuration;

    public event Action<float> OnMoveComplete;

    public bool CanPerformPlayerAction =>
        TurnManager.Instance != null
        && TurnManager.Instance.Phase == TurnPhase.TurnAction
        && TurnManager.Instance.CurrentActor == asc
        && !IsDead
        && !IsStunned;

    public bool IsStunned =>
        asc != null && asc.HasTag(GameplayTag.Debuff.Stun);

    public bool HasHyperArmor => HyperArmor.IsActive(asc);

    public bool IsChanneling =>
        asc != null && asc.IsChanneling;

    /// <summary>突进动画事件 OnDashChargeStart 已触发，DashChargeState 可开始位移。</summary>
    public bool DashChargeMovementAuthorized => dashChargeMovementAuthorized;

    /// <summary>本回合是否可发起移动。引导中不封锁：点地/移动会先取消引导。</summary>
    public bool CanAcceptMove =>
        CanPerformPlayerAction
        && !isMoving
        && stateMachine.CanTransitionTo(CharacterStateType.Move);

    /// <summary>是否可进入技能表现（含插入行动；移动中不可施法）。</summary>
    public bool CanAcceptAbilityPresentation()
    {
        if (IsDead || IsStunned || isMoving
            || stateMachine.CurrentType == CharacterStateType.Ability
            || stateMachine.CurrentType == CharacterStateType.DashCharge)
            return false;

        bool isInsert = TurnManager.Instance != null
                        && TurnManager.Instance.CurrentActor != asc;

        if (!isInsert && !CanPerformPlayerAction)
            return false;

        return stateMachine.CanTransitionTo(CharacterStateType.Ability)
               || stateMachine.CanTransitionTo(CharacterStateType.DashCharge);
    }

    private bool CanAcceptDashChargePresentation()
    {
        if (IsDead || IsStunned || isMoving
            || stateMachine.CurrentType == CharacterStateType.Ability
            || stateMachine.CurrentType == CharacterStateType.DashCharge)
            return false;

        bool isInsert = TurnManager.Instance != null
                        && TurnManager.Instance.CurrentActor != asc;

        if (!isInsert && !CanPerformPlayerAction)
            return false;

        return stateMachine.CanTransitionTo(CharacterStateType.DashCharge);
    }

    void Awake()
    {
        asc = GetComponent<AbilitySystemComponent>();
        movement = GetComponent<CharacterMovementController>();
        animatorDriver = GetComponent<CharacterAnimatorDriver>();
        facing = GetComponent<CharacterFacing>();
        vfxPlayer = GetComponent<AbilityVfxPlayer>();
        if (facing == null)
            facing = gameObject.AddComponent<CharacterFacing>();

        stateMachine = new CharacterStateMachine();
        stateMachine.Initialize(this, new ICharacterState[]
        {
            new IdleState(),
            new MoveState(),
            new AbilityState(),
            new HitState(),
            new DeathState(),
            new KnockbackState(),
            new StunState(),
            new DashChargeState(),
            new PullState()
        });
        stateMachine.TryTransition(CharacterStateType.Idle, default, force: true);
    }

    void OnEnable()
    {
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        if (asc != null)
        {
            asc.OnTagAdded += HandleTagAdded;
            asc.OnTagRemoved += HandleTagRemoved;
        }
    }

    void OnDisable()
    {
        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
        if (asc != null)
        {
            asc.OnTagAdded -= HandleTagAdded;
            asc.OnTagRemoved -= HandleTagRemoved;
        }
    }

    void Update() => stateMachine.Tick(Time.deltaTime);

    // ── 移动 ──────────────────────────────────────────

    public bool MoveAlongWorldPath(List<Vector3> waypoints, float costMeters)
    {
        if (waypoints == null || waypoints.Count == 0) return false;

        asc?.InterruptRitualIfAny();
        if (!CanAcceptMove) return false;

        var payload = CharacterStatePayload.ForWorldPath(waypoints, costMeters);
        if (!stateMachine.TryTransition(CharacterStateType.Move, payload))
            return false;

        pendingMoveMeters = costMeters;
        isMoving = true;
        return true;
    }

    public bool MoveToWorldPoint(Vector3 destination, float costMeters)
    {
        return MoveAlongWorldPath(new List<Vector3> { destination }, costMeters);
    }

    public void CompleteMovement(float distanceMeters = 0f)
    {
        float distance = distanceMeters > 0f ? distanceMeters : pendingMoveMeters;
        isMoving = false;
        pendingMoveMeters = 0f;
        ReturnToIdle();
        OnMoveComplete?.Invoke(distance);
    }

    /// <summary>移动被受击等更高优先级状态打断时，由 MoveState.Exit 调用。</summary>
    public void NotifyMovementInterrupted()
    {
        isMoving = false;
        pendingMoveMeters = 0f;
        movement?.NotifyMovementInterrupted();
    }

    public void ReturnToIdle()
    {
        if (IsDead) return;
        if (IsStunned)
        {
            BeginStunPresentation();
            return;
        }

        stateMachine.TryTransition(CharacterStateType.Idle, default, force: true);
    }

    // ── 击退 ──────────────────────────────────────────

    /// <summary>被动击退 — 绕过回合权限，进入 KnockbackState 平滑位移。</summary>
    /// <summary>Immediate 突进效果在 AbilityUsed 前写入，表现阶段切入 DashChargeState。</summary>
    public void ScheduleDashCharge(DashChargeSpec spec) => pendingDashCharge = spec;

    public bool BeginDashChargePresentation(GameplayAbility ability, AbilityActivationContext context)
    {
        if (ability == null)
            return false;

        if (!pendingDashCharge.HasValue)
            return BeginAbilityPresentation(ability, context);

        if (!CanAcceptDashChargePresentation())
        {
            pendingDashCharge = null;
            return false;
        }

        var payload = CharacterStatePayload.ForDashCharge(
            ability,
            context,
            pendingDashCharge.Value);

        if (!stateMachine.TryTransition(CharacterStateType.DashCharge, payload, force: true))
        {
            pendingDashCharge = null;
            return false;
        }

        pendingDashCharge = null;
        isMoving = true;
        dashChargeMovementAuthorized = false;
        activeAbility = ability;
        activeAbilityContext = context;
        return true;
    }

    public bool BeginKnockbackDirection(Vector3 worldDirection, float distanceMeters, float durationSeconds)
    {
        if (IsDead || distanceMeters <= 0f) return false;

        NotifyMovementInterrupted();

        var payload = CharacterStatePayload.ForKnockbackDirection(worldDirection, distanceMeters, durationSeconds);
        if (!stateMachine.TryTransition(CharacterStateType.Knockback, payload, force: true))
            return false;

        isMoving = true;
        return true;
    }

    public bool BeginKnockback(Vector3 fromCenter, float distanceMeters, float durationSeconds)
    {
        if (IsDead || distanceMeters <= 0f) return false;

        NotifyMovementInterrupted();

        var payload = CharacterStatePayload.ForKnockback(fromCenter, distanceMeters, durationSeconds);
        if (!stateMachine.TryTransition(CharacterStateType.Knockback, payload, force: true))
            return false;

        isMoving = true;
        return true;
    }

    public void CompleteKnockback(float distanceMeters)
    {
        isMoving = false;
        movement?.InvalidateReachableCache();
        if (distanceMeters > 0.01f)
        {
            movement?.FinalizeMovePathEnd(transform.position);
            asc?.NotifyMoved(distanceMeters, movement?.CopyLastMovePath());
        }
        ReturnToIdle();
    }

    // ── 拉取 ──────────────────────────────────────────

    /// <summary>被动拉取到落点 — 二次缓动，过程中保持眩晕表现。</summary>
    public bool BeginPull(Vector3 destination, float durationSeconds)
    {
        if (IsDead) return false;

        NotifyMovementInterrupted();

        var payload = CharacterStatePayload.ForPull(destination, durationSeconds);
        if (!stateMachine.TryTransition(CharacterStateType.Pull, payload, force: true))
            return false;

        isMoving = true;
        return true;
    }

    public void CompletePull(float distanceMeters)
    {
        isMoving = false;
        movement?.InvalidateReachableCache();
        if (distanceMeters > 0.01f)
        {
            movement?.FinalizeMovePathEnd(transform.position);
            asc?.NotifyMoved(distanceMeters, movement?.CopyLastMovePath());
        }
        ReturnToIdle();
    }

    // ── 技能 ──────────────────────────────────────────

    public bool BeginAbilityPresentation(GameplayAbility ability, AbilityActivationContext context)
    {
        if (ability == null || !CanAcceptAbilityPresentation())
            return false;

        var payload = CharacterStatePayload.ForAbility(ability, context);
        if (!stateMachine.TryTransition(CharacterStateType.Ability, payload))
            return false;

        activeAbility = ability;
        activeAbilityContext = context;
        return true;
    }

    /// <summary>动画事件 OnAbilityCastVfx / OnCastVfx2 / OnCastVfx3</summary>
    public void PlayActiveAbilityCastVfx(VfxTiming timing = VfxTiming.OnCast)
    {
        if (timing != VfxTiming.OnCast
            && timing != VfxTiming.OnCast2
            && timing != VfxTiming.OnCast3)
            timing = VfxTiming.OnCast;

        PlayAbilityVfx(timing);
    }

    /// <summary>动画事件 OnAbilityHit / OnHit2 / OnHit3 / OnHit4</summary>
    public void OnAbilityHitEvent(AbilityEffectPhase hitPhase = AbilityEffectPhase.OnHit)
    {
        if (asc == null || !asc.HasPendingAbility) return;
        if (stateMachine.CurrentType != CharacterStateType.Ability) return;
        if (!IsHitPhase(hitPhase)) return;

        PlayAbilityVfx(ToHitVfxTiming(hitPhase));
        asc.ResolvePendingAbilityPhase(hitPhase);
    }

    private static bool IsHitPhase(AbilityEffectPhase phase)
    {
        return phase == AbilityEffectPhase.OnHit
            || phase == AbilityEffectPhase.OnHit2
            || phase == AbilityEffectPhase.OnHit3
            || phase == AbilityEffectPhase.OnHit4;
    }

    private static VfxTiming ToHitVfxTiming(AbilityEffectPhase phase)
    {
        switch (phase)
        {
            case AbilityEffectPhase.OnHit2: return VfxTiming.OnHit2;
            case AbilityEffectPhase.OnHit3: return VfxTiming.OnHit3;
            case AbilityEffectPhase.OnHit4: return VfxTiming.OnHit4;
            default: return VfxTiming.OnHit;
        }
    }

    /// <summary>动画事件 OnAbilityComplete — Ability 与 DashCharge 共用收招。</summary>
    public void OnAbilityCompleteEvent()
    {
        if (asc == null || !asc.HasPendingAbility) return;

        var state = stateMachine.CurrentType;
        if (state != CharacterStateType.Ability && state != CharacterStateType.DashCharge)
            return;

        PlayAbilityVfx(VfxTiming.OnComplete);

        asc.ResolvePendingAbilityPhase(AbilityEffectPhase.OnComplete);
        asc.ClearPendingAbility();
        ClearActiveAbilityPresentation();
        ResetDashChargeMovementGate();
        ReturnToIdle();
    }

    /// <summary>动画事件 OnDashChargeStart — 蓄力结束，突进位移开始。</summary>
    public void OnDashChargeStartEvent()
    {
        if (stateMachine.CurrentType != CharacterStateType.DashCharge) return;
        dashChargeMovementAuthorized = true;
    }

    private void PlayAbilityVfx(VfxTiming timing)
    {
        if (activeAbility == null || asc == null || vfxPlayer == null) return;

        var presentation = asc.GetPresentation(activeAbility);
        vfxPlayer.PlayTiming(timing, presentation, activeAbilityContext, transform);
    }

    // ── 受击 ──────────────────────────────────────────

    public void BeginHitPresentation()
    {
        if (IsDead || HasHyperArmor) return;

        // 跑步/突进中受击：立刻停位移，再进 Hit（避免跑完才结算）
        InterruptLocomotionForHit();

        if (stateMachine.CurrentType == CharacterStateType.Hit)
        {
            animatorDriver?.SetMoving(false);
            animatorDriver?.SetSpeed(0f);
            animatorDriver?.TriggerHit();
            return;
        }

        if (!stateMachine.TryTransition(CharacterStateType.Hit, default, force: true))
            return;

        animatorDriver?.SetMoving(false);
        animatorDriver?.SetSpeed(0f);

        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = CombatEventType.HitReacted,
            target = asc
        });
    }

    /// <summary>受击时打断主动位移（Move / DashCharge），并结算本轮移动动作。</summary>
    private void InterruptLocomotionForHit()
    {
        var type = stateMachine.CurrentType;
        bool inLocomotion = type == CharacterStateType.Move
                            || type == CharacterStateType.DashCharge
                            || isMoving
                            || (movement != null && movement.IsMoving);

        if (!inLocomotion)
            return;

        bool wasPlayerMove = type == CharacterStateType.Move || (movement != null && movement.IsMoving);

        animatorDriver?.SetMoving(false);
        animatorDriver?.SetSpeed(0f);
        NotifyMovementInterrupted();

        if (wasPlayerMove)
            TurnManager.Instance?.NotifyActionResolved();
    }

    /// <summary>动画事件 OnHitComplete — 逻辑收招 + 驱动 Animator 离开 Hit。</summary>
    public void OnHitCompleteEvent()
    {
        if (stateMachine.CurrentType != CharacterStateType.Hit) return;
        animatorDriver?.EndHitPresentation();
        ReturnToIdle();
    }

    public void BeginDeathPresentation()
    {
        stateMachine.TryTransition(CharacterStateType.Death, default, force: true);
    }

    // ── 眩晕 ──────────────────────────────────────────

    public void BeginStunPresentation()
    {
        if (IsDead || HasHyperArmor) return;
        if (stateMachine.CurrentType == CharacterStateType.Stun) return;

        NotifyMovementInterrupted();
        ClearActiveAbilityPresentation();
        if (!stateMachine.TryTransition(CharacterStateType.Stun, default, force: true))
            return;

        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = CombatEventType.StunEntered,
            target = asc
        });
    }

    /// <summary>引导被打断后退出技能表现，回到 Idle。</summary>
    public void ReleaseFromChannel()
    {
        ClearActiveAbilityPresentation();
        asc?.ClearPendingAbility();
        if (stateMachine.CurrentType == CharacterStateType.Ability)
            stateMachine.TryTransition(CharacterStateType.Idle, default, force: true);
    }

    private void HandleTagAdded(GameplayTag tag)
    {
        if (!tag.Matches(GameplayTag.Debuff.Stun)) return;
        if (HasHyperArmor)
        {
            asc?.RemoveTag(tag);
            return;
        }

        BeginStunPresentation();
    }

    private void HandleTagRemoved(GameplayTag tag)
    {
        if (!tag.Matches(GameplayTag.Debuff.Stun)) return;
        if (stateMachine.CurrentType != CharacterStateType.Stun) return;

        stateMachine.TryTransition(CharacterStateType.Idle, default, force: true);
    }

    // ── 内部 ──────────────────────────────────────────

    private void ClearActiveAbilityPresentation()
    {
        activeAbility = null;
        activeAbilityContext = default;
        animatorDriver?.StopSkill();
    }

    private void ResetDashChargeMovementGate() => dashChargeMovementAuthorized = false;

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (evt.instigator == asc && evt.type == CombatEventType.AbilityUsed && evt.ability != null)
        {
            var ctx = evt.abilityContext;
            if (!ctx.HasExplicitTargets && !ctx.HasTargetPoint && !ctx.HasAimDirection && !ctx.HasDirection)
            {
                ctx = evt.target != null
                    ? AbilityActivationContext.SingleTarget(evt.target)
                    : AbilityActivationContext.Self();
            }

            if (!BeginDashChargePresentation(evt.ability, ctx))
                BeginAbilityPresentation(evt.ability, ctx);
            return;
        }

        if (evt.target != asc) return;

        switch (evt.type)
        {
            case CombatEventType.DamageTaken:
                if (!IsDead && asc.Attributes != null && !asc.Attributes.IsDead())
                    BeginHitPresentation();
                break;

            case CombatEventType.CharacterKilled:
                BeginDeathPresentation();
                break;
        }
    }

    [System.Obsolete("Grid movement deprecated.")]
    public Vector3 GetCellWorldPosition(Vector2Int cell)
    {
        if (movement != null)
            return movement.ApplyFootOffset(
                BattleGrid.Instance != null ? BattleGrid.Instance.CellToWorld(cell) : transform.position);

        var grid = BattleGrid.Instance;
        return grid != null ? grid.CellToWorld(cell) : transform.position;
    }

    [System.Obsolete("Use MoveToWorldPoint for NavMesh straight movement.")]
    public bool MoveAlongPath(List<Vector2Int> path)
    {
        if (path == null || path.Count < 2) return false;
        if (!CanAcceptMove) return false;

        asc?.InterruptRitualIfAny();

        var payload = CharacterStatePayload.ForMove(path);
        if (!stateMachine.TryTransition(CharacterStateType.Move, payload))
            return false;

        isMoving = true;
        return true;
    }
}
