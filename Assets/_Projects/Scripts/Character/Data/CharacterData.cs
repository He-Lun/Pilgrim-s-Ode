using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "巡礼之诗/角色数据")]
public class CharacterDataSO : ScriptableObject
{
    [Header("========== 基本信息 ==========")]
        [Header("名字")]
        [SerializeField] public new string name;
        [Header("角色介绍")]
        [SerializeField] public string description="这是一个角色";
        [Header("职业")]
        [SerializeField] public GameplayTag job;
        [Header("出身王国")]
        [SerializeField] public GameplayTag kingdom;

    [Header("========== 基础属性 ==========")]
        [Header("基础生命值")]
        [SerializeField] public float baseHealth = 100f;
        [Header("基础攻击力")]
        [SerializeField] public float baseAttack = 10f;
        [Header("基础防御力")]
        [SerializeField] public float baseDefense = 5f;
        [Header("基础敏捷值（决定行动频率）")]
        [SerializeField] public float baseAgility = 10f;
        [Header("速度（每回合移动力）")]
        [SerializeField] public float baseSpeed = 10f;
    
    [Header("========== 天赋技能 ==========")]
        [Header("天赋技能效果")]
        [SerializeField] public List<GameplayAbility> innateAbilities;

    [Header("========== 激励系统 ==========")]
        [Header("激励技能效果")]
        [SerializeField] public GameplayAbility inspirationAbility;
        [Header("激励任务")]
        [SerializeField] public InspirationTaskSO inspirationTask;
}
