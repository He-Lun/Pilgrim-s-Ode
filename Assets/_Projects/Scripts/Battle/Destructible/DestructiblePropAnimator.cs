using System.Collections;
using UnityEngine;

/// <summary>
/// 可摧毁召唤物 Animator 驱动 — In（默认入场）→ Stay（循环驻留）→ Out（摧毁 Trigger）。
/// 默认对接 MoltenPillarController；状态名/Trigger 可在 Inspector 改。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class DestructiblePropAnimator : MonoBehaviour
{
    [SerializeField] private string inStateName = "MoltenPillarAppearIn";
    [SerializeField] private string stayStateName = "MoltenPillarAppearStay";
    [SerializeField] private string outStateName = "MoltenPillarAppearOut";
    [SerializeField] private string outTrigger = "Out";
    [SerializeField] private float outWaitTimeout = 4f;

    private Animator animator;
    private bool playingOut;

    public Animator Animator => animator != null ? animator : (animator = GetComponent<Animator>());
    public string InStateName => inStateName;
    public string StayStateName => stayStateName;
    public string OutStateName => outStateName;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>估算 In 片段时长（用于召唤特效壳收尾）。</summary>
    public float GetInDuration()
    {
        if (Animator == null || Animator.runtimeAnimatorController == null) return 0f;

        foreach (var clip in Animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == inStateName)
                return clip.length;
        }

        // 状态名与 clip 名不一致时，用常见 In clip
        foreach (var clip in Animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf("In", System.StringComparison.OrdinalIgnoreCase) >= 0
                && clip.name.IndexOf("Out", System.StringComparison.OrdinalIgnoreCase) < 0)
                return clip.length;
        }

        return 1f;
    }

    /// <summary>是否已进入 Stay（或已离开 In）。</summary>
    public bool HasReachedStay()
    {
        if (Animator == null) return true;

        var info = Animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(stayStateName)) return true;
        if (info.IsName(inStateName)) return info.normalizedTime >= 0.95f;
        return !info.IsName(inStateName);
    }

    /// <summary>触发 Out，播完后销毁目标物体。</summary>
    public void PlayOutThenDestroy(GameObject destroyTarget)
    {
        if (playingOut) return;
        playingOut = true;
        StartCoroutine(PlayOutThenDestroyRoutine(destroyTarget != null ? destroyTarget : gameObject));
    }

    private IEnumerator PlayOutThenDestroyRoutine(GameObject destroyTarget)
    {
        if (Animator != null && !string.IsNullOrEmpty(outTrigger))
        {
            Animator.ResetTrigger(outTrigger);
            Animator.SetTrigger(outTrigger);

            int outHash = Animator.StringToHash(outStateName);
            float deadline = Time.time + outWaitTimeout;

            while (Time.time < deadline)
            {
                if (Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == outHash)
                    break;
                yield return null;
            }

            var info = Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == outHash)
            {
                float wait = info.length * Mathf.Max(0f, 1f - info.normalizedTime);
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);
            }
            else
            {
                // Trigger 未接上时兜底：按 Out clip 长度等一会
                float fallback = 1f;
                if (Animator.runtimeAnimatorController != null)
                {
                    foreach (var clip in Animator.runtimeAnimatorController.animationClips)
                    {
                        if (clip != null && clip.name == outStateName)
                        {
                            fallback = clip.length;
                            break;
                        }
                    }
                }

                yield return new WaitForSeconds(fallback);
            }
        }

        if (destroyTarget != null)
            Destroy(destroyTarget);
    }
}
