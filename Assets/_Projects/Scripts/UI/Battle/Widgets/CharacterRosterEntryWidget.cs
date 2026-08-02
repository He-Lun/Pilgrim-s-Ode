using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左侧角色条目 — 头像、名字、血条、激励任务进度条。
/// </summary>
public class CharacterRosterEntryWidget : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private Text nameText;
    [SerializeField] private HealthBarView healthBar;
    [SerializeField] private InspirationTaskProgressBarView inspirationProgressBar;

    private AbilitySystemComponent boundActor;
    private AttributeSet boundAttributes;
    private InspirationTaskTracker boundTracker;
    private System.Action<float> healthChangedHandler;
    private System.Action<string, float, float> attributeChangedHandler;
    private System.Action<GameplayTag> modifierChangedHandler;
    private System.Action<AbilitySystemComponent> deathHandler;
    private System.Action<InspirationObjective, int, int> inspirationProgressHandler;
    private System.Action<int, MoonPhase> moonSoulChangedHandler;

    public void Bind(AbilitySystemComponent actor)
    {
        Unbind();
        boundActor = actor;
        boundAttributes = actor != null ? actor.Attributes : null;
        boundTracker = actor != null ? actor.InspirationTracker : null;

        if (boundAttributes == null)
        {
            gameObject.SetActive(false);
            return;
        }

        EnsureDepletionChips();
        ApplyIdentity(actor);
        BindHealthEvents(actor);
        BindInspirationEvents(actor);

        SyncDisplay();
        RefreshInspiration();
        SetDimmed(boundAttributes.IsDead());
        gameObject.SetActive(true);
    }

    public void Unbind()
    {
        UnbindHealthEvents();
        UnbindInspirationEvents();
        boundActor = null;
        boundAttributes = null;
        boundTracker = null;
    }

    void OnDestroy() => Unbind();

    private void BindHealthEvents(AbilitySystemComponent actor)
    {
        healthChangedHandler = _ => RefreshHealth();
        attributeChangedHandler = (name, _, __) =>
        {
            if (name == "Health")
                RefreshHealth();
        };

        boundAttributes.OnHealthChanged += healthChangedHandler;
        boundAttributes.OnAttributeChanged += attributeChangedHandler;
        modifierChangedHandler = _ => SyncDisplay();
        boundAttributes.OnModifierAdded += modifierChangedHandler;
        boundAttributes.OnModifierRemoved += modifierChangedHandler;

        deathHandler = _ => SetDimmed(true);
        actor.OnDeath += deathHandler;
    }

    private void UnbindHealthEvents()
    {
        if (boundAttributes != null && healthChangedHandler != null)
            boundAttributes.OnHealthChanged -= healthChangedHandler;
        if (boundAttributes != null && attributeChangedHandler != null)
            boundAttributes.OnAttributeChanged -= attributeChangedHandler;
        if (boundAttributes != null && modifierChangedHandler != null)
        {
            boundAttributes.OnModifierAdded -= modifierChangedHandler;
            boundAttributes.OnModifierRemoved -= modifierChangedHandler;
        }
        if (boundActor != null && deathHandler != null)
            boundActor.OnDeath -= deathHandler;

        healthChangedHandler = null;
        attributeChangedHandler = null;
        modifierChangedHandler = null;
        deathHandler = null;
    }

    private void BindInspirationEvents(AbilitySystemComponent actor)
    {
        if (boundTracker == null || boundTracker.TaskDef == null)
            return;

        inspirationProgressHandler = (_, __, ___) => RefreshInspiration();
        boundTracker.OnProgressChanged += inspirationProgressHandler;

        if (actor.HasMoonSoul)
        {
            moonSoulChangedHandler = (_, __) => RefreshInspiration();
            actor.MoonSoul.OnChanged += moonSoulChangedHandler;
        }
    }

    private void UnbindInspirationEvents()
    {
        if (boundTracker != null && inspirationProgressHandler != null)
            boundTracker.OnProgressChanged -= inspirationProgressHandler;

        if (boundActor != null && moonSoulChangedHandler != null)
            boundActor.MoonSoul.OnChanged -= moonSoulChangedHandler;

        inspirationProgressHandler = null;
        moonSoulChangedHandler = null;
    }

    private void ApplyIdentity(AbilitySystemComponent actor)
    {
        EnsureNameLabel();
        string displayName = actor.GetDisplayName();
        BattleUiFonts.ApplyToLabel(nameText, displayName);
        nameText.gameObject.SetActive(true);

        var data = actor.CharacterData;

        if (portraitImage == null)
            return;

        Sprite portrait = data != null ? data.portrait : null;
        if (portrait == null && data?.inspirationAbility != null)
            portrait = data.inspirationAbility.icon;

        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
    }

    private void EnsureNameLabel()
    {
        if (nameText == null)
            ResolveNameTextIfMissing();

        if (nameText != null)
        {
            ConfigureNameRect(nameText.rectTransform);
            return;
        }

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nameGo.transform.SetParent(transform, false);
        nameGo.transform.SetSiblingIndex(portraitImage != null ? portraitImage.transform.GetSiblingIndex() + 1 : 0);

        nameText = nameGo.GetComponent<Text>();
        ConfigureNameRect(nameGo.GetComponent<RectTransform>());
        nameText.fontSize = 18;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;
        nameText.supportRichText = false;
    }

    private static void ConfigureNameRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(68f, 24f);
        rect.sizeDelta = new Vector2(-12f, 28f);
        rect.localScale = Vector3.one;
    }

    private void ResolveNameTextIfMissing()
    {
        var texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "Name")
            {
                nameText = texts[i];
                return;
            }
        }

        if (texts.Length > 0)
            nameText = texts[0];
    }

    private void RefreshHealth()
    {
        if (healthBar != null && boundAttributes != null)
            healthBar.SetValues(boundAttributes.CurrentHealth, boundAttributes.MaxHealth);
    }

    public void SyncDisplay()
    {
        if (healthBar != null && boundAttributes != null)
            healthBar.SyncValues(boundAttributes.CurrentHealth, boundAttributes.MaxHealth);
    }

    private void RefreshInspiration()
    {
        if (inspirationProgressBar == null || boundTracker == null || boundTracker.TaskDef == null)
            return;

        inspirationProgressBar.gameObject.SetActive(true);
        inspirationProgressBar.SetProgress(boundTracker.GetProgressRatio());
    }

    private void SetDimmed(bool dimmed)
    {
        if (portraitImage != null)
            portraitImage.color = dimmed ? new Color(0.45f, 0.45f, 0.45f, 0.85f) : Color.white;
        if (nameText != null)
            nameText.color = dimmed ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
    }

    private void EnsureDepletionChips()
    {
        if (healthBar == null)
            return;

        var chips = healthBar.GetComponent<HealthBarDepletionChips>();
        if (chips == null)
            chips = healthBar.gameObject.AddComponent<HealthBarDepletionChips>();
        chips.Bind(healthBar);
    }
}
