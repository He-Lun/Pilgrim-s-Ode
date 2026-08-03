using System;
using UnityEngine;

/// <summary>
/// 受击相机抖动参数。
/// </summary>
[Serializable]
public class BattleCameraImpulseSettings
{
    [Header("开关")]
    public bool enabled = true;

    [Header("强度（按最终伤害缩放）")]
    [Tooltip("低于此伤害不抖动")]
    public float minDamageToShake = 1f;
    [Tooltip("达到此伤害时使用 maxIntensity")]
    public float maxDamageForFullForce = 30f;
    [Tooltip("最小抖动幅度（FollowOffset 米）")]
    public float minIntensity = 0.35f;
    [Tooltip("最大抖动幅度（FollowOffset 米）")]
    public float maxIntensity = 1.1f;

    [Header("波形")]
    public float shakeDuration = 0.24f;

    [Header("限流")]
    [Tooltip("两次抖动最短间隔（秒）")]
    public float cooldownSeconds = 0.06f;

    [Header("Cinemachine Impulse（可选叠加）")]
    public bool useCinemachineImpulse = true;
    public float impulseGain = 2.5f;
}
