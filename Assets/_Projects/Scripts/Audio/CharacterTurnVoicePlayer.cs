using System.Collections.Generic;
using UnityEngine;

/// <summary>角色回合开始语音。</summary>
[RequireComponent(typeof(AbilitySystemComponent))]
public class CharacterTurnVoicePlayer : MonoBehaviour
{
    [SerializeField] private AbilitySystemComponent asc;

    private bool subscribed;
    private readonly List<AudioClipEntry> voiceBuffer = new List<AudioClipEntry>();

    void Awake()
    {
        asc ??= GetComponent<AbilitySystemComponent>();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Update()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan += HandleTurnBegan;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan -= HandleTurnBegan;
        subscribed = false;
    }

    private void HandleTurnBegan(AbilitySystemComponent actor)
    {
        if (actor == null || actor != asc)
            return;

        var data = asc.CharacterData;
        if (data == null || !data.TryGetRandomTurnStartVoice(out var entry))
            return;

        var manager = AudioManager.Instance ?? AudioManager.Ensure();
        manager?.PlaySFX(entry, ResolveWorldPosition(entry));
    }

    private Vector3? ResolveWorldPosition(AudioClipEntry entry)
    {
        if (entry.spatialBlend <= 0.001f)
            return null;

        return transform.position;
    }
}
