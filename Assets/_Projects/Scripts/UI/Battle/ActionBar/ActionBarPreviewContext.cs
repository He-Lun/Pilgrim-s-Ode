using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行动条 UI 的拉条预演 — 选技能瞄准时写入，松手/取消时清除。
/// </summary>
public static class ActionBarPreviewContext
{
    public static AbilitySystemComponent AdvanceTarget { get; private set; }
    public static float AdvancePercent { get; private set; }
    public static bool HasAdvancePreview => AdvanceTarget != null && AdvancePercent > 0f;

    public static event Action Changed;

    public static void SetAdvancePreview(AbilitySystemComponent target, float percent)
    {
        AdvanceTarget = target;
        AdvancePercent = Mathf.Max(0f, percent);
        Changed?.Invoke();
    }

    public static void Clear()
    {
        if (AdvanceTarget == null && AdvancePercent <= 0f)
            return;

        AdvanceTarget = null;
        AdvancePercent = 0f;
        Changed?.Invoke();
    }
}
