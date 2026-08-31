using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>回合阶段横幅。</summary>
[DisallowMultipleComponent]
public class BattleTurnPhaseBanner : MonoBehaviour
{
    private static readonly Color AllyColor = new Color(0.38f, 0.96f, 0.48f, 1f);
    private static readonly Color EnemyColor = new Color(0.98f, 0.38f, 0.38f, 1f);

    [SerializeField] private float displaySeconds = 1.35f;
    [SerializeField] private float fadeSeconds = 0.35f;
    [SerializeField] private int fontSize = 52;

    private Text label;
    private CanvasGroup canvasGroup;
    private bool turnSubscribed;
    private Coroutine hideRoutine;

    public static BattleTurnPhaseBanner Ensure(Transform battleUiRoot)
    {
        if (battleUiRoot == null)
            return null;

        var canvas = battleUiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return null;

        var existing = canvas.GetComponentInChildren<BattleTurnPhaseBanner>(true);
        if (existing != null)
        {
            existing.SubscribeTurnEvents();
            return existing;
        }

        return Create(canvas.transform);
    }

    public static BattleTurnPhaseBanner Create(Transform canvasTransform)
    {
        var rootGo = new GameObject(
            "BattleTurnPhaseBanner",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(BattleTurnPhaseBanner));

        rootGo.transform.SetParent(canvasTransform, false);
        rootGo.transform.SetAsLastSibling();

        var rect = rootGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(640f, 120f);

        var group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(rootGo.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGo.GetComponent<Text>();
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 52;
        labelText.fontStyle = FontStyle.Bold;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        BattleUiFonts.ApplyToLabel(labelText, string.Empty);

        var widget = rootGo.GetComponent<BattleTurnPhaseBanner>();
        widget.label = labelText;
        widget.canvasGroup = group;
        widget.SubscribeTurnEvents();
        return widget;
    }

    void OnEnable()
    {
        SubscribeTurnEvents();
    }

    void OnDisable()
    {
        UnsubscribeTurnEvents();
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void Update()
    {
        SubscribeTurnEvents();
    }

    private void SubscribeTurnEvents()
    {
        if (turnSubscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan += HandleTurnBegan;
        turnSubscribed = true;
    }

    private void UnsubscribeTurnEvents()
    {
        if (!turnSubscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan -= HandleTurnBegan;
        turnSubscribed = false;
    }

    private void HandleTurnBegan(AbilitySystemComponent actor)
    {
        if (actor == null)
            return;

        bool isAllyTurn = actor.TeamId == ResolveLocalTeamId();
        Show(isAllyTurn ? "我方回合" : "敌方回合", isAllyTurn ? AllyColor : EnemyColor);
    }

    private static int ResolveLocalTeamId()
    {
        if (BattleNetworkGate.IsNetworkBattleActive)
        {
            int networkTeam = BattleNetworkGate.LocalTeamId;
            if (networkTeam >= 0)
                return networkTeam;
        }

        var config = HealthBarUIConfig.LoadDefault();
        return config != null ? config.localTeamId : 0;
    }

    private void Show(string message, Color color)
    {
        if (label == null || canvasGroup == null)
            return;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        label.color = color;
        BattleUiFonts.ApplyToLabel(label, message);
        label.fontSize = fontSize;
        canvasGroup.alpha = 1f;
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        float hold = Mathf.Max(0.1f, displaySeconds);
        float fade = Mathf.Max(0.05f, fadeSeconds);

        yield return new WaitForSecondsRealtime(hold);

        float elapsed = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        while (elapsed < fade)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fade);
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        hideRoutine = null;
    }
}
