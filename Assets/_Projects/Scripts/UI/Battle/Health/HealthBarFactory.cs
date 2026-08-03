using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 由 Sprite 或预制体构建 ProgressBars 风格血条。
/// </summary>
public static class HealthBarFactory
{
    public static WorldHealthBarWidget CreateWorldBar(
        Transform parent,
        HealthBarSpritePair sprites,
        HealthBarUIConfig config)
    {
        if (sprites == null || sprites.background == null || sprites.fill == null)
            return null;

        var canvasSize = config != null ? config.worldBarCanvasSize : new Vector2(872f, 50f);
        var scale = config != null ? config.worldBarScale : new Vector3(0.01f, 0.01f, 0.01f);

        var root = new GameObject("WorldHealthBar", typeof(WorldHealthBarWidget), typeof(BillboardToCamera));
        root.transform.SetParent(parent, false);
        root.transform.localScale = scale;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        var backgroundRect = CreateStretchImage(canvasGo.transform, "Background", sprites.background);
        var padding = config != null ? config.worldFillPadding : HealthBarFillPadding.WorldStyle1Default;
        var fillImage = CreateLoLFillImage(backgroundRect, "Fill", sprites.fill, canvasSize.y, padding);

        var view = canvasGo.AddComponent<HealthBarView>();
        view.Configure(fillImage, fillImage.rectTransform, HealthBarView.FillMode.Width, padding);
        AttachDepletionChips(view);

        var widget = root.GetComponent<WorldHealthBarWidget>();
        AssignWorldHealthBar(widget, view);
        return widget;
    }

    public static HealthBarView CreateOverlayBar(Transform parent, HealthBarSpritePair sprites, Vector2? size = null, HealthBarFillPadding? padding = null)
    {
        if (sprites == null || sprites.background == null || sprites.fill == null)
            return null;

        var sizeDelta = size ?? new Vector2(196f, 22f);
        var fillPadding = padding ?? HealthBarFillPadding.OverlayStyle4Default;
        var root = new GameObject("HealthBar", typeof(RectTransform), typeof(HealthBarView));
        root.transform.SetParent(parent, false);

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = sizeDelta;

        var backgroundRect = CreateStretchImage(root.transform, "Background", sprites.background);
        var fillImage = CreateLoLFillImage(backgroundRect, "Fill", sprites.fill, sizeDelta.y, fillPadding);

        var view = root.GetComponent<HealthBarView>();
        view.Configure(fillImage, fillImage.rectTransform, HealthBarView.FillMode.Width, fillPadding);
        AttachDepletionChips(view);
        return view;
    }

    private static void AttachDepletionChips(HealthBarView view)
    {
        if (view == null)
            return;

        var chips = view.GetComponent<HealthBarDepletionChips>();
        if (chips == null)
            chips = view.gameObject.AddComponent<HealthBarDepletionChips>();
        chips.Bind(view);
    }

    public static CharacterRosterEntryWidget CreateRosterEntry(
        Transform parent,
        HealthBarSpritePair barSprites,
        HealthBarUIConfig config = null)
    {
        var root = new GameObject("RosterEntry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CharacterRosterEntryWidget));
        root.transform.SetParent(parent, false);

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 72f);

        var bg = root.GetComponent<Image>();
        bg.color = Color.clear;
        bg.raycastTarget = false;

        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portraitGo.transform.SetParent(root.transform, false);
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(8f, 0f);
        portraitRect.sizeDelta = new Vector2(56f, 56f);

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        nameGo.transform.SetParent(root.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(68f, 24f);
        nameRect.sizeDelta = new Vector2(-12f, 28f);
        var nameText = nameGo.GetComponent<Text>();
        nameText.fontSize = 18;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;
        nameText.supportRichText = false;

        var barRoot = new GameObject("BarAnchor", typeof(RectTransform));
        barRoot.transform.SetParent(root.transform, false);
        var barRect = barRoot.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 30f);
        barRect.sizeDelta = new Vector2(-80f, 22f);

        var bar = CreateOverlayBar(
            barRoot.transform,
            barSprites,
            config?.overlayBarSize ?? new Vector2(0f, 22f),
            config?.overlayFillPadding);
        bar.gameObject.name = "HealthBar";
        StretchFull(bar.GetComponent<RectTransform>());

        InspirationTaskProgressBarView inspirationBar = null;
        if (config != null && config.inspirationBarEmpty != null && config.inspirationBarInProgressFill != null)
        {
            var inspirationRoot = new GameObject("InspirationBarAnchor", typeof(RectTransform));
            inspirationRoot.transform.SetParent(root.transform, false);
            var inspirationRect = inspirationRoot.GetComponent<RectTransform>();
            inspirationRect.anchorMin = new Vector2(0f, 0f);
            inspirationRect.anchorMax = new Vector2(1f, 0f);
            inspirationRect.pivot = new Vector2(0.5f, 0f);
            inspirationRect.anchoredPosition = new Vector2(0f, 8f);
            inspirationRect.sizeDelta = new Vector2(-80f, config.inspirationBarSize.y);

            inspirationBar = CreateInspirationProgressBar(inspirationRoot.transform, config);
            if (inspirationBar != null)
                StretchFull(inspirationBar.GetComponent<RectTransform>());
        }

        rect.sizeDelta = inspirationBar != null ? new Vector2(280f, 94f) : new Vector2(280f, 72f);

        var widget = root.GetComponent<CharacterRosterEntryWidget>();
        AssignRosterEntry(widget, portraitGo.GetComponent<Image>(), nameText, bar, inspirationBar);
        return widget;
    }

    public static InspirationTaskProgressBarView CreateInspirationProgressBar(Transform parent, HealthBarUIConfig config)
    {
        if (config == null || config.inspirationBarEmpty == null || config.inspirationBarInProgressFill == null)
            return null;

        var root = new GameObject("InspirationBar", typeof(RectTransform), typeof(InspirationTaskProgressBarView));
        root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = config.inspirationBarSize;

        var backgroundRect = CreateStretchImage(root.transform, "Background", config.inspirationBarEmpty);
        var fillImage = CreateLoLFillImage(
            backgroundRect,
            "Fill",
            config.inspirationBarInProgressFill,
            config.inspirationBarSize.y,
            config.inspirationFillPadding);

        var view = root.GetComponent<InspirationTaskProgressBarView>();
        AssignInspirationBar(
            view,
            backgroundRect,
            fillImage.rectTransform,
            fillImage,
            config.inspirationBarInProgressFill,
            config.inspirationBarCompleteFill,
            config.inspirationFillPadding);
        return view;
    }

    private static RectTransform CreateStretchImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        StretchFull(go.GetComponent<RectTransform>());
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateLoLFillImage(RectTransform background, string name, Sprite sprite, float height, HealthBarFillPadding padding)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(background, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(padding.left, (padding.bottom - padding.top) * 0.5f);

        float parentWidth = background.rect.width > 0f ? background.rect.width : background.sizeDelta.x;
        float fillWidth = Mathf.Max(0f, parentWidth - padding.Horizontal);
        float fillHeight = Mathf.Max(0f, height - padding.Vertical);
        rect.sizeDelta = new Vector2(fillWidth, fillHeight);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        return image;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void AssignWorldHealthBar(WorldHealthBarWidget widget, HealthBarView view)
    {
        var field = typeof(WorldHealthBarWidget).GetField("healthBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(widget, view);
    }

    private static void AssignRosterEntry(
        CharacterRosterEntryWidget widget,
        Image portrait,
        Text name,
        HealthBarView bar,
        InspirationTaskProgressBarView inspirationBar = null)
    {
        var type = typeof(CharacterRosterEntryWidget);
        type.GetField("portraitImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(widget, portrait);
        type.GetField("nameText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(widget, name);
        type.GetField("healthBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(widget, bar);
        type.GetField("inspirationProgressBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(widget, inspirationBar);
    }

    private static void AssignInspirationBar(
        InspirationTaskProgressBarView view,
        RectTransform track,
        RectTransform fill,
        Image fillImage,
        Sprite inProgress,
        Sprite complete,
        HealthBarFillPadding padding)
    {
        var type = typeof(InspirationTaskProgressBarView);
        type.GetField("trackRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, track);
        type.GetField("fillRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, fill);
        type.GetField("fillImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, fillImage);
        type.GetField("inProgressFill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, inProgress);
        type.GetField("completeFill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, complete);
        type.GetField("fillPadding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(view, padding);
    }
}
