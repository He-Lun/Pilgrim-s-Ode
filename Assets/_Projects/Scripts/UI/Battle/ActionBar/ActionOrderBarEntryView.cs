using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 行动条单格 — 头像 + 边框；插入/拉条预演时缓慢闪烁。
/// </summary>
public class ActionOrderBarEntryView : MonoBehaviour
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image previewGlowImage;
    [SerializeField] private CanvasGroup canvasGroup;

    private bool blink;
    private bool dimmed;
    private float blinkPhase;

    void Awake()
    {
        frameImage ??= transform.Find("Frame")?.GetComponent<Image>();
        portraitImage ??= transform.Find("Portrait")?.GetComponent<Image>();
        previewGlowImage ??= transform.Find("PreviewGlow")?.GetComponent<Image>();
        canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (!blink)
            return;

        blinkPhase += Time.deltaTime * 2.2f;
        float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(blinkPhase));
        canvasGroup.alpha = dimmed ? pulse * 0.55f : pulse;

        if (previewGlowImage != null && previewGlowImage.enabled)
        {
            var c = previewGlowImage.color;
            c.a = 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(blinkPhase));
            previewGlowImage.color = c;
        }
    }

    public void Apply(ActionBarDisplayEntry entry, bool isAlly, Sprite portrait, float displaySize)
    {
        var rect = transform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(displaySize, displaySize);

        blink = entry.blink;
        dimmed = entry.dimmed;

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
            portraitImage.color = Color.white;
        }

        bool isCurrentTurn = entry.kind == ActionBarEntryKind.CurrentTurn;

        if (frameImage != null)
        {
            Stretch(frameImage.rectTransform, 0f);

            frameImage.color = isCurrentTurn || entry.isCurrentActor
                ? new Color(1f, 0.85f, 0.2f, 0.98f)
                : isAlly
                    ? new Color(0.25f, 0.85f, 0.45f, 0.9f)
                    : new Color(0.95f, 0.3f, 0.25f, 0.9f);
        }

        bool isAdvancePreview = entry.kind == ActionBarEntryKind.AdvancePreview;
        bool isNextTurnPreview = entry.kind == ActionBarEntryKind.NextTurnPreview;
        if (previewGlowImage != null)
        {
            previewGlowImage.enabled = isAdvancePreview || isNextTurnPreview
                || entry.kind == ActionBarEntryKind.Insert;
            Stretch(previewGlowImage.rectTransform, 6f);
            previewGlowImage.color = isAdvancePreview
                ? new Color(0.55f, 0.85f, 1f, 0.45f)
                : isNextTurnPreview
                    ? new Color(1f, 0.85f, 0.35f, 0.45f)
                    : new Color(1f, 0.75f, 0.25f, 0.35f);
        }

        if (!blink)
            canvasGroup.alpha = dimmed ? 0.45f : 1f;
    }

    public void Apply(ActionBarDisplayEntry entry, bool isAlly, Sprite portrait)
    {
        Apply(entry, isAlly, portrait, 48f);
    }

    public static ActionOrderBarEntryView Create(Transform parent, float size)
    {
        var root = new GameObject("ActionEntry", typeof(RectTransform), typeof(CanvasGroup), typeof(ActionOrderBarEntryView));
        root.transform.SetParent(parent, false);

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);

        var glowGo = new GameObject("PreviewGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(root.transform, false);
        Stretch(glowGo.GetComponent<RectTransform>(), 6f);
        var glow = glowGo.GetComponent<Image>();
        glow.raycastTarget = false;
        glow.enabled = false;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frameGo.transform.SetParent(root.transform, false);
        Stretch(frameGo.GetComponent<RectTransform>(), 0f);
        var frame = frameGo.GetComponent<Image>();
        frame.raycastTarget = false;
        frame.color = new Color(0.2f, 0.8f, 0.4f, 0.9f);

        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitGo.transform.SetParent(root.transform, false);
        Stretch(portraitGo.GetComponent<RectTransform>(), 4f);
        var portrait = portraitGo.GetComponent<Image>();
        portrait.raycastTarget = false;
        portrait.preserveAspect = true;

        var view = root.GetComponent<ActionOrderBarEntryView>();
        view.frameImage = frame;
        view.portraitImage = portrait;
        view.previewGlowImage = glow;
        view.canvasGroup = root.GetComponent<CanvasGroup>();
        return view;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }
}
