using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 战斗空间全局配置 — 米制移动、NavMesh 采样参数。
/// </summary>
public class BattleSpaceSettings : MonoBehaviour
{
    public static BattleSpaceSettings Instance { get; private set; }

    [Header("移动（米制）")]
    [Tooltip("Speed 属性 1 点对应多少米移动力（BG3 约 1.5m/格）")]
    [SerializeField] private float metersPerSpeedPoint = 1.5f;

    [Header("NavMesh")]
    [SerializeField] private float navMeshSampleRadius = 2f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
    [Tooltip("洪水填充采样格距（米），越小越精细、计算量越大")]
    [SerializeField] private float floodFillCellSize = 1f;

    [Header("场景配置提示")]
    [TextArea(2, 4)]
    [SerializeField] private string setupHint =
        "1) 地面 Bake NavMesh\n2) 墙/柱 Navigation Static\n3) 角色不要进 NavMesh 烘焙\n4) 可选：添加 BattleBounds 限定战场";

    public float MetersPerSpeedPoint => metersPerSpeedPoint;
    public float NavMeshSampleRadius => navMeshSampleRadius;
    public float FloodFillCellSize => floodFillCellSize;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static float GetMetersPerSpeedPoint()
    {
        return Instance != null ? Instance.metersPerSpeedPoint : 1.5f;
    }

    public static float GetNavMeshSampleRadius()
    {
        return Instance != null ? Instance.navMeshSampleRadius : 2f;
    }

    public static int GetNavMeshAreaMask()
    {
        return Instance != null ? Instance.navMeshAreaMask : NavMesh.AllAreas;
    }

    public static float GetFloodFillCellSize()
    {
        return Instance != null ? Instance.floodFillCellSize : 1f;
    }
}
