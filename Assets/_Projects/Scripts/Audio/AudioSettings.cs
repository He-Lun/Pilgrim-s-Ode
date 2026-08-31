using UnityEngine;

/// <summary>全局音频配置。</summary>
[CreateAssetMenu(menuName = "Pilgrim/Audio Settings", fileName = "AudioSettings")]
public class AudioSettings : ScriptableObject
{
    public const string ResourcesPath = "AudioSettings";

    private const string PrefMaster = "Audio.MasterVolume";
    private const string PrefBgm = "Audio.BgmVolume";
    private const string PrefSfx = "Audio.SfxVolume";

    [Header("默认音量（0~1）")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("运行时")]
    [Tooltip("SFX AudioSource 池大小")]
    public int sfxPoolSize = 10;

    [Tooltip("BGM 默认淡入淡出时长（秒）")]
    public float defaultBgmFadeSeconds = 0.5f;

    [Header("数据")]
    public AudioPresentationCatalog catalog;

    static AudioSettings cached;

    public static AudioSettings LoadOrDefault()
    {
        if (cached != null)
            return cached;

        cached = Resources.Load<AudioSettings>(ResourcesPath);
        if (cached == null)
            cached = CreateInstance<AudioSettings>();

        return cached;
    }

    public static void InvalidateCache() => cached = null;

    public float ResolveMasterVolume()
    {
        return PlayerPrefs.HasKey(PrefMaster)
            ? PlayerPrefs.GetFloat(PrefMaster, masterVolume)
            : masterVolume;
    }

    public float ResolveBgmVolume()
    {
        return PlayerPrefs.HasKey(PrefBgm)
            ? PlayerPrefs.GetFloat(PrefBgm, bgmVolume)
            : bgmVolume;
    }

    public float ResolveSfxVolume()
    {
        return PlayerPrefs.HasKey(PrefSfx)
            ? PlayerPrefs.GetFloat(PrefSfx, sfxVolume)
            : sfxVolume;
    }

    public static void SaveMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(PrefMaster, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static void SaveBgmVolume(float value)
    {
        PlayerPrefs.SetFloat(PrefBgm, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static void SaveSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(PrefSfx, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
