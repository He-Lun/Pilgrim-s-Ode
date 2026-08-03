using UnityEngine;

/// <summary>
/// 血条预制体与阵营配色配置。
/// </summary>
[CreateAssetMenu(fileName = "HealthBarUIConfig", menuName = "巡礼之诗/UI/血条配置")]
public class HealthBarUIConfig : ScriptableObject
{
    private static HealthBarUIConfig cached;

    [Header("世界空间 · 样式 1")]
    public HealthBarSpritePair worldAllySprites;
    public HealthBarSpritePair worldEnemySprites;
    public WorldHealthBarWidget worldBarAllyPrefab;
    public WorldHealthBarWidget worldBarEnemyPrefab;

    [Header("Overlay · 样式 4")]
    public HealthBarSpritePair overlayAllySprites;
    public HealthBarSpritePair overlayEnemySprites;
    public CharacterRosterEntryWidget rosterEntryAllyPrefab;
    public CharacterRosterEntryWidget rosterEntryEnemyPrefab;

    [Header("阵营")]
    [Tooltip("与该 TeamId 相同为友方（绿），否则为敌方（红）")]
    public int localTeamId = 0;

    [Header("尺寸")]
    [Tooltip("世界血条 Canvas 逻辑尺寸（像素）。改宽高比例；改 worldBarScale 改整体大小。")]
    public Vector2 worldBarCanvasSize = new Vector2(872f, 50f);

    [Tooltip("世界血条 Transform 缩放。数值越大，头顶血条越大。")]
    public Vector3 worldBarScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Tooltip("左侧列表 Overlay 血条尺寸（像素宽 × 高）。")]
    public Vector2 overlayBarSize = new Vector2(196f, 22f);

    [Header("世界血条挂点")]
    [Tooltip("角色 Prefab 上 AbilityVfxAttachPoints 中的挂点 id，或同名 Transform。")]
    public string worldAttachPointId = "HeadForHp";

    [Header("填充内边距")]
    [Tooltip("世界血条填充区距底图边框的留白（像素）。填充超出空条时调大 left/right/top/bottom。")]
    public HealthBarFillPadding worldFillPadding = HealthBarFillPadding.WorldStyle1Default;

    [Tooltip("Overlay 血条填充区内边距。")]
    public HealthBarFillPadding overlayFillPadding = HealthBarFillPadding.OverlayStyle4Default;

    [Header("激励任务进度条 · 样式 4")]
    public Sprite inspirationBarEmpty;
    public Sprite inspirationBarInProgressFill;
    public Sprite inspirationBarCompleteFill;
    public Vector2 inspirationBarSize = new Vector2(196f, 18f);
    public HealthBarFillPadding inspirationFillPadding = HealthBarFillPadding.OverlayStyle4Default;

    [Header("行动条头像")]
    [Tooltip("在 Inspector 中为每个角色拖入对应头像 Sprite。")]
    public ActionBarPortraitConfig actionBarPortraits;

    public static HealthBarUIConfig LoadDefault()
    {
        if (cached != null)
            return cached;

#if UNITY_EDITOR
        cached = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthBarUIConfig>("Assets/_Projects/Prefab/UI/HealthBarUIConfig.asset");
        if (cached != null)
            return cached;
#endif

        cached = Resources.Load<HealthBarUIConfig>("HealthBarUIConfig");
        return cached;
    }

    public static void InvalidateCache()
    {
        cached = null;
    }

    public bool IsAlly(AbilitySystemComponent actor)
    {
        return actor != null && actor.TeamId == localTeamId;
    }

    public WorldHealthBarWidget ResolveWorldPrefab(AbilitySystemComponent actor)
    {
        return IsAlly(actor) ? worldBarAllyPrefab : worldBarEnemyPrefab;
    }

    public HealthBarSpritePair ResolveWorldSprites(AbilitySystemComponent actor)
    {
        return IsAlly(actor) ? worldAllySprites : worldEnemySprites;
    }

    public CharacterRosterEntryWidget ResolveRosterEntryPrefab(AbilitySystemComponent actor)
    {
        return IsAlly(actor) ? rosterEntryAllyPrefab : rosterEntryEnemyPrefab;
    }

    public HealthBarSpritePair ResolveOverlaySprites(AbilitySystemComponent actor)
    {
        return IsAlly(actor) ? overlayAllySprites : overlayEnemySprites;
    }
}
