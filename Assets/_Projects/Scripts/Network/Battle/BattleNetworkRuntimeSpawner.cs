using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Server 生成 Resources/NetworkBattleNetwork 预制体；Client 须先在 spawnPrefabs 注册。
/// 场景 NetworkBattleRuntime 上不挂 NetworkIdentity，只用预制体 Spawn。
/// </summary>
public static class BattleNetworkRuntimeSpawner
{
    public const string PrefabResourcePath = "NetworkBattleNetwork";
    public const string PrefabAssetGuid = "ae7ff902d407f194bb942573c0768c35";
    public const string SceneRuntimeName = "NetworkBattleRuntime";

    static GameObject cachedPrefab;
    static uint cachedStableAssetId;
    static readonly HashSet<int> spawnHandshakeSent = new HashSet<int>();

    public static void ResetSessionState() => spawnHandshakeSent.Clear();

    public static void ForgetConnection(int connectionId) => spawnHandshakeSent.Remove(connectionId);

    public static bool IsNetworkSpawned(NetworkBehaviour behaviour)
    {
        return behaviour != null
            && behaviour.netIdentity != null
            && behaviour.netIdentity.netId != 0;
    }

    public static GameObject LoadPrefab()
    {
        if (cachedPrefab != null)
            return cachedPrefab;

        cachedPrefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (cachedPrefab == null)
        {
            Debug.LogError(
                $"[BattleNetworkRuntimeSpawner] 找不到 Resources/{PrefabResourcePath}.prefab\n" +
                "请运行：巡礼之诗 → Network → Create NetworkBattleNetwork Prefab");
        }

        return cachedPrefab;
    }

    public static void RegisterSpawnPrefab(NetworkManager networkManager)
    {
        var prefab = LoadPrefab();
        if (prefab == null || networkManager == null)
            return;

        EnsurePrefabAssetId(prefab);

        if (!networkManager.spawnPrefabs.Contains(prefab))
            networkManager.spawnPrefabs.Add(prefab);

        for (int i = 0; i < networkManager.spawnPrefabs.Count; i++)
        {
            var entry = networkManager.spawnPrefabs[i];
            if (entry != null)
                EnsurePrefabAssetId(entry);
        }
    }

    /// <summary>Mirror 预制体须非零 assetId，否则 Client 收不到 Spawn。</summary>
    public static void EnsurePrefabAssetId(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity identity))
            return;

        if (identity.assetId != 0)
            return;

        uint assetId = ResolveStableAssetId();
        NetworkClient.RegisterPrefab(prefab, assetId);
        Debug.Log($"[BattleNetworkRuntimeSpawner] 已补登记预制体 assetId={assetId}（Resources/{PrefabResourcePath}）");
    }

    public static uint ResolveStableAssetId()
    {
        if (cachedStableAssetId != 0)
            return cachedStableAssetId;

        cachedStableAssetId = NetworkIdentity.AssetGuidToUint(new System.Guid(PrefabAssetGuid));
        return cachedStableAssetId;
    }

    /// <summary>去掉场景遗留的 NetworkIdentity，避免 Mirror 把它当 scene 对象同步。</summary>
    public static void StripSceneNetworkComponents()
    {
        var runtime = GameObject.Find(SceneRuntimeName);
        if (runtime == null)
            return;

        DestroyComponentImmediate<NetworkIdentity>(runtime);
        DestroyComponentImmediate<NetworkBattleState>(runtime);
        DestroyComponentImmediate<NetworkBattleController>(runtime);
    }

    /// <summary>
    /// SpawnObjects() 可能在 OnStartServer 之后把场景 NetworkIdentity 注册为 netId=2，
    /// 而 Client 侧已 Strip sceneId，会导致 "Spawn scene object not found"。
    /// </summary>
    public static void CleanupAfterServerSpawnObjects()
    {
        if (!NetworkServer.active)
            return;

        StripSceneNetworkComponents();

        var identities = UnityEngine.Object.FindObjectsOfType<NetworkIdentity>(true);
        for (int i = 0; i < identities.Length; i++)
        {
            var identity = identities[i];
            if (identity == null || identity.sceneId == 0)
                continue;

            if (!IsBattleNetworkSceneObject(identity.gameObject))
                continue;

            if (identity.netId != 0)
            {
                Debug.LogWarning(
                    $"[BattleNetworkRuntimeSpawner] 移除错误的场景网络对象 netId={identity.netId}, sceneId={identity.sceneId:X}, name={identity.name}");
                NetworkServer.UnSpawn(identity.gameObject);
            }

            DestroyComponentImmediate<NetworkIdentity>(identity.gameObject);
            DestroyComponentImmediate<NetworkBattleState>(identity.gameObject);
            DestroyComponentImmediate<NetworkBattleController>(identity.gameObject);
        }

        EnsureSpawned();
        RebuildObserversForReadyClients();
    }

    static bool IsBattleNetworkSceneObject(GameObject go)
    {
        if (go == null)
            return false;

        if (go.name == SceneRuntimeName || go.name == PrefabResourcePath)
            return go.GetComponent<NetworkBattleState>() != null || go.GetComponent<NetworkBattleController>() != null;

        return false;
    }

    public static void EnsureSpawned()
    {
        if (!NetworkServer.active)
            return;

        var existing = ResolveState();
        if (existing != null && IsNetworkSpawned(existing))
            return;

        var prefab = LoadPrefab();
        if (prefab == null)
            return;

        var instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = PrefabResourcePath;
        UnityEngine.Object.DontDestroyOnLoad(instance);

        var identity = instance.GetComponent<NetworkIdentity>();
        uint assetId = identity != null && identity.assetId != 0
            ? identity.assetId
            : ResolveStableAssetId();

        if (identity != null && identity.assetId == 0)
            NetworkServer.Spawn(instance, assetId);
        else
            NetworkServer.Spawn(instance);

        var netId = identity?.netId ?? 0;
        Debug.Log($"[BattleNetworkRuntimeSpawner] Server Spawn 完成 netId={netId}, assetId={assetId}, sceneId={identity?.sceneId ?? 0}");
    }

    /// <summary>
    /// autoCreatePlayer=false 时 Mirror 不调 SpawnObserversForConnection，
    /// Client 的 isSpawnFinished 永远 false，OnStartClient 也不会触发，这里手动补握手。
    /// </summary>
    public static void RebuildObserversForReadyClients()
    {
        if (!NetworkServer.active)
            return;

        var state = ResolveState();
        if (state == null || !IsNetworkSpawned(state))
            return;

        var pending = new List<NetworkConnectionToClient>();
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null || !conn.isReady)
                continue;
            if (conn is LocalConnectionToClient)
                continue;
            if (!spawnHandshakeSent.Add(conn.connectionId))
                continue;

            pending.Add(conn);
        }

        for (int i = 0; i < pending.Count; i++)
            pending[i].Send(new ObjectSpawnStartedMessage());

        NetworkServer.RebuildObservers(state.netIdentity, initialize: true);

        for (int i = 0; i < pending.Count; i++)
            pending[i].Send(new ObjectSpawnFinishedMessage());

        if (pending.Count > 0)
        {
            Debug.Log(
                $"[BattleNetworkRuntimeSpawner] 已向 {pending.Count} 个 Client 完成 spawn 握手 " +
                $"netId={state.netIdentity.netId}, assetId={state.netIdentity.assetId}");
        }
    }

    public static NetworkBattleState ResolveState()
    {
        var cached = NetworkBattleState.Instance;
        if (IsNetworkSpawned(cached) && cached.netIdentity.sceneId == 0)
            return cached;

        var all = UnityEngine.Object.FindObjectsOfType<NetworkBattleState>(true);
        NetworkBattleState fallback = null;

        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null || !IsNetworkSpawned(candidate))
                continue;

            // 只认预制体 Spawn（sceneId=0），忽略错误的场景对象。
            if (candidate.netIdentity.sceneId != 0)
            {
                fallback = fallback ?? candidate;
                continue;
            }

            return candidate;
        }

        return fallback ?? cached;
    }

    public static NetworkBattleController ResolveController()
    {
        var state = ResolveState();
        if (state != null)
            return state.GetComponent<NetworkBattleController>();

        var all = UnityEngine.Object.FindObjectsOfType<NetworkBattleController>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && IsNetworkSpawned(all[i]) && all[i].netIdentity.sceneId == 0)
                return all[i];
        }

        return NetworkBattleController.Instance;
    }

    static void DestroyComponentImmediate<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null)
            UnityEngine.Object.DestroyImmediate(component);
    }
}
