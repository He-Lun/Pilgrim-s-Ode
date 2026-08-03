using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一管理世界空间血条与左侧角色信息面板。
/// </summary>
public class BattleHealthBarController : MonoBehaviour
{
    private static Transform sharedWorldBarRoot;

    [SerializeField] private HealthBarUIConfig config;
    [SerializeField] private CharacterRosterPanel rosterPanel;
    [SerializeField] private string headAttachPointId = "HeadForHp";
    [SerializeField] private Vector3 headOffsetFallback = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private Transform worldBarRoot;

    private readonly List<WorldHealthBarWidget> worldBars = new List<WorldHealthBarWidget>();
    private bool subscribed;

    public CharacterRosterPanel RosterPanel => rosterPanel;

    void Awake()
    {
        config ??= HealthBarUIConfig.LoadDefault();
        EnsureWorldBarRoot();
    }

    void OnEnable()
    {
        SubscribeBattleEvents();
    }

    void OnDisable()
    {
        UnsubscribeBattleEvents();
        Clear();
    }

    public void Configure(CharacterRosterPanel panel, HealthBarUIConfig uiConfig = null)
    {
        rosterPanel = panel;
        if (uiConfig != null)
            config = uiConfig;

        rosterPanel?.Configure(config);
        EnsureWorldBarRoot();
    }

    public void SyncFromBattle()
    {
        if (config == null)
            config = HealthBarUIConfig.LoadDefault();

        if (config == null)
        {
            Debug.LogWarning("[BattleHealthBarController] 缺少 HealthBarUIConfig，无法创建血条。");
            return;
        }

        EnsureWorldBarRoot();
        ClearInternal();

        var actors = CollectBattleActors();
        if (actors.Count == 0)
        {
            Debug.Log("[BattleHealthBarController] 当前没有可绑定角色，等待战斗开始后再试。");
            return;
        }

        rosterPanel?.SetRoster(actors);
        foreach (var actor in actors)
            SpawnWorldBar(actor);

        SubscribeBattleEvents();
    }

    public void Clear()
    {
        ClearInternal();
    }

    private void ClearInternal()
    {
        rosterPanel?.ClearEntries();

        for (int i = 0; i < worldBars.Count; i++)
        {
            if (worldBars[i] != null)
            {
                worldBars[i].Unbind();
                Destroy(worldBars[i].gameObject);
            }
        }

        worldBars.Clear();
    }

    private void SubscribeBattleEvents()
    {
        if (subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        subscribed = true;
    }

    private void UnsubscribeBattleEvents()
    {
        if (!subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        subscribed = false;
    }

    private void HandlePhaseChanged(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.BattleStart:
                SyncFromBattle();
                break;
            case TurnPhase.TurnStart:
            case TurnPhase.TurnAction:
                RefreshExistingBars();
                break;
        }
    }

    private void RefreshExistingBars()
    {
        if (worldBars.Count == 0 && rosterPanel != null)
        {
            SyncFromBattle();
            return;
        }

        for (int i = 0; i < worldBars.Count; i++)
            worldBars[i]?.Refresh();

        rosterPanel?.RefreshEntries();
    }

    private void EnsureWorldBarRoot()
    {
        if (worldBarRoot != null)
            return;

        if (sharedWorldBarRoot != null)
        {
            worldBarRoot = sharedWorldBarRoot;
            return;
        }

        var existing = GameObject.Find("WorldHealthBarRoot");
        if (existing != null)
        {
            sharedWorldBarRoot = existing.transform;
            worldBarRoot = sharedWorldBarRoot;
            return;
        }

        var rootGo = new GameObject("WorldHealthBarRoot");
        DontDestroyOnLoad(rootGo);
        sharedWorldBarRoot = rootGo.transform;
        worldBarRoot = sharedWorldBarRoot;
    }

    private void SpawnWorldBar(AbilitySystemComponent actor)
    {
        if (actor == null || actor.Attributes == null)
            return;

        var parent = worldBarRoot != null ? worldBarRoot : transform;
        WorldHealthBarWidget instance = null;

        var prefab = config.ResolveWorldPrefab(actor);
        if (prefab != null)
            instance = Instantiate(prefab, parent);

        if (instance == null)
        {
            var sprites = config.ResolveWorldSprites(actor);
            instance = HealthBarFactory.CreateWorldBar(parent, sprites, config);
        }

        if (instance == null)
            return;

        instance.transform.localScale = config.worldBarScale;
        ApplyWorldCanvasSize(instance);
        ApplyWorldFillPadding(instance);
        instance.gameObject.SetActive(true);

        var anchor = ResolveHeadAnchor(actor);
        instance.Bind(actor, anchor);
        worldBars.Add(instance);
    }

    private void ApplyWorldFillPadding(WorldHealthBarWidget instance)
    {
        if (config == null || instance == null)
            return;

        var view = instance.GetComponentInChildren<HealthBarView>(true);
        view?.SetFillPadding(config.worldFillPadding);
    }

    private void ApplyWorldCanvasSize(WorldHealthBarWidget instance)
    {
        if (config == null || instance == null)
            return;

        var canvasRect = instance.GetComponentInChildren<RectTransform>();
        if (canvasRect == null || canvasRect.gameObject.name != "Canvas")
        {
            foreach (var rect in instance.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.gameObject.name == "Canvas")
                {
                    canvasRect = rect;
                    break;
                }
            }
        }

        if (canvasRect != null)
            canvasRect.sizeDelta = config.worldBarCanvasSize;
    }

    private Transform ResolveHeadAnchor(AbilitySystemComponent actor)
    {
        if (actor == null)
            return null;

        string attachId = !string.IsNullOrEmpty(config?.worldAttachPointId)
            ? config.worldAttachPointId
            : headAttachPointId;

        var attachPoints = actor.GetComponentInChildren<AbilityVfxAttachPoints>(true);
        if (attachPoints != null && attachPoints.TryGet(attachId, out var point) && point != null)
            return point;

        var byName = FindChildTransform(actor.transform, attachId);
        if (byName != null)
            return byName;

        Debug.LogWarning($"[BattleHealthBarController] {actor.name} 找不到血条挂点「{attachId}」，使用 fallback 偏移。");
        var anchorGo = new GameObject($"{actor.name}_HealthBarAnchor");
        anchorGo.transform.SetParent(actor.transform, false);
        anchorGo.transform.localPosition = headOffsetFallback;
        return anchorGo.transform;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static List<AbilitySystemComponent> CollectBattleActors()
    {
        var result = new List<AbilitySystemComponent>();

        if (TurnManager.Instance != null)
        {
            var fromTurn = TurnManager.Instance.AllActors;
            if (fromTurn != null && fromTurn.Count > 0)
            {
                result.AddRange(fromTurn);
                return result;
            }
        }

        var found = Object.FindObjectsOfType<AbilitySystemComponent>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].Attributes != null)
                result.Add(found[i]);
        }

        return result;
    }
}
