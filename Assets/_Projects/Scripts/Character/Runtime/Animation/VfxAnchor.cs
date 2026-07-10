using UnityEngine;

/// <summary>
/// 特效锚点类型 — 统一描述"特效在哪里生成"。
/// </summary>
public enum VfxAnchorType
{
    /// <summary>施法者根节点。</summary>
    CasterRoot,
    /// <summary>施法者脚底（根节点投影到地面 y）。</summary>
    CasterGround,
    /// <summary>主目标根节点。</summary>
    TargetRoot,
    /// <summary>主目标胸口（根节点 + 高度偏移）。</summary>
    TargetChest,
    /// <summary>玩家鼠标点击的世界坐标（context.targetWorldPoint）。</summary>
    MouseWorldPoint,
    /// <summary>角色 Prefab 上的命名挂点（AbilityVfxAttachPoints，如剑尖/胸口/脚）。</summary>
    NamedPoint
}

/// <summary>
/// 特效生成位置 — 语义锚点或命名挂点二选一，附带局部偏移。
/// </summary>
[System.Serializable]
public struct VfxAnchor
{
    [Tooltip("锚点类型")]
    public VfxAnchorType type;

    [Tooltip("NamedPoint 时使用：AbilityVfxAttachPoints 中的挂点 id，如 WeaponTip / Chest / Feet")]
    public string attachPointId;

    [Tooltip("在解析出的锚点位置基础上的额外偏移；Parented 时为局部偏移，否则为世界偏移")]
    public Vector3 localOffset;
}
