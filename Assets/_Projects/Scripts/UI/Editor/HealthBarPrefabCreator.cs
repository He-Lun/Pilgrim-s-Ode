#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使用 ProgressBars #4 素材生成血条预制体，并写入 HealthBarUIConfig。
/// </summary>
public static class HealthBarPrefabCreator
{
    private const string OutputDir = "Assets/_Projects/Prefab/UI";
    private const string ConfigPath = OutputDir + "/HealthBarUIConfig.asset";

    private const string WorldEmpty = "Assets/ProgressBars #4/PNGs/1-Empty.png";
    private const string WorldAllyFill = "Assets/ProgressBars #4/PNGs/1-G.png";
    private const string WorldEnemyFill = "Assets/ProgressBars #4/PNGs/1-R.png";
    private const string OverlayEmpty = "Assets/ProgressBars #4/PNGs/4-Empty.png";
    private const string OverlayAllyFill = "Assets/ProgressBars #4/PNGs/4-EmeraldGreen.png";
    private const string OverlayEnemyFill = "Assets/ProgressBars #4/PNGs/4-Red.png";
    private const string InspirationEmpty = "Assets/ProgressBars #4/PNGs/4-Empty.png";
    private const string InspirationInProgress = "Assets/ProgressBars #4/PNGs/4-LimeGreen.png";
    private const string InspirationComplete = "Assets/ProgressBars #4/PNGs/4-Orange.png";

    [MenuItem("巡礼之诗/UI/生成血条预制体")]
    public static void CreateAll()
    {
        CreateAllInternal();
    }

    [InitializeOnLoadMethod]
    private static void AutoCreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            var allyPath = OutputDir + "/HealthBar_World_Ally.prefab";
            if (!File.Exists(allyPath))
                CreateAllInternal();
        };
    }

    private static void CreateAllInternal()
    {
        EnsureDirectory(OutputDir);

        var worldEmpty = LoadSprite(WorldEmpty);
        var worldAlly = LoadSprite(WorldAllyFill);
        var worldEnemy = LoadSprite(WorldEnemyFill);
        var overlayEmpty = LoadSprite(OverlayEmpty);
        var overlayAlly = LoadSprite(OverlayAllyFill);
        var overlayEnemy = LoadSprite(OverlayEnemyFill);
        var inspirationEmpty = LoadSprite(InspirationEmpty);
        var inspirationInProgress = LoadSprite(InspirationInProgress);
        var inspirationComplete = LoadSprite(InspirationComplete);

        var worldAllyBar = SaveWorldPrefab("HealthBar_World_Ally.prefab", worldEmpty, worldAlly);
        var worldEnemyBar = SaveWorldPrefab("HealthBar_World_Enemy.prefab", worldEmpty, worldEnemy);
        var overlayAllyBar = SaveOverlayBarPrefab("HealthBar_Overlay_Ally.prefab", overlayEmpty, overlayAlly);
        var overlayEnemyBar = SaveOverlayBarPrefab("HealthBar_Overlay_Enemy.prefab", overlayEmpty, overlayEnemy);

        var rosterAlly = SaveRosterEntryPrefab("CharacterRosterEntry_Ally.prefab", overlayAllyBar, inspirationEmpty, inspirationInProgress, inspirationComplete);
        var rosterEnemy = SaveRosterEntryPrefab("CharacterRosterEntry_Enemy.prefab", overlayEnemyBar, inspirationEmpty, inspirationInProgress, inspirationComplete);

        var config = AssetDatabase.LoadAssetAtPath<HealthBarUIConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<HealthBarUIConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        config.worldBarAllyPrefab = worldAllyBar;
        config.worldBarEnemyPrefab = worldEnemyBar;
        config.rosterEntryAllyPrefab = rosterAlly;
        config.rosterEntryEnemyPrefab = rosterEnemy;

        config.worldAllySprites = new HealthBarSpritePair { background = worldEmpty, fill = worldAlly };
        config.worldEnemySprites = new HealthBarSpritePair { background = worldEmpty, fill = worldEnemy };
        config.overlayAllySprites = new HealthBarSpritePair { background = overlayEmpty, fill = overlayAlly };
        config.overlayEnemySprites = new HealthBarSpritePair { background = overlayEmpty, fill = overlayEnemy };
        config.localTeamId = 0;
        config.worldBarCanvasSize = new Vector2(872f, 50f);
        config.worldBarScale = new Vector3(0.01f, 0.01f, 0.01f);
        config.overlayBarSize = new Vector2(196f, 22f);
        config.worldFillPadding = HealthBarFillPadding.WorldStyle1Default;
        config.overlayFillPadding = HealthBarFillPadding.OverlayStyle4Default;
        config.inspirationBarEmpty = inspirationEmpty;
        config.inspirationBarInProgressFill = inspirationInProgress;
        config.inspirationBarCompleteFill = inspirationComplete;
        config.inspirationBarSize = new Vector2(196f, 18f);
        config.inspirationFillPadding = HealthBarFillPadding.OverlayStyle4Default;

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[HealthBarPrefabCreator] 血条预制体已生成至 " + OutputDir);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Projects/Prefab"))
            AssetDatabase.CreateFolder("Assets/_Projects", "Prefab");

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets/_Projects/Prefab", "UI");
    }

    private static Sprite LoadSprite(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            throw new FileNotFoundException("找不到 Sprite: " + assetPath);

        return sprite;
    }

    private static WorldHealthBarWidget SaveWorldPrefab(string fileName, Sprite background, Sprite fill)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        root.AddComponent<BillboardToCamera>();

        var canvasGo = CreateChild(root.transform, "Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(872f, 50f);

        var worldPadding = HealthBarFillPadding.WorldStyle1Default;
        var backgroundImage = CreateStretchImage(canvasGo.transform, "Background", background);
        var fillImage = CreateLoLFillImage(backgroundImage.rectTransform, "Fill", fill, canvasRect.sizeDelta.y, worldPadding);

        var healthBar = canvasGo.AddComponent<HealthBarView>();
        SetHealthBarReferences(healthBar, fillImage, fillImage.rectTransform, HealthBarView.FillMode.Width, worldPadding);
        AttachDepletionChips(healthBar);

        var widget = root.AddComponent<WorldHealthBarWidget>();
        SetWorldWidgetReferences(widget, healthBar);

        root.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var path = $"{OutputDir}/{fileName}";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<WorldHealthBarWidget>();
    }

    private static HealthBarView SaveOverlayBarPrefab(string fileName, Sprite background, Sprite fill)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(fileName), typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(196f, 22f);

        var overlayPadding = HealthBarFillPadding.OverlayStyle4Default;
        CreateStretchImage(root.transform, "Background", background);
        var backgroundRect = root.GetComponent<RectTransform>().Find("Background") as RectTransform;
        var fillImage = CreateLoLFillImage(
            backgroundRect != null ? backgroundRect : root.GetComponent<RectTransform>(),
            "Fill",
            fill,
            rect.sizeDelta.y,
            overlayPadding);

        var healthBar = root.AddComponent<HealthBarView>();
        SetHealthBarReferences(healthBar, fillImage, fillImage.rectTransform, HealthBarView.FillMode.Width, overlayPadding);
        AttachDepletionChips(healthBar);

        var path = $"{OutputDir}/{fileName}";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<HealthBarView>();
    }

    private static CharacterRosterEntryWidget SaveRosterEntryPrefab(
        string fileName,
        HealthBarView barPrefab,
        Sprite inspirationEmpty,
        Sprite inspirationInProgress,
        Sprite inspirationComplete)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(fileName), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 94f);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);

        var portraitGo = CreateChild(root.transform, "Portrait");
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(8f, 0f);
        portraitRect.sizeDelta = new Vector2(56f, 56f);
        var portraitImage = portraitGo.AddComponent<Image>();
        portraitImage.color = Color.white;

        var nameGo = CreateChild(root.transform, "Name");
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(68f, 24f);
        nameRect.sizeDelta = new Vector2(-12f, 28f);
        var nameText = nameGo.AddComponent<Text>();
        nameText.fontSize = 18;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;
        nameText.supportRichText = false;

        var barAnchor = CreateChild(root.transform, "BarAnchor");
        var barAnchorRect = barAnchor.GetComponent<RectTransform>();
        barAnchorRect.anchorMin = new Vector2(0f, 0f);
        barAnchorRect.anchorMax = new Vector2(1f, 0f);
        barAnchorRect.pivot = new Vector2(0.5f, 0f);
        barAnchorRect.anchoredPosition = new Vector2(0f, 30f);
        barAnchorRect.sizeDelta = new Vector2(-80f, 22f);

        var barInstance = (HealthBarView)PrefabUtility.InstantiatePrefab(barPrefab, barAnchor.transform);
        barInstance.gameObject.name = "HealthBar";
        StretchRect(barInstance.GetComponent<RectTransform>());

        var inspirationAnchor = CreateChild(root.transform, "InspirationBarAnchor");
        var inspirationAnchorRect = inspirationAnchor.GetComponent<RectTransform>();
        inspirationAnchorRect.anchorMin = new Vector2(0f, 0f);
        inspirationAnchorRect.anchorMax = new Vector2(1f, 0f);
        inspirationAnchorRect.pivot = new Vector2(0.5f, 0f);
        inspirationAnchorRect.anchoredPosition = new Vector2(0f, 8f);
        inspirationAnchorRect.sizeDelta = new Vector2(-80f, 18f);

        var overlayPadding = HealthBarFillPadding.OverlayStyle4Default;
        CreateStretchImage(inspirationAnchor.transform, "Background", inspirationEmpty);
        var inspirationBackground = inspirationAnchor.transform.Find("Background") as RectTransform;
        CreateLoLFillImage(
            inspirationBackground != null ? inspirationBackground : inspirationAnchorRect,
            "Fill",
            inspirationInProgress,
            18f,
            overlayPadding);

        var inspirationView = inspirationAnchor.AddComponent<InspirationTaskProgressBarView>();
        var fillImage = inspirationBackground != null
            ? inspirationBackground.Find("Fill")?.GetComponent<Image>()
            : null;
        SetInspirationBarReferences(
            inspirationView,
            inspirationBackground,
            fillImage != null ? fillImage.rectTransform : null,
            fillImage,
            inspirationInProgress,
            inspirationComplete,
            overlayPadding);

        var widget = root.AddComponent<CharacterRosterEntryWidget>();
        SetRosterEntryReferences(widget, portraitImage, nameText, barInstance, inspirationView);

        var path = $"{OutputDir}/{fileName}";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<CharacterRosterEntryWidget>();
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateStretchImage(Transform parent, string name, Sprite sprite)
    {
        var go = CreateChild(parent, name);
        go.AddComponent<CanvasRenderer>();
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        StretchRect(go.GetComponent<RectTransform>());
        return image;
    }

    private static Image CreateLoLFillImage(RectTransform background, string name, Sprite sprite, float height, HealthBarFillPadding padding)
    {
        var go = CreateChild(background, name);
        go.AddComponent<CanvasRenderer>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(padding.left, (padding.bottom - padding.top) * 0.5f);

        float parentWidth = background.rect.width > 0f ? background.rect.width : background.sizeDelta.x;
        float fillWidth = Mathf.Max(0f, parentWidth - padding.Horizontal);
        float fillHeight = Mathf.Max(0f, height - padding.Vertical);
        rect.sizeDelta = new Vector2(fillWidth, fillHeight);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        return image;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetHealthBarReferences(HealthBarView view, Image fill, RectTransform fillRect, HealthBarView.FillMode mode, HealthBarFillPadding padding)
    {
        var so = new SerializedObject(view);
        so.FindProperty("fillImage").objectReferenceValue = fill;
        so.FindProperty("fillRect").objectReferenceValue = fillRect;
        so.FindProperty("fillMode").enumValueIndex = (int)mode;
        so.FindProperty("fillPadding").FindPropertyRelative("left").intValue = padding.left;
        so.FindProperty("fillPadding").FindPropertyRelative("right").intValue = padding.right;
        so.FindProperty("fillPadding").FindPropertyRelative("top").intValue = padding.top;
        so.FindProperty("fillPadding").FindPropertyRelative("bottom").intValue = padding.bottom;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetWorldWidgetReferences(WorldHealthBarWidget widget, HealthBarView bar)
    {
        var so = new SerializedObject(widget);
        so.FindProperty("healthBar").objectReferenceValue = bar;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRosterEntryReferences(
        CharacterRosterEntryWidget widget,
        Image portrait,
        Text name,
        HealthBarView bar,
        InspirationTaskProgressBarView inspirationBar)
    {
        var so = new SerializedObject(widget);
        so.FindProperty("portraitImage").objectReferenceValue = portrait;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("healthBar").objectReferenceValue = bar;
        so.FindProperty("inspirationProgressBar").objectReferenceValue = inspirationBar;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInspirationBarReferences(
        InspirationTaskProgressBarView view,
        RectTransform track,
        RectTransform fill,
        Image fillImage,
        Sprite inProgress,
        Sprite complete,
        HealthBarFillPadding padding)
    {
        var so = new SerializedObject(view);
        so.FindProperty("trackRect").objectReferenceValue = track;
        so.FindProperty("fillRect").objectReferenceValue = fill;
        so.FindProperty("fillImage").objectReferenceValue = fillImage;
        so.FindProperty("inProgressFill").objectReferenceValue = inProgress;
        so.FindProperty("completeFill").objectReferenceValue = complete;
        so.FindProperty("fillPadding").FindPropertyRelative("left").intValue = padding.left;
        so.FindProperty("fillPadding").FindPropertyRelative("right").intValue = padding.right;
        so.FindProperty("fillPadding").FindPropertyRelative("top").intValue = padding.top;
        so.FindProperty("fillPadding").FindPropertyRelative("bottom").intValue = padding.bottom;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AttachDepletionChips(HealthBarView view)
    {
        if (view == null)
            return;

        var chips = view.GetComponent<HealthBarDepletionChips>();
        if (chips == null)
            chips = view.gameObject.AddComponent<HealthBarDepletionChips>();
        chips.Bind(view);
    }
}
#endif
