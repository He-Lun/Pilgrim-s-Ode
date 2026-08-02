using System;
using UnityEngine;

/// <summary>
/// BG3 风格战术相机可调参数。
/// </summary>
[Serializable]
public class BattleCameraSettings
{
    [Header("平移（右键拖拽）")]
    [Tooltip("屏幕像素位移 → 世界位移的倍率")]
    public float panSpeed = 0.02f;

    [Header("旋转（中键拖拽）")]
    public float rotateSpeed = 0.25f;
    [Tooltip("Orbital Y 轴最小值（俯仰，Cinemachine 0~1 映射）")]
    [Range(0f, 1f)] public float minPitch = 0.25f;
    [Range(0f, 1f)] public float maxPitch = 0.65f;

    [Header("缩放（滚轮）")]
    [Tooltip("线性缩放：每滚轮格改变的距离（米）")]
    public float zoomSpeed = 1.2f;
    public float minDistance = 8f;
    public float maxDistance = 40f;
    [Tooltip("对数缩放底数；<=1 时使用线性 zoomSpeed")]
    public float zoomFactor = 1.06f;
    [Tooltip("滚轮输入倍率（越小每格变化越细）")]
    public float zoomScrollScale = 4f;
    [Tooltip("缩放平滑时间（秒），0 为即时")]
    public float zoomSmoothTime = 0.15f;

    [Header("初始状态")]
    public float defaultDistance = 18f;
    [Range(0f, 1f)] public float defaultPitch = 0.45f;
    public float defaultYaw = 0f;

    [Header("边界")]
    public bool clampToBattleBounds = true;
    [Tooltip("Pivot 距 BattleBounds 边缘的内缩（米）")]
    public float boundsMargin = 1f;
}
