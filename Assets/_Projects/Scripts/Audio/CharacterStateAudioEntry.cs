using System;
using UnityEngine;

/// <summary>角色状态音效条目。</summary>
[Serializable]
public struct CharacterStateAudioEntry
{
    public CharacterStateType state;
    public AudioClipEntry sfx;
}
