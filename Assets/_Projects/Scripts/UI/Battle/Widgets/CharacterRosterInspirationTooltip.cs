using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>角色激励悬停提示。</summary>
public class CharacterRosterInspirationTooltip : MonoBehaviour
{
    public static CharacterRosterInspirationTooltip Instance { get; private set; }

    static readonly Color TooltipTextColor = new Color(1f, 0.78f, 0.35f, 1f);

    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Text bodyText;
    [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);
    [SerializeField] private float maxTextWidth = 320f;
    [SerializeField]     private float screenMargin = 12f;

    public static CharacterRosterInspirationTooltip Ensure(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return null;

        if (Instance != null)
            return Instance;

        var existing = canvasTransform.GetComponentInChildren<CharacterRosterInspirationTooltip>(true);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        return Create(canvasTransform);
    }

    public static CharacterRosterInspirationTooltip Create(Transform canvasTransform)
    {
        const float textWidth = 320f;

        var rootGo = new GameObject(
            "CharacterRosterInspirationTooltip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CharacterRosterInspirationTooltip));

        rootGo.transform.SetParent(canvasTransform, false);

        var panel = rootGo.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(textWidth + 28f, 80f);

        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        bg.raycastTarget = false;

        var tooltip = rootGo.GetComponent<CharacterRosterInspirationTooltip>();
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
        tooltip.bodyText.fontSize = 16;
        tooltip.bodyText.lineSpacing = 1.08f;
        tooltip.bodyText.color = TooltipTextColor;
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

    public void Show(AbilitySystemComponent actor, Vector2 screenPosition)
    {
        if (actor == null || bodyText == null || panelRect == null)
        {
            Hide();
            return;
        }

        string content = BuildContent(actor);
        if (string.IsNullOrWhiteSpace(content))
        {
            Hide();
            return;
        }

        BattleUiFonts.ApplyToLabel(bodyText, content);
        bodyText.color = TooltipTextColor;
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

    public static string BuildContent(AbilitySystemComponent actor)
    {
        if (actor == null)
            return null;

        var data = actor.CharacterData;
        var task = actor.InspirationTracker?.TaskDef ?? data?.inspirationTask;
        var ability = actor.InspirationAbility ?? data?.inspirationAbility;

        if (task == null && ability == null)
            return null;

        var sb = new StringBuilder(256);

        if (task != null)
        {
            sb.Append("【激励任务】");
            sb.AppendLine(string.IsNullOrWhiteSpace(task.taskName) ? "未命名任务" : task.taskName);

            if (!string.IsNullOrWhiteSpace(task.description))
                sb.AppendLine(task.description.Trim());

            AppendTaskProgress(sb, actor, task);
            sb.AppendLine();
        }

        if (ability != null)
        {
            sb.Append("【激励技能】");
            sb.AppendLine(string.IsNullOrWhiteSpace(ability.abilityName) ? "未命名技能" : ability.abilityName);

            if (!string.IsNullOrWhiteSpace(ability.description))
                sb.Append(ability.description.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendTaskProgress(StringBuilder sb, AbilitySystemComponent actor, InspirationTaskSO task)
    {
        var tracker = actor.InspirationTracker;
        if (task.objectives == null || task.objectives.Count == 0)
            return;

        sb.AppendLine();
        sb.Append("进度：");

        if (tracker != null)
        {
            int percent = Mathf.RoundToInt(tracker.GetProgressRatio() * 100f);
            sb.Append(percent);
            sb.AppendLine("%");
        }
        else
        {
            sb.AppendLine("0%");
        }

        for (int i = 0; i < task.objectives.Count; i++)
        {
            var objective = task.objectives[i];
            if (objective == null)
                continue;

            int current = tracker != null ? tracker.GetProgress(objective) : 0;
            int target = objective.GetProgressTarget();
            string label = !string.IsNullOrWhiteSpace(objective.displayName)
                ? objective.displayName
                : objective.GetType().Name;

            sb.Append("  · ");
            sb.Append(label);
            sb.Append(' ');
            sb.Append(current);
            sb.Append('/');
            sb.AppendLine(target.ToString());
        }
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
