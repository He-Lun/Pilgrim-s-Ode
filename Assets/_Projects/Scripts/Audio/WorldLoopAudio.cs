using System.Collections;
using UnityEngine;

/// <summary>世界物体循环音效。</summary>
[DisallowMultipleComponent]
public sealed class WorldLoopAudio : MonoBehaviour
{
    private AudioSource source;
    private AudioClipEntry activeEntry;

    public void Play(AudioClipEntry entry)
    {
        StopInternal();

        if (entry == null || !entry.IsValid)
            return;

        AudioManager.Ensure();
        activeEntry = entry;

        source = gameObject.AddComponent<AudioSource>();
        entry.ApplyWorldLoopTo(source);
        source.Play();

        if (!source.isPlaying)
            StartCoroutine(RetryPlayNextFrame());
    }

    public void Stop() => StopInternal();

    void OnDestroy() => StopInternal();

    private IEnumerator RetryPlayNextFrame()
    {
        yield return null;

        if (source == null || activeEntry == null || !activeEntry.IsValid)
            yield break;

        activeEntry.ApplyWorldLoopTo(source);
        source.Play();
    }

    private void StopInternal()
    {
        activeEntry = null;

        if (source == null)
            return;

        source.Stop();
        Destroy(source);
        source = null;
    }
}
