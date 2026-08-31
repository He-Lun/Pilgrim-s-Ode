using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>伤害飘字总控，订阅 CombatEventBus 生成并驱动飘字。</summary>
[DisallowMultipleComponent]
public class BattleDamagePopupSpawner : MonoBehaviour
{
    public static BattleDamagePopupSpawner Instance { get; private set; }

    private DamagePopupConfig config;
    private RectTransform canvasRect;
    private RectTransform layer;
    private Font font;
    private Camera cachedCamera;
    private bool subscribed;

    private readonly List<DamagePopupWidget> active = new List<DamagePopupWidget>();
    private readonly Stack<DamagePopupWidget> pool = new Stack<DamagePopupWidget>();
    private readonly Dictionary<AbilitySystemComponent, StackState> stacks =
        new Dictionary<AbilitySystemComponent, StackState>();

    private struct StackState
    {
        public float lastTime;
        public int steps;
    }

    public static BattleDamagePopupSpawner Ensure()
    {
        if (Instance != null)
        {
            // 重开战斗时总线可能被清过监听，这里补订阅。
            Instance.Subscribe();
            return Instance;
        }

        var existing = FindObjectOfType<BattleDamagePopupSpawner>();
        if (existing != null)
        {
            Instance = existing;
            existing.Subscribe();
            return existing;
        }

        var rootGo = new GameObject("BattleDamagePopupRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 压在血条 UI(10) 之上，飘字被挡住就失去意义了。
        canvas.sortingOrder = 50;

        var scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var spawner = rootGo.AddComponent<BattleDamagePopupSpawner>();
        DontDestroyOnLoad(rootGo);
        return spawner;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        config = DamagePopupConfig.LoadOrDefault();
        font = ResolveFont();
        canvasRect = GetComponent<RectTransform>();
        EnsureLayer();
    }

    void OnEnable() => Subscribe();

    void OnDisable()
    {
        Unsubscribe();
        ClearActive();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (active.Count == 0)
            return;

        var cam = ResolveCamera();
        float dt = Time.deltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var widget = active[i];
            if (widget == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (widget.Tick(dt, cam, canvasRect))
                continue;

            widget.Recycle();
            active.RemoveAt(i);
            pool.Push(widget);
        }
    }

    /// <summary>幂等订阅。</summary>
    private void Subscribe()
    {
        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
        subscribed = false;
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        // 只听 DamageTaken，不听 DamageDealt——同一次伤害两个事件都会发。
        switch (evt.type)
        {
            case CombatEventType.DamageTaken:
                Spawn(evt.target, evt.value, DamagePopupKind.Damage, evt.tag);
                break;

            case CombatEventType.HealApplied:
                Spawn(evt.target, evt.value, DamagePopupKind.Heal, default);
                break;

            case CombatEventType.HealthCostApplied:
                Spawn(evt.target, evt.value, DamagePopupKind.HealthCost, default);
                break;
        }
    }

    public void Spawn(AbilitySystemComponent target, float amount, DamagePopupKind kind, GameplayTag damageType)
    {
        if (target == null || amount <= 0f || config == null)
            return;

        int rounded = Mathf.Max(1, Mathf.RoundToInt(amount));
        var style = config.ResolveStyle(kind);
        var color = kind == DamagePopupKind.Damage
            ? config.ResolveDamageColor(damageType)
            : style.color;

        float maxHealth = target.Attributes != null ? target.Attributes.MaxHealth : 0f;
        int fontSize = config.ResolveFontSize(amount, maxHealth, style.fontScale);

        Vector3 world = ResolveWorldAnchor(target);
        Vector2 velocity = new Vector2(
            Random.Range(-config.horizontalSpread, config.horizontalSpread),
            config.riseSpeed);

        Vector2 startDrift = new Vector2(0f, config.screenOffsetY + ResolveStackOffset(target));

        var widget = Rent();
        widget.Play(
            style.prefix + rounded,
            color,
            style.outlineColor,
            fontSize,
            style.fontStyle,
            world,
            velocity,
            startDrift,
            config);

        active.Add(widget);
    }

    /// <summary>连击时逐条上移避免重叠。</summary>
    private float ResolveStackOffset(AbilitySystemComponent target)
    {
        float now = Time.unscaledTime;
        PruneStacks(now);

        int steps = 0;
        if (stacks.TryGetValue(target, out var state)
            && now - state.lastTime < config.stackWindowSeconds)
        {
            steps = Mathf.Min(state.steps + 1, Mathf.Max(0, config.maxStackSteps));
        }

        stacks[target] = new StackState { lastTime = now, steps = steps };
        return steps * config.stackOffsetY;
    }

    private void PruneStacks(float now)
    {
        if (stacks.Count < 16)
            return;

        var stale = new List<AbilitySystemComponent>();
        foreach (var pair in stacks)
        {
            if (pair.Key == null || now - pair.Value.lastTime > config.stackWindowSeconds * 4f)
                stale.Add(pair.Key);
        }

        for (int i = 0; i < stale.Count; i++)
            stacks.Remove(stale[i]);
    }

    private Vector3 ResolveWorldAnchor(AbilitySystemComponent target)
    {
        var attachPoints = target.GetComponentInChildren<AbilityVfxAttachPoints>(true);
        if (attachPoints != null
            && !string.IsNullOrEmpty(config.attachPointId)
            && attachPoints.TryGet(config.attachPointId, out var point)
            && point != null)
        {
            return point.position;
        }

        return target.transform.position + config.worldOffsetFallback;
    }

    private DamagePopupWidget Rent()
    {
        while (pool.Count > 0)
        {
            var pooled = pool.Pop();
            if (pooled != null)
                return pooled;
        }

        return DamagePopupWidget.Create(layer, font);
    }

    private void ClearActive()
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] == null)
                continue;

            active[i].Recycle();
            pool.Push(active[i]);
        }

        active.Clear();
    }

    private void EnsureLayer()
    {
        if (layer != null)
            return;

        var layerGo = new GameObject("PopupLayer", typeof(RectTransform));
        layerGo.transform.SetParent(transform, false);

        layer = layerGo.GetComponent<RectTransform>();
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.pivot = new Vector2(0.5f, 0.5f);
    }

    private Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        cachedCamera = Camera.main;
        if (cachedCamera == null)
            cachedCamera = FindObjectOfType<Camera>();

        return cachedCamera;
    }

    private static Font ResolveFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return font;
    }
}
