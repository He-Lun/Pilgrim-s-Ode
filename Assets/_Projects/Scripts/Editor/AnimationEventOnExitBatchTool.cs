#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为已有 OnAbilityComplete 的 Clip 批量追加同帧 OnExit 事件。
/// 菜单：巡礼之诗 → 动画 → 批量添加 OnExit 事件
/// </summary>
public static class AnimationEventOnExitBatchTool
{
    private static readonly Regex CompleteEventPattern = new Regex(
        @"(  - time: ([0-9.]+)\r?\n    functionName: OnAbilityComplete\r?\n    data: \r?\n    objectReferenceParameter: \{fileID: 0\}\r?\n    floatParameter: 0\r?\n    intParameter: 0\r?\n    messageOptions: 0\r?\n)",
        RegexOptions.Multiline);

    [MenuItem("巡礼之诗/动画/批量添加 OnExit 事件")]
    public static void AddOnExitToAllSkillClips()
    {
        const string root = "Assets/Model&Ani";
        if (!AssetDatabase.IsValidFolder(root))
        {
            Debug.LogError("[OnExitBatch] 未找到 Assets/Model&Ani");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { root });
        int updated = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            var existing = AnimationUtility.GetAnimationEvents(clip);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].functionName == "OnExit")
                    goto nextClip;
            }

            var events = new List<AnimationEvent>(existing);
            bool changed = false;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].functionName != "OnAbilityComplete") continue;
                events.Add(new AnimationEvent
                {
                    time = existing[i].time,
                    functionName = "OnExit"
                });
                changed = true;
            }

            if (!changed) continue;

            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
            updated++;
            Debug.Log($"[OnExitBatch] +OnExit: {path}");

            nextClip: ;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[OnExitBatch] 完成，更新 {updated} 个 Clip。");
    }

    [MenuItem("巡礼之诗/动画/批量添加 OnExit 事件 (YAML)")]
    public static void AddOnExitViaYaml()
    {
        const string root = "Assets/Model&Ani";
        int updated = 0;
        foreach (var file in Directory.GetFiles(root, "*.anim", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("functionName: OnExit") || !text.Contains("functionName: OnAbilityComplete"))
                continue;

            var newText = CompleteEventPattern.Replace(
                text,
                m => m.Groups[1].Value
                     + "  - time: " + m.Groups[2].Value + "\n    functionName: OnExit\n    data: \n    objectReferenceParameter: {fileID: 0}\n    floatParameter: 0\n    intParameter: 0\n    messageOptions: 0\n");

            if (newText == text) continue;
            File.WriteAllText(file, newText);
            updated++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[OnExitBatch YAML] 完成，更新 {updated} 个 .anim 文件。");
    }
}
#endif
