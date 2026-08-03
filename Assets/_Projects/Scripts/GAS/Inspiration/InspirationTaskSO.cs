using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 激励任务配置 — 每个角色绑定一个
/// </summary>
[CreateAssetMenu(fileName = "NewInspirationTask", menuName = "巡礼之诗/GAS/激励任务")]
public class InspirationTaskSO : ScriptableObject
{
    [Header("========== 基础信息 ==========")]
    public string taskName;
    [TextArea(2, 4)] public string description;

    [Header("========== 任务目标 ==========")]
    [SerializeReference, SubclassSelector] public List<InspirationObjective> objectives = new List<InspirationObjective>();

    [Tooltip("true=全部目标达成, false=任一目标达成")]
    public bool requireAllObjectives = true;

    [Tooltip("是否可在单场战斗中重复完成")]
    public bool repeatable = true;

    [Header("========== 完成奖励 ==========")]
    public int actionPointReward = 3;
    [Tooltip("行动插队优先级提升，1=100%")]
    public float actionPriorityBoost = 1f;
}
