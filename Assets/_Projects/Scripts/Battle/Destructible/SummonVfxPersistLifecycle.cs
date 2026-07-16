using System.Collections;
using UnityEngine;

/// <summary>
/// 召唤特效收尾 — In/开场粒子结束后，把 Persist 晶石解绑留场（继续 Stay），销毁其余特效壳。
/// </summary>
public sealed class SummonVfxPersistLifecycle : MonoBehaviour
{
    private Transform persistRoot;
    private float introDurationSeconds;
    private DestructiblePropAnimator propAnimator;
    private bool finished;

    public void Begin(Transform persist, float introDuration, DestructiblePropAnimator animator = null)
    {
        persistRoot = persist;
        introDurationSeconds = Mathf.Max(0f, introDuration);
        propAnimator = animator;
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        PrepareEphemeralParticles(gameObject, persistRoot);

        if (introDurationSeconds > 0f)
        {
            yield return new WaitForSeconds(introDurationSeconds);
        }
        else if (propAnimator != null)
        {
            // 等 Animator 从 In 进入 Stay
            float timeout = Time.time + Mathf.Max(0.5f, propAnimator.GetInDuration() + 0.5f);
            while (Time.time < timeout && !propAnimator.HasReachedStay())
                yield return null;
        }
        else
        {
            float wait = EstimateEphemeralParticleDuration(gameObject, persistRoot);
            if (wait > 0f)
                yield return new WaitForSeconds(wait);
        }

        Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        if (persistRoot == null)
        {
            Destroy(gameObject);
            return;
        }

        // 晶石即根：整棵留下播 Stay
        if (persistRoot == transform)
            return;

        persistRoot.SetParent(null, worldPositionStays: true);
        Destroy(gameObject);
    }

    private static void PrepareEphemeralParticles(GameObject root, Transform persist)
    {
        if (root == null) return;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (IsInsidePersist(ps.transform, persist)) continue;

            var main = ps.main;
            if (main.loop)
            {
                var module = main;
                module.loop = false;
                ps.Play(true);
            }
        }
    }

    private static float EstimateEphemeralParticleDuration(GameObject root, Transform persist)
    {
        float maxEnd = 0.5f;
        if (root == null) return maxEnd;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (IsInsidePersist(ps.transform, persist)) continue;

            var main = ps.main;
            float startLife = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                ? main.startLifetime.constant
                : main.startLifetime.constantMax;
            maxEnd = Mathf.Max(maxEnd, main.duration + startLife);
        }

        return maxEnd;
    }

    private static bool IsInsidePersist(Transform t, Transform persist)
    {
        if (persist == null || t == null) return false;
        return t == persist || t.IsChildOf(persist);
    }
}
