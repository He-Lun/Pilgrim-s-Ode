using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色身上的命名 VFX 挂点 — 在 Prefab 上配置，技能通过 id 引用。
/// </summary>
public class AbilityVfxAttachPoints : MonoBehaviour
{
    [Serializable]
    public struct NamedPoint
    {
        [Tooltip("挂点 id，如 WeaponTip / Shield / Chest / Ground")]
        public string id;
        public Transform transform;
    }

    [SerializeField] private List<NamedPoint> points = new List<NamedPoint>();

    public bool TryGet(string id, out Transform point)
    {
        point = null;
        if (string.IsNullOrEmpty(id) || points == null) return false;

        foreach (var entry in points)
        {
            if (entry.transform == null || string.IsNullOrEmpty(entry.id)) continue;
            if (entry.id != id) continue;

            point = entry.transform;
            return true;
        }

        return false;
    }
}
