#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build 前同步血条配置。</summary>
public class HealthBarConfigBuildSync : IPreprocessBuildWithReport
{
    const string ResourcesConfigPath = "Assets/_Projects/Resources/HealthBarUIConfig.asset";
    const string LegacyConfigPath = "Assets/_Projects/Prefab/UI/HealthBarUIConfig.asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => SyncIfNeeded();

    [MenuItem("巡礼之诗/UI/同步血条配置到 Resources")]
    public static void SyncIfNeeded()
    {
        var resources = AssetDatabase.LoadAssetAtPath<HealthBarUIConfig>(ResourcesConfigPath);
        var legacy = AssetDatabase.LoadAssetAtPath<HealthBarUIConfig>(LegacyConfigPath);

        if (resources == null && legacy != null)
        {
            AssetDatabase.CopyAsset(LegacyConfigPath, ResourcesConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[HealthBarConfigBuildSync] 已从 Prefab/UI 复制 HealthBarUIConfig 到 Resources。");
            return;
        }

        if (resources == null)
        {
            Debug.LogError("[HealthBarConfigBuildSync] 缺少 Resources/HealthBarUIConfig.asset，请先运行「巡礼之诗/UI/生成血条预制体」。");
            return;
        }

        if (legacy != null && IsIncomplete(resources) && !IsIncomplete(legacy))
        {
            EditorUtility.CopySerialized(legacy, resources);
            EditorUtility.SetDirty(resources);
            AssetDatabase.SaveAssets();
            Debug.Log("[HealthBarConfigBuildSync] 已用 Prefab/UI 配置补全 Resources/HealthBarUIConfig。");
        }

        HealthBarUIConfig.InvalidateCache();
    }

    static bool IsIncomplete(HealthBarUIConfig config)
    {
        if (config == null)
            return true;

        return config.rosterEntryAllyPrefab == null
               || config.worldBarAllyPrefab == null
               || config.actionBarPortraits == null;
    }
}
#endif
