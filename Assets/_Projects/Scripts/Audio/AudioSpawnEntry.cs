using System;
using UnityEngine;

/// <summary>技能音效播放时机。</summary>
public enum AudioTiming
{
    OnCast = 0,
    OnHit = 1,
    OnComplete = 2,
    OnHit2 = 3,
    OnHit3 = 4,
    OnCast2 = 5,
    OnCast3 = 6,
    OnHit4 = 7,
    Immediate = 100,
    OnVfxCast = 101
}

public static class AudioTimingUtility
{
    public static AudioTiming FromVfxTiming(VfxTiming timing) => (AudioTiming)(int)timing;
}

/// <summary>技能音效定义。</summary>
[Serializable]
public class AudioSpawnEntry
{
    public AudioClipEntry audio = new AudioClipEntry();

    [Tooltip("Immediate=技能开始；OnVfxCast=OnAbilityCastVfx；OnCast/OnHit 等=对应动画事件")]
    public AudioTiming timing = AudioTiming.OnCast;

    [Tooltip("3D 音效播放位置；spatialBlend=0 时忽略")]
    public VfxAnchor anchor = new VfxAnchor { type = VfxAnchorType.CasterRoot };

    public bool IsValid => audio != null && audio.IsValid;
}
