using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血量条展示 — 默认 Width 模式，左端固定、从右往左扣（英雄联盟风格）。
/// </summary>
public class HealthBarView : MonoBehaviour
{
    public enum FillMode
    {
        Auto,
        FillAmount,
        Width
    }

    public enum DepleteDirection
    {
        /// <summary>左端固定，扣血时右端往左缩。</summary>
        RightToLeft,
        /// <summary>右端固定，扣血时左端往右缩。</summary>
        LeftToRight
    }

    [Header("填充目标（二选一或同时配置，Auto 优先 Width）")]
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillRect;

    [SerializeField] private FillMode fillMode = FillMode.Auto;
    [SerializeField] private DepleteDirection depleteDirection = DepleteDirection.RightToLeft;
    [SerializeField] private HealthBarFillPadding fillPadding = HealthBarFillPadding.WorldStyle1Default;

    private float maxFillWidth;
    private float fillHeight;
    private float lastRatio = 1f;
    private float lastCurrentHealth = -1f;
    private FillMode resolvedMode;
    private bool layoutApplied;

    /// <summary>血量比例下降时触发（旧比例, 新比例）。</summary>
    public event Action<float, float> OnRatioDecreased;

    void Awake()
    {
        AutoBindFromHierarchy();
        ResolveMode();
    }

    void Start()
    {
        if (resolvedMode == FillMode.Width && fillRect != null)
        {
            layoutApplied = false;
            ApplyWidthLayout();
            CacheMaxFillWidth();
        }

        ApplyRatioVisual(lastRatio);
    }

    void OnRectTransformDimensionsChange()
    {
        if (resolvedMode == FillMode.Width)
            CacheMaxFillWidth();
    }

    public void Configure(Image fill, RectTransform fillTransform, FillMode mode = FillMode.Auto, HealthBarFillPadding? padding = null)
    {
        fillImage = fill;
        fillRect = fillTransform;
        fillMode = mode;
        if (padding.HasValue)
            fillPadding = padding.Value;
        layoutApplied = false;
        ResolveMode();
    }

    public void SetFillPadding(HealthBarFillPadding padding)
    {
        fillPadding = padding;
        layoutApplied = false;
        ResolveMode();
        ApplyRatioVisual(lastRatio);
    }

    /// <summary>
    /// 从子节点自动绑定 — 预制体 Fill 节点命名为 Fill / FillBar / 血量条 均可。
    /// </summary>
    public void AutoBindFromHierarchy()
    {
        if (fillImage == null)
        {
            Transform fill = transform.Find("Fill")
                ?? transform.Find("FillBar")
                ?? transform.Find("血量条");

            if (fill != null)
                fillImage = fill.GetComponent<Image>();
        }

        if (fillRect == null && fillImage != null)
            fillRect = fillImage.rectTransform;
    }

    public void SetValues(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        bool healthDropped = lastCurrentHealth >= 0f && current < lastCurrentHealth - 0.01f;

        if (healthDropped)
            ApplyRatio(ratio, lastRatio);
        else
            SyncRatio(ratio);

        lastCurrentHealth = current;
    }

    /// <summary>同步显示但不触发扣血碎块（绑定、重建、加血、最大血量变化等）。</summary>
    public void SyncValues(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        SyncRatio(ratio);
        lastCurrentHealth = current;
    }

    public void SetRatio(float ratio)
    {
        ApplyRatio(Mathf.Clamp01(ratio), lastRatio);
    }

    private void SyncRatio(float ratio)
    {
        lastRatio = Mathf.Clamp01(ratio);
        EnsureLayoutReady();
        ApplyRatioVisual(lastRatio);
    }

    /// <summary>本次扣血对应的 UI 区域（与 Fill 同一坐标系，相对 Background 左缘）。</summary>
    public bool TryGetLostHealthBand(float oldRatio, float newRatio, out LostHealthBand band)
    {
        band = default;
        oldRatio = Mathf.Clamp01(oldRatio);
        newRatio = Mathf.Clamp01(newRatio);
        if (oldRatio <= newRatio + 0.0001f)
            return false;

        EnsureLayoutReady();
        if (maxFillWidth <= 0f)
            return false;

        float yCenter = (fillPadding.bottom - fillPadding.top) * 0.5f;
        float halfH = fillHeight > 0f ? fillHeight * 0.5f : 4f;
        band.yMin = yCenter - halfH;
        band.yMax = yCenter + halfH;

        if (depleteDirection == DepleteDirection.RightToLeft)
        {
            band.xMin = fillPadding.left + maxFillWidth * newRatio;
            band.xMax = fillPadding.left + maxFillWidth * oldRatio;
        }
        else
        {
            var parent = fillRect != null ? fillRect.parent as RectTransform : null;
            float parentWidth = GetParentWidth(parent);
            float fillRight = parentWidth - fillPadding.right;
            band.xMax = fillRight - maxFillWidth * newRatio;
            band.xMin = fillRight - maxFillWidth * oldRatio;
        }

        return band.xMax > band.xMin + 0.01f;
    }

    private void ApplyRatio(float ratio, float previousRatio)
    {
        if (ratio < previousRatio - 0.0001f)
            OnRatioDecreased?.Invoke(previousRatio, ratio);

        lastRatio = ratio;
        EnsureLayoutReady();
        ApplyRatioVisual(ratio);
    }

    private void ApplyRatioVisual(float ratio)
    {
        switch (resolvedMode)
        {
            case FillMode.FillAmount:
                if (fillImage != null)
                    fillImage.fillAmount = ratio;
                break;

            case FillMode.Width:
                if (fillRect != null)
                {
                    if (maxFillWidth <= 0f)
                        CacheMaxFillWidth();

                    float width = maxFillWidth > 0f ? maxFillWidth * ratio : 0f;
                    fillRect.sizeDelta = new Vector2(width, fillHeight > 0f ? fillHeight : fillRect.sizeDelta.y);
                }
                break;
        }
    }

    public struct LostHealthBand
    {
        public float xMin;
        public float xMax;
        public float yMin;
        public float yMax;
    }

    private void ResolveMode()
    {
        resolvedMode = fillMode;
        if (resolvedMode == FillMode.Auto)
        {
            resolvedMode = fillRect != null ? FillMode.Width : FillMode.FillAmount;
            if (resolvedMode == FillMode.FillAmount && depleteDirection == DepleteDirection.RightToLeft)
                resolvedMode = FillMode.Width;
        }

        if (resolvedMode == FillMode.Width && fillRect != null)
        {
            ApplyWidthLayout();
            CacheMaxFillWidth();
        }
        else if (resolvedMode == FillMode.FillAmount && fillImage != null)
        {
            SetupFillAmountLayout();
            SetupFillAmountImage();
        }
    }

    private void SetupFillAmountLayout()
    {
        if (fillRect == null)
            return;

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(fillPadding.left, fillPadding.bottom);
        fillRect.offsetMax = new Vector2(-fillPadding.right, -fillPadding.top);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
    }

    private void SetupFillAmountImage()
    {
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = lastRatio;
    }

    private void EnsureLayoutReady()
    {
        if (resolvedMode != FillMode.Width || fillRect == null)
            return;

        if (maxFillWidth > 0f)
            return;

        CacheMaxFillWidth();
        if (maxFillWidth > 0f)
            return;

        layoutApplied = false;
        ApplyWidthLayout();
        CacheMaxFillWidth();
    }

    private void ApplyWidthLayout()
    {
        if (fillRect == null || layoutApplied)
            return;

        var parent = fillRect.parent as RectTransform;
        float parentWidth = GetParentWidth(parent);
        float parentHeight = parent != null ? parent.rect.height : 22f;

        if (depleteDirection == DepleteDirection.RightToLeft)
        {
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(fillPadding.left, (fillPadding.bottom - fillPadding.top) * 0.5f);
        }
        else
        {
            fillRect.anchorMin = new Vector2(1f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.pivot = new Vector2(1f, 0.5f);
            fillRect.anchoredPosition = new Vector2(-fillPadding.right, (fillPadding.bottom - fillPadding.top) * 0.5f);
        }

        maxFillWidth = Mathf.Max(0f, parentWidth - fillPadding.Horizontal);
        fillHeight = Mathf.Max(0f, parentHeight - fillPadding.Vertical);
        if (maxFillWidth <= 0f && fillRect.sizeDelta.x > fillPadding.Horizontal)
            maxFillWidth = fillRect.sizeDelta.x - fillPadding.Horizontal;
        if (fillHeight <= 0f && fillRect.sizeDelta.y > fillPadding.Vertical)
            fillHeight = fillRect.sizeDelta.y - fillPadding.Vertical;

        fillRect.sizeDelta = new Vector2(Mathf.Max(0f, maxFillWidth * lastRatio), fillHeight);

        if (fillImage != null)
            fillImage.type = Image.Type.Simple;

        layoutApplied = true;
    }

    private void CacheMaxFillWidth()
    {
        if (fillRect == null)
            return;

        var parent = fillRect.parent as RectTransform;
        float width = GetParentWidth(parent);
        float height = parent != null ? parent.rect.height : 0f;

        if (width > 0f)
            maxFillWidth = Mathf.Max(0f, width - fillPadding.Horizontal);
        else if (fillRect.sizeDelta.x > 0f)
            maxFillWidth = Mathf.Max(0f, fillRect.sizeDelta.x);

        if (height > 0f)
            fillHeight = Mathf.Max(0f, height - fillPadding.Vertical);
        else if (fillRect.sizeDelta.y > 0f)
            fillHeight = fillRect.sizeDelta.y;
    }

    private static float GetParentWidth(RectTransform parent)
    {
        return parent != null ? parent.rect.width : 0f;
    }
}
