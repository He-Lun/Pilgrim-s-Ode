using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 星铁式行动条 — 右对齐纵列 + 顶部向左插入（7 字布局）。
/// </summary>
public class ActionOrderBarPanel : MonoBehaviour
{
    [SerializeField] private RectTransform confirmRowRect;
    [SerializeField] private RectTransform timelineTrackRect;
    [SerializeField] private Image trackBackground;
    [SerializeField] private HealthBarUIConfig teamConfig;
    [SerializeField] private ActionBarPortraitConfig portraitConfig;

    [Header("尺寸")]
    [SerializeField] private float panelWidth = 72f;
    [SerializeField] private float panelHeight = 720f;
    [SerializeField] private float confirmRowHeight = 56f;
    [SerializeField] private float entrySize = 48f;
    [SerializeField] private float confirmSpacing = 6f;
    [SerializeField] private float timelineSpacing = 8f;
    [SerializeField] private float trackPaddingRight = 0f;
    [SerializeField] private float timelinePaddingTop = 4f;
    [SerializeField] private float trackPaddingBottom = 12f;

    private readonly List<ActionOrderBarEntryView> confirmEntryPool = new List<ActionOrderBarEntryView>();
    private readonly List<ActionOrderBarEntryView> timelineEntryPool = new List<ActionOrderBarEntryView>();
    private bool subscribed;
    private bool queueSubscribed;
    private bool turnSubscribed;

    void Awake()
    {
        EnsureLayout();
        teamConfig ??= HealthBarUIConfig.LoadDefault();
        portraitConfig ??= teamConfig != null ? teamConfig.actionBarPortraits : null;
        DisablePanelBackground();
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public void Configure(HealthBarUIConfig config)
    {
        if (config != null)
        {
            teamConfig = config;
            portraitConfig ??= config.actionBarPortraits;
        }
        Refresh();
    }

    public void Refresh()
    {
        EnsureRuntimeSubscriptions();
        EnsureLayout();

        var queue = ActionQueue.Instance;
        if (queue == null || timelineTrackRect == null)
        {
            HideAllEntries();
            return;
        }

        var current = TurnManager.Instance != null ? TurnManager.Instance.CurrentActor : null;
        var snapshot = ActionBarSnapshotBuilder.Build(queue, current);

        LayoutConfirmRow(snapshot.confirmRow);
        LayoutTimeline(snapshot.timeline);
    }

    private void LayoutConfirmRow(List<ActionBarDisplayEntry> entries)
    {
        if (confirmRowRect == null)
            return;

        int count = entries != null ? entries.Count : 0;
        EnsurePoolSize(confirmEntryPool, count, confirmRowRect, entrySize);

        float contentWidth = count > 0
            ? count * entrySize + (count - 1) * confirmSpacing
            : entrySize;
        ApplyPanelWidth(contentWidth);

        for (int i = 0; i < confirmEntryPool.Count; i++)
        {
            bool active = i < count;
            confirmEntryPool[i].gameObject.SetActive(active);
            if (!active)
                continue;

            var data = entries[i];
            int slotFromRight = count - 1 - i;
            float x = -trackPaddingRight - slotFromRight * (entrySize + confirmSpacing);

            var entryRect = confirmEntryPool[i].GetComponent<RectTransform>();
            entryRect.anchorMin = new Vector2(1f, 0.5f);
            entryRect.anchorMax = new Vector2(1f, 0.5f);
            entryRect.pivot = new Vector2(1f, 0.5f);
            entryRect.anchoredPosition = new Vector2(x, 0f);
            entryRect.sizeDelta = new Vector2(entrySize, entrySize);

            bool isAlly = teamConfig == null || teamConfig.IsAlly(data.actor);
            confirmEntryPool[i].Apply(data, isAlly, ResolvePortrait(data.actor), entrySize);
        }
    }

    private void LayoutTimeline(List<ActionBarDisplayEntry> entries)
    {
        if (timelineTrackRect == null)
            return;

        int count = entries != null ? entries.Count : 0;
        if (count == 0)
        {
            for (int i = 0; i < timelineEntryPool.Count; i++)
                timelineEntryPool[i].gameObject.SetActive(false);
            return;
        }

        float trackHeight = timelineTrackRect.rect.height > 0f
            ? timelineTrackRect.rect.height - timelinePaddingTop - trackPaddingBottom
            : panelHeight - confirmRowHeight - timelinePaddingTop - trackPaddingBottom;

        float spacing = ResolveTimelineSpacing(count, trackHeight);
        float slotStride = entrySize + spacing;

        EnsurePoolSize(timelineEntryPool, count, timelineTrackRect, entrySize);

        for (int i = 0; i < timelineEntryPool.Count; i++)
        {
            bool active = i < count;
            timelineEntryPool[i].gameObject.SetActive(active);
            if (!active)
                continue;

            var data = entries[i];
            float y = -timelinePaddingTop - i * slotStride;

            var entryRect = timelineEntryPool[i].GetComponent<RectTransform>();
            entryRect.anchorMin = new Vector2(1f, 1f);
            entryRect.anchorMax = new Vector2(1f, 1f);
            entryRect.pivot = new Vector2(1f, 1f);
            entryRect.anchoredPosition = new Vector2(-trackPaddingRight, y);
            entryRect.sizeDelta = new Vector2(entrySize, entrySize);

            bool isAlly = teamConfig == null || teamConfig.IsAlly(data.actor);
            timelineEntryPool[i].Apply(data, isAlly, ResolvePortrait(data.actor), entrySize);
        }
    }

    private void ApplyPanelWidth(float confirmContentWidth)
    {
        var panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        float width = Mathf.Max(panelWidth, confirmContentWidth);
        panelRect.sizeDelta = new Vector2(width, panelHeight);
    }

    private float ResolveTimelineSpacing(int entryCount, float trackHeight)
    {
        if (entryCount <= 1)
            return timelineSpacing;

        float needed = entryCount * entrySize + (entryCount - 1) * timelineSpacing;
        if (needed <= trackHeight)
            return timelineSpacing;

        float compressed = (trackHeight - entryCount * entrySize) / (entryCount - 1);
        return Mathf.Max(2f, compressed);
    }

    private void EnsureLayout()
    {
        var panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        if (confirmRowRect == null || timelineTrackRect == null)
        {
            confirmRowRect = transform.Find("ConfirmRow") as RectTransform;
            timelineTrackRect = transform.Find("TimelineTrack") as RectTransform;
        }

        if (confirmRowRect == null || timelineTrackRect == null)
            BuildLayoutHierarchy(panelRect);

        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        confirmRowRect.anchorMin = new Vector2(0f, 1f);
        confirmRowRect.anchorMax = new Vector2(1f, 1f);
        confirmRowRect.pivot = new Vector2(1f, 1f);
        confirmRowRect.anchoredPosition = Vector2.zero;
        confirmRowRect.sizeDelta = new Vector2(0f, confirmRowHeight);

        timelineTrackRect.anchorMin = Vector2.zero;
        timelineTrackRect.anchorMax = Vector2.one;
        timelineTrackRect.offsetMin = Vector2.zero;
        timelineTrackRect.offsetMax = new Vector2(0f, -confirmRowHeight);
    }

    private void BuildLayoutHierarchy(RectTransform panelRect)
    {
        var confirmGo = new GameObject("ConfirmRow", typeof(RectTransform));
        confirmGo.transform.SetParent(panelRect, false);
        confirmRowRect = confirmGo.GetComponent<RectTransform>();

        var timelineGo = new GameObject("TimelineTrack", typeof(RectTransform));
        timelineGo.transform.SetParent(panelRect, false);
        timelineTrackRect = timelineGo.GetComponent<RectTransform>();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        EnsureRuntimeSubscriptions();
        ActionBarPreviewContext.Changed += Refresh;
        subscribed = true;
    }

    private void EnsureRuntimeSubscriptions()
    {
        if (!queueSubscribed && ActionQueue.Instance != null)
        {
            ActionQueue.Instance.Changed += Refresh;
            queueSubscribed = true;
        }

        if (!turnSubscribed && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            TurnManager.Instance.OnTurnBegan += HandleTurnActor;
            TurnManager.Instance.OnTurnEnded += HandleTurnActor;
            turnSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed && !queueSubscribed && !turnSubscribed)
            return;

        if (queueSubscribed && ActionQueue.Instance != null)
            ActionQueue.Instance.Changed -= Refresh;

        if (turnSubscribed && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            TurnManager.Instance.OnTurnBegan -= HandleTurnActor;
            TurnManager.Instance.OnTurnEnded -= HandleTurnActor;
        }

        if (subscribed)
            ActionBarPreviewContext.Changed -= Refresh;

        subscribed = false;
        queueSubscribed = false;
        turnSubscribed = false;
    }

    private void HandlePhaseChanged(TurnPhase _) => Refresh();
    private void HandleTurnActor(AbilitySystemComponent _) => Refresh();

    private static void EnsurePoolSize(
        List<ActionOrderBarEntryView> pool,
        int count,
        RectTransform parent,
        float defaultSize)
    {
        while (pool.Count < count)
        {
            var entry = ActionOrderBarEntryView.Create(parent, defaultSize);
            entry.gameObject.SetActive(false);
            pool.Add(entry);
        }
    }

    private void HideAllEntries()
    {
        for (int i = 0; i < confirmEntryPool.Count; i++)
            confirmEntryPool[i].gameObject.SetActive(false);
        for (int i = 0; i < timelineEntryPool.Count; i++)
            timelineEntryPool[i].gameObject.SetActive(false);
    }

    private Sprite ResolvePortrait(AbilitySystemComponent actor)
    {
        var data = actor?.CharacterData;
        if (data == null)
            return null;

        if (portraitConfig != null && portraitConfig.TryGetPortrait(data, out var portrait))
            return portrait;

        if (data.portrait != null)
            return data.portrait;

        return data.inspirationAbility != null ? data.inspirationAbility.icon : null;
    }

    private void DisablePanelBackground()
    {
        trackBackground ??= GetComponent<Image>();
        if (trackBackground == null)
            return;

        trackBackground.color = Color.clear;
        trackBackground.raycastTarget = false;
    }

    public static ActionOrderBarPanel Create(Transform canvasRoot, HealthBarUIConfig config)
    {
        var panelGo = new GameObject("ActionOrderBarPanel", typeof(RectTransform), typeof(ActionOrderBarPanel));
        panelGo.transform.SetParent(canvasRoot, false);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-24f, 0f);

        var panel = panelGo.GetComponent<ActionOrderBarPanel>();
        panel.teamConfig = config;
        panel.portraitConfig = config != null ? config.actionBarPortraits : null;
        panel.EnsureLayout();
        panel.DisablePanelBackground();
        return panel;
    }
}
