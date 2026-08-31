#if UNITY_EDITOR
using System.IO;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class BattleNetworkSceneSetup
{
    /// <summary>项目实际战斗测试场景。</summary>
    const string DefaultBattleScenePath = "Assets/Idyllic Ancient Ruins/Demo Scenes/DEMO Ruins 01.unity";

    /// <summary>可选：复制一份联机专用场景时使用。</summary>
    const string BattleNetworkCopyPath = "Assets/_Projects/Scenes/BattleNetwork.unity";

    const string PreparationScenePath = "Assets/_Projects/Scenes/Preparation.unity";

    [MenuItem("巡礼之诗/Network/Setup Preparation Scene (Matchmaking)")]
    public static void SetupPreparationScene()
    {
        EnsureNetworkBattlePrefab();
        CreateOrUpdatePreparationScene();
        StripNetworkManagerFromBattleScene(DefaultBattleScenePath);
        SetBuildSceneOrder();
        Debug.Log("[BattleNetworkSceneSetup] 准备场景已配置。Build Settings 首场景为 Preparation，战斗场景内 NetworkManager 已移除。");
    }

    [MenuItem("巡礼之诗/Network/Setup Battle Network Scene (DEMO Ruins 01)")]
    public static void SetupDefaultBattleScene()
    {
        SetupBattleNetworkScene(DefaultBattleScenePath, saveToSameScene: true, addNetworkManager: false);
    }

    [MenuItem("巡礼之诗/Network/Setup Battle Network Scene Copy")]
    public static void SetupBattleNetworkCopy()
    {
        SetupBattleNetworkScene(DefaultBattleScenePath, saveToSameScene: false, addNetworkManager: false);
    }

    static void CreateOrUpdatePreparationScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreparationScenePath) ?? "Assets/_Projects/Scenes");

        bool created = !File.Exists(PreparationScenePath);
        var scene = created
            ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(PreparationScenePath, OpenSceneMode.Single);

        if (Object.FindObjectOfType<Camera>() == null)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 1f, -10f);
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var networkManagerGo = FindOrCreate("NetworkManager");
        if (networkManagerGo.GetComponent<PilgrimNetworkManager>() == null)
            networkManagerGo.AddComponent<PilgrimNetworkManager>();
        if (networkManagerGo.GetComponent<KcpTransport>() == null)
            networkManagerGo.AddComponent<KcpTransport>();
        DestroyComponentIfPresent<NetworkManagerHUD>(networkManagerGo);

        var pilgrimNetworkManager = networkManagerGo.GetComponent<PilgrimNetworkManager>();
        var kcp = networkManagerGo.GetComponent<KcpTransport>() ?? networkManagerGo.AddComponent<KcpTransport>();
        kcp.DualMode = false;
        pilgrimNetworkManager.transport = kcp;
        pilgrimNetworkManager.networkAddress = "127.0.0.1";
        pilgrimNetworkManager.offlineScene = PreparationMatchmakingController.SceneName;
        pilgrimNetworkManager.onlineScene = PreparationMatchmakingController.SceneName;

        WireNetworkManagerPrefab(pilgrimNetworkManager);

        var matchmakingGo = FindOrCreate("PreparationMatchmaking");
        if (matchmakingGo.GetComponent<PreparationMatchmakingController>() == null)
            matchmakingGo.AddComponent<PreparationMatchmakingController>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, PreparationScenePath);
    }

    static void StripNetworkManagerFromBattleScene(string battleScenePath)
    {
        if (!File.Exists(battleScenePath))
        {
            Debug.LogWarning($"[BattleNetworkSceneSetup] 找不到战斗场景：{battleScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Single);
        var networkManagers = Object.FindObjectsOfType<PilgrimNetworkManager>(true);
        foreach (var networkManager in networkManagers)
        {
            Debug.Log($"[BattleNetworkSceneSetup] 从战斗场景移除 {networkManager.name}");
            Object.DestroyImmediate(networkManager.gameObject);
        }

        var legacyManagers = Object.FindObjectsOfType<NetworkManager>(true);
        foreach (var networkManager in legacyManagers)
        {
            Debug.Log($"[BattleNetworkSceneSetup] 从战斗场景移除 {networkManager.name}");
            Object.DestroyImmediate(networkManager.gameObject);
        }

        EnsureBattleRuntimeComponents();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, battleScenePath);
    }

    static void EnsureBattleRuntimeComponents()
    {
        var runtimeGo = FindOrCreate("NetworkBattleRuntime");
        if (runtimeGo.GetComponent<NetworkBattleBootstrap>() == null)
            runtimeGo.AddComponent<NetworkBattleBootstrap>();
        if (runtimeGo.GetComponent<BattlePresentationSync>() == null)
            runtimeGo.AddComponent<BattlePresentationSync>();

        DestroyComponentIfPresent<NetworkIdentity>(runtimeGo);
        DestroyComponentIfPresent<NetworkBattleState>(runtimeGo);
        DestroyComponentIfPresent<NetworkBattleController>(runtimeGo);
    }

    static void SetBuildSceneOrder()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(PreparationScenePath, true),
            new EditorBuildSettingsScene(DefaultBattleScenePath, true)
        };

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (entry.path == PreparationScenePath || entry.path == DefaultBattleScenePath)
                continue;

            scenes.Add(entry);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void SetupBattleNetworkScene(string sourceScenePath, bool saveToSameScene, bool addNetworkManager)
    {
        if (!File.Exists(sourceScenePath))
        {
            Debug.LogError($"[BattleNetworkSceneSetup] 找不到战斗场景：{sourceScenePath}");
            return;
        }

        EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);

        if (Object.FindObjectsOfType<AbilitySystemComponent>().Length == 0)
        {
            Debug.LogWarning("[BattleNetworkSceneSetup] 当前场景中没有 AbilitySystemComponent，请确认角色 prefab 已放入场景。");
        }

        if (addNetworkManager)
        {
            var networkManagerGo = FindOrCreate("NetworkManager");
            if (networkManagerGo.GetComponent<PilgrimNetworkManager>() == null)
                networkManagerGo.AddComponent<PilgrimNetworkManager>();
            if (networkManagerGo.GetComponent<KcpTransport>() == null)
                networkManagerGo.AddComponent<KcpTransport>();

            var pilgrimNetworkManager = networkManagerGo.GetComponent<PilgrimNetworkManager>();
            var kcp = networkManagerGo.GetComponent<KcpTransport>() ?? networkManagerGo.AddComponent<KcpTransport>();
            kcp.DualMode = false;
            pilgrimNetworkManager.transport = kcp;
            pilgrimNetworkManager.networkAddress = "127.0.0.1";
            WireNetworkManagerPrefab(pilgrimNetworkManager);
        }

        EnsureBattleRuntimeComponents();
        EnsureNetworkBattlePrefab();

        string savePath = saveToSameScene ? sourceScenePath : EnsureBattleNetworkCopy(sourceScenePath);
        AddSceneToBuildSettings(savePath);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, savePath);

        Debug.Log($"[BattleNetworkSceneSetup] 已在 {savePath} 配置战斗联机组件（NetworkManager 位于准备场景）。");
    }

    static string EnsureBattleNetworkCopy(string sourceScenePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BattleNetworkCopyPath) ?? "Assets/_Projects/Scenes");

        if (!File.Exists(BattleNetworkCopyPath))
        {
            if (!AssetDatabase.CopyAsset(sourceScenePath, BattleNetworkCopyPath))
                throw new System.InvalidOperationException($"无法复制 {sourceScenePath} → {BattleNetworkCopyPath}");
            AssetDatabase.Refresh();
        }

        EditorSceneManager.OpenScene(BattleNetworkCopyPath, OpenSceneMode.Single);
        return BattleNetworkCopyPath;
    }

    [MenuItem("巡礼之诗/Network/Create NetworkBattleNetwork Prefab")]
    public static void CreateNetworkBattlePrefab()
    {
        EnsureNetworkBattlePrefab(forceRecreate: true);

        var networkManager = Object.FindObjectOfType<PilgrimNetworkManager>();
        WireNetworkManagerPrefab(networkManager);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BattleNetworkSceneSetup] 已创建预制体并写入 NetworkManager.spawnPrefabs，请保存场景后重新编译。");
    }

    static void EnsureNetworkBattlePrefab(bool forceRecreate = false)
    {
        const string resourcePath = "Assets/_Projects/Resources/NetworkBattleNetwork.prefab";
        DeleteInvalidPrefabAssets(resourcePath);

        if (!forceRecreate)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(resourcePath);
            if (existing != null && existing.GetComponent<NetworkBattleState>() != null)
                return;
        }
        else if (AssetDatabase.LoadAssetAtPath<GameObject>(resourcePath) != null)
        {
            AssetDatabase.DeleteAsset(resourcePath);
        }

        var go = new GameObject("NetworkBattleNetwork");
        go.AddComponent<NetworkIdentity>();
        go.AddComponent<NetworkBattleState>();
        go.AddComponent<NetworkBattleController>();

        System.IO.Directory.CreateDirectory("Assets/_Projects/Resources");
        PrefabUtility.SaveAsPrefabAsset(go, resourcePath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        PersistPrefabAssetId(resourcePath);
    }

    static void PersistPrefabAssetId(string resourcePath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(resourcePath);
        if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity identity))
            return;

        string guidString = AssetDatabase.AssetPathToGUID(resourcePath);
        if (string.IsNullOrWhiteSpace(guidString))
            return;

        uint assetId = NetworkIdentity.AssetGuidToUint(new System.Guid(guidString));
        var so = new SerializedObject(identity);
        so.FindProperty("_assetId").longValue = assetId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BattleNetworkSceneSetup] 预制体 assetId={assetId}（guid={guidString}）");
    }

    static void DeleteInvalidPrefabAssets(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            return;

        string metaPath = assetPath + ".meta";
        if (System.IO.File.Exists(metaPath))
            System.IO.File.Delete(metaPath);
        if (System.IO.File.Exists(assetPath))
            System.IO.File.Delete(assetPath);

        AssetDatabase.Refresh();
    }

    static void WireNetworkManagerPrefab(PilgrimNetworkManager networkManager)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projects/Resources/NetworkBattleNetwork.prefab");
        if (prefab == null || networkManager == null)
            return;

        var so = new SerializedObject(networkManager);
        so.FindProperty("battleNetworkPrefab").objectReferenceValue = prefab;

        var spawnPrefabs = so.FindProperty("spawnPrefabs");
        bool alreadyListed = false;
        for (int i = 0; i < spawnPrefabs.arraySize; i++)
        {
            if (spawnPrefabs.GetArrayElementAtIndex(i).objectReferenceValue == prefab)
            {
                alreadyListed = true;
                break;
            }
        }

        if (!alreadyListed)
        {
            spawnPrefabs.InsertArrayElementAtIndex(spawnPrefabs.arraySize);
            spawnPrefabs.GetArrayElementAtIndex(spawnPrefabs.arraySize - 1).objectReferenceValue = prefab;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject FindOrCreate(string name)
    {
        var existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    static void DestroyComponentIfPresent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component);
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var entry in scenes)
        {
            if (entry.path == scenePath)
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
