using System.Collections;
using UnityEngine;

/// <summary>
/// 世界屏障特效生命周期 — 持续循环 Appearin，到期 Trigger BarrierOut 后销毁。
/// 挂于带 Animator 的屏障 prefab（也可在生成时自动添加）。
/// </summary>
public class BarrierWorldVfx : MonoBehaviour
{
    [SerializeField] private string expireTrigger = "BarrierOut";
    [SerializeField] private string expireStateName = "WaterBarrierAppearout";
    [SerializeField] private float expireWaitTimeout = 4f;

    private Animator animator;
    private bool expiring;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.receiveShadows = false;
    }

    public void BeginExpire()
    {
        if (expiring) return;
        expiring = true;

        if (animator == null)
        {
            Destroy(gameObject);
            return;
        }

        animator.SetTrigger(expireTrigger);
        StartCoroutine(WaitExpireAndDestroy());
    }

    private IEnumerator WaitExpireAndDestroy()
    {
        int stateHash = Animator.StringToHash(expireStateName);
        float deadline = Time.time + expireWaitTimeout;

        while (Time.time < deadline)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash)
                break;
            yield return null;
        }

        var info = animator.GetCurrentAnimatorStateInfo(0);
        float wait = info.length * Mathf.Max(0f, 1f - info.normalizedTime);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        Destroy(gameObject);
    }
}
