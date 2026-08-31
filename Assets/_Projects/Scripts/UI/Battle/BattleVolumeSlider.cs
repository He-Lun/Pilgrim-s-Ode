using UnityEngine;
using UnityEngine.UI;

/// <summary>主音量滑条。</summary>
[DisallowMultipleComponent]
public class BattleVolumeSlider : MonoBehaviour
{
    private Slider slider;
    private Text percentText;
    private bool suppressCallbacks;

    public static BattleVolumeSlider Ensure(Transform battleUiRoot)
    {
        if (battleUiRoot == null)
            return null;

        var canvas = battleUiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return null;

        var existing = canvas.GetComponentInChildren<BattleVolumeSlider>(true);
        if (existing != null)
        {
            existing.RefreshFromManager();
            return existing;
        }

        return Create(canvas.transform);
    }

    public static BattleVolumeSlider Create(Transform canvasTransform)
    {
        var rootGo = new GameObject(
            "BattleVolumeSlider",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleVolumeSlider));

        rootGo.transform.SetParent(canvasTransform, false);

        var rect = rootGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(28f, 28f);
        rect.sizeDelta = new Vector2(300f, 52f);

        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);
        bg.raycastTarget = true;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(rootGo.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(12f, 0f);
        labelRect.sizeDelta = new Vector2(48f, 32f);

        var labelText = labelGo.GetComponent<Text>();
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.fontSize = 18;
        labelText.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        BattleUiFonts.ApplyToLabel(labelText, "音量");

        var percentGo = new GameObject("Percent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        percentGo.transform.SetParent(rootGo.transform, false);
        var percentRect = percentGo.GetComponent<RectTransform>();
        percentRect.anchorMin = new Vector2(1f, 0.5f);
        percentRect.anchorMax = new Vector2(1f, 0.5f);
        percentRect.pivot = new Vector2(1f, 0.5f);
        percentRect.anchoredPosition = new Vector2(-12f, 0f);
        percentRect.sizeDelta = new Vector2(52f, 32f);

        var percentLabel = percentGo.GetComponent<Text>();
        percentLabel.alignment = TextAnchor.MiddleRight;
        percentLabel.fontSize = 18;
        percentLabel.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        BattleUiFonts.ApplyToLabel(percentLabel, "100%");

        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(rootGo.transform, false);
        var sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = new Vector2(-132f, 20f);

        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        var trackSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        var knobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        var trackGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackGo.transform.SetParent(sliderGo.transform, false);
        StretchFull(trackGo.GetComponent<RectTransform>());
        var trackImage = trackGo.GetComponent<Image>();
        if (trackSprite != null)
            trackImage.sprite = trackSprite;
        trackImage.type = Image.Type.Sliced;
        trackImage.color = new Color(0.22f, 0.24f, 0.28f, 1f);

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        StretchFull(fillGo.GetComponent<RectTransform>());
        var fillImage = fillGo.GetComponent<Image>();
        if (trackSprite != null)
            fillImage.sprite = trackSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.72f, 0.58f, 0.22f, 1f);

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        var handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
        StretchFull(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRect = handleGo.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 18f);
        var handleImage = handleGo.GetComponent<Image>();
        if (knobSprite != null)
            handleImage.sprite = knobSprite;
        handleImage.color = new Color(0.98f, 0.96f, 0.92f, 1f);

        slider.targetGraphic = handleImage;
        slider.fillRect = fillGo.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;

        var widget = rootGo.GetComponent<BattleVolumeSlider>();
        widget.slider = slider;
        widget.percentText = percentLabel;
        widget.slider.onValueChanged.AddListener(widget.OnSliderChanged);
        widget.RefreshFromManager();
        return widget;
    }

    void OnEnable()
    {
        RefreshFromManager();
    }

    private void RefreshFromManager()
    {
        if (slider == null)
            return;

        var manager = AudioManager.Instance ?? AudioManager.Ensure();
        suppressCallbacks = true;
        slider.value = manager.GetMasterVolume();
        suppressCallbacks = false;
        UpdatePercentLabel(slider.value);
    }

    private void OnSliderChanged(float value)
    {
        if (suppressCallbacks)
            return;

        var manager = AudioManager.Instance ?? AudioManager.Ensure();
        manager.SetMasterVolume(value);
        UpdatePercentLabel(value);
    }

    private void UpdatePercentLabel(float value)
    {
        if (percentText == null)
            return;

        BattleUiFonts.ApplyToLabel(percentText, $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%");
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
