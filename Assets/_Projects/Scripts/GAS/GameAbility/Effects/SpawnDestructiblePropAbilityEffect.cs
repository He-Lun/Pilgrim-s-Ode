using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 播放召唤特效；特效结束后留下 Prefab 内标记的晶石（同一实例，不另生成）。
/// 晶石可摧毁、不进行动条；绑定 Buff 随晶石生命周期移除。
/// </summary>
[Serializable]
public class SpawnDestructiblePropAbilityEffect : AbilityEffect
{
    [Serializable]
    public class BoundBuff
    {
        [Tooltip("属性名；Status 表示纯状态（如禁疗）")]
        public string attributeName = "Attack";

        [Tooltip("乘法加成：最终 = 基础 × (1 + 值)；Status 可填 0")]
        public float multiplicativeBonus = 2f;

        [Tooltip("实例 tag，如 Buff.Anger.EternalFlame；须与其它技能区分")]
        public GameplayTag buffTag = new GameplayTag("Buff.Anger.EternalFlame");
    }

    [Header("召唤特效（必填）")]
    [Tooltip("整段召唤特效 Prefab；晶石作为其子物体，并挂 DestructiblePropPersistMarker")]
    public GameObject summonVfxPrefab;

    [Tooltip("相对落点的本地偏移")]
    public Vector3 spawnOffset;

    [Tooltip("开场特效时长（秒）。0 = 按非晶石粒子自动估算")]
    public float introDurationSeconds;

    [Header("显示信息")]
    [Tooltip("显示名称（信息面板用）")]
    public string displayName = "永世怒火晶石";

    [TextArea(2, 4)]
    [Tooltip("简介（信息面板用）")]
    public string propDescription = "火神遗迹的晶石。在场期间使召唤者保持狂暴；可被摧毁。";

    [Header("战斗数据")]
    [Tooltip("最大生命值；归零时销毁并移除绑定 Buff")]
    public float maxHealth = 200f;

    [Tooltip("无碰撞体时自动添加的球半径（米）")]
    public float fallbackColliderRadius = 0.9f;

    [Tooltip("地形障碍半径（米）；占用/挤开/NavMesh 雕刻。0 = 用碰撞体水平半径")]
    public float obstacleRadiusMeters;

    [Tooltip("生成时把踩在障碍内的角色挤到外侧")]
    public bool displaceOverlappingActors = true;

    [Tooltip("召唤物标识；同施法者再召唤时替换旧的")]
    public GameplayTag propTag = new GameplayTag("Prop.EternalFlameCrystal");

    [Tooltip("生命周期绑定的永久 Buff（duration=0）")]
    public List<BoundBuff> boundBuffs = new List<BoundBuff>
    {
        new BoundBuff
        {
            attributeName = "Attack",
            multiplicativeBonus = 2f,
            buffTag = new GameplayTag("Buff.Anger.EternalFlame")
        },
        new BoundBuff
        {
            attributeName = "Status",
            multiplicativeBonus = 0f,
            buffTag = new GameplayTag("Debuff.HealBlock.EternalFlame")
        }
    };

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || caster == null)
            return;

        if (summonVfxPrefab == null)
        {
            Debug.LogWarning("[SpawnDestructibleProp] 未配置 summonVfxPrefab（召唤特效）。");
            return;
        }

        if (!TryResolveCenter(caster, context, out Vector3 center))
            return;

        Vector3 forward = context.hasAimDirection ? context.aimDirectionWorld : caster.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        BattleDestructiblePropManager.Instance.DestroyByTag(propTag, caster);

        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * summonVfxPrefab.transform.localRotation;
        var vfxRoot = UnityEngine.Object.Instantiate(summonVfxPrefab, center + spawnOffset, rot);
        if (vfxRoot == null) return;

        var persist = ResolvePersistHost(vfxRoot);
        EnsureCombatComponents(persist, caster, out var propAsc, out var attributes);
        if (propAsc == null || attributes == null)
        {
            WorldVfxSpawner.DestroyInstance(vfxRoot);
            return;
        }

        attributes.InitializeAsProp(Mathf.Max(1f, maxHealth));
        propAsc.InitializeAsProp(attributes, caster.TeamId);
        propAsc.SetParticipatesInActionQueue(false);

        var boundTags = new List<GameplayTag>();
        ApplyBoundBuffs(caster, boundTags);

        var prop = persist.GetComponent<DestructibleBattleProp>();
        if (prop == null)
            prop = persist.AddComponent<DestructibleBattleProp>();

        float terrainRadius = ResolveObstacleRadius(persist);
        prop.Configure(
            displayName,
            propDescription,
            propTag,
            propAsc,
            caster,
            boundTags,
            terrainRadius,
            displaceOverlappingActors);
        BattleDestructiblePropManager.Instance.Register(prop);

        var propAnimator = EnsurePropAnimator(persist);

        // 同一实例：In 结束后解绑晶石（继续 Stay），毁掉特效壳
        if (persist != vfxRoot)
        {
            var lifecycle = vfxRoot.GetComponent<SummonVfxPersistLifecycle>();
            if (lifecycle == null)
                lifecycle = vfxRoot.AddComponent<SummonVfxPersistLifecycle>();
            lifecycle.Begin(persist.transform, introDurationSeconds, propAnimator);
        }
        else
        {
            Debug.LogWarning(
                "[SpawnDestructibleProp] Prefab 未找到 DestructiblePropPersistMarker，整棵特效将作为晶石留下。" +
                "请把 Marker 挂到要留下的晶石子物体上。");
        }
    }

    private static DestructiblePropAnimator EnsurePropAnimator(GameObject host)
    {
        if (host == null) return null;

        var anim = host.GetComponent<Animator>();
        if (anim == null)
            anim = host.GetComponentInChildren<Animator>(true);
        if (anim == null)
            return null;

        var driver = anim.GetComponent<DestructiblePropAnimator>();
        if (driver == null)
            driver = anim.gameObject.AddComponent<DestructiblePropAnimator>();
        return driver;
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        // 召唤走三参数 Execute。
    }

    private static GameObject ResolvePersistHost(GameObject vfxRoot)
    {
        var marker = vfxRoot.GetComponentInChildren<DestructiblePropPersistMarker>(true);
        return marker != null ? marker.gameObject : vfxRoot;
    }

    private void ApplyBoundBuffs(AbilitySystemComponent caster, List<GameplayTag> boundTags)
    {
        if (caster?.Attributes == null || boundBuffs == null) return;

        for (int i = 0; i < boundBuffs.Count; i++)
        {
            var buff = boundBuffs[i];
            if (buff == null || string.IsNullOrEmpty(buff.buffTag.TagName)) continue;

            string attr = string.IsNullOrEmpty(buff.attributeName) ? "Attack" : buff.attributeName;
            var op = attr == "Status"
                ? ModifierOperation.Additive
                : ModifierOperation.Multiplicative;
            float value = attr == "Status" ? 0f : buff.multiplicativeBonus;

            caster.Attributes.AddModifier(new AttributeModifier(
                attr,
                value,
                op,
                buff.buffTag,
                0));

            caster.ApplyBuffTo(caster, buff.buffTag, caster);
            boundTags.Add(buff.buffTag);
        }
    }

    private void EnsureCombatComponents(
        GameObject host,
        AbilitySystemComponent caster,
        out AbilitySystemComponent propAsc,
        out AttributeSet attributes)
    {
        propAsc = host.GetComponent<AbilitySystemComponent>();
        if (propAsc == null)
            propAsc = host.AddComponent<AbilitySystemComponent>();

        attributes = host.GetComponent<AttributeSet>();
        if (attributes == null)
            attributes = host.AddComponent<AttributeSet>();

        // 实体碰撞：可顶开/阻挡；并保证可被射线点选
        var colliders = host.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            var sphere = host.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Max(0.1f, fallbackColliderRadius);
            sphere.center = Vector3.up * sphere.radius;
            sphere.isTrigger = false;
        }
        else
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = false;
            }
        }

        if (caster != null)
            host.layer = caster.gameObject.layer;
    }

    private float ResolveObstacleRadius(GameObject host)
    {
        if (obstacleRadiusMeters > 0f)
            return obstacleRadiusMeters;

        var capsule = host.GetComponentInChildren<CapsuleCollider>(true);
        if (capsule != null)
        {
            Vector3 scale = capsule.transform.lossyScale;
            float horiz = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Mathf.Max(0.1f, capsule.radius * horiz);
        }

        var sphere = host.GetComponentInChildren<SphereCollider>(true);
        if (sphere != null)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float horiz = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Mathf.Max(0.1f, sphere.radius * horiz);
        }

        return Mathf.Max(0.1f, fallbackColliderRadius);
    }

    private static bool TryResolveCenter(
        AbilitySystemComponent caster,
        AbilityActivationContext context,
        out Vector3 center)
    {
        center = default;

        if (context.HasTargetPoint)
        {
            center = context.targetWorldPoint;
            return true;
        }

#pragma warning disable 618
        if (context.HasTargetCell && BattleGrid.Instance != null)
        {
            center = BattleGrid.Instance.CellToWorld(context.targetCell);
            return true;
        }
#pragma warning restore 618

        if (caster != null)
        {
            center = caster.transform.position;
            return true;
        }

        return false;
    }
}
