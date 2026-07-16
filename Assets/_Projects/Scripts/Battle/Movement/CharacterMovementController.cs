using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 每角色移动规则 — NavMesh 洪水填充范围 + 绕障路径移动。
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(AbilitySystemComponent))]
public class CharacterMovementController : MonoBehaviour
{
    [Tooltip("脚底相对 NavMesh 表面的抬高量。模型轴心在脚底填 0")]
    [SerializeField] private float footGroundOffset = 0f;
    [SerializeField] private float personalSpaceRadius = 0.6f;
    [SerializeField] private bool snapToNavMeshOnStart = true;

    private CharacterMotor motor;
    private AbilitySystemComponent asc;
    private AttributeSet attributes;
    private float remainingMoveMeters;
    private bool isMoving;
    private float lastMoveCostMeters;
    private readonly List<Vector3> lastMovePath = new List<Vector3>();

    private HashSet<Vector3> cachedReachable;
    private bool reachableDirty = true;

    public float PersonalSpaceRadius => personalSpaceRadius;
    public float RemainingMoveMeters => remainingMoveMeters;
    public float LastMoveCostMeters => lastMoveCostMeters;
    public bool IsMoving => isMoving;
    /// <summary>最近一次位移折线（含起点），供领域穿行判定。</summary>
    public IReadOnlyList<Vector3> LastMovePath => lastMovePath;

    public event Action<MoveResult> OnMoveFailed;
    public event Action<float> OnMoveSucceeded;

    void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        asc = GetComponent<AbilitySystemComponent>();
        attributes = GetComponent<AttributeSet>();
    }

    void OnEnable()
    {
        BattleOccupancy.Register(this);
        if (motor != null)
            motor.OnMoveComplete += HandleMoveComplete;
    }

    void OnDisable()
    {
        BattleOccupancy.Unregister(this);
        if (motor != null)
            motor.OnMoveComplete -= HandleMoveComplete;
    }

    void Start()
    {
        if (snapToNavMeshOnStart)
            SnapToNavMesh();
    }

    public void SnapToNavMesh()
    {
        if (NavPathMovementPlanner.TrySampleNavMesh(transform.position, out Vector3 snapped))
            transform.position = snapped + Vector3.up * footGroundOffset;
    }

    public void SnapToWorldPosition(Vector3 worldPosition)
    {
        if (NavPathMovementPlanner.TrySampleNavMesh(worldPosition, out Vector3 snapped))
            transform.position = snapped + Vector3.up * footGroundOffset;
        else
            transform.position = worldPosition + Vector3.up * footGroundOffset;
    }

    public Vector3 ApplyFootOffset(Vector3 groundPoint)
    {
        return groundPoint + Vector3.up * footGroundOffset;
    }

    public void RefreshMoveBudget()
    {
        if (attributes == null) return;
        remainingMoveMeters = Mathf.Max(0f, attributes.Speed * BattleSpaceSettings.GetMetersPerSpeedPoint());
        reachableDirty = true;
    }

    /// <summary>移动被更高优先级状态打断时，由 MoveState.Exit 调用。</summary>
    public void NotifyMovementInterrupted()
    {
        isMoving = false;
        reachableDirty = true;
    }

    public HashSet<Vector3> GetReachablePoints()
    {
        if (reachableDirty || cachedReachable == null)
        {
            cachedReachable = NavReachabilityFloodFill.ComputeReachable(
                transform.position,
                remainingMoveMeters,
                BattleSpaceSettings.GetFloodFillCellSize(),
                personalSpaceRadius,
                this);
            reachableDirty = false;
        }

        return cachedReachable;
    }

    public void InvalidateReachableCache()
    {
        reachableDirty = true;
    }

    /// <summary>
    /// 平滑击退 — 进入 KnockbackState，NavMesh 射线挡墙 + 占用检测。
    /// </summary>
    public bool TryApplyKnockback(Vector3 fromCenter, float distanceMeters, float durationSeconds = 0.35f)
    {
        if (distanceMeters <= 0f || motor == null) return false;

        if (isMoving)
        {
            motor.NotifyMovementInterrupted();
            NotifyMovementInterrupted();
        }

        CaptureMovePath(transform.position, null);
        return motor.BeginKnockback(fromCenter, distanceMeters, durationSeconds);
    }

    /// <summary>沿指定世界方向击退（突进侧向推开等）。</summary>
    public bool TryApplyKnockbackDirection(
        Vector3 worldDirection,
        float distanceMeters,
        float durationSeconds = 0.35f)
    {
        if (distanceMeters <= 0f || motor == null) return false;

        if (isMoving)
        {
            motor.NotifyMovementInterrupted();
            NotifyMovementInterrupted();
        }

        CaptureMovePath(transform.position, null);
        return motor.BeginKnockbackDirection(worldDirection, distanceMeters, durationSeconds);
    }

    /// <summary>拉取到世界落点 — 二次缓动，过程中保持眩晕。</summary>
    public bool TryApplyPullToPoint(Vector3 destination, float durationSeconds = 0.55f)
    {
        if (motor == null) return false;

        if (isMoving)
        {
            motor.NotifyMovementInterrupted();
            NotifyMovementInterrupted();
        }

        CaptureMovePath(transform.position, new List<Vector3> { destination });
        return motor.BeginPull(destination, durationSeconds);
    }

    /// <summary>被动位移结束时校正路径终点（拉取/击退）。</summary>
    public void FinalizeMovePathEnd(Vector3 worldEnd)
    {
        if (lastMovePath.Count == 0)
        {
            lastMovePath.Add(worldEnd);
            return;
        }

        if (lastMovePath.Count == 1)
            lastMovePath.Add(worldEnd);
        else
            lastMovePath[lastMovePath.Count - 1] = worldEnd;
    }

    public List<Vector3> CopyLastMovePath()
    {
        return lastMovePath.Count > 0 ? new List<Vector3>(lastMovePath) : null;
    }

    private void CaptureMovePath(Vector3 start, List<Vector3> waypoints)
    {
        lastMovePath.Clear();
        lastMovePath.Add(start);

        if (waypoints == null) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 wp = waypoints[i];
            if (BattleOccupancy.HorizontalDistance(lastMovePath[lastMovePath.Count - 1], wp) < 0.05f)
                continue;
            lastMovePath.Add(wp);
        }
    }

    [System.Obsolete("Use TryApplyKnockback for smooth NavMesh-aware knockback.")]
    public bool TryForceKnockback(Vector3 fromCenter, float distanceMeters)
    {
        return TryApplyKnockback(fromCenter, distanceMeters);
    }

    public MovePlan TryPreviewMove(Vector3 targetWorldPoint)
    {
        return NavPathMovementPlanner.TryPlan(
            transform.position,
            targetWorldPoint,
            remainingMoveMeters,
            personalSpaceRadius,
            this,
            GetReachablePoints());
    }

    public MoveResult TryMoveToWorldPoint(Vector3 targetWorldPoint)
    {
        if (motor == null || !motor.CanPerformPlayerAction)
        {
            Fail(MoveResult.NotYourTurn);
            return MoveResult.NotYourTurn;
        }

        if (!motor.CanAcceptMove)
        {
            var blocked = motor.IsMoving || isMoving ? MoveResult.AlreadyMoving : MoveResult.Blocked;
            Fail(blocked);
            return blocked;
        }

        var plan = TryPreviewMove(targetWorldPoint);
        if (!plan.isValid)
        {
            Fail(plan.result);
            return plan.result;
        }

        return ExecuteMove(plan);
    }

    private MoveResult ExecuteMove(MovePlan plan)
    {
        lastMoveCostMeters = plan.costMeters;
        remainingMoveMeters = Mathf.Max(0f, remainingMoveMeters - plan.costMeters);
        isMoving = true;
        reachableDirty = true;

        Vector3 start = transform.position;
        var waypoints = new List<Vector3>(plan.pathPoints.Count);
        foreach (var p in plan.pathPoints)
        {
            Vector3 wp = ApplyFootOffset(p);
            if (waypoints.Count == 0 && BattleOccupancy.HorizontalDistance(start, wp) < 0.2f)
                continue;
            waypoints.Add(wp);
        }

        if (waypoints.Count == 0)
            waypoints.Add(ApplyFootOffset(plan.destination));

        CaptureMovePath(start, waypoints);

        if (!motor.MoveAlongWorldPath(waypoints, plan.costMeters))
        {
            remainingMoveMeters += plan.costMeters;
            isMoving = false;
            reachableDirty = true;
            lastMovePath.Clear();
            Fail(MoveResult.AlreadyMoving);
            return MoveResult.AlreadyMoving;
        }

        return MoveResult.Success;
    }

    private void HandleMoveComplete(float distanceMeters)
    {
        isMoving = false;
        reachableDirty = true;
        FinalizeMovePathEnd(transform.position);
        asc?.NotifyMoved(distanceMeters, CopyLastMovePath());
        OnMoveSucceeded?.Invoke(distanceMeters);
        TurnManager.Instance?.NotifyActionResolved();
    }

    private void Fail(MoveResult result)
    {
        OnMoveFailed?.Invoke(result);
    }
}
