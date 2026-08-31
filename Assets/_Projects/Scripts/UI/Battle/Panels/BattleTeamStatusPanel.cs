using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>双方 AP 与手牌总数面板。</summary>
public class BattleTeamStatusPanel : MonoBehaviour
{
    [SerializeField] private HealthBarUIConfig config;
    [SerializeField] private Text allyLineText;
    [SerializeField] private Text enemyLineText;

    private bool turnSubscribed;
    private readonly HashSet<TeamResourceManager> subscribedResources = new HashSet<TeamResourceManager>();
    private readonly HashSet<HandCardManager> subscribedHands = new HashSet<HandCardManager>();

    public static BattleTeamStatusPanel Ensure(Transform battleUiRoot, HealthBarUIConfig uiConfig = null)
    {
        if (battleUiRoot == null)
            return null;

        var canvas = battleUiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return null;

        var existing = canvas.GetComponentInChildren<BattleTeamStatusPanel>(true);
        if (existing != null)
        {
            existing.config ??= uiConfig ?? HealthBarUIConfig.LoadDefault();
            existing.SyncFromBattle();
            return existing;
        }

        return Create(canvas.transform, uiConfig ?? HealthBarUIConfig.LoadDefault());
    }

    public static BattleTeamStatusPanel Create(Transform canvasTransform, HealthBarUIConfig uiConfig)
    {
        var panelGo = new GameObject(
            "BattleTeamStatusPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleTeamStatusPanel));

        panelGo.transform.SetParent(canvasTransform, false);
        panelGo.transform.SetAsFirstSibling();

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(280f, 72f);

        var bg = panelGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);
        bg.raycastTarget = false;

        var panel = panelGo.GetComponent<BattleTeamStatusPanel>();
        panel.config = uiConfig;

        panel.allyLineText = CreateLine(panelGo.transform, "AllyLine", new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.96f), 20);
        panel.enemyLineText = CreateLine(panelGo.transform, "EnemyLine", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.48f), 20);

        panel.SyncFromBattle();
        return panel;
    }

    void OnEnable()
    {
        SubscribeTurnEvents();
        ResubscribeActorEvents();
        Refresh();
    }

    void OnDisable()
    {
        UnsubscribeTurnEvents();
        UnsubscribeActorEvents();
    }

    public void Configure(HealthBarUIConfig uiConfig)
    {
        if (uiConfig != null)
            config = uiConfig;
        Refresh();
    }

    public void SyncFromBattle()
    {
        SubscribeTurnEvents();
        ResubscribeActorEvents();
        Refresh();
    }

    public void Refresh()
    {
        EnsureTexts();
        config ??= HealthBarUIConfig.LoadDefault();
        int localTeam = config != null ? config.ResolveLocalTeamId() : 0;
        int enemyTeam = ResolveEnemyTeamId(localTeam);

        BuildTeamLine(localTeam, true, allyLineText);
        BuildTeamLine(enemyTeam, false, enemyLineText);
    }

    private void BuildTeamLine(int teamId, bool isAlly, Text label)
    {
        if (label == null)
            return;

        if (!TryCollectTeamStats(teamId, out int ap, out int maxAp, out int handCount))
        {
            BattleUiFonts.ApplyToLabel(label, isAlly ? "我方  —" : "敌方  —");
            label.color = isAlly ? new Color(0.72f, 0.95f, 0.78f, 1f) : new Color(1f, 0.72f, 0.72f, 1f);
            return;
        }

        string teamLabel = isAlly ? "我方" : "敌方";
        string handLabel = handCount < 0 ? "手牌 ?" : $"手牌 {handCount}";
        string line = $"{teamLabel}  AP {ap}/{maxAp}  ·  {handLabel}";
        BattleUiFonts.ApplyToLabel(label, line);
        label.color = isAlly ? new Color(0.72f, 0.95f, 0.78f, 1f) : new Color(1f, 0.72f, 0.72f, 1f);
    }

    private bool TryCollectTeamStats(int teamId, out int ap, out int maxAp, out int handCount)
    {
        ap = 0;
        maxAp = 0;
        handCount = 0;

        var actors = CollectActors();
        if (actors.Count == 0)
            return false;

        TeamResourceManager resource = null;
        bool found = false;

        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null || actor.TeamId != teamId)
                continue;

            if (!BattleTargeting.IsAlive(actor))
                continue;

            found = true;
            handCount += actor.HandCards != null ? actor.HandCards.HandCount : 0;
            resource ??= actor.TeamResource;
        }

        if (!found)
            return false;

        if (resource != null)
        {
            ap = resource.CurrentActionPoints;
            maxAp = resource.MaxActionPoints;
        }

        // 联机 PvP：仅本队手牌可见（与 BattleNetworkGate.CanLocalViewHand 一致）。
        if (BattleNetworkGate.IsNetworkBattleActive
            && !BattleNetworkGate.IsSoloHostTest)
        {
            config ??= HealthBarUIConfig.LoadDefault();
            int localTeam = config != null ? config.ResolveLocalTeamId() : BattleNetworkGate.LocalTeamId;
            if (teamId != localTeam)
                handCount = -1;
        }

        return true;
    }

    private static List<AbilitySystemComponent> CollectActors()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.AllActors.Count > 0)
            return new List<AbilitySystemComponent>(TurnManager.Instance.AllActors);

        return BattleTargeting.FindAllBattleActors();
    }

    private static int ResolveEnemyTeamId(int localTeam)
    {
        var actors = CollectActors();
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null || actor.TeamId == localTeam)
                continue;
            return actor.TeamId;
        }

        return localTeam == 0 ? 1 : 0;
    }

    private void EnsureTexts()
    {
        if (allyLineText != null && enemyLineText != null)
            return;

        if (allyLineText == null)
            allyLineText = transform.Find("AllyLine")?.GetComponent<Text>();

        if (enemyLineText == null)
            enemyLineText = transform.Find("EnemyLine")?.GetComponent<Text>();
    }

    private void SubscribeTurnEvents()
    {
        if (turnSubscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnPhaseChanged += HandleTurnEvent;
        TurnManager.Instance.OnTurnBegan += HandleTurnActor;
        TurnManager.Instance.OnTurnEnded += HandleTurnActor;
        TurnManager.Instance.OnBattleEnded += HandleBattleEnded;
        turnSubscribed = true;
    }

    private void UnsubscribeTurnEvents()
    {
        if (!turnSubscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnPhaseChanged -= HandleTurnEvent;
        TurnManager.Instance.OnTurnBegan -= HandleTurnActor;
        TurnManager.Instance.OnTurnEnded -= HandleTurnActor;
        TurnManager.Instance.OnBattleEnded -= HandleBattleEnded;
        turnSubscribed = false;
    }

    private void ResubscribeActorEvents()
    {
        UnsubscribeActorEvents();

        var actors = CollectActors();
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (actor == null)
                continue;

            var resource = actor.TeamResource;
            if (resource != null && subscribedResources.Add(resource))
                resource.OnActionPointsChanged += HandleApChanged;

            var hand = actor.HandCards;
            if (hand != null && subscribedHands.Add(hand))
                hand.HandChanged += HandleHandChanged;
        }
    }

    private void UnsubscribeActorEvents()
    {
        foreach (var resource in subscribedResources)
        {
            if (resource != null)
                resource.OnActionPointsChanged -= HandleApChanged;
        }

        subscribedResources.Clear();

        foreach (var hand in subscribedHands)
        {
            if (hand != null)
                hand.HandChanged -= HandleHandChanged;
        }

        subscribedHands.Clear();
    }

    private void HandleTurnEvent(TurnPhase _) => Refresh();

    private void HandleTurnActor(AbilitySystemComponent _) => Refresh();

    private void HandleBattleEnded(int _) => Refresh();

    private void HandleApChanged(int _) => Refresh();

    private void HandleHandChanged() => Refresh();

    private static Text CreateLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        BattleUiFonts.ApplyToLabel(text, string.Empty);
        return text;
    }
}
