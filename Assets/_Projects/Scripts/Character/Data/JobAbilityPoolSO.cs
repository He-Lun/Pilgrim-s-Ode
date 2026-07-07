using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业技能池 — 配置某职业可选的全部 GameplayAbility
/// </summary>
[CreateAssetMenu(fileName = "NewJobAbilityPool", menuName = "巡礼之诗/职业技能池")]
public class JobAbilityPoolSO : ScriptableObject
{
    [Header("========== 职业信息 ==========")]
    [SerializeField] public GameplayTag job;
    [SerializeField] public string displayName;

    [Header("========== 技能池 ==========")]
    [Tooltip("该职业可供构筑选择的全部技能")]
    [SerializeField] public List<GameplayAbility> abilities = new List<GameplayAbility>();
}
