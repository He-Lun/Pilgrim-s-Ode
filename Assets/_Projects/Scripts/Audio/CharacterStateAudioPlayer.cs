using System.Collections.Generic;
using UnityEngine;

/// <summary>角色状态音效播放器。</summary>
[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(AbilitySystemComponent))]
public class CharacterStateAudioPlayer : MonoBehaviour
{
    [SerializeField] private CharacterMotor motor;
    [SerializeField] private AbilitySystemComponent asc;

    private readonly Dictionary<CharacterStateType, AudioClipEntry> stateAudio =
        new Dictionary<CharacterStateType, AudioClipEntry>();
    private readonly Dictionary<int, float> cooldownUntil = new Dictionary<int, float>();
    private bool subscribed;

    void Awake()
    {
        motor ??= GetComponent<CharacterMotor>();
        asc ??= GetComponent<AbilitySystemComponent>();
        RebuildStateAudioMap();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Start()
    {
        RebuildStateAudioMap();
        TrySubscribe();
    }

    public void RebuildStateAudioMap()
    {
        stateAudio.Clear();

        var data = asc != null ? asc.CharacterData : null;
        if (data == null || data.stateAudio == null)
            return;

        for (int i = 0; i < data.stateAudio.Count; i++)
        {
            var row = data.stateAudio[i];
            if (row.sfx == null || !row.sfx.IsValid)
                continue;

            stateAudio[row.state] = row.sfx;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed || motor == null || motor.StateMachine == null)
            return;

        motor.StateMachine.StateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || motor == null || motor.StateMachine == null)
            return;

        motor.StateMachine.StateChanged -= HandleStateChanged;
        subscribed = false;
    }

    private void HandleStateChanged(CharacterStateType current, CharacterStateType previous)
    {
        if (current == previous)
            return;

        if (!stateAudio.TryGetValue(current, out var entry) || entry == null || !entry.IsValid)
            return;

        if (!CanPlay(entry))
            return;

        var manager = AudioManager.Instance ?? AudioManager.Ensure();
        if (manager == null)
            return;

        Vector3? position = entry.spatialBlend > 0.001f ? transform.position : null;
        manager.PlaySFX(entry, position);
        MarkPlayed(entry);
    }

    private bool CanPlay(AudioClipEntry entry)
    {
        if (entry.cooldownSeconds <= 0f || entry.clip == null)
            return true;

        int key = entry.clip.GetInstanceID();
        return !cooldownUntil.TryGetValue(key, out float until) || Time.unscaledTime >= until;
    }

    private void MarkPlayed(AudioClipEntry entry)
    {
        if (entry.cooldownSeconds <= 0f || entry.clip == null)
            return;

        cooldownUntil[entry.clip.GetInstanceID()] = Time.unscaledTime + entry.cooldownSeconds;
    }
}
