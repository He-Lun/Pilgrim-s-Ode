using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阵营资源管理器（管理行动点、、、、公共资源）
/// </summary>
public class TeamResourceManager : MonoBehaviour
{
    [Header("========== 行动点配置 ==========")]
    [SerializeField] private int maxActionPoints = 10;
    [SerializeField] private int currentActionPoints = 5;

    // ---------- 事件 ----------
    public System.Action<int> OnActionPointsChanged; //当前值
    public System.Action<int> OnMaxActionPointsChanged;

    // ---------- 属性访问 ----------
    public int CurrentActionPoints
    {
        get => currentActionPoints;
        private set
        {
            int old = currentActionPoints;
            currentActionPoints = Mathf.Clamp(value, 0, maxActionPoints);
            if (old != currentActionPoints)
                OnActionPointsChanged?.Invoke(currentActionPoints);
        }
    }

    public int MaxActionPoints => maxActionPoints;

    // ---------- 初始化 ----------
    public void Initialize(int maxAP, int startAP)
    {
        maxActionPoints = maxAP;
        currentActionPoints = Mathf.Clamp(startAP, 0, maxAP);
        OnActionPointsChanged?.Invoke(currentActionPoints);
    }

    // ---------- 行动点操作 ----------
    /// <summary>
    /// 消耗行动点（返回是否成功）
    /// </summary>
    public bool TryConsumeActionPoints(int amount)
    {
        if (currentActionPoints < amount) return false;
        CurrentActionPoints -= amount;
        return true;
    }

    /// <summary>
    /// 增加行动点
    /// </summary>
    public void AddActionPoints(int amount)
    {
        CurrentActionPoints += amount;
    }

    /// <summary>
    /// 回合开始时恢复行动点
    /// </summary>
    public void OnTurnStart(int recoverAmount)
    {
        AddActionPoints(recoverAmount);
        Debug.Log($"[TeamResource] 回合开始，恢复 {recoverAmount} 行动点，当前: {CurrentActionPoints}");
    }

    // ---------- 调试 ----------
    public string GetDebugInfo() => $"AP: {CurrentActionPoints}/{MaxActionPoints}";
}