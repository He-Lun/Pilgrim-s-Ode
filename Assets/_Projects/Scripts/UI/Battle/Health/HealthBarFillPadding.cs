using UnityEngine;

/// <summary>
/// 血条填充区相对底图（Background）的内边距，避免填充 sprite 超出空条边框。
/// </summary>
[System.Serializable]
public struct HealthBarFillPadding
{
    public int left;
    public int right;
    public int top;
    public int bottom;

    public static HealthBarFillPadding WorldStyle1Default => new HealthBarFillPadding
    {
        left = 54,
        right = 54,
        top = 14,
        bottom = 14
    };

    public static HealthBarFillPadding OverlayStyle4Default => new HealthBarFillPadding
    {
        left = 10,
        right = 10,
        top = 5,
        bottom = 5
    };

    public float Horizontal => left + right;
    public float Vertical => top + bottom;
}
