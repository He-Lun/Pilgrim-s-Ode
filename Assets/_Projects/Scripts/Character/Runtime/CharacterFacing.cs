using UnityEngine;

/// <summary>
/// 角色朝向 — 移动时平滑且迅速地转向移动方向；技能等可瞬时对齐。
/// 不使用固定角速度，而是按目标朝向做指数平滑 Slerp。
/// </summary>
[DisallowMultipleComponent]
public class CharacterFacing : MonoBehaviour
{
    [Header("转向")]
    [Tooltip("转向响应速度，越大越快（指数平滑，非角速度）")]
    [SerializeField] private float turnSharpness = 22f;
    [Tooltip("与目标朝向夹角小于此值时直接对齐，避免末端抖动")]
    [SerializeField] private float snapAngleThreshold = 0.35f;

    [Header("正前方标注")]
    [SerializeField] private bool showForwardIndicator = true;
    [SerializeField] private float indicatorLength = 1.25f;
    [SerializeField] private float indicatorHeight = 0.08f;
    [SerializeField] private float indicatorWidth = 0.04f;
    [SerializeField] private Color indicatorColor = new Color(0.1f, 0.95f, 1f, 0.95f);

    private Vector3 desiredForward = Vector3.forward;
    private bool hasDesiredForward;
    private bool snapNextFrame;
    private LineRenderer forwardLine;

    public Vector3 Forward => transform.forward;

    /// <summary>每帧传入移动方向（世界空间），会平滑转向该方向。</summary>
    public void FaceMoveDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        desiredForward = worldDirection.normalized;
        hasDesiredForward = true;
        snapNextFrame = false;
    }

    /// <summary>每帧传入移动目标点，转向该点所在方向。</summary>
    public void FaceToward(Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - transform.position;
        FaceMoveDirection(dir);
    }

    /// <summary>立即对齐到指定方向（技能释放等）。</summary>
    public void SnapFaceDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        desiredForward = worldDirection.normalized;
        hasDesiredForward = true;
        snapNextFrame = true;
        ApplyRotation(true);
    }

    /// <summary>立即对齐到目标点方向。</summary>
    public void SnapFaceToward(Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - transform.position;
        SnapFaceDirection(dir);
    }

    /// <summary>兼容旧接口 — 移动中请用 FaceToward。</summary>
    public void LookAt(Vector3 worldTarget) => FaceToward(worldTarget);

    /// <summary>兼容旧接口 — 瞬时对齐。</summary>
    public void SnapLookAt(Vector3 worldTarget) => SnapFaceToward(worldTarget);

    public void FaceAbilityContext(AbilityActivationContext context, AbilitySystemComponent asc)
    {
        if (context.HasExplicitTargets && context.explicitTargets.Count > 0)
        {
            var target = context.explicitTargets[0];
            if (target != null)
            {
                SnapFaceToward(target.transform.position);
                return;
            }
        }

        if (context.HasTargetPoint)
        {
            SnapFaceToward(context.targetWorldPoint);
            return;
        }

#pragma warning disable 618
        if (context.HasTargetCell && BattleGrid.Instance != null)
        {
            SnapFaceToward(BattleGrid.Instance.CellToWorld(context.targetCell));
            return;
        }
#pragma warning restore 618

        if (context.HasAimDirection)
        {
            SnapFaceDirection(context.aimDirectionWorld);
            return;
        }

        if (context.HasDirection)
        {
            var dir = new Vector3(context.direction.x, 0f, context.direction.y);
            if (dir.sqrMagnitude > 0.001f)
                SnapFaceDirection(dir);
        }
    }

    void Awake()
    {
        desiredForward = FlattenForward(transform.forward);
        hasDesiredForward = desiredForward.sqrMagnitude > 0.0001f;
        EnsureForwardIndicator();
    }

    void LateUpdate()
    {
        ApplyRotation(snapNextFrame);
        snapNextFrame = false;
        UpdateForwardIndicator();
    }

    void OnValidate()
    {
        turnSharpness = Mathf.Max(0.01f, turnSharpness);
        indicatorLength = Mathf.Max(0.1f, indicatorLength);
    }

    private void ApplyRotation(bool snap)
    {
        if (!hasDesiredForward) return;

        var targetRot = Quaternion.LookRotation(desiredForward, Vector3.up);

        if (snap || turnSharpness <= 0f)
        {
            transform.rotation = targetRot;
            return;
        }

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle <= snapAngleThreshold)
        {
            transform.rotation = targetRot;
            return;
        }

        float t = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    private void EnsureForwardIndicator()
    {
        if (!showForwardIndicator)
        {
            if (forwardLine != null)
                forwardLine.enabled = false;
            return;
        }

        if (forwardLine == null)
        {
            var lineGo = new GameObject("ForwardIndicator");
            lineGo.transform.SetParent(transform, false);
            forwardLine = lineGo.AddComponent<LineRenderer>();
            forwardLine.useWorldSpace = true;
            forwardLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            forwardLine.receiveShadows = false;
            forwardLine.loop = false;
            forwardLine.positionCount = 2;
            forwardLine.material = new Material(Shader.Find("Sprites/Default"));
        }

        forwardLine.enabled = true;
        forwardLine.startWidth = indicatorWidth;
        forwardLine.endWidth = indicatorWidth * 0.35f;
        forwardLine.startColor = indicatorColor;
        forwardLine.endColor = indicatorColor;
    }

    private void UpdateForwardIndicator()
    {
        if (!showForwardIndicator)
        {
            if (forwardLine != null)
                forwardLine.enabled = false;
            return;
        }

        EnsureForwardIndicator();
        if (forwardLine == null) return;

        Vector3 origin = transform.position + Vector3.up * indicatorHeight;
        Vector3 tip = origin + transform.forward * indicatorLength;
        forwardLine.SetPosition(0, origin);
        forwardLine.SetPosition(1, tip);
    }

    void OnDrawGizmosSelected()
    {
        DrawForwardGizmo(1f);
    }

    void OnDrawGizmos()
    {
        if (!showForwardIndicator) return;
        DrawForwardGizmo(0.85f);
    }

    private void DrawForwardGizmo(float alpha)
    {
        Vector3 origin = transform.position + Vector3.up * indicatorHeight;
        Vector3 forward = Application.isPlaying ? transform.forward : FlattenForward(transform.forward);
        if (forward.sqrMagnitude < 0.0001f) return;

        Vector3 tip = origin + forward.normalized * indicatorLength;

        var color = indicatorColor;
        color.a *= alpha;
        Gizmos.color = color;
        Gizmos.DrawLine(origin, tip);

        Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
        float head = indicatorLength * 0.14f;
        Gizmos.DrawLine(tip, tip - forward.normalized * head + right * head * 0.45f);
        Gizmos.DrawLine(tip, tip - forward.normalized * head - right * head * 0.45f);
    }

    private static Vector3 FlattenForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
}
