using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>战斗事件/标签/BGM 音效映射。</summary>
[CreateAssetMenu(menuName = "Pilgrim/Audio Presentation Catalog", fileName = "AudioPresentationCatalog")]
public class AudioPresentationCatalog : ScriptableObject
{
    [Serializable]
    public struct CombatEventEntry
    {
        public CombatEventType type;
        [Tooltip("留空则匹配该事件类型的默认条目")]
        public GameplayTag tagFilter;
        public AudioClipEntry sfx;
    }

    [Serializable]
    public struct TagEntry
    {
        public GameplayTag tag;
        public AudioClipEntry sfx;
    }

    [Serializable]
    public struct BgmEntry
    {
        public string key;
        public AudioClipEntry bgm;
    }

    [SerializeField] private List<CombatEventEntry> combatEvents = new List<CombatEventEntry>();
    [SerializeField] private List<TagEntry> tagEntries = new List<TagEntry>();
    [SerializeField] private List<BgmEntry> bgmEntries = new List<BgmEntry>();

    public bool TryGetCombatSfx(CombatEvent evt, out AudioClipEntry entry)
    {
        entry = null;
        if (combatEvents == null || combatEvents.Count == 0)
            return false;

        // 1. 事件 + tag 精确匹配
        if (!string.IsNullOrEmpty(evt.tag.TagName))
        {
            for (int i = 0; i < combatEvents.Count; i++)
            {
                var row = combatEvents[i];
                if (row.type != evt.type) continue;
                if (string.IsNullOrEmpty(row.tagFilter.TagName)) continue;
                if (!row.tagFilter.Matches(evt.tag)) continue;
                if (row.sfx == null || !row.sfx.IsValid) continue;
                entry = row.sfx;
                return true;
            }
        }

        // 2. 事件类型默认（tagFilter 为空）
        for (int i = 0; i < combatEvents.Count; i++)
        {
            var row = combatEvents[i];
            if (row.type != evt.type) continue;
            if (!string.IsNullOrEmpty(row.tagFilter.TagName)) continue;
            if (row.sfx == null || !row.sfx.IsValid) continue;
            entry = row.sfx;
            return true;
        }

        // 3. 按事件 tag 查 Tag 表
        if (!string.IsNullOrEmpty(evt.tag.TagName) && TryGetTagSfx(evt.tag, out entry))
            return true;

        return false;
    }

    public bool TryGetTagSfx(GameplayTag tag, out AudioClipEntry entry)
    {
        entry = null;
        if (tagEntries == null || string.IsNullOrEmpty(tag.TagName))
            return false;

        for (int i = 0; i < tagEntries.Count; i++)
        {
            if (!tagEntries[i].tag.Matches(tag)) continue;
            if (tagEntries[i].sfx == null || !tagEntries[i].sfx.IsValid) continue;
            entry = tagEntries[i].sfx;
            return true;
        }

        return false;
    }

    public bool TryGetBgm(string key, out AudioClipEntry entry)
    {
        entry = null;
        if (bgmEntries == null || string.IsNullOrEmpty(key))
            return false;

        for (int i = 0; i < bgmEntries.Count; i++)
        {
            if (!string.Equals(bgmEntries[i].key, key, StringComparison.Ordinal)) continue;
            if (bgmEntries[i].bgm == null || !bgmEntries[i].bgm.IsValid) continue;
            entry = bgmEntries[i].bgm;
            return true;
        }

        return false;
    }
}
