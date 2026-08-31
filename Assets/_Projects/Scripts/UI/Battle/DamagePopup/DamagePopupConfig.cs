using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>飘字类别 — 决定用哪一组配色与前缀。</summary>
public enum DamagePopupKind
{
    Damage,
    Heal,
    /// <summary>献祭/自残扣血，与受击伤害区分开。</summary>
    HealthCost
}

/// <summary>单个类别的外观。</summary>
[Serializable]
public class DamagePopupStyle
{
    public Color color = Color.white;
    public Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("数字前缀，如治疗的 +")]
    public string prefix = string.Empty;
    public FontStyle fontStyle = FontStyle.Bold;
    [Tooltip("在基础字号上再乘一个倍率")]
    public float fontScale = 1f;
}

/// <summary>按伤害类型 tag 覆盖颜色。</summary>
[Serializable]
public class DamageTypeColorEntry
{
    public GameplayTag damageType;
    public Color color = Color.white;
}

/// <summary>伤害飘字配置，无资产时用内置默认值。</summary>
[CreateAssetMenu(menuName = "Pilgrim/UI/Damage Popup Config", fileName = "DamagePopupConfig")]
public class DamagePopupConfig : ScriptableObject
{
    public const string ResourcesPath = "DamagePopupConfig";

    [Header("样式")]
    public DamagePopupStyle damage = new DamagePopupStyle
    {
        color = new Color(1f, 0.92f, 0.82f),
        fontScale = 1f
    };

    public DamagePopupStyle heal = new DamagePopupStyle
    {
        color = new Color(0.45f, 1f, 0.55f),
        prefix = "+",
        fontScale = 0.9f
    };

    public DamagePopupStyle healthCost = new DamagePopupStyle
    {
        color = new Color(0.85f, 0.45f, 1f),
        fontScale = 0.85f
    };

    [Header("伤害类型配色")]
    [Tooltip("按 damageType tag 覆盖伤害飘字颜色；留空则统一用上面的 damage.color")]
    public List<DamageTypeColorEntry> damageTypeColors = new List<DamageTypeColorEntry>();

    [Header("字号")]
    public int minFontSize = 26;
    public int maxFontSize = 56;
    [Tooltip("单次伤害占目标最大生命的比例达到该值时用最大字号")]
    [Range(0.01f, 1f)] public float fontSizeSaturationRatio = 0.25f;

    [Header("运动")]
    [Tooltip("整条飘字的存活秒数")]
    public float lifetime = 1.05f;
    [Tooltip("初始上升速度（像素/秒）")]
    public float riseSpeed = 260f;
    [Tooltip("下坠加速度（像素/秒²），造成先升后落的抛物线")]
    public float gravity = 520f;
    [Tooltip("水平初速度的随机范围（像素/秒）")]
    public float horizontalSpread = 80f;
    [Tooltip("生命进度超过该比例后开始淡出")]
    [Range(0f, 1f)] public float fadeStartRatio = 0.5f;

    [Header("弹入")]
    public float punchDuration = 0.14f;
    [Tooltip("弹入时的最大缩放，1 = 不弹")]
    public float punchScale = 1.3f;

    [Header("锚点")]
    [Tooltip("角色身上的挂点名，与血条共用；找不到时退回下面的偏移")]
    public string attachPointId = "HeadForHp";
    public Vector3 worldOffsetFallback = new Vector3(0f, 2.1f, 0f);
    [Tooltip("在锚点屏幕位置上再抬高的像素")]
    public float screenOffsetY = 12f;

    [Header("连击堆叠")]
    [Tooltip("同一目标在该秒数内连续挨打时，飘字逐条上移避免重叠")]
    public float stackWindowSeconds = 0.55f;
    public float stackOffsetY = 36f;
    public int maxStackSteps = 4;

    static DamagePopupConfig cached;

    /// <summary>无配置资产时返回内置默认值。</summary>
    public static DamagePopupConfig LoadOrDefault()
    {
        if (cached != null)
            return cached;

        cached = Resources.Load<DamagePopupConfig>(ResourcesPath);
        if (cached == null)
            cached = CreateInstance<DamagePopupConfig>();

        return cached;
    }

    public static void InvalidateCache() => cached = null;

    public DamagePopupStyle ResolveStyle(DamagePopupKind kind)
    {
        switch (kind)
        {
            case DamagePopupKind.Heal: return heal ?? new DamagePopupStyle();
            case DamagePopupKind.HealthCost: return healthCost ?? new DamagePopupStyle();
            default: return damage ?? new DamagePopupStyle();
        }
    }

    public Color ResolveDamageColor(GameplayTag damageType)
    {
        var fallback = ResolveStyle(DamagePopupKind.Damage).color;
        if (damageTypeColors == null || string.IsNullOrEmpty(damageType.TagName))
            return fallback;

        for (int i = 0; i < damageTypeColors.Count; i++)
        {
            var entry = damageTypeColors[i];
            if (entry != null && damageType.Matches(entry.damageType))
                return entry.color;
        }

        return fallback;
    }

    /// <summary>按伤害占比计算字号。</summary>
    public int ResolveFontSize(float amount, float maxHealth, float styleScale)
    {
        float ratio = maxHealth > 0f ? amount / maxHealth : 0f;
        float t = fontSizeSaturationRatio > 0f
            ? Mathf.Clamp01(ratio / fontSizeSaturationRatio)
            : 0f;

        float size = Mathf.Lerp(minFontSize, maxFontSize, t) * Mathf.Max(0.1f, styleScale);
        return Mathf.Max(1, Mathf.RoundToInt(size));
    }
}
