using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 可摧毁召唤物（如永世怒火晶石）— 有名字/血量、可被选中受击，但不进入行动条。
/// 作为地形障碍：NavMesh 雕刻 + 占用检测；生成时可挤开重叠角色。
/// </summary>
public sealed class DestructibleBattleProp : MonoBehaviour
{
    [SerializeField] private string displayName = "晶石";
    [SerializeField] [TextArea(2, 4)] private string description;
    [SerializeField] private GameplayTag propTag;
    [SerializeField] private float obstacleRadius = 0.9f;

    private AbilitySystemComponent selfAsc;
    private AbilitySystemComponent owner;
    private readonly List<GameplayTag> boundBuffTags = new List<GameplayTag>();
    private bool tearingDown;
    private bool occupancyRegistered;
    private DestructiblePropAnimator propAnimator;
    private NavMeshObstacle navObstacle;

    public string DisplayName => displayName;
    public string Description => description;
    public GameplayTag PropTag => propTag;
    public float ObstacleRadius => Mathf.Max(0.1f, obstacleRadius);
    public AbilitySystemComponent SelfAsc => selfAsc;
    public AbilitySystemComponent Owner => owner;
    public IReadOnlyList<GameplayTag> BoundBuffTags => boundBuffTags;

    public void Configure(
        string name,
        string desc,
        GameplayTag tag,
        AbilitySystemComponent propAsc,
        AbilitySystemComponent propOwner,
        List<GameplayTag> buffTags,
        float terrainRadius,
        bool displaceOverlappingActors)
    {
        displayName = string.IsNullOrEmpty(name) ? gameObject.name : name;
        description = desc ?? string.Empty;
        propTag = tag;
        selfAsc = propAsc;
        owner = propOwner;
        obstacleRadius = Mathf.Max(0.1f, terrainRadius);

        boundBuffTags.Clear();
        if (buffTags != null)
        {
            for (int i = 0; i < buffTags.Count; i++)
            {
                if (!string.IsNullOrEmpty(buffTags[i].TagName))
                    boundBuffTags.Add(buffTags[i]);
            }
        }

        gameObject.name = displayName;
        EnsurePropAnimator();
        SetupNavMeshObstacle();

        if (!occupancyRegistered)
        {
            BattleOccupancy.RegisterProp(this);
            occupancyRegistered = true;
        }

        if (displaceOverlappingActors)
            DisplaceOverlappingActors();

        if (selfAsc != null)
            selfAsc.OnDeath += HandleSelfDeath;
    }

    public void StripBoundBuffsFromOwner()
    {
        if (owner?.Attributes == null) return;

        for (int i = 0; i < boundBuffTags.Count; i++)
        {
            var tag = boundBuffTags[i];
            owner.Attributes.RemoveModifier(tag);
            owner.RemoveTag(tag);
        }
    }

    public void Teardown(bool destroyGameObject, bool immediateVfx = true)
    {
        if (tearingDown) return;
        tearingDown = true;

        if (selfAsc != null)
            selfAsc.OnDeath -= HandleSelfDeath;

        StripBoundBuffsFromOwner();
        UnregisterTerrain();
        BattleDestructiblePropManager.Instance.Unregister(this);

        if (!destroyGameObject || gameObject == null)
            return;

        if (immediateVfx)
            WorldVfxSpawner.DestroyInstance(gameObject);
        else
            BeginDestroyPresentation();
    }

    void OnDestroy()
    {
        UnregisterTerrain();

        if (tearingDown) return;
        tearingDown = true;

        if (selfAsc != null)
            selfAsc.OnDeath -= HandleSelfDeath;

        StripBoundBuffsFromOwner();
        BattleDestructiblePropManager.Instance.Unregister(this);
    }

    private void HandleSelfDeath(AbilitySystemComponent _)
    {
        if (tearingDown) return;
        tearingDown = true;

        if (selfAsc != null)
            selfAsc.OnDeath -= HandleSelfDeath;

        StripBoundBuffsFromOwner();
        UnregisterTerrain();
        BattleDestructiblePropManager.Instance.Unregister(this);
        BeginDestroyPresentation();
    }

    private void UnregisterTerrain()
    {
        if (navObstacle != null)
            navObstacle.enabled = false;

        if (!occupancyRegistered) return;
        BattleOccupancy.UnregisterProp(this);
        occupancyRegistered = false;
    }

    private void SetupNavMeshObstacle()
    {
        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null)
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();

        navObstacle.shape = NavMeshObstacleShape.Capsule;
        navObstacle.radius = ObstacleRadius;
        navObstacle.height = Mathf.Max(1f, ObstacleRadius * 2f);
        navObstacle.center = Vector3.up * (navObstacle.height * 0.5f);
        navObstacle.carving = true;
        navObstacle.carveOnlyStationary = true;
        navObstacle.carvingMoveThreshold = 0.1f;
        navObstacle.carvingTimeToStationary = 0.1f;
        navObstacle.enabled = true;
    }

    /// <summary>把站在障碍半径内的角色推到边缘外侧的 NavMesh 点。</summary>
    public void DisplaceOverlappingActors()
    {
        Vector3 center = transform.position;
        center.y = 0f;

        var movers = BattleOccupancy.GetRegisteredMovers();
        for (int i = 0; i < movers.Count; i++)
        {
            var mover = movers[i];
            if (mover == null) continue;

            Vector3 actorPos = mover.transform.position;
            float need = ObstacleRadius + mover.PersonalSpaceRadius + 0.05f;
            float dist = BattleOccupancy.HorizontalDistance(center, actorPos);
            if (dist >= need) continue;

            Vector3 push = actorPos - center;
            push.y = 0f;
            if (push.sqrMagnitude < 0.0001f)
                push = Vector3.forward;
            push.Normalize();

            bool placed = false;
            for (int step = 0; step < 8; step++)
            {
                Vector3 desired = center + push * (need + step * 0.25f);
                desired.y = mover.transform.position.y;

                if (!NavPathMovementPlanner.TrySampleNavMesh(desired, out Vector3 snapped))
                    continue;
                if (!BattleOccupancy.IsClearOfProps(snapped, mover.PersonalSpaceRadius))
                    continue;

                mover.SnapToWorldPosition(snapped);
                placed = true;
                break;
            }

            if (!placed)
            {
                Vector3 fallback = center + push * need;
                fallback.y = mover.transform.position.y;
                mover.SnapToWorldPosition(fallback);
            }

            mover.InvalidateReachableCache();
        }

        BattleOccupancy.InvalidateAllReachableCaches();
    }

    private void BeginDestroyPresentation()
    {
        EnsurePropAnimator();
        if (propAnimator != null)
        {
            propAnimator.PlayOutThenDestroy(gameObject);
            return;
        }

        WorldVfxSpawner.BeginExpire(gameObject);
    }

    private void EnsurePropAnimator()
    {
        if (propAnimator != null) return;

        propAnimator = GetComponent<DestructiblePropAnimator>();
        if (propAnimator == null && GetComponent<Animator>() != null)
            propAnimator = gameObject.AddComponent<DestructiblePropAnimator>();
    }
}
