using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff / 领域类别 → 持续特效。角色 buff 挂身上；Zone.* 等领域挂世界坐标。
/// </summary>
[CreateAssetMenu(fileName = "BuffPresentationCatalog", menuName = "Pilgrim/Buff Presentation Catalog")]
public class BuffPresentationCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public GameplayTag category;
        public VfxSpawnEntry vfx;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public bool TryGet(GameplayTag category, out VfxSpawnEntry vfx)
    {
        vfx = null;
        if (string.IsNullOrEmpty(category.TagName)) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].category.Matches(category)) continue;
            vfx = entries[i].vfx;
            return vfx != null && vfx.IsValid;
        }

        return false;
    }

    /// <summary>
    /// 收集同一类别的全部有效特效（持续 + 获得提醒等）。
    /// autoDestroySeconds &gt; 0 的条目由 AbilityVfxPlayer 作为一次性提醒播放。
    /// </summary>
    public int CollectForCategory(GameplayTag category, List<VfxSpawnEntry> results)
    {
        results?.Clear();
        if (results == null || string.IsNullOrEmpty(category.TagName)) return 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var vfx = entries[i].vfx;
            if (!entries[i].category.Matches(category) || vfx == null || !vfx.IsValid) continue;
            results.Add(vfx);
        }

        return results.Count;
    }
}
