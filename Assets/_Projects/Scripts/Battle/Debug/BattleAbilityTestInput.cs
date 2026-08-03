using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 临时技能测试 — 按键进入选目标模式，点击射程内敌人释放技能。
/// 挂到场景中与 BattleTestBootstrap 同级；默认 1 键选技能1，左键点敌释放，Esc 取消。
/// </summary>
public class BattleAbilityTestInput : MonoBehaviour
{
    [Header("测试技能")]
    [SerializeField] private List<GameplayAbility> testAbilities = new List<GameplayAbility>();
    [SerializeField] private KeyCode[] abilityHotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

    [Header("输入")]
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private LayerMask unitLayer = ~0;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("视觉")]
    [SerializeField] private bool showCastRange = true;
    [SerializeField] private bool showTargetMarkers = true;
    [SerializeField] private Color rangeColor = new Color(1f, 0.85f, 0.2f, 0.25f);
    [SerializeField] private Color directedRectColor = new Color(1f, 0.55f, 0.15f, 0.3f);
    [SerializeField] private Color areaPreviewColor = new Color(0.25f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color areaPreviewOutlineColor = new Color(0.95f, 0.98f, 1f, 0.9f);
    [SerializeField] private Color directedArrowColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    [SerializeField] private Color validTargetColor = new Color(1f, 0.3f, 0.25f, 0.9f);
    [SerializeField] private Color hoverTargetColor = new Color(1f, 0.55f, 0.2f, 1f);
    [SerializeField] private float markerRadius = 0.45f;

    private BattleInputController battleInput;
    private MeshFilter rangeMeshFilter;
    private MeshRenderer rangeMeshRenderer;
    private LineRenderer areaOutlineLine;
    private LineRenderer directedOutlineLine;
    private LineRenderer directedArrowLine;
    private readonly List<LineRenderer> targetMarkers = new List<LineRenderer>();

    private GameplayAbility armedAbility;
    private Vector3 aimDirection = Vector3.forward;
    private Vector3 areaPreviewCenter;
    private bool hasAreaPreview;
    private readonly List<AbilitySystemComponent> validTargets = new List<AbilitySystemComponent>();
    private AbilitySystemComponent hoveredTarget;
    private string lastResultMessage = string.Empty;

    public bool IsTargeting => armedAbility != null;

    void Awake()
    {
        if (battleCamera == null)
            battleCamera = BattleCameraController.Instance?.ActiveCamera ?? Camera.main;

        battleInput = FindObjectOfType<BattleInputController>();
        EnsureRangeVisual();
    }

    void Start()
    {
        if (battleCamera == null)
            battleCamera = BattleCameraController.Instance?.ActiveCamera ?? Camera.main;
    }

    void Update()
    {
        RefreshTargetingState();
        HandleHotkeys();

        if (!IsTargeting) return;
        if (!CanUseBattleInput(out AbilitySystemComponent actor)) return;

        // 选目标模式下 Move 输入被关掉，点地/点目标都在这里取消引导
        if (Input.GetMouseButtonDown(0) && actor.IsChanneling)
            actor.InterruptRitualIfAny();

        var scope = GetTargetingScope(armedAbility, actor);

        if (scope == TargetScope.DirectedRect || scope == TargetScope.DirectedSector)
        {
            UpdateDirectedRectAim(actor);
            if (Input.GetMouseButtonDown(0))
                TryCastDirectedRect(actor);
            if (Input.GetKeyDown(cancelKey))
                CancelTargeting();
            return;
        }

        if (scope == TargetScope.Area)
        {
            RefreshAreaPreviewTargets(actor);
            if (Input.GetMouseButtonDown(0))
                TryCastArea(actor);
            if (Input.GetKeyDown(cancelKey))
                CancelTargeting();
            return;
        }

        if (scope == TargetScope.AreaAroundSelf)
        {
            RefreshAreaPreviewTargets(actor);
            if (Input.GetMouseButtonDown(0))
                TryCastAreaAroundSelf(actor, armedAbility);
            if (Input.GetKeyDown(cancelKey))
                CancelTargeting();
            return;
        }

        UpdateHoverTarget();

        if (Input.GetMouseButtonDown(0))
            TryCastOnHoveredTarget();

        if (Input.GetKeyDown(cancelKey))
            CancelTargeting();
    }

    void LateUpdate()
    {
        RefreshVisuals();
    }

    private void HandleHotkeys()
    {
        if (!CanUseBattleInput(out AbilitySystemComponent actor)) return;

        int count = Mathf.Min(testAbilities.Count, abilityHotkeys.Length);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(abilityHotkeys[i])) continue;
            var ability = testAbilities[i];
            if (ability == null)
            {
                lastResultMessage = $"技能槽 {i + 1} 未配置。";
                return;
            }

            var scope = GetTargetingScope(ability, actor);

            if (scope == TargetScope.AllAllies || scope == TargetScope.AllEnemies)
            {
                TryCastGlobalScope(actor, ability);
                return;
            }

            if (scope == TargetScope.AreaAroundSelf)
            {
                BeginAreaAroundSelfTargeting(actor, ability);
                return;
            }

            if (scope == TargetScope.DirectedRect || scope == TargetScope.DirectedSector)
            {
                BeginDirectedRectTargeting(actor, ability);
                return;
            }

            if (scope == TargetScope.Area)
            {
                BeginAreaTargeting(actor, ability);
                return;
            }

            BeginTargeting(actor, ability);
            return;
        }
    }

    private void BeginTargeting(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        armedAbility = ability;
        validTargets.Clear();
        validTargets.AddRange(
            BattleTargeting.GetValidTargetsInRange(
                caster,
                ability,
                BattleTargeting.FindAllBattleActors()));

        var scope = GetTargetingScope(ability, caster);
        if (scope != TargetScope.Self
            && scope != TargetScope.AreaAroundSelf
            && scope != TargetScope.AllAllies
            && scope != TargetScope.AllEnemies
            && scope != TargetScope.DirectedRect
            && scope != TargetScope.DirectedSector
            && scope != TargetScope.Area
            && validTargets.Count == 0)
        {
            lastResultMessage = $"「{ability.abilityName}」射程内没有合法目标（射程 {BattleTargeting.GetCastRangeMeters(ability):F1}m）。";
            armedAbility = null;
            SetMoveInputBlocked(false);
            return;
        }

        SetMoveInputBlocked(true);
        lastResultMessage = $"已选择「{ability.abilityName}」，点击高亮敌人释放（{validTargets.Count} 个可选），{cancelKey} 取消。";
        UpdateAdvancePreview(caster);
    }

    private void BeginAreaAroundSelfTargeting(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        armedAbility = ability;
        SetAbilityTargetMode(true);
        RefreshAreaPreviewTargets(caster);
        lastResultMessage = $"「{ability.abilityName}」自身范围 {ability.GetEffectiveAreaRadiusMeters(caster):F1}m：左键确认释放，{cancelKey} 取消。";
        UpdateAdvancePreview(caster);
    }

    private static TargetScope GetTargetingScope(GameplayAbility ability, AbilitySystemComponent caster)
    {
        if (ability == null || caster == null)
            return TargetScope.Self;
        if (ability.targetScope == TargetScope.AreaAroundSelf)
            return TargetScope.AreaAroundSelf;
        return ability.GetEffectiveTargetScope(caster);
    }

    private void RefreshAreaPreviewTargets(AbilitySystemComponent caster)
    {
        validTargets.Clear();
        hasAreaPreview = false;

        if (armedAbility == null || caster == null)
            return;

        var scope = GetTargetingScope(armedAbility, caster);
        if (scope == TargetScope.AreaAroundSelf)
        {
            areaPreviewCenter = caster.transform.position;
            float radius = armedAbility.GetEffectiveAreaRadiusMeters(caster);
            hasAreaPreview = radius > 0f;
            if (hasAreaPreview)
            {
                validTargets.AddRange(
                    BattleTargeting.FilterEnemiesInRadius(caster, areaPreviewCenter, radius));
            }

            return;
        }

        if (scope != TargetScope.Area)
            return;

        if (!TryRaycastGround(out Vector3 hit))
            return;

        areaPreviewCenter = hit;
        float areaRadius = armedAbility.GetAreaRadiusMeters();
        hasAreaPreview = areaRadius > 0f;
        if (hasAreaPreview)
        {
            validTargets.AddRange(
                BattleTargeting.FilterEnemiesInRadius(caster, hit, areaRadius));
        }
    }

    private void SetAbilityTargetMode(bool active)
    {
        if (battleInput == null)
            return;

        battleInput.enabled = true;
        battleInput.DisarmAbilityTargeting();
        battleInput.Mode = active ? BattleInputMode.TargetCell : BattleInputMode.Move;
    }

    private void TryCastGlobalScope(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        var context = AbilityActivationContext.Self();
        var result = caster.ActivateAbility(ability, context);

        if (result != AbilityActivationResult.Success)
        {
            lastResultMessage = $"释放失败: {result}";
            return;
        }

        lastResultMessage = $"释放「{ability.abilityName}」";
        TurnManager.Instance?.NotifyActionResolved();
    }

    private void TryCastAreaAroundSelf(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (ability == null) return;

        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        var result = caster.ActivateAbility(ability, AbilityActivationContext.Self());
        if (result != AbilityActivationResult.Success)
        {
            lastResultMessage = $"释放失败: {result}";
            return;
        }

        lastResultMessage = $"释放「{ability.abilityName}」";
        TurnManager.Instance?.NotifyActionResolved();
        CancelTargeting();
    }

    private void BeginDirectedRectTargeting(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        armedAbility = ability;
        aimDirection = FlattenDirection(caster.transform.forward);
        RefreshDirectedRectPreview(caster);
        SetMoveInputBlocked(true);
        lastResultMessage = $"「{ability.abilityName}」选向中：移动鼠标调整箭头，左键释放，{cancelKey} 取消。";
    }

    private void UpdateDirectedRectAim(AbilitySystemComponent caster)
    {
        if (!TryRaycastGround(out Vector3 hitPoint))
            return;

        Vector3 dir = hitPoint - caster.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.04f)
            return;

        aimDirection = dir.normalized;
        RefreshDirectedRectPreview(caster);
    }

    private void RefreshDirectedRectPreview(AbilitySystemComponent caster)
    {
        validTargets.Clear();
        if (armedAbility == null) return;

        if (armedAbility.GetEffectiveTargetScope(caster) == TargetScope.DirectedSector)
        {
            validTargets.AddRange(
                BattleTargeting.PreviewDirectedSectorTargets(caster, armedAbility, aimDirection));
            return;
        }

        validTargets.AddRange(
            BattleTargeting.PreviewDirectedRectTargets(caster, armedAbility, aimDirection));
    }

    private void TryCastDirectedRect(AbilitySystemComponent caster)
    {
        if (armedAbility == null) return;

        var context = AbilityActivationContext.WithAimDirection(aimDirection);
        var result = caster.ActivateAbility(armedAbility, context);

        if (result != AbilityActivationResult.Success)
        {
            lastResultMessage = $"释放失败: {result}";
            return;
        }

        int hitCount = validTargets.Count;
        lastResultMessage = hitCount > 0
            ? $"释放「{armedAbility.abilityName}」→ 命中 {hitCount} 名敌人"
            : $"释放「{armedAbility.abilityName}」（未命中）";
        TurnManager.Instance?.NotifyActionResolved();
        CancelTargeting();
    }

    private void BeginAreaTargeting(AbilitySystemComponent caster, GameplayAbility ability)
    {
        if (!CanCasterUseAbility(caster, ability, out string reason))
        {
            lastResultMessage = reason;
            return;
        }

        armedAbility = ability;
        hasAreaPreview = false;
        validTargets.Clear();
        SetAbilityTargetMode(true);
        RefreshAreaPreviewTargets(caster);
        lastResultMessage = $"「{ability.abilityName}」点地放置：移动鼠标预览范围，左键确认，{cancelKey} 取消。";
        UpdateAdvancePreview(caster);
    }

    private void TryCastArea(AbilitySystemComponent caster)
    {
        RefreshAreaPreviewTargets(caster);
        if (armedAbility == null || !hasAreaPreview)
        {
            lastResultMessage = "请点击地面选择领域位置。";
            return;
        }

        var context = AbilityActivationContext.WithTargetPoint(areaPreviewCenter);
        var result = caster.ActivateAbility(armedAbility, context);

        if (result != AbilityActivationResult.Success)
        {
            lastResultMessage = $"释放失败: {result}";
            return;
        }

        lastResultMessage = $"释放「{armedAbility.abilityName}」→ 领域已放置";
        TurnManager.Instance?.NotifyActionResolved();
        CancelTargeting();
    }

    private void CancelTargeting()
    {
        armedAbility = null;
        aimDirection = Vector3.forward;
        hasAreaPreview = false;
        validTargets.Clear();
        hoveredTarget = null;
        HideAreaDiskPreview();
        SetAbilityTargetMode(false);
        ActionBarPreviewContext.Clear();
        lastResultMessage = "已取消选目标。";
    }

    private void TryCastOnHoveredTarget()
    {
        if (armedAbility == null) return;
        if (!CanUseBattleInput(out AbilitySystemComponent caster)) return;

        AbilitySystemComponent target = hoveredTarget;

        if (armedAbility.targetScope == TargetScope.Self)
            target = caster;

        if (target == null)
        {
            lastResultMessage = "请点击射程内的合法目标。";
            return;
        }

        if (!BattleTargeting.IsValidAbilityTarget(caster, target, armedAbility))
        {
            lastResultMessage = "目标不合法或超出射程。";
            return;
        }

        var context = BuildContext(target);
        var result = caster.ActivateAbility(armedAbility, context);

        if (result != AbilityActivationResult.Success)
        {
            lastResultMessage = $"释放失败: {result}";
            return;
        }

        lastResultMessage = $"释放「{armedAbility.abilityName}」→ {target.name}";
        TurnManager.Instance?.NotifyActionResolved();
        CancelTargeting();
    }

    private static AbilityActivationContext BuildContext(AbilitySystemComponent target)
    {
        if (target == null)
            return AbilityActivationContext.Self();

        return AbilityActivationContext.SingleTarget(target);
    }

    private void RefreshTargetingState()
    {
        if (!IsTargeting) return;

        if (!CanUseBattleInput(out AbilitySystemComponent caster))
        {
            CancelTargeting();
            return;
        }

        if (!CanCasterUseAbility(caster, armedAbility, out string reason))
        {
            lastResultMessage = reason;
            CancelTargeting();
            return;
        }

        var scope = GetTargetingScope(armedAbility, caster);

        if (scope == TargetScope.DirectedRect || scope == TargetScope.DirectedSector)
        {
            RefreshDirectedRectPreview(caster);
            UpdateAdvancePreview(caster);
            return;
        }

        if (scope == TargetScope.Area || scope == TargetScope.AreaAroundSelf)
        {
            RefreshAreaPreviewTargets(caster);
            UpdateAdvancePreview(caster);
            return;
        }

        validTargets.Clear();
        validTargets.AddRange(
            BattleTargeting.GetValidTargetsInRange(
                caster,
                armedAbility,
                BattleTargeting.FindAllBattleActors()));
        UpdateAdvancePreview(caster);
    }

    private void UpdateHoverTarget()
    {
        hoveredTarget = BattleTargeting.RaycastUnit(battleCamera, unitLayer);
        if (hoveredTarget != null && !validTargets.Contains(hoveredTarget))
            hoveredTarget = null;

        UpdateAdvancePreview(CanUseBattleInput(out AbilitySystemComponent caster) ? caster : null);
    }

    private void UpdateAdvancePreview(AbilitySystemComponent caster)
    {
        if (armedAbility == null || caster == null
            || !ActionBarAbilityPreviewUtility.TryGetAdvancePercent(armedAbility, out float percent))
        {
            ActionBarPreviewContext.Clear();
            return;
        }

        var scope = GetTargetingScope(armedAbility, caster);
        AbilitySystemComponent previewTarget = null;
        if (scope == TargetScope.Self || scope == TargetScope.AreaAroundSelf)
            previewTarget = caster;
        else if (hoveredTarget != null)
            previewTarget = hoveredTarget;
        else if (validTargets.Count > 0)
            previewTarget = validTargets[0];

        if (previewTarget != null)
            ActionBarPreviewContext.SetAdvancePreview(previewTarget, percent);
        else
            ActionBarPreviewContext.Clear();
    }

    private bool CanUseBattleInput(out AbilitySystemComponent actor)
    {
        actor = null;
        if (TurnManager.Instance == null) return false;
        if (TurnManager.Instance.Phase != TurnPhase.TurnAction) return false;

        actor = TurnManager.Instance.CurrentActor;
        if (actor == null) return false;
        if (TurnManager.Instance.IsAnyonePresentingAbilityExcept(actor)) return false;
        return true;
    }

    private static bool CanCasterUseAbility(
        AbilitySystemComponent caster,
        GameplayAbility ability,
        out string reason)
    {
        reason = string.Empty;
        if (caster == null || ability == null)
        {
            reason = "角色或技能为空。";
            return false;
        }

        var motor = caster.GetComponent<CharacterMotor>();
        if (motor != null)
        {
            // 选技能/释放前先取消引导，避免被 Ability 态门闩挡住而无法走到 ActivateAbility
            caster.InterruptRitualIfAny();

            if (motor.IsDead)
            {
                reason = "角色已阵亡。";
                return false;
            }

            if (!motor.CanPerformPlayerAction)
            {
                reason = TurnManager.Instance != null
                         && TurnManager.Instance.IsAnyonePresentingAbilityExcept(caster)
                    ? "其他角色行动尚未结束，请等待。"
                    : "当前不是你的回合。";
                return false;
            }

            if (motor.IsMoving)
            {
                reason = "移动中无法放技能。";
                return false;
            }

            if (!motor.CanAcceptAbilityPresentation())
            {
                reason = motor.StateMachine != null
                         && motor.StateMachine.CurrentType == CharacterStateType.Ability
                    ? "正在施法中。"
                    : motor.StateMachine != null
                      && motor.StateMachine.CurrentType == CharacterStateType.Hit
                        ? "受击硬直中，等待 OnHitComplete 收招。"
                        : "当前状态无法施法。";
                return false;
            }
        }

        var movement = caster.GetComponent<CharacterMovementController>();
        if (movement != null && movement.IsMoving)
        {
            reason = "移动中无法放技能。";
            return false;
        }

        if (ability.CanActivate(caster) != AbilityActivationResult.Success)
        {
            reason = $"技能当前不可用（AP 或标签条件不满足）。";
            return false;
        }

        return true;
    }

    private void RefreshVisuals()
    {
        if (!IsTargeting || !CanUseBattleInput(out AbilitySystemComponent caster))
        {
            SetRangeVisible(false);
            SetDirectedVisualVisible(false);
            ClearTargetMarkers();
            HideAreaDiskPreview();
            return;
        }

        var scope = GetTargetingScope(armedAbility, caster);

        if (scope == TargetScope.Area || scope == TargetScope.AreaAroundSelf)
        {
            SetDirectedVisualVisible(false);
            RefreshAreaPreviewTargets(caster);

            if (hasAreaPreview)
            {
                float radius = scope == TargetScope.AreaAroundSelf
                    ? armedAbility.GetEffectiveAreaRadiusMeters(caster)
                    : armedAbility.GetAreaRadiusMeters();
                Color fill = scope == TargetScope.AreaAroundSelf ? directedRectColor : areaPreviewColor;
                DrawAreaDiskPreview(areaPreviewCenter, radius, fill, areaPreviewOutlineColor);
            }
            else
            {
                HideAreaDiskPreview();
            }

            if (showTargetMarkers)
                DrawTargetMarkers();
            else
                ClearTargetMarkers();
            return;
        }
        if (scope == TargetScope.DirectedRect || scope == TargetScope.DirectedSector)
        {
            HideAreaDiskPreview();
            if (showCastRange)
            {
                if (scope == TargetScope.DirectedSector)
                    DrawDirectedSectorVisual(caster);
                else
                    DrawDirectedRectVisual(caster);
            }
            else
                SetDirectedVisualVisible(false);

            if (showTargetMarkers)
                DrawTargetMarkers();
            else
                ClearTargetMarkers();
            return;
        }

        SetDirectedVisualVisible(false);

        if (showCastRange)
        {
            float radius = BattleTargeting.GetCastRangeMeters(armedAbility);
            DrawCastRangeRing(caster.transform.position, radius);
        }
        else
            SetRangeVisible(false);

        if (showTargetMarkers)
            DrawTargetMarkers();
        else
            ClearTargetMarkers();
    }

    private void DrawDirectedSectorVisual(AbilitySystemComponent caster)
    {
        if (rangeMeshFilter == null || armedAbility == null) return;

        var sector = DirectedSectorUtility.Build(
            caster.transform.position,
            aimDirection,
            armedAbility.GetAreaRadiusMeters(),
            armedAbility.GetSectorHalfAngleDegrees());

        rangeMeshFilter.mesh = DirectedSectorUtility.BuildFillMesh(sector);
        if (rangeMeshRenderer != null)
            rangeMeshRenderer.material.color = directedRectColor;

        EnsureDirectedLines();
        SetRangeVisible(true);
        SetDirectedVisualVisible(true);

        var outline = DirectedSectorUtility.GetOutlinePoints(sector);
        directedOutlineLine.positionCount = outline.Length;
        directedOutlineLine.SetPositions(outline);

        Vector3 origin = caster.transform.position + Vector3.up * 0.1f;
        Vector3 tip = origin + aimDirection * (armedAbility.GetAreaRadiusMeters() + 0.35f);
        directedArrowLine.positionCount = 2;
        directedArrowLine.SetPosition(0, origin);
        directedArrowLine.SetPosition(1, tip);
        directedOutlineLine.enabled = true;
        directedArrowLine.enabled = true;
    }

    private void DrawDirectedRectVisual(AbilitySystemComponent caster)
    {
        if (rangeMeshFilter == null || armedAbility == null) return;

        var rect = DirectedRectUtility.Build(
            caster.transform.position,
            aimDirection,
            armedAbility.GetAreaRadiusMeters(),
            armedAbility.GetAreaWidthMeters());

        rangeMeshFilter.mesh = DirectedRectUtility.BuildFillMesh(rect);
        if (rangeMeshRenderer != null)
            rangeMeshRenderer.material.color = directedRectColor;
        SetRangeVisible(true);

        EnsureDirectedLines();
        var corners = DirectedRectUtility.GetCorners(rect);
        directedOutlineLine.positionCount = 5;
        for (int i = 0; i < 4; i++)
            directedOutlineLine.SetPosition(i, corners[i]);
        directedOutlineLine.SetPosition(4, corners[0]);
        directedOutlineLine.enabled = true;

        Vector3 origin = caster.transform.position + Vector3.up * 0.1f;
        Vector3 tip = origin + aimDirection * (armedAbility.GetAreaRadiusMeters() + 0.35f);
        directedArrowLine.positionCount = 2;
        directedArrowLine.SetPosition(0, origin);
        directedArrowLine.SetPosition(1, tip);
        directedArrowLine.enabled = true;
    }

    private void EnsureDirectedLines()
    {
        if (directedOutlineLine == null)
            directedOutlineLine = CreateLineRenderer("DirectedRectOutline", 0.05f, directedArrowColor, loop: true);
        if (directedArrowLine == null)
            directedArrowLine = CreateLineRenderer("DirectedRectArrow", 0.06f, directedArrowColor, loop: false);
    }

    private void SetDirectedVisualVisible(bool visible)
    {
        if (directedOutlineLine != null)
            directedOutlineLine.enabled = visible;
        if (directedArrowLine != null)
            directedArrowLine.enabled = visible;
    }

    private bool TryRaycastGround(out Vector3 hitPoint)
    {
        hitPoint = default;
        if (battleCamera == null) return false;

        Ray ray = battleCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayer, QueryTriggerInteraction.Ignore))
            return false;

        hitPoint = BattleTargeting.ProjectToGround(hit.point);
        return true;
    }

    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private void DrawCastRangeRing(Vector3 center, float radiusMeters)
    {
        if (radiusMeters <= 0f || rangeMeshFilter == null)
        {
            HideAreaDiskPreview();
            return;
        }

        DrawAreaDiskPreview(center, radiusMeters, rangeColor, areaPreviewOutlineColor);
    }

    private void DrawTargetMarkers()
    {
        EnsureMarkerPool(validTargets.Count);

        for (int i = 0; i < targetMarkers.Count; i++)
        {
            var line = targetMarkers[i];
            if (i >= validTargets.Count)
            {
                line.enabled = false;
                continue;
            }

            var target = validTargets[i];
            Color color = target == hoveredTarget ? hoverTargetColor : validTargetColor;
            DrawFootCircle(line, target.transform.position, markerRadius, color);
        }
    }

    private static void DrawFootCircle(LineRenderer line, Vector3 center, float radius, Color color)
    {
        const int segments = 24;
        line.positionCount = segments + 1;
        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            var point = center + new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius);
            line.SetPosition(i, point);
        }

        line.enabled = true;
    }

    private void DrawAreaDiskPreview(Vector3 center, float radius, Color fill, Color outline)
    {
        if (rangeMeshFilter == null || radius <= 0f)
        {
            HideAreaDiskPreview();
            return;
        }

        center = BattleTargeting.ProjectToGround(center);
        rangeMeshFilter.transform.SetPositionAndRotation(center, Quaternion.identity);
        rangeMeshFilter.mesh = CircleDiskMeshBuilder.BuildLocal(radius, 64);
        rangeMeshRenderer.material.color = fill;
        SetRangeVisible(true);

        if (areaOutlineLine == null)
            areaOutlineLine = CreateLineRenderer("AreaPreviewOutline", 0.06f, outline);

        const int segments = 64;
        float y = center.y + CircleDiskMeshBuilder.DefaultYOffset + 0.02f;
        areaOutlineLine.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            areaOutlineLine.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                y,
                center.z + Mathf.Sin(angle) * radius));
        }

        areaOutlineLine.startColor = outline;
        areaOutlineLine.endColor = outline;
        areaOutlineLine.enabled = true;
    }

    private void HideAreaDiskPreview()
    {
        SetRangeVisible(false);
        if (areaOutlineLine != null)
            areaOutlineLine.enabled = false;
    }

    private void EnsureRangeVisual()
    {
        var rangeGo = new GameObject("AbilityCastRangeMesh");
        rangeGo.transform.SetParent(transform, false);
        rangeMeshFilter = rangeGo.AddComponent<MeshFilter>();
        rangeMeshRenderer = rangeGo.AddComponent<MeshRenderer>();
        rangeMeshRenderer.material = CreateTransparentMaterial(rangeColor);
        rangeMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        rangeMeshRenderer.receiveShadows = false;
    }

    private void EnsureMarkerPool(int count)
    {
        while (targetMarkers.Count < count)
        {
            var line = CreateLineRenderer($"AbilityTargetMarker_{targetMarkers.Count}", 0.04f, validTargetColor);
            targetMarkers.Add(line);
        }
    }

    private LineRenderer CreateLineRenderer(string name, float width, Color color, bool loop = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);

        if (shader.name.Contains("Universal Render Pipeline"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }

    private void SetRangeVisible(bool visible)
    {
        if (rangeMeshRenderer != null)
            rangeMeshRenderer.enabled = visible;
    }

    private void ClearTargetMarkers()
    {
        foreach (var line in targetMarkers)
        {
            if (line != null)
                line.enabled = false;
        }
    }

    private void SetMoveInputBlocked(bool blocked)
    {
        if (battleInput == null)
            return;

        if (!blocked)
        {
            battleInput.DisarmAbilityTargeting();
            battleInput.enabled = true;
            return;
        }

        battleInput.DisarmAbilityTargeting();
        battleInput.enabled = false;
    }
}
