using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>全局音频管理器。</summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSettings settings;

    private AudioSource bgmSource;
    private readonly List<AudioSource> sfxPool = new List<AudioSource>();
    private int sfxPoolIndex;
    private Coroutine bgmFadeRoutine;

    private float masterVolume = 1f;
    private float bgmVolume = 0.7f;
    private float sfxVolume = 1f;
    private float bgmEntryVolume = 1f;

    public AudioSettings Settings => settings;
    public AudioPresentationCatalog Catalog => settings != null ? settings.catalog : null;

    public static AudioManager Ensure()
    {
        if (Instance != null)
            return Instance;

        var existing = FindObjectOfType<AudioManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        var rootGo = new GameObject("AudioManagerRoot");
        var manager = rootGo.AddComponent<AudioManager>();
        rootGo.AddComponent<CombatAudioPlayer>();
        DontDestroyOnLoad(rootGo);
        return manager;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        settings ??= AudioSettings.LoadOrDefault();
        LoadPersistedVolumes();
        EnsureAudioSources();
        ApplyAllVolumes();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayBGM(string key, float fadeSeconds = -1f)
    {
        if (string.IsNullOrEmpty(key))
            return;

        var catalog = Catalog;
        if (catalog == null || !catalog.TryGetBgm(key, out var entry))
        {
            Debug.LogWarning($"[AudioManager] BGM key not found: {key}");
            return;
        }

        PlayBGM(entry, fadeSeconds);
    }

    public void PlayBGM(AudioClipEntry entry, float fadeSeconds = -1f)
    {
        if (entry == null || !entry.IsValid)
            return;

        float fade = fadeSeconds >= 0f ? fadeSeconds : settings.defaultBgmFadeSeconds;
        StartBgmFade(entry, fade);
    }

    public void PlayBGM(AudioClip clip, float fadeSeconds = -1f, bool loop = true, float volume = 1f)
    {
        if (clip == null)
            return;

        var entry = new AudioClipEntry
        {
            clip = clip,
            volume = volume,
            loop = loop
        };

        float fade = fadeSeconds >= 0f ? fadeSeconds : settings.defaultBgmFadeSeconds;
        StartBgmFade(entry, fade);
    }

    public void StopBGM(float fadeSeconds = -1f)
    {
        if (bgmSource == null || !bgmSource.isPlaying)
            return;

        float fade = fadeSeconds >= 0f ? fadeSeconds : settings.defaultBgmFadeSeconds;
        if (bgmFadeRoutine != null)
            StopCoroutine(bgmFadeRoutine);

        if (fade <= 0f)
        {
            bgmSource.Stop();
            return;
        }

        bgmFadeRoutine = StartCoroutine(FadeBgmRoutine(bgmSource.volume, 0f, fade, stopAtEnd: true));
    }

    public void PlaySFX(AudioClipEntry entry, Vector3? worldPosition = null)
    {
        if (entry == null || !entry.IsValid)
            return;

        var source = AcquireSfxSource();
        if (source == null)
            return;

        ConfigureSfxSource(source, entry, worldPosition);
        source.Play();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        AudioSettings.SaveMasterVolume(masterVolume);
        ApplyAllVolumes();
    }

    public void SetBgmVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        AudioSettings.SaveBgmVolume(bgmVolume);
        ApplyAllVolumes();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        AudioSettings.SaveSfxVolume(sfxVolume);
        ApplyAllVolumes();
    }

    public float GetMasterVolume() => masterVolume;
    public float GetBgmVolume() => bgmVolume;
    public float GetSfxVolume() => sfxVolume;

    private void LoadPersistedVolumes()
    {
        masterVolume = settings.ResolveMasterVolume();
        bgmVolume = settings.ResolveBgmVolume();
        sfxVolume = settings.ResolveSfxVolume();
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            var bgmGo = new GameObject("BGM");
            bgmGo.transform.SetParent(transform, false);
            bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
        }

        int targetSize = Mathf.Max(1, settings.sfxPoolSize);
        while (sfxPool.Count < targetSize)
        {
            var sfxGo = new GameObject($"SFX_{sfxPool.Count}");
            sfxGo.transform.SetParent(transform, false);
            var source = sfxGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            sfxPool.Add(source);
        }
    }

    private void ApplyAllVolumes()
    {
        if (bgmSource != null)
            bgmSource.volume = masterVolume * bgmVolume * bgmEntryVolume;
    }

    private AudioSource AcquireSfxSource()
    {
        if (sfxPool.Count == 0)
            return null;

        for (int i = 0; i < sfxPool.Count; i++)
        {
            int index = (sfxPoolIndex + i) % sfxPool.Count;
            var source = sfxPool[index];
            if (source != null && !source.isPlaying)
            {
                sfxPoolIndex = (index + 1) % sfxPool.Count;
                return source;
            }
        }

        var fallback = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Count;
        return fallback;
    }

    private void ConfigureSfxSource(AudioSource source, AudioClipEntry entry, Vector3? worldPosition)
    {
        source.clip = entry.clip;
        source.loop = entry.loop;
        source.pitch = Mathf.Max(0.01f, entry.ResolvePitch());
        source.volume = masterVolume * sfxVolume * entry.volume;
        source.spatialBlend = entry.spatialBlend;

        if (entry.spatialBlend > 0.001f && worldPosition.HasValue)
        {
            source.transform.position = worldPosition.Value;
            source.minDistance = entry.minDistance;
            source.maxDistance = entry.maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        else
        {
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
        }
    }

    private void StartBgmFade(AudioClipEntry entry, float fadeSeconds)
    {
        if (bgmFadeRoutine != null)
            StopCoroutine(bgmFadeRoutine);

        bgmFadeRoutine = StartCoroutine(SwitchBgmRoutine(entry, fadeSeconds));
    }

    private IEnumerator SwitchBgmRoutine(AudioClipEntry entry, float fadeSeconds)
    {
        if (bgmSource == null)
            yield break;

        bool sameClip = bgmSource.clip == entry.clip && bgmSource.isPlaying;
        if (sameClip)
            yield break;

        bgmEntryVolume = entry.volume;
        float targetVolume = masterVolume * bgmVolume * bgmEntryVolume;

        if (bgmSource.isPlaying && fadeSeconds > 0f)
        {
            yield return FadeBgmRoutine(bgmSource.volume, 0f, fadeSeconds * 0.5f, stopAtEnd: false);
            bgmSource.Stop();
        }
        else if (bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }

        bgmSource.clip = entry.clip;
        bgmSource.loop = entry.loop;
        bgmSource.pitch = entry.ResolvePitch();
        bgmSource.volume = fadeSeconds > 0f ? 0f : targetVolume;
        bgmSource.Play();

        if (fadeSeconds > 0f)
            yield return FadeBgmRoutine(bgmSource.volume, targetVolume, fadeSeconds * 0.5f, stopAtEnd: false);

        bgmFadeRoutine = null;
    }

    private IEnumerator FadeBgmRoutine(float from, float to, float duration, bool stopAtEnd)
    {
        if (bgmSource == null)
            yield break;

        if (duration <= 0f)
        {
            bgmSource.volume = to;
            if (stopAtEnd)
                bgmSource.Stop();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bgmSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        bgmSource.volume = to;
        if (stopAtEnd)
            bgmSource.Stop();
    }
}
