using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 UI 字体 — 使用系统字体渲染中文角色名。
/// </summary>
public static class BattleUiFonts
{
    private static Font cachedRosterFont;

    private static readonly string[] CjkOsFontCandidates =
    {
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "SimHei",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Arial Unicode MS"
    };

    private static readonly string[] WindowsFontPaths =
    {
        @"C:\Windows\Fonts\simhei.ttf",
        @"C:\Windows\Fonts\msyh.ttc",
        @"C:\Windows\Fonts\msyhbd.ttc",
        @"C:\Windows\Fonts\simsun.ttc"
    };

    public static Font GetRosterFont()
    {
        if (cachedRosterFont != null)
            return cachedRosterFont;

        cachedRosterFont = Resources.Load<Font>("Fonts/RosterUI");
        if (IsUsableFont(cachedRosterFont))
            return cachedRosterFont;

        cachedRosterFont = Font.CreateDynamicFontFromOSFont(CjkOsFontCandidates, 32);
        if (IsUsableFont(cachedRosterFont))
            return cachedRosterFont;

        for (int i = 0; i < WindowsFontPaths.Length; i++)
        {
            if (!File.Exists(WindowsFontPaths[i]))
                continue;

            try
            {
                var font = new Font(WindowsFontPaths[i]);
                if (IsUsableFont(font))
                {
                    cachedRosterFont = font;
                    return cachedRosterFont;
                }
            }
            catch
            {
                // try next path
            }
        }

        cachedRosterFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return cachedRosterFont;
    }

    public static void ApplyToLabel(Text text, string content)
    {
        if (text == null)
            return;

        var font = GetRosterFont();
        if (font != null)
        {
            if (!string.IsNullOrEmpty(content))
                font.RequestCharactersInTexture(content, text.fontSize, text.fontStyle);

            text.font = font;
        }

        text.text = content ?? string.Empty;
        text.SetAllDirty();
    }

    private static bool IsUsableFont(Font font)
    {
        return font != null && !string.IsNullOrEmpty(font.name);
    }
}
