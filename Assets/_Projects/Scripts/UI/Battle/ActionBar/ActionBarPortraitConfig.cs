using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行动条头像映射 — 在 Inspector 中为每个 CharacterDataSO 拖入对应 Sprite。
/// </summary>
[CreateAssetMenu(fileName = "ActionBarPortraitConfig", menuName = "巡礼之诗/UI/行动条头像配置")]
public class ActionBarPortraitConfig : ScriptableObject
{
    [Serializable]
    public struct Mapping
    {
        public CharacterDataSO character;
        public Sprite portrait;
    }

    [SerializeField] private List<Mapping> mappings = new List<Mapping>();

    public bool TryGetPortrait(CharacterDataSO data, out Sprite portrait)
    {
        portrait = null;
        if (data == null)
            return false;

        for (int i = 0; i < mappings.Count; i++)
        {
            var entry = mappings[i];
            if (entry.character != data || entry.portrait == null)
                continue;

            portrait = entry.portrait;
            return true;
        }

        return false;
    }
}
