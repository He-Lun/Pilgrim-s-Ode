using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>战斗入口，联机由 Server 开战，Client 注册表现 roster。</summary>
[DefaultExecutionOrder(-90)]
public class NetworkBattleBootstrap : MonoBehaviour
{
    public const string BattleSceneName = "DEMO Ruins 01";

    public static NetworkBattleBootstrap Instance { get; private set; }

    public static bool RequestOfflineStartOnLoad { get; set; }

    [Header("参战角色（留空则自动收集）")]
    [SerializeField] private List<AbilitySystemComponent> actors = new List<AbilitySystemComponent>();

    [Header("开战配置")]
    [SerializeField] private int firstTeamId = 0;
    [SerializeField] private int firstPlayerAP = 4;
    [SerializeField] private int secondPlayerAP = 5;
    [Tooltip("勾选：Host 单独也能开战。取消：必须等 Client 连上。")]
    [SerializeField] private bool allowSoloHostTest = false;
    [Tooltip("场景含 NetworkManager 时不会自动单机开战。")]
    [SerializeField] private bool autoStartOfflineOnPlay = false;
    [SerializeField] private float offlineStartDelay = 0.5f;

    [Header("输入")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.Space;

    [Header("调试")]
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private bool snapActorsToNavMeshOnStart = true;
    [SerializeField] private bool autoSplitTeamsWhenSingleSide = true;
    [SerializeField] private BuffPresentationCatalog buffPresentationCatalog;

    private bool battleStarted;
    private bool simulationStarted;
    private bool networkBattleSynced;
    private bool clientPrepared;
    private Coroutine serverStartRoutine;
    private MonoBehaviour serverStartRoutineHost;
    private Coroutine serverRefreshRoutine;
    private MonoBehaviour serverRefreshRoutineHost;
    private Coroutine clientPrepareRoutine;
    private MonoBehaviour clientPrepareRoutineHost;
    private float lastWaitingLogTime;
    private float lastTeamRequestTime;

    public static NetworkBattleBootstrap FindInstance()
    {
        if (Instance != null)
            return Instance;
        return FindObjectOfType<NetworkBattleBootstrap>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
            return;
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeSimulationEvents();
    }

    void Start()
    {
        if (RequestOfflineStartOnLoad)
        {
            RequestOfflineStartOnLoad = false;
            Invoke(nameof(TryBeginOfflineBattle), offlineStartDelay);
            return;
        }

        if (ShouldScheduleOfflineStart())
            Invoke(nameof(TryBeginOfflineBattle), offlineStartDelay);
    }

    void Update()
    {
        if (BattleNetworkGate.IsNetworkBattleActive)
            CancelOfflineStart();

        if (!clientPrepared && NetworkClient.active && !NetworkServer.active)
            EnsureClientPresentationReady();

        if (NetworkClient.active && !NetworkServer.active && BattleNetworkGate.LocalTeamId < 0)
        {
            if (Time.unscaledTime - lastTeamRequestTime > 1f)
            {
                lastTeamRequestTime = Time.unscaledTime;
                NetworkBattleController.Instance?.RequestLocalTeamAssignment();
            }
        }

        if (!simulationStarted && NetworkServer.active)
        {
            if (!CanStartServerBattle())
                LogWaitingForClient();
            else
                TryBeginServerBattle();
        }
        else if (simulationStarted && NetworkServer.active && !networkBattleSynced)
        {
            TryEnsureNetworkBattleSynced();
        }

        if (simulationStarted && Input.GetKeyDown(endTurnKey))
            NetworkBattleController.RequestEndTurnFromInput();
    }

    public void NotifyNetworkSessionChanged()
    {
        CancelOfflineStart();

        if (NetworkClient.active && !NetworkServer.active)
        {
            EnsureClientPresentationReady();
            return;
        }

        if (!NetworkServer.active)
            return;

        if (serverStartRoutine != null)
            StopManagedCoroutine(ref serverStartRoutine, ref serverStartRoutineHost);

        serverStartRoutine = StartManagedCoroutine(TryBeginServerBattleWhenReady(), ref serverStartRoutineHost);
    }

    private IEnumerator TryBeginServerBattleWhenReady()
    {
        float startedAt = Time.unscaledTime;

        while (NetworkServer.active && !simulationStarted)
        {
            if (CanStartServerBattle() && HasBattleRosterReady())
            {
                TryBeginServerBattle();
                if (simulationStarted)
                    break;
            }

            if (verboseLogging && Time.unscaledTime - startedAt > 8f && (!CanStartServerBattle() || !HasBattleRosterReady()))
            {
                Debug.Log($"[NetworkBattleBootstrap] 仍在等待开战条件… {DescribeConnections()}, roster={BattleRosterSetup.CollectActors(actors).Count}");
                startedAt = Time.unscaledTime;
            }

            yield return null;
        }

        serverStartRoutine = null;
        serverStartRoutineHost = null;
    }

    [ContextMenu("Begin Offline Battle")]
    public void TryBeginOfflineBattle()
    {
        if (BattleNetworkGate.IsNetworkBattleActive)
            return;

        if (battleStarted)
            return;

        if (!BeginBattleCore(syncNetworkState: false))
            return;

        Debug.Log($"[NetworkBattleBootstrap] 单机战斗开始。左键移动，{endTurnKey} 结束回合；联机请点 Host。");
    }

    [ContextMenu("Begin Network Battle (Server)")]
    public void TryBeginServerBattle()
    {
        if (!NetworkServer.active)
            return;

        CancelOfflineStart();

        if (!CanStartServerBattle())
            return;

        if (battleStarted || simulationStarted)
        {
            TryEnsureNetworkBattleSynced();
            return;
        }

        if (!BeginBattleCore(syncNetworkState: true))
            return;

        Debug.Log($"[NetworkBattleBootstrap] Server 战斗开始。{DescribeConnections()}");
    }

    private bool ShouldScheduleOfflineStart()
    {
        if (!autoStartOfflineOnPlay)
            return false;

        if (FindObjectOfType<NetworkManager>() != null)
            return false;

        return true;
    }

    private void CancelOfflineStart()
    {
        CancelInvoke(nameof(TryBeginOfflineBattle));
    }

    private bool CanStartServerBattle()
    {
        if (allowSoloHostTest)
            return true;

        return NetworkServer.HasExternalConnections();
    }

    private bool HasBattleRosterReady()
    {
        return BattleRosterSetup.CollectActors(actors).Count > 0;
    }

    private bool BeginBattleCore(bool syncNetworkState)
    {
        if (!BattleRosterSetup.EnsureBattleSystems())
        {
            Debug.LogError("[NetworkBattleBootstrap] EnsureBattleSystems 失败。");
            return false;
        }

        var prep = BuildPreparedRoster();
        if (prep.roster.Count == 0)
        {
            Debug.LogError("[NetworkBattleBootstrap] 场景中没有参战角色（AbilitySystemComponent）。");
            return false;
        }

        NetworkBattleActor.RegisterRoster(prep.roster);

        if (buffPresentationCatalog != null)
        {
            BattleBarrierManager.Instance.BindCatalog(buffPresentationCatalog);
            BattleZoneManager.Instance.BindCatalog(buffPresentationCatalog);
        }

        if (!simulationStarted)
        {
            TurnManager.Instance.StartBattle(prep.roster, firstTeamId, firstPlayerAP, secondPlayerAP);
            simulationStarted = true;
        }

        if (syncNetworkState)
        {
            if (!TryEnsureNetworkBattleSynced())
            {
                StartManagedCoroutine(RetryEnsureNetworkBattleSynced());
                Debug.LogWarning("[NetworkBattleBootstrap] NetworkBattleState 尚未就绪，稍后重试同步开战状态。");
            }
        }
        else
        {
            battleStarted = true;
        }

        BattleRosterSetup.EnsureBattleCamera(prep.roster);
        BattleRosterSetup.EnsureBattleAudio();
        StartManagedCoroutine(EnsureBattleUiNextFrames());

        if (syncNetworkState && NetworkServer.active)
            serverRefreshRoutine = StartManagedCoroutine(ServerRefreshLoop(), ref serverRefreshRoutineHost);

        return true;
    }

    private IEnumerator ServerRefreshLoop()
    {
        var wait = new WaitForSeconds(0.12f);
        while (simulationStarted && NetworkServer.active)
        {
            if (NetworkBattleState.Instance != null && AnyActorMoving())
                NetworkBattleState.Instance.RefreshFromSimulation();

            yield return wait;
        }

        serverRefreshRoutine = null;
        serverRefreshRoutineHost = null;
    }

    private static bool AnyActorMoving()
    {
        foreach (var marker in NetworkBattleActor.AllSlots.Values)
        {
            var movement = marker?.Asc?.GetComponent<CharacterMovementController>();
            if (movement != null && movement.IsMoving)
                return true;
        }

        return false;
    }

    private void EnsureNetworkBattleSynced()
    {
        TryEnsureNetworkBattleSynced();
    }

    private bool TryEnsureNetworkBattleSynced()
    {
        if (networkBattleSynced)
            return true;

        if (NetworkServer.active)
            BattleNetworkRuntimeSpawner.EnsureSpawned();

        var state = BattleNetworkRuntimeSpawner.ResolveState();
        if (state == null)
        {
            Debug.LogWarning("[NetworkBattleBootstrap] NetworkBattleState 未 spawn，稍后重试。");
            return false;
        }

        if (!state.isServer)
        {
            Debug.LogWarning("[NetworkBattleBootstrap] NetworkBattleState 尚未在 Server 就绪，稍后重试。");
            return false;
        }

        state.MarkBattleStarted();
        BattleNetworkRuntimeSpawner.ResolveController()?.AssignTeamsToClients();
        SubscribeSimulationEvents();
        state.RefreshFromSimulation();
        networkBattleSynced = true;
        battleStarted = true;

        if (verboseLogging)
            Debug.Log($"[NetworkBattleBootstrap] 已 MarkBattleStarted，connections={NetworkServer.connections.Count}");

        return true;
    }

    private IEnumerator RetryEnsureNetworkBattleSynced()
    {
        for (int i = 0; i < 300; i++)
        {
            if (!NetworkServer.active)
                yield break;

            if (TryEnsureNetworkBattleSynced())
                yield break;

            yield return null;
        }

        Debug.LogError("[NetworkBattleBootstrap] 多次重试后仍无法同步 NetworkBattleState。");
    }

    public void NotifyClientBattleStarted()
    {
        if (NetworkServer.active || !NetworkClient.active)
            return;

        BattlePresentationSync.EnsureSubscribed();

        if (!battleStarted)
        {
            battleStarted = true;

            var roster = new List<AbilitySystemComponent>();
            foreach (var kv in NetworkBattleActor.AllSlots)
            {
                if (kv.Value?.Asc != null)
                    roster.Add(kv.Value.Asc);
            }

            if (roster.Count > 0)
            {
                BattleRosterSetup.EnsureBattleCamera(roster);
                BattleRosterSetup.EnsureBattleAudio();
                BattleHealthBarBootstrap.EnsureAndSync();
                StartManagedCoroutine(EnsureBattleUiNextFrames());
            }

            RequestPresentationResyncIfBattleAlreadyStarted();
            Debug.Log("[NetworkBattleBootstrap] Client 已收到开战同步，刷新相机与 UI。");
        }
    }

    public void ResetClientPresentationState()
    {
        clientPrepared = false;
        battleStarted = false;
        simulationStarted = false;
        networkBattleSynced = false;
        StopManagedCoroutine(ref clientPrepareRoutine, ref clientPrepareRoutineHost);
    }

    public void EnsureClientPresentationReady()
    {
        if (NetworkServer.active || !NetworkClient.active)
            return;

        if (clientPrepared || clientPrepareRoutine != null)
            return;

        clientPrepareRoutine = StartManagedCoroutine(PrepareClientPresentationWhenReady(), ref clientPrepareRoutineHost);
    }

    private IEnumerator PrepareClientPresentationWhenReady()
    {
        // 等待 Mirror spawn，超时后仍准备相机/UI。
        for (int i = 0; i < 600; i++)
        {
            if (!NetworkClient.active || NetworkServer.active)
                yield break;

            if (NetworkBattleState.IsNetworkReady)
                break;

            yield return null;
        }

        if (!NetworkBattleState.IsNetworkReady)
        {
            Debug.LogWarning("[NetworkBattleBootstrap] Client 等待 NetworkBattleState spawn 超时，仍尝试准备表现层。");
        }

        if (NetworkClient.active && !NetworkServer.active && !clientPrepared)
        {
            clientPrepared = true;
            PrepareClientPresentation();
        }

        clientPrepareRoutine = null;
        clientPrepareRoutineHost = null;
    }

    private void PrepareClientPresentation()
    {
        CancelOfflineStart();
        battleStarted = false;
        networkBattleSynced = false;

        if (!BattleRosterSetup.EnsureBattleSystems())
            return;

        var prep = BuildPreparedRoster();
        if (prep.roster.Count == 0)
        {
            Debug.LogWarning("[NetworkBattleBootstrap] Client 场景中没有参战角色。");
            return;
        }

        NetworkBattleActor.RegisterRoster(prep.roster);

        // Client 也要绑 catalog，否则领域/屏障特效查不到 prefab。
        if (buffPresentationCatalog != null)
        {
            BattleBarrierManager.Instance.BindCatalog(buffPresentationCatalog);
            BattleZoneManager.Instance.BindCatalog(buffPresentationCatalog);
        }

        TurnManager.Instance?.RegisterPresentationRoster(prep.roster);
        ActionQueue.Instance?.RegisterPresentationRoster(prep.roster);
        BattleRosterSetup.EnsureBattleCamera(prep.roster);
        BattleHealthBarBootstrap.EnsureAndSync();
        BattlePresentationSync.EnsureSubscribed();
        StartManagedCoroutine(EnsureBattleUiNextFrames());
        StartManagedCoroutine(WaitForClientBattleSync());
        RequestPresentationResyncIfBattleAlreadyStarted();

        Debug.Log("[NetworkBattleBootstrap] Client 已注册表现 roster，等待 Server 同步开战。");
    }

    private static void RequestPresentationResyncIfBattleAlreadyStarted()
    {
        if (!NetworkClient.active || NetworkServer.active)
            return;

        var state = BattleNetworkRuntimeSpawner.ResolveState();
        if (state == null || !state.battleStarted)
            return;

        BattleNetworkRuntimeSpawner.ResolveController()?.RequestPresentationResync();
    }

    private IEnumerator WaitForClientBattleSync()
    {
        NetworkBattleState state = null;
        System.Action onRefreshed = null;

        for (int i = 0; i < 1800; i++)
        {
            if (!NetworkClient.active || NetworkServer.active)
                yield break;

            state = BattleNetworkRuntimeSpawner.ResolveState();
            if (state != null && state.battleStarted)
            {
                if (onRefreshed != null)
                    state.StateRefreshed -= onRefreshed;
                NotifyClientBattleStarted();
                yield break;
            }

            if (state != null && onRefreshed == null)
            {
                onRefreshed = () =>
                {
                    if (state != null && state.battleStarted)
                        NotifyClientBattleStarted();
                };
                state.StateRefreshed += onRefreshed;
            }

            yield return null;
        }

        if (state != null && onRefreshed != null)
            state.StateRefreshed -= onRefreshed;

        Debug.LogWarning("[NetworkBattleBootstrap] Client 等待开战 SyncVar 超时（30s）。");
    }

    private BattleRosterSetup.Result BuildPreparedRoster()
    {
        return BattleRosterSetup.Prepare(new BattleRosterSetup.Options
        {
            explicitActors = actors,
            teamResourceParent = transform,
            buffCatalog = buffPresentationCatalog,
            snapActorsToNavMesh = snapActorsToNavMeshOnStart,
            autoSplitTeamsWhenSingleSide = autoSplitTeamsWhenSingleSide
        });
    }

    private void LogWaitingForClient()
    {
        if (!verboseLogging || Time.unscaledTime - lastWaitingLogTime < 3f)
            return;

        lastWaitingLogTime = Time.unscaledTime;
        Debug.Log($"[NetworkBattleBootstrap] 等待 Client 连接… {DescribeConnections()}");
    }

    private static string DescribeConnections()
    {
        if (!NetworkServer.active)
            return "Server 未启动";

        var ids = new List<int>();
        foreach (var kv in NetworkServer.connections)
            ids.Add(kv.Key);

        ids.Sort();
        return $"connections={NetworkServer.connections.Count}, ids=[{string.Join(",", ids)}], external={NetworkServer.HasExternalConnections()}";
    }

    private string BuildStartFailureMessage()
    {
        return "[NetworkBattleBootstrap] 联机开战失败。" +
               $" {DescribeConnections()}" +
               $" | allowSoloHostTest={allowSoloHostTest}" +
               $" | rosterActors={BattleRosterSetup.CollectActors(actors).Count}" +
               $" | battleState={(NetworkBattleState.Instance != null ? "OK" : "缺失")}";
    }

    private void SubscribeSimulationEvents()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        UnsubscribeSimulationEvents();
        tm.OnTurnBegan += HandleSimulationEvent;
        tm.OnTurnEnded += HandleSimulationEvent;
        tm.OnPhaseChanged += HandlePhaseEvent;
        tm.OnBattleEnded += HandleBattleEnded;

        foreach (var actor in tm.AllActors)
        {
            if (actor?.Attributes == null) continue;
            actor.Attributes.OnHealthChanged -= HandleActorHealthChanged;
            actor.Attributes.OnHealthChanged += HandleActorHealthChanged;
        }
    }

    private void UnsubscribeSimulationEvents()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        tm.OnTurnBegan -= HandleSimulationEvent;
        tm.OnTurnEnded -= HandleSimulationEvent;
        tm.OnPhaseChanged -= HandlePhaseEvent;
        tm.OnBattleEnded -= HandleBattleEnded;

        if (tm.AllActors == null) return;
        foreach (var actor in tm.AllActors)
        {
            if (actor?.Attributes == null) continue;
            actor.Attributes.OnHealthChanged -= HandleActorHealthChanged;
        }
    }

    private void HandleSimulationEvent(AbilitySystemComponent _) => RefreshState();
    private void HandlePhaseEvent(TurnPhase _) => RefreshState();
    private void HandleBattleEnded(int _) => RefreshState();
    private void HandleActorHealthChanged(float _) => RefreshState();

    private static void RefreshState() => BattleNetworkRuntimeSpawner.ResolveState()?.RefreshFromSimulation();

    private IEnumerator EnsureBattleUiNextFrames()
    {
        yield return null;
        yield return null;
        BattleHealthBarBootstrap.EnsureAndSync();
    }

    private Coroutine StartManagedCoroutine(IEnumerator routine)
    {
        MonoBehaviour host = null;
        return StartManagedCoroutine(routine, ref host);
    }

    private Coroutine StartManagedCoroutine(IEnumerator routine, ref MonoBehaviour hostOut)
    {
        if (routine == null)
            return null;

        var host = ResolveCoroutineHost();
        if (host == null)
        {
            Debug.LogError("[NetworkBattleBootstrap] 无法启动协程：NetworkBattleRuntime 未激活且无 NetworkManager。");
            hostOut = null;
            return null;
        }

        hostOut = host;
        return host.StartCoroutine(routine);
    }

    private static void StopManagedCoroutine(ref Coroutine routine, ref MonoBehaviour host)
    {
        if (routine != null && host != null)
            host.StopCoroutine(routine);

        routine = null;
        host = null;
    }

    private MonoBehaviour ResolveCoroutineHost()
    {
        if (isActiveAndEnabled)
            return this;

        var nm = PilgrimNetworkManager.Instance;
        if (nm != null && nm.isActiveAndEnabled)
            return nm;

        return null;
    }
}
