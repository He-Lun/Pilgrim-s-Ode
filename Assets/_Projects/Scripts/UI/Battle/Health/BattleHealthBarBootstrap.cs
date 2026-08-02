using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 战斗血条 UI 启动器 — Play 后仅创建/刷新血条，不走菜单流程。
/// </summary>
public static class BattleHealthBarBootstrap
{
    public static void EnsureAndSync()
    {
        var root = BattleHealthBarUiRoot.Instance;
        if (root == null)
            root = CreateRuntimeRoot();

        root.SyncFromBattle();
    }

    private static BattleHealthBarUiRoot CreateRuntimeRoot()
    {
        var existing = Object.FindObjectOfType<BattleHealthBarUiRoot>();
        if (existing != null)
            return existing;

        var rootGo = new GameObject("BattleHealthBarUiRoot");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(rootGo.transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        StretchFull(canvasGo.GetComponent<RectTransform>());

        var rosterGo = new GameObject("CharacterRosterPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CharacterRosterPanel));
        rosterGo.transform.SetParent(canvasGo.transform, false);

        var rosterRect = rosterGo.GetComponent<RectTransform>();
        rosterRect.anchorMin = new Vector2(0f, 0.5f);
        rosterRect.anchorMax = new Vector2(0f, 0.5f);
        rosterRect.pivot = new Vector2(0f, 0.5f);
        rosterRect.anchoredPosition = new Vector2(24f, 0f);
        rosterRect.sizeDelta = new Vector2(300f, 720f);

        var rosterBg = rosterGo.GetComponent<Image>();
        rosterBg.color = Color.clear;
        rosterBg.raycastTarget = false;

        var containerGo = new GameObject("EntryContainer", typeof(RectTransform));
        containerGo.transform.SetParent(rosterGo.transform, false);
        StretchFull(containerGo.GetComponent<RectTransform>());

        var layout = containerGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AssignField(rosterGo.GetComponent<CharacterRosterPanel>(), "entryContainer", containerGo.GetComponent<RectTransform>());

        var controller = rootGo.AddComponent<BattleHealthBarController>();
        var uiRoot = rootGo.AddComponent<BattleHealthBarUiRoot>();
        var config = HealthBarUIConfig.LoadDefault();
        controller.Configure(rosterGo.GetComponent<CharacterRosterPanel>(), config);
        AssignField(uiRoot, "healthBarController", controller);
        AssignField(uiRoot, "rosterPanel", rosterGo.GetComponent<CharacterRosterPanel>());
        AssignField(uiRoot, "config", config);

        var actionBar = ActionOrderBarPanel.Create(canvasGo.transform, config);
        AssignField(uiRoot, "actionOrderBarPanel", actionBar);

        Object.DontDestroyOnLoad(rootGo);
        return uiRoot;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void AssignField(Object target, string fieldName, Object value)
    {
        if (target == null)
            return;

        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            type = type.BaseType;
        }
    }
}
