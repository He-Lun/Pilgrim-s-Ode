using UnityEngine;
using UnityEngine.UI;

/// <summary>单张手牌 Widget。</summary>
public class BattleHandCardWidget : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image faceImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Image costBadge;
    [SerializeField] private Text costText;
    [SerializeField] private CanvasGroup canvasGroup;

    public GameplayAbility ability;
    public int handIndex = -1;

    private static Sprite fallbackFaceSprite;

    public RectTransform Rect => (RectTransform)transform;

    void Awake() => EnsureHierarchy();

    public void Bind(GameplayAbility skill, int index, Sprite faceSprite)
    {
        EnsureHierarchy();
        ability = skill;
        handIndex = index;

        if (titleText != null)
            titleText.text = skill != null ? skill.abilityName : string.Empty;

        int cost = skill != null ? skill.actionPointCost : 0;
        if (costBadge != null)
            costBadge.enabled = skill != null;

        if (costText != null)
            BattleUiFonts.ApplyToLabel(costText, skill != null ? cost.ToString() : string.Empty);

        if (faceImage != null)
        {
            faceImage.sprite = faceSprite != null ? faceSprite : GetFallbackFaceSprite();
            faceImage.enabled = faceImage.sprite != null;
        }
    }

    public void SetAffordable(bool affordable)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = affordable ? 1f : 0.45f;
    }

    private void EnsureHierarchy()
    {
        EnsureRoot();
        EnsureFace();
        EnsureTitle();
        EnsureCostBadge();
    }

    private void EnsureRoot()
    {
        if (background != null)
            return;

        canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);
        background.raycastTarget = true;
    }

    private void EnsureFace()
    {
        if (faceImage != null)
            return;

        faceImage = CreateImage("Face", new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.88f));
        faceImage.preserveAspect = true;
    }

    private void EnsureTitle()
    {
        if (titleText != null)
            return;

        titleText = CreateText("Title", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.2f), 16);
    }

    private void EnsureCostBadge()
    {
        if (costBadge != null && costText != null)
            return;

        var legacy = transform.Find("Cost");
        if (legacy != null)
            Destroy(legacy.gameObject);

        var costRoot = new GameObject("CostBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        costRoot.transform.SetParent(transform, false);
        var costRect = costRoot.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0.02f, 0.86f);
        costRect.anchorMax = new Vector2(0.22f, 0.98f);
        costRect.offsetMin = Vector2.zero;
        costRect.offsetMax = Vector2.zero;
        costBadge = costRoot.GetComponent<Image>();
        costBadge.color = new Color(0.85f, 0.65f, 0.1f, 0.95f);
        costBadge.raycastTarget = false;

        costText = CreateText("CostText", Vector2.zero, Vector2.one, 18);
        costText.transform.SetParent(costRoot.transform, false);
        var costTextRect = costText.GetComponent<RectTransform>();
        costTextRect.anchorMin = Vector2.zero;
        costTextRect.anchorMax = Vector2.one;
        costTextRect.offsetMin = Vector2.zero;
        costTextRect.offsetMax = Vector2.zero;
        costText.alignment = TextAnchor.MiddleCenter;
        costText.color = Color.white;
        BattleUiFonts.ApplyToLabel(costText, string.Empty);
    }

    private Image CreateImage(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        BattleUiFonts.ApplyToLabel(text, string.Empty);
        return text;
    }

    private static Sprite GetFallbackFaceSprite()
    {
        if (fallbackFaceSprite != null)
            return fallbackFaceSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, new Color(0.35f, 0.4f, 0.55f, 1f));
        tex.Apply();
        fallbackFaceSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return fallbackFaceSprite;
    }
}
