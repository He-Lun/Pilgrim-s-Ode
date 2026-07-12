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

    /// <summary>本回合是否可发起移动。引导中不封锁：点地/移动会先取消引导。</summary>
    public bool CanAcceptMove =>
        CanPerformPlayerAction
        && !isMoving
        && stateMachine.CanTransitionTo(CharacterStateType.Move);

    /// <summary>是否可进入技能表现（含插入行动；移动中不可施法）。</summary>
    public bool CanAcceptAbilityPresentation()
    {
        if (IsDead || IsStunned || isMoving || stateMachine.CurrentType == CharacterStateType.Ability)
            return false;

        bool isInsert = TurnManager.Instance != null
                        && TurnManager.Instance.CurrentActor != asc;

        if (!isInsert && !CanPerformPlayerAction)
            return false;

        return stateMachine.CanTransitionTo(CharacterStateType.Ability);
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
            new StunState()
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
            asc?.NotifyMoved(distanceMeters);
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

    /// <summary>动画事件 OnAbilityCastVfx</summary>
    public void PlayActiveAbilityCastVfx() => PlayAbilityVfx(VfxTiming.OnCast);

    /// <summary>动画事件 OnAbilityHit</summary>
    public void OnAbilityHitEvent()
    {
        if (asc == null || !asc.HasPendingAbility) return;
        if (stateMachine.CurrentType != CharacterStateType.Ability) return;

        PlayAbilityVfx(VfxTiming.OnHit);

        asc.ResolvePendingAbilityPhase(AbilityEffectPhase.OnHit);
    }

    /// <summary>动画事件 OnAbilityComplete</summary>
    public void OnAbilityCompleteEvent()
    {
        if (asc == null || stateMachine.CurrentType != CharacterStateType.Ability) return;
        if (!asc.HasPendingAbility) return;

        PlayAbilityVfx(VfxTiming.OnComplete);

        asc.ResolvePendingAbilityPhase(AbilityEffectPhase.OnComplete);
        asc.ClearPendingAbility();
        ClearActiveAbilityPresentation();
        ReturnToIdle();
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

        if (stateMachine.CurrentType == CharacterStateType.Hit)
        {
            animatorDriver?.TriggerHit();
            return;
        }

        if (!stateMachine.TryTransition(CharacterStateType.Hit, default, force: true))
            return;

        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = CombatEventType.HitReacted,
            target = asc
        });
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
