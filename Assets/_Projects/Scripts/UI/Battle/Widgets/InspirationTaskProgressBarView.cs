using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 激励任务进度条 — 4-Empty / 4-LimeGreen / 4-Orange。
/// </summary>
public class InspirationTaskProgressBarView : MonoBehaviour
{
    [SerializeField] private RectTransform trackRect;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Sprite inProgressFill;
    [SerializeField] private Sprite completeFill;
    [SerializeField] private HealthBarFillPadding fillPadding = HealthBarFillPadding.OverlayStyle4Default;

    public void SetProgress(float ratio)
    {
        if (fillRect == null || fillImage == null || trackRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        ratio = Mathf.Clamp01(ratio);
        fillImage.sprite = ratio >= 1f ? completeFill : inProgressFill;

        float innerWidth = trackRect.rect.width - fillPadding.Horizontal;
        float innerHeight = trackRect.rect.height - fillPadding.Vertical;
        fillRect.sizeDelta = new Vector2(innerWidth * ratio, innerHeight);
    }
}
