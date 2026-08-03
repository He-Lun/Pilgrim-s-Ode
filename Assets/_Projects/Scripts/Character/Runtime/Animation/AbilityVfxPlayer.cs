using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色特效播放器 — 技能时机特效 + 类别 Buff 持续特效（查 BuffPresentationCatalog）。
/// </summary>
[RequireComponent(typeof(AbilitySystemComponent))]
public class AbilityVfxPlayer : MonoBehaviour
{
    [SerializeField] private BuffPresentationCatalog presentationCatalog;
    [SerializeField] private AbilityVfxAttachPoints attachPoints;
    [SerializeField] private float defaultChestHeight = 1.2f;
    [SerializeField] private float defaultAutoDestroySeconds = 3f;

    private readonly Dictionary<GameplayTag, GameObject> activeCategoryVfx = new Dictionary<GameplayTag, GameObject>();
    private readonly List<VfxSpawnEntry> categoryVfxBuffer = new List<VfxSpawnEntry>();
    private AbilitySystemComponent asc;

    void Awake()
    {
        asc = GetComponent<AbilitySystemComponent>();
    }

    void OnEnable()
    {
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        if (asc != null)
            asc.OnTagRemoved += HandleTagRemoved;
    }

    void OnDisable()
    {
        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
        if (asc != null)
            asc.OnTagRemoved -= HandleTagRemoved;
    }

    void OnDestroy() => activeCategoryVfx.Clear();

    public void BindCatalog(BuffPresentationCatalog catalog) => presentationCatalog = catalog;

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
            SpawnOneShot(entry, context, caster);
        }
    }

    public bool TryGetAnchorWorld(VfxAnchor anchor, AbilityActivationContext context, out Vector3 position, out Quaternion rotation)
    {
        if (!TryResolveAnchor(anchor, context, transform, out _, out position, out rotation))
            return false;
        return true;
    }

    public static GameObject SpawnOneShotAt(GameObject prefab, Vector3 position, Quaternion rotation, float autoDestroySeconds = 3f)
    {
        if (prefab == null) return null;

        var instance = Instantiate(prefab, position, rotation);
        float particleLifetime = PrepareOneShotParticles(instance);
        float destroyAfter = autoDestroySeconds > 0f ? Mathf.Max(autoDestroySeconds, particleLifetime) : particleLifetime;
        Destroy(instance, destroyAfter);
        return instance;
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (evt.type != CombatEventType.BuffApplied || evt.target != asc) return;

        if (string.IsNullOrEmpty(evt.tag.TagName))
        {
            if (evt.effectVfx != null && evt.effectVfx.IsValid)
                SpawnOneShot(evt.effectVfx, AbilityActivationContext.Self(), transform);
            return;
        }

        if (presentationCatalog == null) return;

        var category = BuffCategoryTag.Resolve(evt.tag);
        if (presentationCatalog.CollectForCategory(category, categoryVfxBuffer) == 0) return;

        var context = AbilityActivationContext.Self();
        bool hasSustainActive = activeCategoryVfx.ContainsKey(category);

        for (int i = 0; i < categoryVfxBuffer.Count; i++)
        {
            var entry = categoryVfxBuffer[i];
            if (entry.autoDestroySeconds > 0f)
            {
                SpawnOneShot(entry, context, transform);
                continue;
            }

            if (hasSustainActive) continue;

            var instance = CreateInstance(entry, context, transform);
            if (instance == null) continue;

            activeCategoryVfx[category] = instance;
            hasSustainActive = true;
        }
    }

    private void HandleTagRemoved(GameplayTag tag)
    {
        var category = BuffCategoryTag.Resolve(tag);
        if (asc.HasActiveEffectCategory(category)) return;
        StopCategory(category);
    }

    private void StopCategory(GameplayTag category)
    {
        if (!activeCategoryVfx.TryGetValue(category, out var instance)) return;
        if (instance != null)
            Destroy(instance);
        activeCategoryVfx.Remove(category);
    }

    private void SpawnOneShot(VfxSpawnEntry entry, AbilityActivationContext context, Transform caster)
    {
        var instance = CreateInstance(entry, context, caster);
        if (instance == null) return;

        float particleLifetime = PrepareOneShotParticles(instance);
        float configured = entry.autoDestroySeconds > 0f ? entry.autoDestroySeconds : defaultAutoDestroySeconds;
        float destroyAfter = configured > 0f ? Mathf.Max(configured, particleLifetime) : particleLifetime;
        Destroy(instance, destroyAfter);
    }

    private GameObject CreateInstance(VfxSpawnEntry entry, AbilityActivationContext context, Transform caster)
    {
        if (entry == null || entry.prefab == null) return null;
        if (!TryResolveAnchor(entry.anchor, context, caster, out Transform parent, out Vector3 position, out Quaternion anchorRot))
            return null;

        Quaternion prefabLocal = entry.prefab.transform.localRotation;
        Quaternion worldRot = ResolveModeRotation(entry, context, caster, anchorRot) * prefabLocal;

        if (entry.attachMode == VfxAttachMode.Parented && parent != null)
        {
            var instance = Instantiate(entry.prefab, parent);
            instance.transform.localPosition = entry.anchor.localOffset;
            instance.transform.rotation = entry.rotationMode == VfxRotationMode.PrefabDefault
                ? prefabLocal
                : worldRot;
            return instance;
        }

        return Instantiate(entry.prefab, position, worldRot);
    }

    private bool TryResolveAnchor(
        VfxAnchor anchor,
        AbilityActivationContext context,
        Transform caster,
        out Transform parent,
        out Vector3 position,
        out Quaternion rotation)
    {
        parent = null;
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Vector3 basePos = caster != null ? caster.position : transform.position;

        switch (anchor.type)
        {
            case VfxAnchorType.CasterRoot:
                parent = caster != null ? caster : transform;
                basePos = parent.position;
                rotation = parent.rotation;
                break;

            case VfxAnchorType.CasterGround:
                basePos = caster != null ? caster.position : transform.position;
                basePos.y = 0f;
                rotation = caster != null ? caster.rotation : transform.rotation;
                break;

            case VfxAnchorType.TargetRoot:
            case VfxAnchorType.TargetChest:
            {
                Transform target = GetPrimaryTargetTransform(context);
                if (target == null) return false;
                parent = target;
                basePos = target.position;
                if (anchor.type == VfxAnchorType.TargetChest)
                    basePos += Vector3.up * defaultChestHeight;
                rotation = target.rotation;
                break;
            }

            case VfxAnchorType.MouseWorldPoint:
                if (!context.hasTargetPoint && !(context.hasAimDirection && caster != null))
                    return false;
                basePos = context.hasTargetPoint
                    ? BattleTargeting.ProjectToGround(context.targetWorldPoint)
                    : caster.position + context.aimDirectionWorld;
                break;

            case VfxAnchorType.NamedPoint:
                if (attachPoints == null || string.IsNullOrEmpty(anchor.attachPointId)
                    || !attachPoints.TryGet(anchor.attachPointId, out Transform point) || point == null)
                    return false;
                parent = point;
                basePos = point.position;
                rotation = point.rotation;
                break;

            default:
                return false;
        }

        position = basePos + anchor.localOffset;
        return true;
    }

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
                    : caster != null ? caster.forward : Vector3.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(dir.normalized, Vector3.up);
                return caster != null ? caster.rotation : Quaternion.identity;
            }

            default:
                return Quaternion.identity;
        }
    }

    private static Transform GetPrimaryTargetTransform(AbilityActivationContext context)
    {
        var targets = context.GetExplicitTargets();
        if (targets == null) return null;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                return targets[i].transform;
        }

        return null;
    }

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
