using System.Collections.Generic;
using UnityEngine;

/// <summary>战斗音效，订阅 CombatEventBus。</summary>
[DisallowMultipleComponent]
public class CombatAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private AudioPresentationCatalog catalogOverride;

    private bool subscribedCombat;
    private bool subscribedTurn;
    private readonly Dictionary<int, float> cooldownUntil = new Dictionary<int, float>();

    void Awake()
    {
        audioManager ??= GetComponent<AudioManager>();
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Update()
    {
        Subscribe();
    }

    public void Subscribe()
    {
        if (!subscribedCombat)
        {
            CombatEventBus.Instance.OnEvent += HandleCombatEvent;
            subscribedCombat = true;
        }

        if (!subscribedTurn && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnBattleEnded += HandleBattleEnded;
            subscribedTurn = true;
        }
    }

    public void Unsubscribe()
    {
        if (subscribedCombat)
        {
            CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
            subscribedCombat = false;
        }

        if (subscribedTurn && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnBattleEnded -= HandleBattleEnded;
            subscribedTurn = false;
        }
    }

    private AudioPresentationCatalog ResolveCatalog()
    {
        if (catalogOverride != null)
            return catalogOverride;

        if (audioManager == null)
            audioManager = GetComponent<AudioManager>();

        return audioManager != null ? audioManager.Catalog : null;
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        var catalog = ResolveCatalog();
        if (catalog == null || audioManager == null)
            return;

        if (!ShouldPlayEvent(evt.type))
            return;

        if (!catalog.TryGetCombatSfx(evt, out var entry))
            return;

        if (!CanPlay(entry))
            return;

        Vector3? position = ResolveWorldPosition(evt);
        audioManager.PlaySFX(entry, position);
        MarkPlayed(entry);
    }

    private void HandleBattleEnded(int winnerTeamId)
    {
        if (audioManager == null)
            audioManager = GetComponent<AudioManager>();

        audioManager?.PlayBGM("Victory");
    }

    private static bool ShouldPlayEvent(CombatEventType type)
    {
        switch (type)
        {
            case CombatEventType.HealApplied:
            case CombatEventType.HealthCostApplied:
            case CombatEventType.CharacterKilled:
            case CombatEventType.BuffApplied:
            case CombatEventType.TurnEnded:
            case CombatEventType.AbilityUsed:
                return true;
            default:
                return false;
        }
    }

    private static Vector3? ResolveWorldPosition(CombatEvent evt)
    {
        if (evt.target != null)
            return evt.target.transform.position;

        if (evt.instigator != null)
            return evt.instigator.transform.position;

        return null;
    }

    private bool CanPlay(AudioClipEntry entry)
    {
        if (entry.cooldownSeconds <= 0f || entry.clip == null)
            return true;

        int key = entry.clip.GetInstanceID();
        if (cooldownUntil.TryGetValue(key, out float until) && Time.unscaledTime < until)
            return false;

        return true;
    }

    private void MarkPlayed(AudioClipEntry entry)
    {
        if (entry.cooldownSeconds <= 0f || entry.clip == null)
            return;

        cooldownUntil[entry.clip.GetInstanceID()] = Time.unscaledTime + entry.cooldownSeconds;
    }
}
