using UnityEngine;

/// <summary>
/// 特效生成时机 — 对应角色动画事件。
/// </summary>
public enum VfxTiming
{
    /// <summary>起手/施法时（动画事件 OnAbilityCastVfx）。</summary>
    OnCast,
    /// <summary>命中判定时（动画事件 OnAbilityHit）。</summary>
    OnHit,
    /// <summary>技能收招时（动画事件 OnAbilityComplete）。</summary>
    OnComplete
}

/// <summary>
/// 特效挂接方式。
/// </summary>
public enum VfxAttachMode
{
    /// <summary>一次性：在锚点世界位置生成，不跟随。</summary>
    Detached,
    /// <summary>跟随：作为锚点 Transform 的子物体生成，随其移动（如剑光跟随挥剑）。</summary>
    Parented
}

/// <summary>
/// 特效朝向来源。最终世界旋转 = Mode 基朝向 × prefab.localRotation（保留资源自带偏移）。
/// </summary>
public enum VfxRotationMode
{
    /// <summary>仅用预制体本地旋转（基朝向 = Identity）。</summary>
    PrefabDefault,
    /// <summary>对齐锚点 Transform，再乘 prefab.localRotation。</summary>
    MatchAnchor,
    /// <summary>朝向主目标（水平面），再乘 prefab.localRotation。</summary>
    FaceTarget,
    /// <summary>朝向施法方向，再乘 prefab.localRotation。</summary>
    FaceAimDirection
}

/// <summary>
/// 一条技能特效定义 — 数据驱动，编辑器可配。
/// 描述：在什么时机、什么位置、以什么朝向、是否跟随，生成哪个特效。
/// </summary>
[System.Serializable]
public class VfxSpawnEntry
{
    [Tooltip("特效预制体")]
    public GameObject prefab;

    [Tooltip("生成时机（对应动画事件）")]
    public VfxTiming timing = VfxTiming.OnCast;

    [Tooltip("生成位置")]
    public VfxAnchor anchor;

    [Tooltip("挂接方式：一次性 / 跟随锚点")]
    public VfxAttachMode attachMode = VfxAttachMode.Detached;

    [Tooltip("朝向来源")]
    public VfxRotationMode rotationMode = VfxRotationMode.PrefabDefault;

    [Tooltip("存活上限（秒）。<=0 表示仅按粒子时长自动销毁")]
    public float autoDestroySeconds = 3f;

    public bool IsValid => prefab != null;
}
