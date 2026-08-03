using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 战斗战术相机 — Cinemachine 轨道 + 焦点 Pivot；暴露 ActiveCamera 供战斗射线使用。
/// </summary>
[DefaultExecutionOrder(-200)]
public class BattleCameraController : MonoBehaviour
{
    public static BattleCameraController Instance { get; private set; }

    [SerializeField] private BattleCameraSettings settings = new BattleCameraSettings();
    [SerializeField] private Transform pivot;
    [SerializeField] private Camera brainCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    private CinemachineOrbitalTransposer orbital;
    private CinemachineBrain brain;
    private float orbitDistance;
    private float targetOrbitDistance;
    private float zoomVelocity;
    private float orbitPitch;
    private Vector3 shakeFollowOffset;
    private Coroutine hitShakeRoutine;

    public BattleCameraSettings Settings => settings;
    public Transform Pivot => pivot;
    public Camera ActiveCamera => brainCamera;
    public CinemachineVirtualCamera VirtualCamera => virtualCamera;
    public CinemachineOrbitalTransposer Orbital => orbital;
    public float OrbitPitch
    {
        get => orbitPitch;
        set
        {
            orbitPitch = Mathf.Clamp(value, settings.minPitch, settings.maxPitch);
            ApplyFollowOffset();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureRig();
        ApplyDefaultOrbit();
    }

    void LateUpdate()
    {
        UpdateSmoothedZoom();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>将 Pivot 移到目标世界点并 Clamp 边界。</summary>
    public void SetPivotWorldPosition(Vector3 worldPoint)
    {
        if (pivot == null) return;

        pivot.position = ClampPivotPosition(worldPoint);
    }

    /// <summary>参战角色包围盒中心；无角色时保持当前位置。</summary>
    public void FocusOnActors(IReadOnlyList<AbilitySystemComponent> actors)
    {
        if (actors == null || actors.Count == 0 || pivot == null) return;

        var bounds = new Bounds(actors[0].transform.position, Vector3.zero);
        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i] == null) continue;
            bounds.Encapsulate(actors[i].transform.position);
        }

        var center = bounds.center;
        center.y = SampleGroundY(center);
        SetPivotWorldPosition(center);
    }

    public void ClampPivotToBounds()
    {
        if (pivot == null) return;
        pivot.position = ClampPivotPosition(pivot.position);
    }

    public Vector3 ClampPivotPosition(Vector3 worldPoint)
    {
        if (!settings.clampToBattleBounds || BattleBounds.Instance == null)
            return worldPoint;

        Bounds bounds = BattleBounds.Instance.WorldBounds;
        float margin = settings.boundsMargin;
        worldPoint.x = Mathf.Clamp(worldPoint.x, bounds.min.x + margin, bounds.max.x - margin);
        worldPoint.z = Mathf.Clamp(worldPoint.z, bounds.min.z + margin, bounds.max.z - margin);
        worldPoint.y = SampleGroundY(worldPoint);
        return worldPoint;
    }

    public void ApplyOrbitDistance(float distance)
    {
        orbitDistance = Mathf.Clamp(distance, settings.minDistance, settings.maxDistance);
        targetOrbitDistance = orbitDistance;
        zoomVelocity = 0f;
        ApplyFollowOffset();
    }

    /// <summary>滚轮输入：更新目标距离，由 LateUpdate 平滑逼近。</summary>
    public void AddZoomScroll(float scroll)
    {
        if (Mathf.Abs(scroll) < 0.0001f)
            return;

        if (settings.zoomFactor > 1f)
        {
            targetOrbitDistance *= Mathf.Pow(
                settings.zoomFactor,
                -scroll * settings.zoomScrollScale);
        }
        else
        {
            targetOrbitDistance -= scroll * settings.zoomSpeed * settings.zoomScrollScale;
        }

        targetOrbitDistance = Mathf.Clamp(
            targetOrbitDistance,
            settings.minDistance,
            settings.maxDistance);
    }

    public float GetOrbitDistance() => orbitDistance;

    /// <summary>受击抖动 — 在 FollowOffset 上叠加 Perlin 噪声，战术相机下稳定可见。</summary>
    public void ApplyHitShake(float intensity, float duration)
    {
        if (intensity <= 0f || duration <= 0f)
            return;

        if (hitShakeRoutine != null)
            StopCoroutine(hitShakeRoutine);

        hitShakeRoutine = StartCoroutine(HitShakeRoutine(intensity, duration));
    }

    public void ApplyDefaultOrbit()
    {
        if (orbital == null) return;

        orbitPitch = Mathf.Clamp(settings.defaultPitch, settings.minPitch, settings.maxPitch);
        orbitDistance = settings.defaultDistance;
        targetOrbitDistance = settings.defaultDistance;
        zoomVelocity = 0f;
        orbital.m_XAxis.Value = settings.defaultYaw;
        ApplyFollowOffset();
    }

    private void UpdateSmoothedZoom()
    {
        if (orbital == null)
            return;

        if (settings.zoomSmoothTime <= 0f)
        {
            if (Mathf.Approximately(orbitDistance, targetOrbitDistance))
                return;

            orbitDistance = targetOrbitDistance;
            ApplyFollowOffset();
            return;
        }

        float next = Mathf.SmoothDamp(
            orbitDistance,
            targetOrbitDistance,
            ref zoomVelocity,
            settings.zoomSmoothTime);

        if (Mathf.Approximately(next, orbitDistance))
            return;

        orbitDistance = next;
        ApplyFollowOffset();
    }

    private void ApplyFollowOffset()
    {
        if (orbital == null) return;

        float pitchDeg = Mathf.Lerp(15f, 75f, orbitPitch);
        float rad = pitchDeg * Mathf.Deg2Rad;
        orbital.m_FollowOffset = new Vector3(
            0f,
            orbitDistance * Mathf.Sin(rad),
            -orbitDistance * Mathf.Cos(rad)) + shakeFollowOffset;
    }

    private IEnumerator HitShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;
        float seed = Random.value * 100f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / duration);
            float t = elapsed * 32f + seed;

            shakeFollowOffset = new Vector3(
                (Mathf.PerlinNoise(t, 1.2f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(2.3f, t) - 0.5f) * 1.4f,
                (Mathf.PerlinNoise(t, t + 1f) - 0.5f) * 2f) * intensity * damper;

            ApplyFollowOffset();
            yield return null;
        }

        shakeFollowOffset = Vector3.zero;
        ApplyFollowOffset();
        hitShakeRoutine = null;
    }

    private void EnsureRig()
    {
        if (pivot == null)
        {
            var pivotGo = transform.Find("Pivot");
            if (pivotGo == null)
            {
                pivotGo = new GameObject("Pivot").transform;
                pivotGo.SetParent(transform, false);
            }

            pivot = pivotGo;
        }

        if (brainCamera == null)
        {
            brainCamera = Camera.main;
            if (brainCamera == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                brainCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
        }

        if (brain == null)
        {
            brain = brainCamera.GetComponent<CinemachineBrain>();
            if (brain == null)
                brain = brainCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        if (virtualCamera == null)
        {
            var existing = transform.Find("CM_BattleCamera");
            if (existing != null)
                virtualCamera = existing.GetComponent<CinemachineVirtualCamera>();

            if (virtualCamera == null)
            {
                var vcamGo = new GameObject("CM_BattleCamera");
                vcamGo.transform.SetParent(transform, false);
                virtualCamera = vcamGo.AddComponent<CinemachineVirtualCamera>();
            }
        }

        virtualCamera.Follow = pivot;
        virtualCamera.LookAt = pivot;
        virtualCamera.Priority = 10;

        orbital = virtualCamera.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital == null)
            orbital = virtualCamera.AddCinemachineComponent<CinemachineOrbitalTransposer>();

        var composer = virtualCamera.GetCinemachineComponent<CinemachineComposer>();
        if (composer == null)
            composer = virtualCamera.AddCinemachineComponent<CinemachineComposer>();

        composer.m_HorizontalDamping = 0f;
        composer.m_VerticalDamping = 0f;

        orbital.m_XAxis.m_MaxSpeed = 0f;
        orbital.m_XAxis.m_InputAxisName = string.Empty;
        orbital.m_RecenterToTargetHeading.m_enabled = false;
        orbital.m_XDamping = 0f;
        orbital.m_YDamping = 0f;
        orbital.m_ZDamping = 0f;
    }

    private static float SampleGroundY(Vector3 worldPoint)
    {
        return BattleTargeting.ProjectToGround(worldPoint).y;
    }
}
