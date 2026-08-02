using Cinemachine;
using UnityEngine;

/// <summary>
/// 订阅 DamageTaken，驱动战术相机受击抖动。
/// </summary>
[RequireComponent(typeof(BattleCameraController))]
public class BattleCameraImpulsePlayer : MonoBehaviour
{
    [SerializeField] private BattleCameraController controller;
    [SerializeField] private BattleCameraImpulseSettings settings = new BattleCameraImpulseSettings();
    [SerializeField] private CinemachineImpulseSource impulseSource;

    private float lastShakeTime = -999f;
    private bool listenerReady;

    void Awake()
    {
        if (controller == null)
            controller = GetComponent<BattleCameraController>();
    }

    void OnEnable()
    {
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
    }

    void OnDisable()
    {
        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
    }

    void Start()
    {
        EnsureImpulseSetup();
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (!settings.enabled || evt.type != CombatEventType.DamageTaken)
            return;

        if (evt.target == null || evt.value < settings.minDamageToShake)
            return;

        if (Time.unscaledTime - lastShakeTime < settings.cooldownSeconds)
            return;

        if (controller == null)
            return;

        EnsureImpulseSetup();

        lastShakeTime = Time.unscaledTime;

        float intensity = EvaluateIntensity(evt.value);
        controller.ApplyHitShake(intensity, settings.shakeDuration);

        if (settings.useCinemachineImpulse && impulseSource != null)
            FireCinemachineImpulse(evt, intensity);
    }

    private float EvaluateIntensity(float damage)
    {
        float t = Mathf.InverseLerp(
            settings.minDamageToShake,
            settings.maxDamageForFullForce,
            damage);
        return Mathf.Lerp(settings.minIntensity, settings.maxIntensity, t);
    }

    private void FireCinemachineImpulse(CombatEvent evt, float intensity)
    {
        Vector3 velocity = Vector3.down * intensity * settings.impulseGain;

        if (evt.instigator != null && evt.target != null)
        {
            Vector3 dir = evt.target.transform.position - evt.instigator.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                velocity = dir.normalized * intensity * settings.impulseGain;
        }

        Vector3 at = evt.target != null
            ? evt.target.transform.position + Vector3.up
            : transform.position;

        impulseSource.GenerateImpulseAt(at, velocity);
    }

    private void EnsureImpulseSetup()
    {
        if (controller == null)
            return;

        if (!listenerReady)
            listenerReady = EnsureImpulseListener();

        if (!settings.useCinemachineImpulse)
            return;

        EnsureImpulseSource();
        ConfigureImpulseDefinition();
    }

    private void EnsureImpulseSource()
    {
        if (impulseSource != null)
            return;

        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
    }

    private bool EnsureImpulseListener()
    {
        var vcam = controller.VirtualCamera;
        if (vcam == null)
            return false;

        var listener = vcam.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
            listener = vcam.gameObject.AddComponent<CinemachineImpulseListener>();

        listener.m_Gain = settings.impulseGain;
        listener.m_UseCameraSpace = true;
        return true;
    }

    private void ConfigureImpulseDefinition()
    {
        if (impulseSource == null)
            return;

        var def = impulseSource.m_ImpulseDefinition;
        def.m_ImpulseDuration = settings.shakeDuration;
        def.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        def.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.m_DissipationDistance = 1000f;
    }
}
