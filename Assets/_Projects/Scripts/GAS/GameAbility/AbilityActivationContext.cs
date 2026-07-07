using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能释放上下文 — 由 UI / HandCardManager 在出牌时构造，经 Facade 传入 ASC。
/// 不同技能使用不同字段；未使用的字段保持默认值即可。
/// </summary>
public struct AbilityActivationContext
{
    /// <summary>玩家手动选择的目标（火球、单疗等）。突进类技能可为空，由 Effect 在移动后解析。</summary>
    public List<AbilitySystemComponent> explicitTargets;

    /// <summary>网格方向，如 (0,1)=向北。突进、方向性技能使用。</summary>
    public Vector2Int direction;

    /// <summary>玩家点击的目标格子（范围技、位移落点等）。</summary>
    public Vector2Int targetCell;

    /// <summary>移动/突进距离（格数）。0 表示由技能或 Effect 配置决定。</summary>
    public int moveDistance;

    // ---------- 工厂方法（卡牌/UI 使用） ----------

    /// <summary>无额外参数，适用于 TargetScope.Self。</summary>
    public static AbilityActivationContext Self()
    {
        return new AbilityActivationContext
        {
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    /// <summary>单个目标，适用于 SingleEnemy / SingleAlly。</summary>
    public static AbilityActivationContext SingleTarget(AbilitySystemComponent target)
    {
        return new AbilityActivationContext
        {
            explicitTargets = target != null
                ? new List<AbilitySystemComponent> { target }
                : new List<AbilitySystemComponent>()
        };
    }

    /// <summary>多个目标，适用于 AllEnemies 等（由 UI 预先选好列表）。</summary>
    public static AbilityActivationContext FromTargets(List<AbilitySystemComponent> targets)
    {
        return new AbilityActivationContext
        {
            explicitTargets = targets ?? new List<AbilitySystemComponent>()
        };
    }

    /// <summary>方向性技能（突进等），目标在 Effect 执行阶段解析。</summary>
    public static AbilityActivationContext WithDirection(Vector2Int dir, int distance = 0)
    {
        return new AbilityActivationContext
        {
            direction = dir,
            moveDistance = distance,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    /// <summary>点选格子，适用于 Area 等。</summary>
    public static AbilityActivationContext WithTargetCell(Vector2Int cell)
    {
        return new AbilityActivationContext
        {
            targetCell = cell,
            explicitTargets = new List<AbilitySystemComponent>()
        };
    }

    // ---------- 查询 ----------

    public bool HasExplicitTargets =>
        explicitTargets != null && explicitTargets.Count > 0;

    public bool HasDirection => direction != Vector2Int.zero;

    public bool HasTargetCell => targetCell != Vector2Int.zero;

    /// <summary>获取显式目标列表，永不为 null。</summary>
    public List<AbilitySystemComponent> GetExplicitTargets()
    {
        return explicitTargets ?? new List<AbilitySystemComponent>();
    }
}
