using UnityEngine;
using UnityEngine.UI;

/// <summary>单条伤害飘字，由 Spawner 统一驱动。</summary>
[DisallowMultipleComponent]
public class DamagePopupWidget : MonoBehaviour
{
    private RectTransform rect;
    private Text label;
    private Outline outline;

    private Vector3 worldPosition;
    private Vector2 velocity;
    private Vector2 screenDrift;
    private float life;
    private float maxLife;
    private float gravity;
    private float fadeStartRatio;
    private float punchDuration;
    private float punchScale;
    private Color baseColor;

    public bool IsAlive => life > 0f;

    public static DamagePopupWidget Create(Transform parent, Font font)
    {
        var go = new GameObject("DamagePopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);

        var widget = go.AddComponent<DamagePopupWidget>();
        widget.rect = go.GetComponent<RectTransform>();
        widget.label = go.GetComponent<Text>();
        widget.outline = go.GetComponent<Outline>();

        // 锚在父层中心：ScreenPointToLocalPointInRectangle 返回的就是相对中心的坐标。
        widget.rect.anchorMin = new Vector2(0.5f, 0.5f);
        widget.rect.anchorMax = new Vector2(0.5f, 0.5f);
        widget.rect.pivot = new Vector2(0.5f, 0.5f);
        widget.rect.sizeDelta = new Vector2(320f, 80f);

        widget.label.font = font;
        widget.label.alignment = TextAnchor.MiddleCenter;
        widget.label.raycastTarget = false;
        widget.label.supportRichText = false;
        widget.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        widget.label.verticalOverflow = VerticalWrapMode.Overflow;

        widget.outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        widget.outline.effectDistance = new Vector2(2f, -2f);

        go.SetActive(false);
        return widget;
    }

    public void Play(
        string text,
        Color color,
        Color outlineColor,
        int fontSize,
        FontStyle fontStyle,
        Vector3 world,
        Vector2 initialVelocity,
        Vector2 startDrift,
        DamagePopupConfig config)
    {
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        baseColor = color;
        label.color = color;
        outline.effectColor = outlineColor;

        worldPosition = world;
        velocity = initialVelocity;
        screenDrift = startDrift;

        maxLife = Mathf.Max(0.05f, config.lifetime);
        life = maxLife;
        gravity = config.gravity;
        fadeStartRatio = Mathf.Clamp01(config.fadeStartRatio);
        punchDuration = Mathf.Max(0f, config.punchDuration);
        punchScale = Mathf.Max(1f, config.punchScale);

        rect.localScale = Vector3.one * (punchDuration > 0f ? 0.55f : 1f);
        gameObject.SetActive(true);
    }

    /// <summary>推进一帧，返回 false 表示可回收。</summary>
    public bool Tick(float deltaTime, Camera cam, RectTransform canvasRect)
    {
        life -= deltaTime;
        if (life <= 0f)
            return false;

        screenDrift += velocity * deltaTime;
        velocity.y -= gravity * deltaTime;

        if (cam == null || canvasRect == null)
            return true;

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPosition);
        // 角色转到相机背后时 WorldToScreenPoint 会镜像出错误坐标，直接藏起来。
        if (screenPoint.z <= 0f)
        {
            label.enabled = false;
            return true;
        }

        label.enabled = true;

        // 位移加在 Canvas 局部坐标上而不是屏幕像素上，这样飘字幅度和字号
        // 用的是同一套参考分辨率单位，换分辨率时观感一致。
        Vector2 screen = new Vector2(screenPoint.x, screenPoint.y);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            rect.anchoredPosition = local + screenDrift;

        ApplyPunch();
        ApplyFade();
        return true;
    }

    public void Recycle()
    {
        life = 0f;
        label.enabled = true;
        gameObject.SetActive(false);
    }

    private void ApplyPunch()
    {
        if (punchDuration <= 0f)
        {
            rect.localScale = Vector3.one;
            return;
        }

        float elapsed = maxLife - life;
        if (elapsed >= punchDuration)
        {
            rect.localScale = Vector3.one;
            return;
        }

        float p = elapsed / punchDuration;
        float scale = p < 0.5f
            ? Mathf.Lerp(0.55f, punchScale, p * 2f)
            : Mathf.Lerp(punchScale, 1f, (p - 0.5f) * 2f);

        rect.localScale = Vector3.one * scale;
    }

    private void ApplyFade()
    {
        float progress = 1f - life / maxLife;
        float alpha = progress <= fadeStartRatio || fadeStartRatio >= 1f
            ? 1f
            : 1f - (progress - fadeStartRatio) / (1f - fadeStartRatio);

        var color = baseColor;
        color.a = baseColor.a * Mathf.Clamp01(alpha);
        label.color = color;

        var outlineColor = outline.effectColor;
        outlineColor.a = Mathf.Clamp01(alpha) * 0.9f;
        outline.effectColor = outlineColor;
    }
}
