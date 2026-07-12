using UnityEngine;

/// <summary>
/// 战斗输入模式（Move 已实现，其余供 HandCardManager 复用）。
/// </summary>
public enum BattleInputMode
{
    Move,
    TargetUnit,
    TargetCell,
    TargetDirection
}

/// <summary>
/// 点击地面移动 — 洪水填充范围 + NavMesh 绕障路径预览。
/// </summary>
public class BattleInputController : MonoBehaviour
{
    [SerializeField] private Camera battleCamera;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private BattleInputMode mode = BattleInputMode.Move;

    [Header("移动范围（洪水填充）")]
    [SerializeField] private bool showMoveRange = true;
    [SerializeField] private Color rangeColor = new Color(0.2f, 1f, 0.35f, 0.35f);

    [Header("路径预览")]
    [SerializeField] private bool showMovePreview = true;
    [SerializeField] private float previewLineWidth = 0.05f;
    [SerializeField] private Color previewValidColor = new Color(0.2f, 1f, 0.4f, 0.9f);
    [SerializeField] private Color previewInvalidColor = new Color(1f, 0.25f, 0.2f, 0.9f);

    private LineRenderer previewLine;
    private MeshFilter rangeMeshFilter;
    private MeshRenderer rangeMeshRenderer;
    private CharacterMovementController cachedMovement;
    private MovePlan cachedPreview;

    public BattleInputMode Mode
    {
        get => mode;
        set => mode = value;
    }

    void Awake()
    {
        if (battleCamera == null)
            battleCamera = Camera.main;

        EnsureVisuals();
    }

    void Update()
    {
        RefreshVisuals();

        if (!Input.GetMouseButtonDown(0)) return;
        if (TurnManager.Instance == null) return;
        if (TurnManager.Instance.Phase != TurnPhase.TurnAction) return;

        var actor = TurnManager.Instance.CurrentActor;
        if (actor == null) return;

        if (mode == BattleInputMode.Move)
            HandleMoveInput(actor);
    }

    private void HandleMoveInput(AbilitySystemComponent actor)
    {
        // 点地取消引导：放在所有 early-return 之前
        if (actor.IsChanneling)
            actor.InterruptRitualIfAny();

        var movement = actor.GetComponent<CharacterMovementController>();
        if (movement == null || movement.IsMoving) return;
        if (!TryRaycastGround(out Vector3 hitPoint)) return;

        movement.TryMoveToWorldPoint(hitPoint);
    }

    private void RefreshVisuals()
    {
        cachedMovement = null;

        if (TurnManager.Instance == null
            || TurnManager.Instance.Phase != TurnPhase.TurnAction
            || mode != BattleInputMode.Move)
        {
            SetRangeVisible(false);
            SetPreviewVisible(false);
            return;
        }

        var actor = TurnManager.Instance.CurrentActor;
        if (actor == null)
        {
            SetRangeVisible(false);
            SetPreviewVisible(false);
            return;
        }

        cachedMovement = actor.GetComponent<CharacterMovementController>();
        if (cachedMovement == null)
        {
            SetRangeVisible(false);
            SetPreviewVisible(false);
            return;
        }

        UpdateRangeFloodFill(cachedMovement);

        if (showMovePreview && TryRaycastGround(out Vector3 hitPoint))
            UpdatePreviewPath(cachedMovement, hitPoint);
        else
            SetPreviewVisible(false);
    }

    private void UpdateRangeFloodFill(CharacterMovementController movement)
    {
        if (!showMoveRange || rangeMeshFilter == null)
        {
            SetRangeVisible(false);
            return;
        }

        if (movement.RemainingMoveMeters <= 0.01f)
        {
            SetRangeVisible(false);
            return;
        }

        float cellSize = BattleSpaceSettings.GetFloodFillCellSize();
        var reachable = movement.GetReachablePoints();
        rangeMeshFilter.mesh = MoveRangeMeshBuilder.Build(reachable, cellSize);
        SetRangeVisible(reachable != null && reachable.Count > 0);
    }

    private void UpdatePreviewPath(CharacterMovementController movement, Vector3 hitPoint)
    {
        if (previewLine == null)
        {
            SetPreviewVisible(false);
            return;
        }

        cachedPreview = movement.TryPreviewMove(hitPoint);
        var points = cachedPreview.isValid && cachedPreview.pathPoints != null && cachedPreview.pathPoints.Count > 0
            ? cachedPreview.pathPoints
            : null;

        if (points == null || points.Count < 2)
        {
            Vector3 start = movement.transform.position + Vector3.up * 0.08f;
            Vector3 end = hitPoint + Vector3.up * 0.08f;
            previewLine.positionCount = 2;
            previewLine.startColor = previewInvalidColor;
            previewLine.endColor = previewInvalidColor;
            previewLine.SetPosition(0, start);
            previewLine.SetPosition(1, end);
            previewLine.enabled = true;
            return;
        }

        previewLine.positionCount = points.Count;
        Color color = cachedPreview.isValid ? previewValidColor : previewInvalidColor;
        previewLine.startColor = color;
        previewLine.endColor = color;

        for (int i = 0; i < points.Count; i++)
            previewLine.SetPosition(i, movement.ApplyFootOffset(points[i]) + Vector3.up * 0.08f);

        previewLine.enabled = true;
    }

    private void EnsureVisuals()
    {
        var rangeGo = new GameObject("MoveRangeFloodMesh");
        rangeGo.transform.SetParent(transform, false);
        rangeMeshFilter = rangeGo.AddComponent<MeshFilter>();
        rangeMeshRenderer = rangeGo.AddComponent<MeshRenderer>();
        rangeMeshRenderer.material = CreateTransparentMaterial(rangeColor);
        rangeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeMeshRenderer.receiveShadows = false;
        rangeGo.SetActive(false);

        previewLine = CreateLineRenderer("MovePreviewPath", previewLineWidth, previewValidColor, 32);
    }

    private LineRenderer CreateLineRenderer(string name, float width, Color color, int maxPoints)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = maxPoints;
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
        mat.color = color;

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);

        mat.renderQueue = 3000;
        return mat;
    }

    private void SetRangeVisible(bool visible)
    {
        if (rangeMeshRenderer != null)
            rangeMeshRenderer.enabled = visible;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewLine != null) previewLine.enabled = visible;
    }

    private bool TryRaycastGround(out Vector3 hitPoint)
    {
        hitPoint = default;
        if (battleCamera == null) return false;

        Ray ray = battleCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayer, QueryTriggerInteraction.Ignore))
            return false;

        hitPoint = hit.point;
        return true;
    }
}
