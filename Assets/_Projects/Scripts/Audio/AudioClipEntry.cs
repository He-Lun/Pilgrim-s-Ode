using System;
using UnityEngine;

/// <summary>单条音效定义。</summary>
[Serializable]
public class AudioClipEntry
{
    [Tooltip("音频资源")]
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 1f;

    public float pitchMin = 1f;
    public float pitchMax = 1f;

    [Tooltip("同一 clip 的最小重播间隔（秒）")]
    public float cooldownSeconds = 0f;

    [Range(0f, 1f)]
    [Tooltip("0 = 2D UI 音，1 = 3D 世界音")]
    public float spatialBlend = 0f;

    public float minDistance = 1f;
    public float maxDistance = 50f;

    [Tooltip("BGM 条目应勾选")]
    public bool loop = false;

    public bool IsValid => clip != null;

    public float ResolvePitch()
    {
        float min = pitchMin <= 0f ? 1f : pitchMin;
        float max = pitchMax <= 0f ? min : pitchMax;
        if (max < min)
            max = min;

        if (Mathf.Approximately(min, max))
            return min;

        return UnityEngine.Random.Range(min, max);
    }

    /// <summary>世界循环音：spatialBlend=0 时按 3D 处理。</summary>
    public float ResolveWorldSpatialBlend()
    {
        return spatialBlend > 0.001f ? spatialBlend : 1f;
    }

    public float ResolveMinDistance() => minDistance > 0f ? minDistance : 1f;

    public float ResolveMaxDistance() => maxDistance > 0f ? maxDistance : 50f;

    public float ResolveSfxVolume()
    {
        float vol = volume;
        if (AudioManager.Instance != null)
            vol *= AudioManager.Instance.GetMasterVolume() * AudioManager.Instance.GetSfxVolume();
        return vol;
    }

    public void ApplyWorldLoopTo(AudioSource source)
    {
        if (source == null || !IsValid)
            return;

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = ResolveWorldSpatialBlend();
        source.minDistance = ResolveMinDistance();
        source.maxDistance = ResolveMaxDistance();
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.pitch = Mathf.Max(0.01f, ResolvePitch());
        source.volume = ResolveSfxVolume();
    }
}
