using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Area / AreaAroundSelf 技能范围预览 — 贴地实心半透明圆盘 + 描边。
/// </summary>
public class AbilityAreaPreview : MonoBehaviour
{
    [SerializeField] private Color areaColor = new Color(0.25f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color selfColor = new Color(1f, 0.55f, 0.15f, 0.38f);
    [SerializeField] private Color outlineColor = new Color(0.9f, 0.95f, 1f, 0.85f);
    [SerializeField] private int segments = 64;
    [SerializeField] private float outlineWidth = 0.06f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private Transform visualRoot;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private LineRenderer outlineLine;
    private readonly List<AbilitySystemComponent> previewTargets = new List<AbilitySystemComponent>();

    public IReadOnlyList<AbilitySystemComponent> PreviewTargets => previewTargets;
    public Vector3 LastCenter { get; private set; }
    public bool HasCenter { get; private set; }

    void Awake() => EnsureVisuals();

    void OnDestroy()
    {
        if (visualRoot != null)
            Destroy(visualRoot.gameObject);
    }

    public void Hide()
    {
        HasCenter = false;
        previewTargets.Clear();
        if (meshRenderer != null)
            meshRenderer.enabled = false;
        if (outlineLine != null)
            outlineLine.enabled = false;
    }

    public bool Refresh(
        AbilitySystemComponent caster,
        GameplayAbility ability,
        Camera camera)
    {
        previewTargets.Clear();
        HasCenter = false;

        if (caster == null || ability == null || meshFilter == null)
        {
            Hide();
            return false;
        }

        var scope = ability.targetScope == TargetScope.AreaAroundSelf
            ? TargetScope.AreaAroundSelf
            : ability.GetEffectiveTargetScope(caster);

        if (scope == TargetScope.AreaAroundSelf)
        {
            float radius = ability.GetEffectiveAreaRadiusMeters(caster);
            DrawDisk(caster.transform.position, radius, selfColor);
            previewTargets.AddRange(
                BattleTargeting.FilterEnemiesInRadius(caster, caster.transform.position, radius));
            HasCenter = radius > 0f;
            LastCenter = caster.transform.position;
            return HasCenter;
        }

        if (scope == TargetScope.Area)
        {
            if (!TryRaycastGround(camera, out Vector3 hit))
            {
                Hide();
                return false;
            }

            float radius = ability.GetAreaRadiusMeters();
            DrawDisk(hit, radius, areaColor);
            previewTargets.AddRange(
                BattleTargeting.FilterEnemiesInRadius(caster, hit, radius));
            HasCenter = radius > 0f;
            LastCenter = hit;
            return HasCenter;
        }

        Hide();
        return false;
    }

    private void DrawDisk(Vector3 center, float radius, Color fillColor)
    {
        if (radius <= 0f)
        {
            Hide();
            return;
        }

        center = BattleTargeting.ProjectToGround(center);
        visualRoot.SetPositionAndRotation(center, Quaternion.identity);
        meshFilter.mesh = CircleDiskMeshBuilder.BuildLocal(radius, segments);
        ApplyColor(meshRenderer.material, fillColor);
        meshRenderer.enabled = true;

        EnsureOutline();
        float y = center.y + CircleDiskMeshBuilder.DefaultYOffset + 0.02f;
        outlineLine.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            outlineLine.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                y,
                center.z + Mathf.Sin(angle) * radius));
        }

        outlineLine.startColor = outlineColor;
        outlineLine.endColor = outlineColor;
        outlineLine.enabled = true;
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null)
            return;

        var go = new GameObject("AbilityAreaPreviewVisual");
        visualRoot = go.transform;

        meshFilter = go.AddComponent<MeshFilter>();
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = CreateGroundMaterial(areaColor);
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;
    }

    private void EnsureOutline()
    {
        if (outlineLine != null)
            return;

        outlineLine = visualRoot.gameObject.AddComponent<LineRenderer>();
        outlineLine.useWorldSpace = true;
        outlineLine.loop = true;
        outlineLine.startWidth = outlineWidth;
        outlineLine.endWidth = outlineWidth;
        outlineLine.shadowCastingMode = ShadowCastingMode.Off;
        outlineLine.receiveShadows = false;
        outlineLine.material = new Material(Shader.Find("Sprites/Default"));
        outlineLine.enabled = false;
    }

    private bool TryRaycastGround(Camera camera, out Vector3 hitPoint)
    {
        hitPoint = default;
        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayer, QueryTriggerInteraction.Ignore))
            return false;

        hitPoint = hit.point;
        return true;
    }

    private static void ApplyColor(Material mat, Color color)
    {
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
    }

    private static Material CreateGroundMaterial(Color color)
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

        ApplyColor(mat, color);
        return mat;
    }
}
