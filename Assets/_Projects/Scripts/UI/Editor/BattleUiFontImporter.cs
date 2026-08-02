#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将系统中文字体复制到 Resources，供战斗 UI 显示角色名。
/// </summary>
public static class BattleUiFontImporter
{
    private const string OutputDir = "Assets/_Projects/Resources/Fonts";
    private const string OutputPath = OutputDir + "/RosterUI.ttf";
    private const string ResourcePath = "Fonts/RosterUI";

    private static readonly string[] SourceFontPaths =
    {
        @"C:\Windows\Fonts\msyh.ttc",
        @"C:\Windows\Fonts\simhei.ttf",
        @"C:\Windows\Fonts\simsun.ttc"
    };

    [InitializeOnLoadMethod]
    private static void AutoImportIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying && !File.Exists(OutputPath))
                ImportInternal();
        };
    }

    [MenuItem("巡礼之诗/UI/导入角色名字体")]
    public static void ImportFromMenu()
    {
        ImportInternal();
    }

    private static void ImportInternal()
    {
        EnsureDirectory(OutputDir);

        for (int i = 0; i < SourceFontPaths.Length; i++)
        {
            if (!File.Exists(SourceFontPaths[i]))
                continue;

            File.Copy(SourceFontPaths[i], OutputPath, true);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BattleUiFontImporter] 已导入字体: {OutputPath}");
            return;
        }

        Debug.LogWarning("[BattleUiFontImporter] 未找到系统中文字体，战斗 UI 将尝试运行时加载。");
    }

    public static Font LoadImportedFont()
    {
        return AssetDatabase.LoadAssetAtPath<Font>(OutputPath)
            ?? Resources.Load<Font>(ResourcePath);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Projects/Resources"))
            AssetDatabase.CreateFolder("Assets/_Projects", "Resources");

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets/_Projects/Resources", "Fonts");
    }
}
#endif
