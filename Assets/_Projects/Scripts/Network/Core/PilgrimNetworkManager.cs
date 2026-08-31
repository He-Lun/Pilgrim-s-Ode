using kcp2k;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("")]
[DefaultExecutionOrder(-100)]
public class PilgrimNetworkManager : NetworkManager
{
    public static PilgrimNetworkManager Instance => singleton as PilgrimNetworkManager;

    bool pendingPostSpawnCleanup;
    float lastHandshakeSweepTime;

    public override void Awake()
    {
        maxConnections = 2;
        autoCreatePlayer = false;
        exceptionsDisconnect = false;
        EnsureTransport();
        EnsureLoopbackAddress();
        EnsureSceneConfiguration();
        BattleNetworkRuntimeSpawner.StripSceneNetworkComponents();
        BattleNetworkRuntimeSpawner.RegisterSpawnPrefab(this);
        base.Awake();
    }

    void EnsureTransport()
    {
        if (transport == null)
        {
            if (!TryGetComponent(out KcpTransport kcp))
                kcp = gameObject.AddComponent<KcpTransport>();
            transport = kcp;
        }

        if (transport is KcpTransport kcpTransport)
        {
            // 同机 Editor + Build：关 DualMode，避免 localhost/::1 与 IPv4 监听不一致。
            kcpTransport.DualMode = false;
        }
    }

    void EnsureLoopbackAddress()
    {
        if (string.IsNullOrWhiteSpace(networkAddress)
            || networkAddress.Equals("localhost", System.StringComparison.OrdinalIgnoreCase))
        {
            networkAddress = "127.0.0.1";
        }
    }

    void EnsureSceneConfiguration()
    {
        if (string.IsNullOrWhiteSpace(offlineScene))
            offlineScene = PreparationMatchmakingController.SceneName;

        // 联网后仍留准备场景，匹配完成再 ServerChangeScene 进战斗。
        if (string.IsNullOrWhiteSpace(onlineScene))
            onlineScene = PreparationMatchmakingController.SceneName;
    }

    static bool IsInPreparationScene()
    {
        return SceneManager.GetActiveScene().name == PreparationMatchmakingController.SceneName;
    }

    static void TryBeginBattleIfInBattleScene()
    {
        if (IsInPreparationScene())
            return;

        NetworkBattleBootstrap.FindInstance()?.NotifyNetworkSessionChanged();
    }

    static bool IsBattleScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return sceneName == NetworkBattleBootstrap.BattleSceneName
            || sceneName.EndsWith("/" + NetworkBattleBootstrap.BattleSceneName);
    }

    static void PrepareBattleSceneNetworkObjects()
    {
        BattleNetworkRuntimeSpawner.StripSceneNetworkComponents();
        if (NetworkServer.active)
        {
            BattleNetworkRuntimeSpawner.EnsureSpawned();
            BattleNetworkRuntimeSpawner.ResetSessionState();
            BattleNetworkRuntimeSpawner.RebuildObserversForReadyClients();
        }
    }

    public override void OnStartServer()
    {
        NetworkBattleTeamRegistry.Reset();
        BattleNetworkRuntimeSpawner.ResetSessionState();
        base.OnStartServer();
        pendingPostSpawnCleanup = true;
        ushort port = 7777;
        if (transport is KcpTransport kcp)
            port = kcp.Port;
        Debug.Log($"[PilgrimNetworkManager] Server 已启动（KCP {networkAddress}:{port}）。等待 Client 连接…");
        NetworkBattleBootstrap.FindInstance()?.NotifyNetworkSessionChanged();
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        if (!NetworkServer.active)
            return;

        if (pendingPostSpawnCleanup)
        {
            pendingPostSpawnCleanup = false;
            BattleNetworkRuntimeSpawner.CleanupAfterServerSpawnObjects();
            return;
        }

        // 兜底：新 Ready 连接补 spawn 握手（按 connectionId 去重）。
        if (Time.unscaledTime - lastHandshakeSweepTime < 0.5f)
            return;

        lastHandshakeSweepTime = Time.unscaledTime;
        BattleNetworkRuntimeSpawner.RebuildObserversForReadyClients();
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
        pendingPostSpawnCleanup = true;
    }

    public override void OnStartClient()
    {
        BattleNetworkRuntimeSpawner.RegisterSpawnPrefab(this);
        base.OnStartClient();
        ushort port = 7777;
        if (transport is KcpTransport kcp)
            port = kcp.Port;
        Debug.Log($"[PilgrimNetworkManager] Client 正在连接 {networkAddress}:{port} …");
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        // 无 Player Prefab，Mirror 默认不会 SpawnObservers，须手动同步 NetworkBattleState。
        BattleNetworkRuntimeSpawner.EnsureSpawned();
        BattleNetworkRuntimeSpawner.RebuildObserversForReadyClients();

        if (conn.connectionId != 0)
        {
            TryBeginBattleIfInBattleScene();

            var state = BattleNetworkRuntimeSpawner.ResolveState();
            if (state == null)
                Debug.LogError("[PilgrimNetworkManager] Client Ready 但 NetworkBattleState 未 Spawn。");
            else if (state.battleStarted)
                state.ResyncForConnection(conn);
        }
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[PilgrimNetworkManager] Client 已成功连上 Server。");
        if (!NetworkServer.active)
            NetworkBattleBootstrap.FindInstance()?.EnsureClientPresentationReady();
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        if (IsBattleScene(SceneManager.GetActiveScene().name))
        {
            BattlePresentationSync.ResetSubscription();
            PrepareBattleSceneNetworkObjects();
        }

        if (!NetworkServer.active)
            NetworkBattleBootstrap.FindInstance()?.EnsureClientPresentationReady();
        else
            StartCoroutine(DeferredNotifyBattleSceneReady());
    }

    public override void OnClientError(TransportError error, string reason)
    {
        Debug.LogError($"[PilgrimNetworkManager] Client 连接失败: {error} — {reason}");
    }

    public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
    {
        int id = conn != null ? conn.connectionId : -1;
        Debug.LogError($"[PilgrimNetworkManager] Server 传输错误 (conn={id}): {error} — {reason}");
    }

    public override void OnStopServer()
    {
        NetworkBattleTeamRegistry.Reset();
        BattleNetworkRuntimeSpawner.ResetSessionState();
        base.OnStopServer();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        int teamId = NetworkBattleTeamRegistry.RegisterConnection(conn.connectionId);
        Debug.Log($"[PilgrimNetworkManager] Server 收到连接 id={conn.connectionId} → Team {teamId} | {DescribeConnections()}");
        BattleNetworkRuntimeSpawner.ResolveController()?.AssignTeamsToClients();
        StartCoroutine(DeferredHandleServerConnect(conn));
    }

    private IEnumerator DeferredHandleServerConnect(NetworkConnectionToClient conn)
    {
        float timeout = 15f;
        float startedAt = Time.unscaledTime;
        while (conn != null && !conn.isReady && Time.unscaledTime - startedAt < timeout)
            yield return null;

        if (conn == null)
            yield break;

        BattleNetworkRuntimeSpawner.EnsureSpawned();
        BattleNetworkRuntimeSpawner.RebuildObserversForReadyClients();

        if (conn.connectionId != 0)
            TryBeginBattleIfInBattleScene();
        else
            NetworkBattleBootstrap.FindInstance()?.NotifyNetworkSessionChanged();

        var state = BattleNetworkRuntimeSpawner.ResolveState();
        if (conn.connectionId != 0)
        {
            if (state == null)
                Debug.LogError("[PilgrimNetworkManager] Client 已连接但 NetworkBattleState 未 Spawn，无法同步。");
            else if (!state.battleStarted)
                TryBeginBattleIfInBattleScene();
            else
                state.ResyncForConnection(conn);
        }
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!IsBattleScene(sceneName))
            return;

        PrepareBattleSceneNetworkObjects();
        StartCoroutine(DeferredNotifyBattleSceneReady());
    }

    IEnumerator DeferredNotifyBattleSceneReady()
    {
        // 等 ASC / Bootstrap 就绪再开战，避免 roster 为空。
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (!NetworkServer.active)
                yield break;

            if (NetworkBattleBootstrap.FindInstance() != null
                && BattleRosterSetup.CollectActors(null).Count > 0)
            {
                break;
            }

            yield return null;
        }

        NetworkBattleBootstrap.FindInstance()?.NotifyNetworkSessionChanged();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.LogWarning($"[PilgrimNetworkManager] 连接断开 id={conn.connectionId}");
        BattleNetworkRuntimeSpawner.ForgetConnection(conn.connectionId);
        base.OnServerDisconnect(conn);
    }

    public override void OnClientDisconnect()
    {
        Debug.LogWarning("[PilgrimNetworkManager] Client 与 Server 断开。");
        BattleNetworkGate.ClearLocalTeamId();
        NetworkBattleBootstrap.FindInstance()?.ResetClientPresentationState();
        base.OnClientDisconnect();
    }

    static string DescribeConnections()
    {
        if (!NetworkServer.active)
            return "server inactive";

        return $"connections={NetworkServer.connections.Count}, external={NetworkServer.HasExternalConnections()}";
    }
}
