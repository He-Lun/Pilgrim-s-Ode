using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在角色身上播放技能 VFX — 由 CharacterMotor / 动画事件按时机调用。
/// 数据驱动：从 AbilityPresentationEntry.GetEffectiveVfx() 取一组 VfxSpawnEntry，
/// 逐条按锚点(语义/命名挂点)解析位置、按配置决定朝向、是否跟随，然后生成。
/// </summary>
public class AbilityVfxPlayer : MonoBehaviour
{
    [Header("挂点")]
    [Tooltip("命名挂点表（剑尖/胸口/脚等）。留空则在自身及子物体上自动查找")]
    [SerializeField] private AbilityVfxAttachPoints attachPoints;

    [Header("默认值")]
    [Tooltip("TargetChest / CasterGround 未配偏移时的默认胸口高度（米）")]
    [SerializeField] private float defaultChestHeight = 1.2f;
    [Tooltip("实例存活上限（秒）兜底。单条 VfxSpawnEntry 可覆盖")]
    [SerializeField] private float defaultAutoDestroySeconds = 3f;

    // 持续型 buff 特效：按 buff 来源标签管理，生命周期与角色身上的 buff 一致。
    private readonly Dictionary<GameplayTag, GameObject> activeBuffVfx = new Dictionary<GameplayTag, GameObject>();
    private AttributeSet attributes;

    void Awake()
    {
        if (attachPoints == null)
            attachPoints = GetComponentInChildren<AbilityVfxAttachPoints>(true);

        attributes = GetComponent<AttributeSet>();
    }

    void OnEnable()
    {
        if (attributes != null)
            attributes.OnModifierRemoved += StopBuffVfx;
    }

    void OnDisable()
    {
        if (attributes != null)
            attributes.OnModifierRemoved -= StopBuffVfx;
    }

    void OnDestroy()
    {
        activeBuffVfx.Clear();
    }

    // ── 对外入口 ────────────────────────────────────

    /// <summary>按时机播放该技能表现下所有匹配的特效。</summary>
    public void PlayTiming(
        VfxTiming timing,
        AbilityPresentationEntry presentation,
        AbilityActivationContext context,
        Transform caster)
    {
        if (presentation == null) return;

        var entries = presentation.GetEffectiveVfx();
        if (entries == null || entries.Count == 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || !entry.IsValid || entry.timing != timing) continue;
            SpawnEntry(entry, context, caster);
        }
    }

    // ── Buff 持续特效 ────────────────────────────────

    /// <summary>
    /// 播放持续型 buff 特效 —— 保持 loop、不定时销毁，随该来源标签的 buff 存续。
    /// 由 BuffAbilityEffect 在施加 buff 时对目标调用。位置锚点以被 buff 的角色自身为参照。
    /// </summary>
    public void PlayBuffVfx(GameplayTag buffTag, VfxSpawnEntry entry)
    {
        if (entry == null || !entry.IsValid) return;

        // 同标签已有特效则先移除（buff 刷新时不叠加）。
        StopBuffVfx(buffTag);

        var instance = CreateInstance(entry, AbilityActivationContext.Self(), transform);
        if (instance != null)
            activeBuffVfx[buffTag] = instance;
    }

    /// <summary>销毁某来源标签的 buff 特效（由 AttributeSet.OnModifierRemoved 驱动）。</summary>
    public void StopBuffVfx(GameplayTag buffTag)
    {
        if (activeBuffVfx.TryGetValue(buffTag, out var instance))
        {
            if (instance != null)
                Destroy(instance);
            activeBuffVfx.Remove(buffTag);
        }
    }

    // ── 供弹体等外部系统复用 ─────────────────────────

    /// <summary>解析锚点的世界位置与朝向（以本角色为参照），供弹体发射点(枪口)等使用。</summary>
    public bool TryGetAnchorWorld(VfxAnchor anchor, AbilityActivationContext context, out Vector3 position, out Quaternion rotation)
    {
        ResolveAnchor(anchor, context, transform, out _, out position, out rotation);
        return true;
    }

    /// <summary>在指定世界坐标生成一次性特效（关闭 loop、按粒子时长自动销毁），供弹体命中等场景复用。</summary>
    public static GameObject SpawnOneShotAt(GameObject prefab, Vector3 position, Quaternion rotation, float autoDestroySeconds = 3f)
    {
        if (prefab == null) return null;

        var instance = Instantiate(prefab, position, rotation);
        float particleLifetime = PrepareOneShotParticles(instance);
        float destroyAfter = autoDestroySeconds > 0f ? Mathf.Max(autoDestroySeconds, particleLifetime) : particleLifetime;
        Destroy(instance, destroyAfter);
        return instance;
    }

    // ── 生成 ────────────────────────────────────────

    private void SpawnEntry(VfxSpawnEntry entry, AbilityActivationContext context, Transform caster)
    {
        var instance = CreateInstance(entry, context, caster);
        if (instance == null) return;

        float particleLifetime = PrepareOneShotParticles(instance);
        float configured = entry.autoDestroySeconds > 0f ? entry.autoDestroySeconds : defaultAutoDestroySeconds;
        float destroyAfter = configured > 0f ? Mathf.Max(configured, particleLifetime) : particleLifetime;
        Destroy(instance, destroyAfter);
    }

    /// <summary>按 entry 的锚点/朝向/挂接方式实例化特效（不含销毁调度），供一次性与持续型复用。</summary>
    private GameObject CreateInstance(VfxSpawnEntry entry, AbilityActivationContext context, Transform caster)
    {
        if (entry == null || entry.prefab == null) return null;

        ResolveAnchor(entry.anchor, context, caster, out Transform parent, out Vector3 position, out Quaternion anchorRot);

        // 所有 Mode 的最终朝向 = Mode 基朝向 × prefab.localRotation，
        // 保留资源自带偏移（如 Hovl Sword Slash 的 Y=-90）。
        Quaternion prefabLocal = entry.prefab.transform.localRotation;
        Quaternion modeRot = ResolveModeRotation(entry, context, caster, anchorRot);
        Quaternion worldRot = modeRot * prefabLocal;

        GameObject instance;
        if (entry.attachMode == VfxAttachMode.Parented && parent != null)
        {
            instance = Instantiate(entry.prefab, parent);
            instance.transform.localPosition = entry.anchor.localOffset;
            if (entry.rotationMode == VfxRotationMode.PrefabDefault)
                instance.transform.localRotation = prefabLocal;
            else
                instance.transform.rotation = worldRot;
        }
        else
        {
            instance = Instantiate(entry.prefab, position, worldRot);
        }

        return instance;
    }

    // ── 锚点解析 ─────────────────────────────────────

    /// <summary>
    /// 解析锚点 → 父物体(可空)、世界位置、锚点旋转。
    /// localOffset 在 Parented 分支由调用方按局部处理；此处对 Detached 已并入世界位置。
    /// </summary>
    private void ResolveAnchor(
        VfxAnchor anchor,
        AbilityActivationContext context,
        Transform caster,
        out Transform parent,
        out Vector3 position,
        out Quaternion rotation)
    {
        parent = null;
        rotation = Quaternion.identity;
        Vector3 basePos = caster != null ? caster.position : transform.position;

        switch (anchor.type)
        {
            case VfxAnchorType.CasterRoot:
                parent = caster;
                basePos = caster != null ? caster.position : transform.position;
                if (caster != null) rotation = caster.rotation;
                break;

            case VfxAnchorType.CasterGround:
                basePos = caster != null ? caster.position : transform.position;
                basePos.y = 0f;
                if (caster != null) rotation = caster.rotation;
                break;

            case VfxAnchorType.TargetRoot:
            {
                Transform target = GetPrimaryTargetTransform(context);
                if (target != null)
                {
                    parent = target;
                    basePos = target.position;
                    rotation = target.rotation;
                }
                break;
            }

            case VfxAnchorType.TargetChest:
            {
                Transform target = GetPrimaryTargetTransform(context);
                if (target != null)
                {
                    parent = target;
                    basePos = target.position + Vector3.up * defaultChestHeight;
                    rotation = target.rotation;
                }
                break;
            }

            case VfxAnchorType.MouseWorldPoint:
                if (context.hasTargetPoint)
                    basePos = context.targetWorldPoint;
                else if (context.hasAimDirection && caster != null)
                    basePos = caster.position + context.aimDirectionWorld;
                break;

            case VfxAnchorType.NamedPoint:
                if (attachPoints != null && attachPoints.TryGet(anchor.attachPointId, out Transform point) && point != null)
                {
                    parent = point;
                    basePos = point.position;
                    rotation = point.rotation;
                }
                else
                {
                    Debug.LogWarning($"[AbilityVfxPlayer] 未找到命名挂点 '{anchor.attachPointId}'，回退到施法者根节点。", this);
                    parent = caster;
                    basePos = caster != null ? caster.position : transform.position;
                    if (caster != null) rotation = caster.rotation;
                }
                break;
        }

        // Detached 生成时把偏移并入世界位置（Parented 分支自行用 localOffset）。
        position = basePos + anchor.localOffset;
    }

    /// <summary>
    /// 解析 Mode 基朝向（不含 prefab 本地旋转）。
    /// PrefabDefault 返回 Identity，由调用方再乘 prefab.localRotation。
    /// </summary>
    private Quaternion ResolveModeRotation(
        VfxSpawnEntry entry,
        AbilityActivationContext context,
        Transform caster,
        Quaternion anchorRot)
    {
        switch (entry.rotationMode)
        {
            case VfxRotationMode.MatchAnchor:
                return anchorRot;

            case VfxRotationMode.FaceTarget:
            {
                Transform target = GetPrimaryTargetTransform(context);
                if (target != null && caster != null)
                {
                    Vector3 dir = target.position - caster.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                        return Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
                return caster != null ? caster.rotation : Quaternion.identity;
            }

            case VfxRotationMode.FaceAimDirection:
            {
                Vector3 dir = context.hasAimDirection ? context.aimDirectionWorld
                    : (caster != null ? caster.forward : Vector3.forward);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(dir.normalized, Vector3.up);
                return caster != null ? caster.rotation : Quaternion.identity;
            }

            case VfxRotationMode.PrefabDefault:
            default:
                return Quaternion.identity;
        }
    }

    private static Transform GetPrimaryTargetTransform(AbilityActivationContext context)
    {
        var targets = context.GetExplicitTargets();
        if (targets == null || targets.Count == 0) return null;

        foreach (var asc in targets)
        {
            if (asc != null)
                return asc.transform;
        }

        return null;
    }

    /// <summary>关闭 Loop，估算播完所需时间。</summary>
    private static float PrepareOneShotParticles(GameObject root)
    {
        float maxEnd = 0.5f;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (main.loop)
            {
                var mainModule = main;
                mainModule.loop = false;
                ps.Play(true);
            }

            float startLife = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                ? main.startLifetime.constant
                : main.startLifetime.constantMax;
            maxEnd = Mathf.Max(maxEnd, main.duration + startLife);
        }

        return maxEnd;
    }
}
