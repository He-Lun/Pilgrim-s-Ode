using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 可选战场边界 — 超出 Collider 范围的移动目标无效。
/// </summary>
[RequireComponent(typeof(Collider))]
public class BattleBounds : MonoBehaviour
{
    public static BattleBounds Instance { get; private set; }

    private Collider boundsCollider;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        boundsCollider = GetComponent<Collider>();
        if (boundsCollider != null)
            boundsCollider.isTrigger = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (boundsCollider == null) return true;
        return boundsCollider.bounds.Contains(worldPoint);
    }

    public Bounds WorldBounds =>
        boundsCollider != null ? boundsCollider.bounds : new Bounds(Vector3.zero, Vector3.one * 1000f);
}
