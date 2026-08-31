using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>角色技能音效播放器。</summary>
[RequireComponent(typeof(AbilitySystemComponent))]
public class AbilityAudioPlayer : MonoBehaviour
{
    [SerializeField] private AbilityVfxPlayer vfxPlayer;

    private readonly Dictionary<int, float> cooldownUntil = new Dictionary<int, float>();

    void Awake()
    {
        vfxPlayer ??= GetComponent<AbilityVfxPlayer>();
    }

    public void PlayImmediate(
        AbilityPresentationEntry presentation,
        AbilityActivationContext context,
        Transform caster)
    {
        PlayAudioTiming(AudioTiming.Immediate, presentation, context, caster);
    }

    public void PlayVfxCast(
        AbilityPresentationEntry presentation,
        AbilityActivationContext context,
        Transform caster)
    {
        PlayAudioTiming(AudioTiming.OnVfxCast, presentation, context, caster);
    }

    public void PlayTiming(
        VfxTiming timing,
        AbilityPresentationEntry presentation,
        AbilityActivationContext context,
        Transform caster)
    {
        PlayAudioTiming(AudioTimingUtility.FromVfxTiming(timing), presentation, context, caster);
    }

    private void PlayAudioTiming(
        AudioTiming timing,
        AbilityPresentationEntry presentation,
        AbilityActivationContext context,
        Transform caster)
    {
        if (presentation == null)
            return;

        var entries = presentation.GetEffectiveAudio();
        if (entries == null || entries.Count == 0)
            return;

        var manager = AudioManager.Instance;
        if (manager == null)
            manager = AudioManager.Ensure();

        if (manager == null)
            return;

        if (vfxPlayer == null)
            vfxPlayer = GetComponent<AbilityVfxPlayer>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || !entry.IsValid || entry.timing != timing)
                continue;

            if (!CanPlay(entry, i))
                continue;

            Vector3? position = ResolveWorldPosition(entry, context, caster);
            manager.PlaySFX(ResolveOneShotAudio(entry.audio), position);
            MarkPlayed(entry, i);
        }
    }

    private Vector3? ResolveWorldPosition(AudioSpawnEntry entry, AbilityActivationContext context, Transform caster)
    {
        if (entry.audio.spatialBlend <= 0.001f)
            return null;

        if (vfxPlayer != null
            && vfxPlayer.TryGetAnchorWorld(entry.anchor, context, out Vector3 position, out _))
            return position;

        return caster != null ? caster.position : transform.position;
    }

    private bool CanPlay(AudioSpawnEntry entry, int entryIndex)
    {
        if (entry.audio.cooldownSeconds <= 0f || entry.audio.clip == null)
            return true;

        int key = BuildCooldownKey(entry, entryIndex);
        return !cooldownUntil.TryGetValue(key, out float until) || Time.unscaledTime >= until;
    }

    private void MarkPlayed(AudioSpawnEntry entry, int entryIndex)
    {
        if (entry.audio.cooldownSeconds <= 0f || entry.audio.clip == null)
            return;

        cooldownUntil[BuildCooldownKey(entry, entryIndex)] =
            Time.unscaledTime + entry.audio.cooldownSeconds;
    }

    private static int BuildCooldownKey(AudioSpawnEntry entry, int entryIndex)
    {
        return HashCode.Combine(entry.audio.clip.GetInstanceID(), entryIndex);
    }

    private static AudioClipEntry ResolveOneShotAudio(AudioClipEntry entry)
    {
        if (entry == null || !entry.loop)
            return entry;

        return new AudioClipEntry
        {
            clip = entry.clip,
            volume = entry.volume,
            pitchMin = entry.pitchMin,
            pitchMax = entry.pitchMax,
            cooldownSeconds = entry.cooldownSeconds,
            spatialBlend = entry.spatialBlend,
            minDistance = entry.minDistance,
            maxDistance = entry.maxDistance,
            loop = false
        };
    }
}
