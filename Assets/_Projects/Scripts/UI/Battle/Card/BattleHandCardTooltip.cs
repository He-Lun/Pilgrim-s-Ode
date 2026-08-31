using UnityEngine;
using UnityEngine.UI;

/// <summary>手牌悬停提示。</summary>
public class BattleHandCardTooltip : MonoBehaviour
{
    public static BattleHandCardTooltip Instance { get; private set; }

    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Text bodyText;
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);
    [SerializeField] private float maxTextWidth = 300f;
    [SerializeField] private float screenMargin = 12f;

    public static BattleHandCardTooltip Ensure(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return null;

        if (Instance != null)
            return Instance;

        var existing = canvasTransform.GetComponentInChildren<BattleHandCardTooltip>(true);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        return Create(canvasTransform);
    }

    public static BattleHandCardTooltip Create(Transform canvasTransform)
    {
        const float textWidth = 300f;

        var rootGo = new GameObject(
            "BattleHandCardTooltip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleHandCardTooltip));

        rootGo.transform.SetParent(canvasTransform, false);

        var panel = rootGo.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(textWidth + 28f, 80f);

        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        bg.raycastTarget = false;

        var tooltip = rootGo.GetComponent<BattleHandCardTooltip>();
        tooltip.panelRect = panel;
        tooltip.maxTextWidth = textWidth;

        var textGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(rootGo.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        tooltip.bodyText = textGo.GetComponent<Text>();
        tooltip.bodyText.alignment = TextAnchor.UpperLeft;
        tooltip.bodyText.fontSize = 17;
        tooltip.bodyText.lineSpacing = 1.05f;
        tooltip.bodyText.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        tooltip.bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltip.bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        tooltip.bodyText.raycastTarget = false;
        tooltip.bodyText.supportRichText = false;

        var layout = textGo.AddComponent<LayoutElement>();
        layout.preferredWidth = textWidth;

        var fitter = textGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var panelFitter = rootGo.AddComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var panelLayout = rootGo.AddComponent<LayoutElement>();
        panelLayout.preferredWidth = textWidth + 28f;

        tooltip.gameObject.SetActive(false);
        Instance = tooltip;
        return tooltip;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        panelRect ??= GetComponent<RectTransform>();
        bodyText ??= GetComponentInChildren<Text>(true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(GameplayAbility ability, Vector2 screenPosition)
    {
        if (ability == null || bodyText == null || panelRect == null)
        {
            Hide();
            return;
        }

        string content = string.IsNullOrWhiteSpace(ability.description)
            ? ability.abilityName
            : ability.description;

        if (string.IsNullOrWhiteSpace(content))
        {
            Hide();
            return;
        }

        BattleUiFonts.ApplyToLabel(bodyText, content);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxTextWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        SetScreenPosition(screenPosition);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void SetScreenPosition(Vector2 screenPosition)
    {
        Vector2 pos = screenPosition + screenOffset;

        float width = panelRect.rect.width;
        float height = panelRect.rect.height;

        if (pos.x + width > Screen.width - screenMargin)
            pos.x = screenPosition.x - screenOffset.x - width;

        if (pos.y - height < screenMargin)
            pos.y = screenPosition.y - screenOffset.y + height;

        pos.x = Mathf.Clamp(pos.x, screenMargin, Screen.width - width - screenMargin);
        pos.y = Mathf.Clamp(pos.y, height + screenMargin, Screen.height - screenMargin);

        panelRect.position = new Vector3(pos.x, pos.y, panelRect.position.z);
    }
}
